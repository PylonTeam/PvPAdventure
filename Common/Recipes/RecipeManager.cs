using PvPAdventure.Content.Items.BiomeKeyMolds;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using PvPAdventure.Content.Items.BrokenShadowFlameItems;
namespace PvPAdventure.Common.Recipes;

[Autoload(Side = ModSide.Both)]
// - This is where we add new recipes, remove recipes, and change recipes
// - Right now, in order to change a recipe, we just remove the vanilla recipe, and add a new modded recipe, which might be inefficient 
public class RecipeManager : ModSystem
{

    public override void AddRecipes()
    {
        

        // List of items to disable the recipes of, usually in ortder to replace it with our own new recipe

        int[] itemsToRemove = new int[]
        {
            ItemID.MoonlordArrow,
            ItemID.ShroomiteBar,
            ItemID.WormFood,
            ItemID.ChlorophyteBar,
            ItemID.FlaskofCursedFlames,
            ItemID.FlaskofIchor,
            ItemID.FlaskofPoison,
            ItemID.FlaskofVenom,
            ItemID.FlaskofFire,
            ItemID.FlaskofNanites,
            ItemID.FlaskofParty,
            ItemID.SpectreBar,
            ItemID.SuspiciousLookingEye,
            ItemID.DemonWings,
            ItemID.AngelWings,
            ItemID.FairyWings,
            ItemID.SnailStatue,
            ItemID.FrogStatue,
            ItemID.DuckStatue,
            ItemID.ManaCrystal,
            ItemID.DeerThing


        };

        for (int i = 0; i < Main.recipe.Length; i++)
        {
            Recipe recipe = Main.recipe[i];
            if (recipe.createItem.type != ItemID.None && itemsToRemove.Contains(recipe.createItem.type))
            {
                recipe.DisableRecipe();
            }
        }

        // Headstone Recipe
        Recipe.Create(ItemID.Headstone)
            .AddIngredient(ItemID.StoneBlock, 50)
            .AddTile(TileID.HeavyWorkBench)
            .Register();

        Recipe.Create(ItemID.ManaCrystal)
            .AddIngredient(ItemID.FallenStar, 3)
            .Register();


        // Corruption Key Recipe
        Recipe.Create(ItemID.CorruptionKey)
            .AddIngredient(ItemID.TempleKey, 1)
            .AddIngredient(ModContent.ItemType<CorruptionKeyMold>())
            .AddIngredient(ItemID.SoulofFright, 5)
            .AddIngredient(ItemID.SoulofSight, 5)
            .AddIngredient(ItemID.SoulofMight, 5)
            .AddTile(TileID.MythrilAnvil)
            .Register();

        // Frozen Key Recipe
        Recipe.Create(ItemID.FrozenKey)
            .AddIngredient(ItemID.TempleKey, 1)
            .AddIngredient(ModContent.ItemType<FrozenKeyMold>())
            .AddIngredient(ItemID.SoulofFright, 5)
            .AddIngredient(ItemID.SoulofSight, 5)
            .AddIngredient(ItemID.SoulofMight, 5)
            .AddTile(TileID.MythrilAnvil)
            .Register();

        // Jungle Key Recipe
        Recipe.Create(ItemID.JungleKey)
            .AddIngredient(ItemID.TempleKey, 1)
            .AddIngredient(ModContent.ItemType<JungleKeyMold>())
            .AddIngredient(ItemID.SoulofFright, 5)
            .AddIngredient(ItemID.SoulofSight, 5)
            .AddIngredient(ItemID.SoulofMight, 5)
            .AddTile(TileID.MythrilAnvil)
            .Register();

        // Hallowed Key Recipe
        Recipe.Create(ItemID.HallowedKey)
            .AddIngredient(ItemID.TempleKey, 1)
            .AddIngredient(ModContent.ItemType<HallowedKeyMold>())
            .AddIngredient(ItemID.SoulofFright, 5)
            .AddIngredient(ItemID.SoulofSight, 5)
            .AddIngredient(ItemID.SoulofMight, 5)
            .AddTile(TileID.MythrilAnvil)
            .Register();

        // Desert Key Recipe
        Recipe.Create(ItemID.DungeonDesertKey)
            .AddIngredient(ItemID.TempleKey, 1)
            .AddIngredient(ModContent.ItemType<DesertKeyMold>())
            .AddIngredient(ItemID.SoulofFright, 5)
            .AddIngredient(ItemID.SoulofSight, 5)
            .AddIngredient(ItemID.SoulofMight, 5)
            .AddTile(TileID.MythrilAnvil)
            .Register();
        // Make Shroomite Bars cheaper
        Recipe.Create(ItemID.ShroomiteBar)
            .AddIngredient(ItemID.GlowingMushroom, 1)
            .AddIngredient(ItemID.ChlorophyteBar, 1)
            .AddTile(TileID.Autohammer)
            .Register();
        // Make suspicious eyes cheaper
        Recipe.Create(ItemID.SuspiciousLookingEye)
            .AddIngredient(ItemID.Lens, 3)
            .AddTile(TileID.DemonAltar)
            .Register();
        // Make worm food cheaper
        Recipe.Create(ItemID.WormFood)
            .AddIngredient(ItemID.RottenChunk, 6)
            .AddIngredient(ItemID.VilePowder, 10)
            .AddTile(TileID.DemonAltar)
            .Register();
        // Make Chlorophyte bars cheaper
        Recipe.Create(ItemID.ChlorophyteBar)
            .AddIngredient(ItemID.ChlorophyteOre, 4)
            .AddTile(TileID.AdamantiteForge)
            .Register();
        // Make spectre bars cheaper
        Recipe.Create(ItemID.SpectreBar, 3)
            .AddIngredient(ItemID.ChlorophyteBar, 3)
            .AddIngredient(ItemID.Ectoplasm, 1)
            .AddTile(TileID.AdamantiteForge)
            .Register();
        // Make Demon Wings cheaper
        Recipe.Create(ItemID.DemonWings, 1)
            .AddIngredient(ItemID.SoulofNight, 5)
            .AddIngredient(ItemID.Feather, 3)
            .AddIngredient(ItemID.SoulofFlight, 20)
            .AddTile(TileID.MythrilAnvil)
            .Register();
        // Make Angel Wings cheaper
        Recipe.Create(ItemID.AngelWings, 1)
            .AddIngredient(ItemID.SoulofLight, 5)
            .AddIngredient(ItemID.Feather, 3)
            .AddIngredient(ItemID.SoulofFlight, 20)
            .AddTile(TileID.MythrilAnvil)
            .Register();
        // Make Fairy Wings cheaper
        Recipe.Create(ItemID.FairyWings, 1)
            .AddIngredient(ItemID.PixieDust, 45)
            .AddIngredient(ItemID.SoulofFlight, 20)
            .AddTile(TileID.MythrilAnvil)
            .Register();
        // Recipe for Anvil Statue
        Recipe.Create(ItemID.AnvilStatue, 1)
            .AddIngredient(ItemID.IronAnvil, 5)
            .AddIngredient(ItemID.StoneBlock, 50)
            .AddTile(TileID.HeavyWorkBench)
            .Register();
        // Recipe for Boot Statue
        Recipe.Create(ItemID.BootStatue, 1)
            .AddIngredient(ItemID.HermesBoots, 5)
            .AddIngredient(ItemID.StoneBlock, 50)
            .AddTile(TileID.HeavyWorkBench)
            .Register();
        // Recipe for Skull Statue
        Recipe.Create(ItemID.GloomStatue, 1)
            .AddIngredient(ItemID.Skull, 5)
            .AddIngredient(ItemID.StoneBlock, 50)
            .AddTile(TileID.HeavyWorkBench)
            .Register();
        // Recipe for Sunflower Statue
        Recipe.Create(ItemID.SunflowerStatue, 1)
            .AddIngredient(ItemID.Sunflower, 5)
            .AddIngredient(ItemID.StoneBlock, 50)
            .AddTile(TileID.HeavyWorkBench)
            .Register();
        // Recipe for Duck Statue
        Recipe.Create(ItemID.DuckStatue, 1)
            .AddIngredient(ItemID.Duck, 1)
            .AddIngredient(ItemID.StoneBlock, 50)
            .AddTile(TileID.HeavyWorkBench)
            .Register();
        // Recipe for Frog Statue
        Recipe.Create(ItemID.FrogStatue, 1)
            .AddIngredient(ItemID.Frog, 1)
            .AddIngredient(ItemID.StoneBlock, 50)
            .AddTile(TileID.HeavyWorkBench)
            .Register();
        // Recipe for Snail Statue
        Recipe.Create(ItemID.SnailStatue, 1)
            .AddIngredient(ItemID.Snail, 1)
            .AddIngredient(ItemID.StoneBlock, 50)
            .AddTile(TileID.HeavyWorkBench)
            .Register();
        Recipe.Create(ItemID.Hellforge, 1)
            .AddIngredient(ItemID.Furnace, 1)
            .AddIngredient(ItemID.Hellstone, 30)
            .AddTile(TileID.Anvils)
            .DisableDecraft()
            .Register();
        // Recipe for all repaired shadowflame weapons
        Recipe.Create(ItemID.ShadowFlameBow, 1)
        .AddIngredient(ModContent.ItemType<BrokenShadowFlameBow>(), 1)
        .AddTile(TileID.TinkerersWorkbench)
         .Register();
        Recipe.Create(ItemID.ShadowFlameKnife, 1)
        .AddIngredient(ModContent.ItemType<BrokenShadowFlameKnife>(), 1)
        .AddTile(TileID.TinkerersWorkbench)
        .Register();
        Recipe.Create(ItemID.ShadowFlameHexDoll, 1)
        .AddIngredient(ModContent.ItemType<BrokenShadowFlameHexDoll>(), 1)
        .AddTile(TileID.TinkerersWorkbench)
        .Register();

        // Recipe for DeerThing
        Recipe.Create(ItemID.DeerThing, 1)
            .AddIngredient(ItemID.DemoniteOre, 5)
            .AddIngredient(ItemID.Lens, 1)
            .AddIngredient(ItemID.FlinxFur, 3)
            .Register();
    }
}