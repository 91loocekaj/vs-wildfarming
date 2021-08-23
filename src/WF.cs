using HarmonyLib;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.ServerMods;

namespace WildFarming
{
    public class WildFarming : ModSystem
    {
        private Harmony harmony;

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
            api.World.Config.SetBool("WFbushesEnabled", BotanyConfig.Loaded.BushSeedsEnabled);
            api.World.Config.SetBool("WFcactiEnabled", BotanyConfig.Loaded.CactiSeedsEnabled);
            api.World.Config.SetBool("WFmushroomsEnabled", BotanyConfig.Loaded.MushroomSpawnEnabled);
            api.World.Config.SetBool("WFvinesEnabled", BotanyConfig.Loaded.VineGrowthEnabled);
            api.World.Config.SetBool("WFlogScoringEnabled", BotanyConfig.Loaded.LogScoringEnabled);
            api.World.Config.SetBool("WFreedsEnabled", BotanyConfig.Loaded.ReedCloningEnabled);
            api.World.Config.SetBool("WFseaweedEnabled", BotanyConfig.Loaded.SeaweedGrowthEnabled);

        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("wildseed", typeof(WildSeed));
            api.RegisterItemClass("ItemMushroomSpawn", typeof(ItemMushroomSpawn));

            api.RegisterBlockClass("BlockEnhancedVines", typeof(BlockEnhancedVines));
            api.RegisterBlockClass("BlockTrunk", typeof(BlockTrunk));
            api.RegisterBlockClass("BlockLivingLogSection", typeof(BlockLivingLogSection));

            api.RegisterBlockEntityClass("WildPlant", typeof(WildPlantBlockEntity));
            api.RegisterBlockEntityClass("BEVines", typeof(BEVines));
            api.RegisterBlockEntityClass("BESeaweed", typeof(BESeaweed));
            api.RegisterBlockEntityClass("RegenSapling", typeof(BlockEntityRegenSapling));
            api.RegisterBlockEntityClass("TreeTrunk", typeof(BlockEntityTrunk));

            api.RegisterBlockBehaviorClass("Score", typeof(BlockBehaviorScore));

            harmony = new Harmony("com.jakecool19.wildfarming.lootvessel");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }
    }

    public class BotanyConfig
    {
        public static BotanyConfig Loaded { get; set; } = new BotanyConfig();

        //Tree settings
        public int MaxTreeGrowthStages { get; set; } = 4;

        public float SaplingToTreeSize { get; set; } = 0.6f;

        public float TreeSizePerGrowthStage { get; set; } = 0.125f;

        public float TreeRevertGrowthTempThreshold { get; set; } = 5f;

        public float TreeRegenMultiplier { get; set; } = 1f;

        //Wild Plants settings
        public bool HarshWildPlants { get; set; } = true;

        //Enable/Disable settings
        public bool FlowersEnabled { get; set; } = true;

        public bool SeedPanningEnabled { get; set; } = true;

        public bool CropSeedsEnabled { get; set; } = true;

        public bool BushSeedsEnabled { get; set; } = true;

        public bool CactiSeedsEnabled { get; set; } = true;

        public bool MushroomSpawnEnabled { get; set; } = true;

        public bool VineGrowthEnabled { get; set; } = true;

        public bool LogScoringEnabled { get; set; } = true;

        public bool ReedCloningEnabled { get; set; } = true;

        public bool LivingTreesEnabled { get; set; } = true;

        public bool HarshSaplingsEnabled { get; set; } = true;

        public bool SeaweedGrowthEnabled { get; set; } = true;
    }
}
