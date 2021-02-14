using HarmonyLib;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace WildFarming
{
    public class WildFarming : ModSystem
    {
        private Harmony harmony;
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("wildseed", typeof(WildSeed));
            api.RegisterItemClass("ItemMushroomSpawn", typeof(ItemMushroomSpawn));

            api.RegisterBlockClass("BlockEnhancedVines", typeof(BlockEnhancedVines));

            api.RegisterBlockEntityClass("WildPlant", typeof(WildPlantBlockEntity));
            api.RegisterBlockEntityClass("BEVines", typeof(BEVines));
            api.RegisterBlockEntityClass("BESeaweed", typeof(BESeaweed));

            harmony = new Harmony("com.jakecool19.wildfarming.lootvessel");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }
    }

    [HarmonyPatch(typeof(BlockLootVessel))]
    [HarmonyPatch("BlockLootVessel", MethodType.Constructor)]
    class LootVesselPatch
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            BlockLootVessel.lootLists["seed"] = LootList.Create(2,
                LootItem.Item(1f, 5, 7, "seeds-carrot", "seeds-onion", "seeds-spelt", "seeds-turnip", "seeds-rice", "seeds-rye", "seeds-soybean", "seeds-pumpkin", "seeds-cabbage"),
                LootItem.Item(1f, 5, 7, "wildfarming:wildseeds-herb-basil", "wildfarming:wildseeds-herb-thyme", "wildfarming:wildseeds-herb-sage", "wildfarming:wildseeds-herb-chamomile", "wildfarming:wildseeds-herb-mint", "wildfarming:wildseeds-herb-saffron", "wildfarming:wildseeds-herb-marjoram", "wildfarming:wildseeds-herb-cilantro", "wildfarming:wildseeds-herb-lavender")
            );
            BlockLootVessel.lootLists["food"] = LootList.Create(1,
                LootItem.Item(1.5f, 8, 15, "grain-spelt", "grain-rice", "grain-flax", "grain-rye"),
                LootItem.Item(1, 8, 15, "redmeat-cured", "bushmeat-cured", "poultry-cured", "pickledlegume-soybean"),
                LootItem.Item(1, 8, 15, "pickledvegetable-carrot", "pickledvegetable-parsnip", "pickledvegetable-turnip", "pickledvegetable-pumpkin", "pickledvegetable-onion", "pickledvegetable-cabbage"),
                LootItem.Item(0.1f, 1, 1, "resonancearchive-1", "resonancearchive-2", "resonancearchive-3", "resonancearchive-4", "resonancearchive-5", "resonancearchive-6", "resonancearchive-7", "resonancearchive-8", "resonancearchive-9")
            );
            BlockLootVessel.lootLists["forage"] = LootList.Create(2.5f,
                LootItem.Item(1, 2, 6, "flint"),
                LootItem.Item(1, 3, 9, "stick"),
                LootItem.Item(1, 3, 16, "drygrass"),
                LootItem.Item(1, 3, 24, "stone-chalk"),
                LootItem.Item(1, 3, 24, "clay-blue", "clay-fire"),
                LootItem.Item(1, 3, 24, "cattailtops"),
                LootItem.Item(1, 1, 4, "poultice-linen-horsetail"),
                LootItem.Item(0.5f, 1, 12, "flaxfibers"),
                LootItem.Item(0.3f, 1, 3, "honeycomb"),
                LootItem.Item(1f, 2, 6, "herbbundle-basil", "herbbundle-thyme", "herbbundle-sage", "herbbundle-saffron", "herbbundle-marjoram", "herbbundle-lavender", "herbbundle-chamomile", "herbbundle-cilantro", "herbbundle-mint"),
                LootItem.Item(0.3f, 2, 6, "beenade-closed")
            );
            BlockLootVessel.lootLists["farming"] = LootList.Create(2.5f,
                LootItem.Item(0.5f, 5, 7, "seeds-carrot", "seeds-onion", "seeds-spelt", "seeds-turnip", "seeds-rice", "seeds-rye", "seeds-soybean", "seeds-pumpkin", "seeds-cabbage"),
                LootItem.Item(0.5f, 5, 7, "wildfarming:wildseeds-herb-basil", "wildfarming:wildseeds-herb-thyme", "wildfarming:wildseeds-herb-sage", "wildfarming:wildseeds-herb-chamomile", "wildfarming:wildseeds-herb-mint", "wildfarming:wildseeds-herb-saffron", "wildfarming:wildseeds-herb-marjoram", "wildfarming:wildseeds-herb-cilantro", "wildfarming:wildseeds-herb-lavender"),
                LootItem.Item(0.75f, 3, 10, "feather"),
                LootItem.Item(0.75f, 2, 10, "flaxfibers"),
                LootItem.Item(0.35f, 2, 10, "flaxtwine"),
                LootItem.Item(0.5f, 2, 6, "herbbundle-basil", "herbbundle-thyme", "herbbundle-sage", "herbbundle-saffron", "herbbundle-marjoram", "herbbundle-lavender", "herbbundle-chamomile", "herbbundle-cilantro", "herbbundle-mint"),
                LootItem.Item(0.75f, 5, 10, "cattailtops"),
                LootItem.Item(0.1f, 1, 1, "scythe-copper", "scythe-tinbronze")
            );
        }
    }
}
