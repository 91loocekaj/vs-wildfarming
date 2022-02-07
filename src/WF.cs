using HarmonyLib;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.ServerMods;

namespace WildFarming
{
    public class WildFarming : ModSystem
    {
        private Harmony harmony;
        //Static references
        public static string[] Conifers = new string[] { "pine", "baldcypress", "larch", "redwood", "greenspirecypress" };
        public static string[] Decidious = new string[] { "birch", "oak", "maple", "ebony", "walnut", "crimsonkingmaple" };
        public static string[] Tropical = new string[] { "kapok", "purpleheart" };

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);

            try
            {
                BotanyConfig FromDisk;
                if ((FromDisk = api.LoadModConfig<BotanyConfig>("WildFarmingConfig.json")) == null)
                {
                    api.StoreModConfig<BotanyConfig>(BotanyConfig.Loaded, "WildFarmingConfig.json");
                }
                else BotanyConfig.Loaded = FromDisk;
            }
            catch
            {
                api.StoreModConfig<BotanyConfig>(BotanyConfig.Loaded, "WildFarmingConfig.json");
            }

            api.World.Config.SetBool("WFflowersEnabled", BotanyConfig.Loaded.FlowersEnabled);
            api.World.Config.SetBool("WFseedPanningEnabled", BotanyConfig.Loaded.SeedPanningEnabled);
            api.World.Config.SetBool("WFcropsEnabled", BotanyConfig.Loaded.CropSeedsEnabled);
            api.World.Config.SetBool("WFbushesEnabled", BotanyConfig.Loaded.BushSeedsEnabled && !api.ModLoader.IsModEnabled("wildcraft"));
            api.World.Config.SetBool("WFcactiEnabled", BotanyConfig.Loaded.CactiSeedsEnabled);
            api.World.Config.SetBool("WFmushroomsEnabled", BotanyConfig.Loaded.MushroomFarmingEnabled);
            api.World.Config.SetBool("WFvinesEnabled", BotanyConfig.Loaded.VineGrowthEnabled);
            api.World.Config.SetBool("WFlogScoringEnabled", BotanyConfig.Loaded.LogScoringEnabled);
            api.World.Config.SetBool("WFreedsEnabled", BotanyConfig.Loaded.ReedCloningEnabled);
            api.World.Config.SetBool("WFseaweedEnabled", BotanyConfig.Loaded.SeaweedGrowthEnabled);
            api.World.Config.SetBool("WFtermitesEnabled", BotanyConfig.Loaded.TermitesEnabled);

        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("wildseed", typeof(WildSeed));
            api.RegisterItemClass("ItemMushroomSpawn", typeof(ItemMushroomSpawn));

            api.RegisterBlockClass("BlockEnhancedVines", typeof(BlockEnhancedVines));
            api.RegisterBlockClass("BlockTrunk", typeof(BlockTrunk));
            api.RegisterBlockClass("BlockLivingLogSection", typeof(BlockLivingLogSection));
            api.RegisterBlockClass("BlockMushroomSubstrate", typeof(BlockMushroomSubstrate));
            api.RegisterBlockClass("BlockEnhancedMushroom", typeof(BlockEnhancedMushroom));

            api.RegisterBlockEntityClass("WildPlant", typeof(WildPlantBlockEntity));
            api.RegisterBlockEntityClass("MushroomSubstrate", typeof(BlockEntityMushroomSubstrate));
            api.RegisterBlockEntityClass("BEVines", typeof(BEVines));
            api.RegisterBlockEntityClass("BESeaweed", typeof(BESeaweed));
            api.RegisterBlockEntityClass("RegenSapling", typeof(BlockEntityRegenSapling));
            api.RegisterBlockEntityClass("TreeTrunk", typeof(BlockEntityTrunk));
            api.RegisterBlockEntityClass("TermiteMound", typeof(BlockEntityTermiteMound));

            api.RegisterBlockBehaviorClass("Score", typeof(BlockBehaviorScore));

            harmony = new Harmony("com.jakecool19.wildfarming.lootvessel");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }

        public static string GetTreeFamily(string tree)
        {
            if (Conifers.Contains(tree)) return "conifer";
            if (Decidious.Contains(tree)) return "decidious";
            if (Tropical.Contains(tree)) return "tropical";

            return null;
        }
    }
}
