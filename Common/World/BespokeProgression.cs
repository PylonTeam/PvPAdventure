using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PvPAdventure.Core.Net;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Common.World
{
    /// <summary>
    /// Temporary system that makes certain events occur at certain times, always
    ///  40 min elapsed / Day 3 @ 12:00 AM, Spawn Wall of Flesh
    /// 110 min elapsed / Day 5 @ 10:09 PM, Spawn Plantera on a random bulb (or in the jungle)
    /// 130 min elapsed / Day 6 @  2:45 PM, Spawn Golem on the Lihzahrd Altar
    /// </summary>
    internal class BespokeProgression : ModSystem
    {
        public static int WorldDay { get; private set; } = 1;
        private bool _passedMidnightThisNight;

        private bool _hardmodeStarted;
        private bool _planteraDowned;
        private bool _golemDowned;

        private const double MidnightTick = 18_000;
        private const double NightTick_Plantera = 9_900;
        private const double DayTick_Golem = 49_500;

        public override void OnWorldLoad()
        {
            _passedMidnightThisNight = false;
        }

        public override void ClearWorld()
        {
            WorldDay = 1;
            _passedMidnightThisNight = false;
            _hardmodeStarted = false;
            _planteraDowned = false;
            _golemDowned = false;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            tag["worldDay"] = WorldDay;
            tag["hardmodeStarted"] = _hardmodeStarted;
            tag["planteraDowned"] = _planteraDowned;
            tag["golemDowned"] = _golemDowned;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            WorldDay = tag.ContainsKey("worldDay") ? tag.GetAsInt("worldDay") : 1;
            _hardmodeStarted = tag.ContainsKey("hardmodeStarted") ? tag.Get<bool>("hardmodeStarted") : false;
            _planteraDowned = tag.ContainsKey("planteraDowned") ? tag.Get<bool>("planteraDowned") : false;
            _golemDowned = tag.ContainsKey("golemDowned") ? tag.Get<bool>("golemDowned") : false;
        }

        public override void NetSend(System.IO.BinaryWriter writer)
        {
            writer.Write(WorldDay);
            writer.Write(_hardmodeStarted);
            writer.Write(_planteraDowned);
            writer.Write(_golemDowned);
        }

        public override void NetReceive(System.IO.BinaryReader reader)
        {
            int worldDay = reader.ReadInt32();
            bool hardmodeStarted = reader.ReadBoolean();
            bool planteraDowned = reader.ReadBoolean();
            bool golemDowned = reader.ReadBoolean();
            WorldSyncReader.EnsureComplete(reader);

            WorldDay = worldDay;
            _hardmodeStarted = hardmodeStarted;
            _planteraDowned = planteraDowned;
            _golemDowned = golemDowned;
        }

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            AdvanceDayCounter();
            CheckScheduledEvents();
        }

        private void AdvanceDayCounter()
        {
            if (!Main.dayTime)
            {
                if (!_passedMidnightThisNight && Main.time >= MidnightTick)
                {
                    WorldDay++;
                    _passedMidnightThisNight = true;
                }
            }
            else
            {
                _passedMidnightThisNight = false;
            }
        }

        private void CheckScheduledEvents()
        {
            if (!_hardmodeStarted
                && WorldDay >= 3
                && !Main.dayTime
                && Main.time >= MidnightTick)
            {
                SpawnWallOfFlesh();
                _hardmodeStarted = true;
            }

            if (!_planteraDowned
                && WorldDay == 5
                && !Main.dayTime
                && Main.time >= NightTick_Plantera
                && Main.time < MidnightTick)
            {
                SpawnPlantera();
                _planteraDowned = true;
            }

            if (!_golemDowned
                && WorldDay >= 6
                && Main.dayTime
                && Main.time >= DayTick_Golem
                && _planteraDowned)
            {
                SpawnGolem();
                _golemDowned = true;
            }
        }

        private static void SpawnWallOfFlesh()
        {
            // Skip if hardmode is already active (WoF was already killed)
            // or if WoF is currently alive
            if (Main.hardMode || NPC.AnyNPCs(NPCID.WallofFlesh))
                return;

            int playerIndex = FindActivePlayer();
            if (playerIndex == -1)
                return;

            int xPixel = 100 * 16;
            int yTile = Main.UnderworldLayer + (Main.maxTilesY - Main.UnderworldLayer) / 2;
            int yPixel = yTile * 16;

            NPC.NewNPC(Main.player[playerIndex].GetSource_FromAI(),
                xPixel, yPixel, NPCID.WallofFlesh);

            BroadcastMessage("The Wall Of Flesh has awoken (woke) but it is also bespoke (bespoke)",
                new Color(255, 102, 255));
        }

        private static void SpawnPlantera()
        {
            // Skip if Plantera has already been defeated or is currently alive
            if (NPC.downedPlantBoss || NPC.AnyNPCs(NPCID.Plantera))
                return;

            int playerIndex = FindActivePlayer();
            if (playerIndex == -1)
                return;

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
                    return;
                }

                spawnX = jungleSpot.X * 16;
                spawnY = jungleSpot.Y * 16;
            }

            NPC.NewNPC(Main.player[playerIndex].GetSource_FromAI(),
                spawnX, spawnY, NPCID.Plantera);

            BroadcastMessage("Holy fuck... look at your map... its bespoke Plantera...",
                new Color(255, 102, 255));
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

        private static void SpawnGolem()
        {
            // Skip if Golem has already been defeated or is currently alive
            if (NPC.downedGolemBoss || NPC.AnyNPCs(NPCID.Golem))
                return;

            int playerIndex = FindActivePlayer();
            if (playerIndex == -1)
                return;

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
                return;
            }

            int xPixel = altarPos.X * 16;
            int yPixel = (altarPos.Y - 4) * 16;

            NPC.NewNPC(Main.player[playerIndex].GetSource_FromAI(),
                xPixel, yPixel, NPCID.Golem);

            BroadcastMessage("The Bespoke Golem has arrived!",
                new Color(255, 102, 255));
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
