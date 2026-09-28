using PvPAdventure.Content.Items.BiomeKeyMolds;
using PvPAdventure.Content.Portals;
using PvPAdventure.Core.Config;
using System.Linq;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using PvPAdventure.Content.Items.BrokenShadowFlameItems;

namespace PvPAdventure.Common.Loot.DropRates;

public static class AdventureDropDatabase
{
    private class AdventureIsPreHardmode : IItemDropRuleCondition
    {
        private AdventureIsPreHardmode()
        {
        }

        public static AdventureIsPreHardmode The { get; } = new();

        public bool CanDrop(DropAttemptInfo info) => !Main.hardMode;
        public bool CanShowItemDropInUI() => true;
        public string GetConditionDescription() => "Drops pre-hardmode";
    }

    private static void ModifyDropRate(IItemDropRule rule, int type, int numerator, int denominator)
    {
        if (rule is CommonDrop commonDrop && commonDrop.itemId == type)
        {
            commonDrop.chanceNumerator = numerator;
            commonDrop.chanceDenominator = denominator;
        }
        else if (rule is DropBasedOnExpertMode dropBasedOnExpertMode)
        {
            ModifyDropRate(dropBasedOnExpertMode.ruleForNormalMode, type, numerator, denominator);
            ModifyDropRate(dropBasedOnExpertMode.ruleForExpertMode, type, numerator, denominator);
        }

        foreach (var ruleChainAttempt in rule.ChainedRules)
            ModifyDropRate(ruleChainAttempt.RuleToChain, type, numerator, denominator);
    }
    private static bool RuleDropsItem(IItemDropRule rule, int type)
    {
        if (rule is CommonDrop commonDrop && commonDrop.itemId == type)
            return true;
        if (rule is DropBasedOnExpertMode dropBasedOnExpertMode)
            return RuleDropsItem(dropBasedOnExpertMode.ruleForNormalMode, type)
                || RuleDropsItem(dropBasedOnExpertMode.ruleForExpertMode, type);
        return rule.ChainedRules.Any(chain => RuleDropsItem(chain.RuleToChain, type));
    }

