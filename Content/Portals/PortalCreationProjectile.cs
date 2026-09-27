using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Travel.Portals;
using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Content.Portals;

/// <summary>
/// Draws a "pending" portal.
/// </summary>
public sealed class PortalCreationProjectile : ModProjectile
{
    // Let an in-flight result arrive, but never leave a fully formed phantom behind.
    private const int ResultWaitFrames = 120;
    public int OwnerIndex => (int)Projectile.ai[2];
    public int RequestId { get; private set; }

    public override string Texture => "PvPAdventure/Assets/Portals/Portal_NoTeam";
    private float OutlineOpacityProgress => MathHelper.Clamp(ElapsedFrames / 12f, 0f, 1f);
    private float OutlineScaleProgress => MathHelper.Clamp(ElapsedFrames / 22f, 0f, 1f);
    private float HealthBarOpacity => MathHelper.Clamp(ElapsedFrames / 8f, 0f, 1f);

    private int CreationFrames
    {
        get => Math.Max(0, (int)Projectile.ai[0]);
        set => Projectile.ai[0] = Math.Max(0, value);
    }

    private int OwnerTeam
    {
        get => Math.Clamp((int)Projectile.ai[1], 0, Main.teamColor.Length - 1);
        set => Projectile.ai[1] = Math.Clamp(value, 0, Main.teamColor.Length - 1);
    }

    private float ElapsedFrames
    {
        get => Projectile.localAI[0];
        set => Projectile.localAI[0] = value;
    }

    private float Progress => CreationFrames <= 0 ? 1f : MathHelper.Clamp(ElapsedFrames / CreationFrames, 0f, 1f);
    private float Opacity => MathHelper.Lerp(0f, 0.75f, Progress);

    public void Initialize(Vector2 worldPos, int creationFrames, int ownerTeam, int requestId)
    {
        CreationFrames = creationFrames;
        OwnerTeam = ownerTeam;
        ElapsedFrames = 0f;
        RequestId = requestId;
        Projectile.timeLeft = CreationFrames + ResultWaitFrames;

        Projectile.position = worldPos - new Vector2(Projectile.width * 0.5f, Projectile.height);
        Projectile.netUpdate = true;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(CreationFrames);
        writer.Write(OwnerTeam);
        writer.Write(RequestId);
        writer.Write(ElapsedFrames);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        CreationFrames = reader.ReadInt32();
        OwnerTeam = reader.ReadInt32();
        RequestId = reader.ReadInt32();
        ElapsedFrames = reader.ReadSingle();
        Projectile.timeLeft = Math.Max(1, CreationFrames - (int)ElapsedFrames + ResultWaitFrames);
    }

    public override void SetDefaults()
    {
        Projectile.width = PortalNPC.PortalWidth;
        Projectile.height = PortalNPC.PortalHeight;
        Projectile.aiStyle = -1;
        Projectile.timeLeft = 60 * 60;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }

    public override void AI()
    {
        Projectile.velocity = Vector2.Zero;
        bool firstFrame = ElapsedFrames <= 0f;

        ElapsedFrames++;

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            bool staleAttempt = OwnerIndex == Main.myPlayer &&
                !Main.LocalPlayer.GetModPlayer<PortalPlayer>().CreationAttempt.IsPending(RequestId);
            if (staleAttempt || ElapsedFrames >= CreationFrames + ResultWaitFrames)
            {
                Projectile.active = false;
                if (!staleAttempt)
                    PortalSystem.FinishCreationLocally(OwnerIndex, RequestId, completed: false);
                return;
            }
        }

        if (Main.netMode != NetmodeID.Server)
        {
            if (firstFrame)
                PortalDrawer.SpawnPortalFadeInDust(Projectile.Bottom, OwnerTeam);

            PortalDrawer.SpawnPortalDust(Projectile.Bottom, Progress);
        }

        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (!TryGetOwner(out Player owner) || owner.dead || owner.ghost || PortalCreatorItem.IsPortalCreationInterrupted(owner))
        {
            Finish(completed: false);
            return;
        }

        if (ElapsedFrames < CreationFrames)
            return;

        Finish(PortalSystem.CreateOrReplacePortal(owner, Projectile.Bottom));
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = PortalAssets.GetPortalTexture(OwnerTeam);

        int frameCount = Math.Max(1, Main.npcFrameCount[ModContent.NPCType<PortalNPC>()]);
        int frameIndex = (int)(Main.GameUpdateCount / 5 % frameCount);
        Rectangle frame = texture.Frame(1, frameCount, 0, frameIndex);

        Vector2 origin = frame.Size() * 0.5f;
        Vector2 position = Projectile.Center - Main.screenPosition;

        // Draw outline
        PortalDrawer.DrawPortalOutline(Main.spriteBatch, OwnerTeam, position, frame, Projectile.rotation, origin, Projectile.scale * OutlineScaleProgress, OutlineOpacityProgress);
        
        // Draw portal
        Main.spriteBatch.Draw(texture, position, frame, Color.White * Opacity, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

        int visibleHealth = Math.Clamp((int)(PortalNPC.PortalMaxHealth * Progress), 1, PortalNPC.PortalMaxHealth - 1);
        PortalDrawer.DrawPortalHealthBar(Main.spriteBatch, Projectile.Center + new Vector2(0f, 24f * Projectile.scale), visibleHealth, PortalNPC.PortalMaxHealth, Projectile.scale, HealthBarOpacity);

        return false;
    }

    private bool TryGetOwner(out Player owner)
    {
        owner = OwnerIndex >= 0 && OwnerIndex < Main.maxPlayers ? Main.player[OwnerIndex] : null;
        return owner?.active == true;
    }

    internal void Finish(bool completed)
    {
        if (!Projectile.active || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        int identity = Projectile.identity;
        int owner = Projectile.owner;

        Projectile.Kill();
        //Log.Chat("Portal temp creation projectile killed");

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.KillProjectile, -1, -1, null, identity, owner);

        PortalSystem.FinishCreation(OwnerIndex, RequestId, completed);
    }

}
