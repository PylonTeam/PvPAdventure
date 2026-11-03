using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Common.World
{
    /// <summary>
    ///  System that spawns "bespoke" bosses at guaranteed times:
    ///  Wall of Flesh when the 2nd night begins
    ///  Plantera at the second day after all three mechanical bosses are defeated
    ///  Golem at the second dawn after Plantera is defeated.
    /// </summary>
    internal class BespokeProgression : ModSystem
    {
        private enum Stage { Waiting, Queued, Armed, Due, Done }

        private bool? _wasDayTime;
        private int _nightsStarted;
        private bool _wofDone;
        private Stage _planteraStage;
        private Stage _golemStage;

        public override void OnWorldLoad()
        {
            _wasDayTime = null;
        }

        public override void ClearWorld()
        {
            _wasDayTime = null;
            _nightsStarted = 0;
            _wofDone = false;
            _planteraStage = Stage.Waiting;
            _golemStage = Stage.Waiting;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            tag["nightsStarted"] = _nightsStarted;
            tag["wofDone"] = _wofDone;
            tag["planteraStage"] = (int)_planteraStage;
            tag["golemStage"] = (int)_golemStage;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            _nightsStarted = tag.ContainsKey("nightsStarted") ? tag.GetAsInt("nightsStarted") : 0;
            _wofDone = tag.ContainsKey("wofDone") && tag.Get<bool>("wofDone");
            _planteraStage = tag.ContainsKey("planteraStage") ? (Stage)tag.GetAsInt("planteraStage") : Stage.Waiting;
            _golemStage = tag.ContainsKey("golemStage") ? (Stage)tag.GetAsInt("golemStage") : Stage.Waiting;
        }

        public override void NetSend(System.IO.BinaryWriter writer)
        {
            writer.Write(_nightsStarted);
            writer.Write(_wofDone);
            writer.Write((int)_planteraStage);
            writer.Write((int)_golemStage);
        }

        public override void NetReceive(System.IO.BinaryReader reader)
        {
            _nightsStarted = reader.ReadInt32();
            _wofDone = reader.ReadBoolean();
            _planteraStage = (Stage)reader.ReadInt32();
            _golemStage = (Stage)reader.ReadInt32();
        }

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Detect day/night transitions
            bool isDawn = false;
            if (_wasDayTime.HasValue)
            {
                if (_wasDayTime.Value && !Main.dayTime)
                    _nightsStarted++;
                else if (!_wasDayTime.Value && Main.dayTime)
                    isDawn = true;
            }
            _wasDayTime = Main.dayTime;

            CheckScheduledEvents(isDawn);
        }

        private void CheckScheduledEvents(bool isDawn)
        {
            if (!_wofDone && _nightsStarted >= 2)
                _wofDone = SpawnWallOfFlesh();

            Advance(ref _golemStage, NPC.downedPlantBoss, isDawn, SpawnGolem);

            bool allMechsDown = NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3;
            Advance(ref _planteraStage, allMechsDown, isDawn, SpawnPlantera);
        }

        private static void Advance(ref Stage stage, bool trigger, bool isDawn, Func<bool> spawn)
        {
            if (stage == Stage.Waiting && trigger)
                stage = Stage.Queued;
            else if (stage == Stage.Queued && isDawn)
                stage = Stage.Armed;
            else if (stage == Stage.Armed && isDawn)
                stage = Stage.Due;

            if (stage == Stage.Due && spawn())
                stage = Stage.Done;
        }

        private static bool SpawnWallOfFlesh()
        {
            if (Main.hardMode || NPC.AnyNPCs(NPCID.WallofFlesh))
                return true;

            int playerIndex = FindActivePlayer();
            if (playerIndex == -1)
                return false;

            int xPixel = 100 * 16;
            int yTile = Main.UnderworldLayer + (Main.maxTilesY - Main.UnderworldLayer) / 2;
            int yPixel = yTile * 16;

            NPC.NewNPC(Main.player[playerIndex].GetSource_FromAI(),
                xPixel, yPixel, NPCID.WallofFlesh);

            BroadcastMessage("The Wall Of Flesh has awoken (woke) but it is also bespoke (bespoke)",
                new Color(255, 102, 255));
            return true;
        }

        private static bool SpawnPlantera()
        {
            if (NPC.downedPlantBoss || NPC.AnyNPCs(NPCID.Plantera))
                return true;

            int playerIndex = FindActivePlayer();
            if (playerIndex == -1)
                return false;

            int spawnX, spawnY;

            var bulbs = new List<Point>();
            for (int tx = 0; tx < Main.maxTilesX; tx++)
            {
                for (int ty = 0; ty < Main.maxTilesY; ty++)
                {
                    Tile tile = Main.tile[tx, ty];
                    if (tile.HasTile && tile.TileType == TileID.PlanteraBulb)
                        bulbs.Add(new Point(tx, ty));
                }
            }

            if (bulbs.Count > 0)
            {
                Point chosen = bulbs[Main.rand.Next(bulbs.Count)];
                spawnX = chosen.X * 16;
                spawnY = chosen.Y * 16;
            }
            else
            {
                Point jungleSpot = FindUndergroundJungleSpot();

                if (jungleSpot == Point.Zero)
                {
                    NPC.downedPlantBoss = true;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.WorldData);
                    BroadcastMessage("Bespoke Plantera!!!",
                        new Color(255, 102, 255));
                    return true;
                }

                spawnX = jungleSpot.X * 16;
                spawnY = jungleSpot.Y * 16;
            }

            NPC.NewNPC(Main.player[playerIndex].GetSource_FromAI(),
                spawnX, spawnY, NPCID.Plantera);

            BroadcastMessage("Holy fuck... look at your map... its bespoke Plantera...",
                new Color(255, 102, 255));
            return true;
        }

        private static Point FindUndergroundJungleSpot()
        {
            int xStart = Main.maxTilesX / 4;
            int xEnd = Main.maxTilesX * 3 / 4;
            int yStart = (int)Main.worldSurface + 80;
            int yEnd = Main.UnderworldLayer;

            for (int tx = xStart; tx < xEnd; tx++)
            {
                for (int ty = yStart; ty < yEnd; ty++)
                {
                    Tile tile = Main.tile[tx, ty];
                    if (tile.HasTile && tile.TileType == TileID.JungleGrass)
                        return new Point(tx, ty);
                }
            }

            return Point.Zero;
        }

        private static bool SpawnGolem()
        {
            if (NPC.downedGolemBoss || NPC.AnyNPCs(NPCID.Golem))
                return true;

            int playerIndex = FindActivePlayer();
            if (playerIndex == -1)
                return false;

            Point altarPos = Point.Zero;
            bool found = false;

            for (int tx = 0; tx < Main.maxTilesX && !found; tx++)
            {
                for (int ty = 0; ty < Main.maxTilesY && !found; ty++)
                {
                    Tile tile = Main.tile[tx, ty];
                    if (tile.HasTile && tile.TileType == TileID.LihzahrdAltar)
                    {
                        altarPos = new Point(tx, ty);
                        found = true;
                    }
                }
            }

            if (!found)
            {
                NPC.downedGolemBoss = true;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.WorldData);
                return true;
            }

            int xPixel = altarPos.X * 16;
            int yPixel = (altarPos.Y - 4) * 16;

            NPC.NewNPC(Main.player[playerIndex].GetSource_FromAI(),
                xPixel, yPixel, NPCID.Golem);

            BroadcastMessage("The Bespoke Golem has arrived!",
                new Color(255, 102, 255));
            return true;
        }

        private static int FindActivePlayer()
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (Main.player[i].active && !Main.player[i].dead)
                    return i;
            }
            return -1;
        }

        private static void BroadcastMessage(string text, Color colour)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                Terraria.Localization.NetworkText netText =
                    Terraria.Localization.NetworkText.FromLiteral(text);
                ChatHelper.BroadcastChatMessage(netText, colour);
            }
            else
            {
                Main.NewText(text, colour.R, colour.G, colour.B);
            }
        }
    }
}