    public static void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
    {
        var drops = npcLoot.Get();

        foreach (var drop in drops)
            ModifyDropRate(drop, ItemID.Kraken, 1, 20);

        foreach (var drop in drops)
            ModifyDropRate(drop, ItemID.MagnetSphere, 1, 20);

        switch (npc.type)
        {
            case NPCID.BoneLee:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.Tabi, 1, 1);
                break;

            case NPCID.Paladin:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.PaladinsHammer, 3, 3);
                    ModifyDropRate(drop, ItemID.PaladinsShield, 3, 3);
                }

                break;

            case NPCID.MossHornet:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.Stinger, 1, 1);
                    ModifyDropRate(drop, ItemID.TatteredBeeWing, 1, 15);
                }
                break;

            case NPCID.GiantTortoise:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.TurtleShell, 1, 2);
                break;
            case NPCID.Psycho:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.PsychoKnife, 1, 4);
                break;
            case NPCID.Demon:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.DemonScythe, 1, 15);
                break;
            case NPCID.VoodooDemon:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.DemonScythe, 1, 15);
                break;
            case NPCID.Harpy:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.Feather, 1, 1);
                }
                break;
            case NPCID.WyvernHead:
                foreach (var drop in drops)
                {
                    npcLoot.RemoveWhere(drop => true);
                    npcLoot.Add(ItemDropRule.Common(ItemID.SoulofFlight, 1, 10, 20));
                }
                break;
            case NPCID.IceGolem:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.IceFeather, 1, 2);
                break;

            case NPCID.Necromancer:
            case NPCID.NecromancerArmored:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.ShadowbeamStaff, 1, 1);
                break;

            case NPCID.RaggedCaster:
            case NPCID.RaggedCasterOpenCoat:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.SpectreStaff, 1, 1);
                break;

            case NPCID.DiabolistRed:
            case NPCID.DiabolistWhite:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.InfernoFork, 1, 1);
                break;

            case NPCID.SkeletonSniper:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.SniperRifle, 1, 1);
                npcLoot.Add(ItemDropRule.Common(ItemID.RifleScope, 1));
                break;

            case NPCID.TacticalSkeleton:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.TacticalShotgun, 1, 1);
                break;

            case NPCID.SkeletonCommando:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.RocketLauncher, 1, 1);
                break;
            case NPCID.Vampire:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.MoonStone, 1, 5);
                break;

            case NPCID.RustyArmoredBonesAxe:
            case NPCID.RustyArmoredBonesFlail:
            case NPCID.RustyArmoredBonesSword:
            case NPCID.RustyArmoredBonesSwordNoArmor:
            case NPCID.BlueArmoredBones:
            case NPCID.BlueArmoredBonesMace:
            case NPCID.BlueArmoredBonesNoPants:
            case NPCID.BlueArmoredBonesSword:
            case NPCID.HellArmoredBones:
            case NPCID.HellArmoredBonesSpikeShield:
            case NPCID.HellArmoredBonesMace:
            case NPCID.HellArmoredBonesSword:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.MaceWhip, 1, 10);
                    ModifyDropRate(drop, ItemID.BoneFeather, 1, 150);
                }
                break;

            case NPCID.BlackRecluse:
            case NPCID.BlackRecluseWall:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.SpiderFang, 5, 5);
                    ModifyDropRate(drop, ItemID.PoisonStaff, 1, 10);

                }
                break;

            case NPCID.GoblinSummoner:
                npcLoot.RemoveWhere(drop => true); // Removes all drops
                npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 3, 3));
                npcLoot.Add(ItemDropRule.OneFromOptions(1,
                    ModContent.ItemType<BrokenShadowFlameBow>(),
                    ModContent.ItemType<BrokenShadowFlameKnife>(),
                    ModContent.ItemType<BrokenShadowFlameHexDoll>()
                ));
                break;

            case NPCID.Moth:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.ButterflyDust, 1, 1);
                break;

            case NPCID.GiantCursedSkull:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.ShadowJoustingLance, 1, 1);
                break;

            case NPCID.Mothron:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.BrokenHeroSword, 1, 2);
                    ModifyDropRate(drop, ItemID.TheEyeOfCthulhu, 1, 2);
                    ModifyDropRate(drop, ItemID.MothronWings, 1, 6);
                }
                break;

            case NPCID.SkeletonArcher:

                npcLoot.RemoveWhere(drop =>
                    (drop is CommonDrop commonDrop && commonDrop.itemId == ItemID.MagicQuiver) ||
                    drop is LeadingConditionRule);
                npcLoot.Add(ItemDropRule.Common(ItemID.MagicQuiver, 10));
                break;

            case NPCID.RedDevil:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.UnholyTrident, 1, 10);
                npcLoot.Add(ItemDropRule.Common(ItemID.FireFeather, 7, 1, 1));

                break;

            case NPCID.Lihzahrd:
            case NPCID.LihzahrdCrawler:
            case NPCID.FlyingSnake:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.LihzahrdPowerCell, 1, 1);
                npcLoot.RemoveWhere(rule => RuleDropsItem(rule, ItemID.LihzahrdPowerCell));
                npcLoot.Add(ItemDropRule.ByCondition(
                    new Conditions.DownedPlantera(),
                    ItemID.LihzahrdPowerCell,
                    chanceDenominator: 30,
                    chanceNumerator: 1));
                break;

            case NPCID.MartianEngineer:
            case NPCID.MartianOfficer:
            case NPCID.BrainScrambler:
            case NPCID.MartianWalker:
            case NPCID.GrayGrunt:
            case NPCID.GigaZapper:
            case NPCID.RayGunner:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.ChargedBlasterCannon, 1, 300);
                    ModifyDropRate(drop, ItemID.LaserDrill, 1, 300);
                }
                break;
            case NPCID.IceElemental:
            case NPCID.IcyMerman:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.IceSickle, 1, 15);
                    ModifyDropRate(drop, ItemID.FrostStaff, 1, 10);
                }
                break;
            case NPCID.AngryNimbus:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.NimbusRod, 1, 6);
                }
                break;

            case NPCID.AngryTrapper:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.Uzi, 1, 15);
                }
                npcLoot.Add(ItemDropRule.Common(ItemID.Vine, 1, 1, 1));
                break;
            case NPCID.DesertDjinn:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.DjinnLamp, 1, 10);
                }
                break;

            case NPCID.ManEater:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.Vine, 1, 1);
                }
                break;

            case NPCID.SpikedJungleSlime:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.Stinger, 1, 1);
                }
                break;

            case NPCID.Hornet:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.Stinger, 1, 1);
                }
                break;

            case NPCID.Nailhead:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.NailGun, 1, 6);
                }
                break;

            case NPCID.Reaper:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.DeathSickle, 1, 20);
                }
                break;

            case NPCID.DeadlySphere:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.DeadlySphereStaff, 1, 10);
                }
                break;

            case NPCID.DrManFly:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.ToxicFlask, 1, 6);
                }
                break;
            case NPCID.Salamander:
            case NPCID.GiantShelly:
            case NPCID.Crawdad:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.Rally, 1, 6);
                }
                break;
            case NPCID.Skeleton:
            case NPCID.UndeadMiner:
            case NPCID.UndeadViking:
            case NPCID.Piranha:
            case NPCID.GreekSkeleton:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.Hook, 1, 5);
                }
                break;
            case NPCID.CaveBat:
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.ChainKnife, 1, 11);
                }
                break;

            case NPCID.PirateCaptain:
                npcLoot.Add(ItemDropRule.OneFromOptions(1,
                     ItemID.CoinGun,
                     ItemID.LuckyCoin,
                     ItemID.DiscountCard,
                     ItemID.PirateStaff
                            )
                );
                foreach (var drop in drops)
                {

                    ModifyDropRate(drop, ItemID.Cutlass, 1, 10);
                    ModifyDropRate(drop, ItemID.GoldRing, 1, 10);
                }
                break;
            case NPCID.Corruptor:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.RottenChunk, 1, 1);
                break;
            case NPCID.Clinger:
            case NPCID.SeekerHead:
            case NPCID.DesertGhoulCorruption:
                npcLoot.Add(ItemDropRule.Common(ItemID.RottenChunk, 2, 1, 1));
                break;
            case NPCID.EyeofCthulhu:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.Binoculars, 1, 1);
                npcLoot.Add(ItemDropRule.Common(ItemID.VilePowder, 1, 10, 10));
                npcLoot.Add(ItemDropRule.Common(ItemID.Lens, 1, 1, 1));
                npcLoot.Add(ItemDropRule.Common(ItemID.BlackLens, 1, 1, 1));
                npcLoot.RemoveWhere(drop =>
    (drop is CommonDrop commonDrop && commonDrop.itemId == ItemID.CorruptSeeds) ||
    drop is LeadingConditionRule);
                break;

            case NPCID.DungeonSpirit:
                npcLoot.RemoveWhere(drop =>
    (drop is CommonDrop commonDrop && commonDrop.itemId == ItemID.Ectoplasm) ||
    drop is LeadingConditionRule);
                npcLoot.Add(ItemDropRule.Common(ItemID.Ectoplasm, 1, 3, 5));
                break;

            case NPCID.KingSlime:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.SlimySaddle, 1, 1);
                break;

            case NPCID.Golem:
                npcLoot.RemoveWhere(drop =>
                    (drop is CommonDrop commonDrop && (commonDrop.itemId == ItemID.Picksaw || commonDrop.itemId == ItemID.BeetleHusk)) ||
                    drop is LeadingConditionRule);

                var stynger = ItemDropRule.Common(ItemID.Stynger);
                stynger.OnSuccess(ItemDropRule.Common(ItemID.StyngerBolt, 1, 60, 99), hideLootReport: true);

                npcLoot.Add(
                    new FewFromRulesRule(2, 1,
                        stynger,
                        ItemDropRule.Common(ItemID.PossessedHatchet),
                        ItemDropRule.Common(ItemID.GolemFist),
                        ItemDropRule.Common(ItemID.HeatRay),
                        ItemDropRule.Common(ItemID.StaffofEarth)
                    )
                );

                npcLoot.Add(ItemDropRule.FewFromOptions(2, 1,
                    ItemID.Picksaw,
                    ItemID.EyeoftheGolem,
                    ItemID.SunStone,
                    ItemID.ShinyStone)
                );

                npcLoot.Add(ItemDropRule.Common(ItemID.BeetleHusk, 1, 12, 16));
                break;

            case NPCID.QueenSlimeBoss:
                // Remove the big loot pool -- we will re-create it ourselves.
                npcLoot.RemoveWhere(drop => drop is LeadingConditionRule);

                // Always get two pieces of the Crystal Ninja set, separate from other drops.
                npcLoot.Add(ItemDropRule.FewFromOptions(2, 1,
                        ItemID.CrystalNinjaHelmet,
                        ItemID.CrystalNinjaChestplate,
                        ItemID.CrystalNinjaLeggings
                    )
                );

                npcLoot.Add(ItemDropRule.FewFromOptions(2, 1,
                        ItemID.Smolstar,
                        ItemID.QueenSlimeHook,
                        ItemID.QueenSlimeMountSaddle
                    )
                );

                break;
            case NPCID.Antlion:
            case NPCID.GiantWalkingAntlion:
            case NPCID.GiantFlyingAntlion:
            case NPCID.WalkingAntlion:
            case NPCID.FlyingAntlion:
                npcLoot.Add(ItemDropRule.Common(ItemID.FossilOre, 1, 1, 1));
                break;

            case NPCID.AngryBones:
            case NPCID.DarkCaster:
            case NPCID.CursedSkull:
            case NPCID.SkeletronHand:
                npcLoot.Add(ItemDropRule.Common(ItemID.Bone, 1, 1, 3));
                break;
            case NPCID.SnowFlinx:
                npcLoot.Add(ItemDropRule.Common(ItemID.FlinxFur, 1, 1, 1));
                break;
            case NPCID.Mimic:
                npcLoot.RemoveWhere(drop => drop is LeadingConditionRule);

                npcLoot.Add(ItemDropRule.OneFromOptions(1,
                        ItemID.StarCloak,
                        ItemID.MagicDagger,
                        ItemID.DualHook
                    )
                );

                npcLoot.Add(ItemDropRule.OneFromOptions(1,
                        ItemID.CrossNecklace,
                        ItemID.PhilosophersStone,
                        ItemID.TitanGlove
                    )
                );
                npcLoot.Add(ItemDropRule.Common(ItemID.HermesBoots, 1, 1, 1));
                npcLoot.Add(ItemDropRule.Common(ItemID.CloudinaBottle, 1, 1, 1));
                break;

            case NPCID.QueenBee:
                npcLoot.Add(ItemDropRule.Common(ItemID.HoneyedGoggles, 1, 1, 1));
                npcLoot.Add(ItemDropRule.Common(ItemID.HoneyComb, 1, 1, 2));
                npcLoot.Add(ItemDropRule.Common(ItemID.HiveWand, 1, 1, 1));
                npcLoot.Add(ItemDropRule.Common(ItemID.Beenade, 1, 22, 32));
                npcLoot.RemoveWhere(drop =>
                    (drop is CommonDrop commonDrop && commonDrop.itemId == ItemID.HoneyComb) ||
                    drop is DropBasedOnExpertMode);

                npcLoot.Add(ItemDropRule.OneFromOptions(1,
                        ItemID.BeeKeeper,
                        ItemID.BeesKnees
                    )
                );

                npcLoot.Add(ItemDropRule.OneFromOptions(1,
                        ItemID.BeeGun,
                        ItemID.HoneyComb
                    )
                );
                break;

            case NPCID.BigMimicHallow:
                npcLoot.RemoveWhere(drop => true); // Removes all drops
                npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 3, 3));
                npcLoot.Add(ItemDropRule.Common(ItemID.GreaterHealingPotion, 1, 1, 2));
                npcLoot.Add(ItemDropRule.Common(ItemID.GreaterManaPotion, 1, 5, 15));

                npcLoot.Add(ItemDropRule.OneFromOptions(1,
                    ItemID.DaedalusStormbow,
                    ItemID.FlyingKnife
                ));
                npcLoot.Add(ItemDropRule.OneFromOptionsWithNumerator(5, 5,
                    ItemID.IlluminantHook,
                    ItemID.CrystalVileShard
                ));
                break;
            case NPCID.BigMimicCorruption:
                npcLoot.RemoveWhere(drop => true); // Removes all drops
                npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 3, 3));
                npcLoot.Add(ItemDropRule.Common(ItemID.GreaterHealingPotion, 1, 1, 2));
                npcLoot.Add(ItemDropRule.Common(ItemID.GreaterManaPotion, 1, 5, 15));
                npcLoot.Add(ItemDropRule.OneFromOptions(1,
                    ItemID.DartRifle,
                    ItemID.ChainGuillotines
                ));
                npcLoot.Add(ItemDropRule.OneFromOptionsWithNumerator(5, 5,
                    ItemID.PutridScent,
                    ItemID.ClingerStaff,
                    ItemID.WormHook
                ));
                break;

            case NPCID.SkeletronHead:
                npcLoot.Add(ItemDropRule.Common(ItemID.GoldenKey, 1, 2, 2));
                npcLoot.Add(ItemDropRule.Common(ItemID.Marrow, 1000, 1000, 1000));

                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.SkeletronHand, 1, 1);
                break;
            case NPCID.DemonEye:
            case NPCID.WanderingEye:
                foreach (var drop in drops)
                    ModifyDropRate(drop, ItemID.Lens, 1, 1);
                break;

            case NPCID.IceTortoise:
                npcLoot.RemoveWhere(drop =>
                    (drop is CommonDrop commonDrop && commonDrop.itemId == ItemID.FrozenTurtleShell));
                npcLoot.Add(ItemDropRule.Common(ItemID.FrozenKey, 40, 1, 1));
                foreach (var drop in drops) ;
                break;

            case NPCID.MartianSaucerCore:
                npcLoot.RemoveWhere(drop => true); // Removes all drops
                npcLoot.Add(
                    new FewFromRulesRule(2, 1,
                        ItemDropRule.Common(ItemID.XenoStaff),
                        ItemDropRule.Common(ItemID.Xenopopper),
                        ItemDropRule.Common(ItemID.ElectrosphereLauncher),
                        ItemDropRule.Common(ItemID.LaserMachinegun),
                        ItemDropRule.Common(ItemID.InfluxWaver)
                    )

                );
                break;
            case NPCID.Shark:
            case NPCID.SandShark:
            case NPCID.SandsharkHallow:
            case NPCID.SandsharkCorrupt:
            case NPCID.SandsharkCrimson:
                npcLoot.Add(ItemDropRule.Common(ItemID.SharkFin, 1, 6, 7));
                break;
            case NPCID.WallofFlesh:

                npcLoot.RemoveWhere(drop => drop is LeadingConditionRule);
                npcLoot.Add(
                    new FewFromRulesRule(2, 1,
                        ItemDropRule.Common(ItemID.WarriorEmblem),
                        ItemDropRule.Common(ItemID.RangerEmblem),
                        ItemDropRule.Common(ItemID.SorcererEmblem),
                        ItemDropRule.Common(ItemID.SummonerEmblem)
                    )
                );
                npcLoot.Add(
                    new OneFromRulesRule(1,
                        ItemDropRule.Common(ItemID.FireWhip),
                        ItemDropRule.Common(ItemID.BreakerBlade)
                    )
                );
                npcLoot.Add(
                    new OneFromRulesRule(1,
                        ItemDropRule.Common(ItemID.LaserRifle),
                        ItemDropRule.Common(ItemID.ClockworkAssaultRifle)
                    )
                );
                break;
            case NPCID.Deerclops:

                npcLoot.RemoveWhere(drop => drop is LeadingConditionRule);
                npcLoot.Add(
                    new FewFromRulesRule(4, 1,
                        ItemDropRule.Common(ItemID.WeatherPain),
                        ItemDropRule.Common(ItemID.LucyTheAxe),
                        ItemDropRule.Common(ItemID.PewMaticHorn),
                        ItemDropRule.Common(ItemID.HoundiusShootius)
                    )
                );
                break;

            case NPCID.DukeFishron:

                npcLoot.RemoveWhere(drop => drop is LeadingConditionRule);
                npcLoot.Add(ItemDropRule.Common(ItemID.SharkFin, 1, 14, 14));
                npcLoot.Add(
                    new FewFromRulesRule(2, 1,
                        ItemDropRule.Common(ItemID.BubbleGun),
                        ItemDropRule.Common(ItemID.Tsunami),
                        ItemDropRule.Common(ItemID.RazorbladeTyphoon),
                        ItemDropRule.Common(ItemID.TempestStaff),
                        ItemDropRule.Common(ItemID.Flairon)
                    )
                );
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.FishronWings, 0, 1);
                }
                break;
            case NPCID.HallowBoss:
                npcLoot.RemoveWhere(drop => drop is LeadingConditionRule);
                npcLoot.Add(
                    new FewFromRulesRule(2, 1,
                        ItemDropRule.Common(ItemID.PiercingStarlight),
                        ItemDropRule.Common(ItemID.FairyQueenMagicItem),
                        ItemDropRule.Common(ItemID.FairyQueenRangedItem),
                        ItemDropRule.Common(ItemID.RainbowWhip)
                    )
                );
                foreach (var drop in drops)
                {
                    ModifyDropRate(drop, ItemID.RainbowWings, 0, 1);
                }
                break;

            case NPCID.TheDestroyer:
                npcLoot.RemoveWhere(drop =>
                    (drop is CommonDrop commonDrop && commonDrop.itemId == ItemID.HallowedBar));
                npcLoot.Add(ItemDropRule.Common(ItemID.HallowedBar, 1, 20, 30));

                // Remove existing soul drops
                npcLoot.RemoveWhere(drop =>
                    (drop is CommonDrop commonDrop && commonDrop.itemId == ItemID.SoulofMight));
                npcLoot.Add(ItemDropRule.Common(ItemID.SoulofMight, 1, 40, 40));
                break;

            case NPCID.SkeletronPrime:
                npcLoot.RemoveWhere(drop =>
                    (drop is CommonDrop commonDrop && commonDrop.itemId == ItemID.HallowedBar));
                npcLoot.Add(ItemDropRule.Common(ItemID.HallowedBar, 1, 20, 30));

                npcLoot.RemoveWhere(drop =>
                    (drop is CommonDrop commonDrop && commonDrop.itemId == ItemID.SoulofFright));
                npcLoot.Add(ItemDropRule.Common(ItemID.SoulofFright, 1, 40, 40));
                break;

            case NPCID.Retinazer:
            case NPCID.Spazmatism:
                npcLoot.RemoveWhere(rule => true);

                npcLoot.Add(ItemDropRule.ByCondition(
                    new Conditions.LegacyHack_IsBossAndNotExpert(),
                    ItemID.HallowedBar, 1, 20, 30));

                npcLoot.Add(ItemDropRule.ByCondition(
                    new Conditions.LegacyHack_IsBossAndNotExpert(),
                    ItemID.SoulofSight, 1, 40, 40));
                break;

            case NPCID.Plantera:
                npcLoot.RemoveWhere(drop => drop is LeadingConditionRule);
                npcLoot.Add(ItemDropRule.Common(ItemID.TempleKey, 1, 1, 1));
                npcLoot.Add(ItemDropRule.Common(ItemID.TheAxe, 1, 1, 1));
                npcLoot.Add(
                    new FewFromRulesRule(2, 1,
                        ItemDropRule.Common(ItemID.Seedler),
                        ItemDropRule.Common(ItemID.LeafBlower),
                        ItemDropRule.Common(ItemID.WaspGun),
                        ItemDropRule.Common(ItemID.PygmyStaff)
                    )
                );
                var grenadelauncher = ItemDropRule.Common(ItemID.GrenadeLauncher);
                grenadelauncher.OnSuccess(ItemDropRule.Common(ItemID.RocketIII, 1, 400, 500), hideLootReport: true);
                npcLoot.Add(
                    new FewFromRulesRule(2, 1,
                        grenadelauncher,
                        ItemDropRule.Common(ItemID.NettleBurst),
                        ItemDropRule.Common(ItemID.FlowerPow),
                        ItemDropRule.Common(ItemID.VenusMagnum)
                    )
                );
                npcLoot.Add(ItemDropRule.Common(ItemID.ThornHook, 1, 1, 1));
                break;
        }
    }

}
