using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace WildFarming
{
    public class BlockEntityRegenSapling : BlockEntity, ITreePoi
    {
        double totalHoursTillGrowth;
        long growListenerId;
        EnumTreeGrowthStage stage;
        bool plantedFromSeed;
        float maxTemp;
        float minTemp;
        public IBulkBlockAccessor changer;
        POIRegistry treeFinder;

        MeshData dirtMoundMesh
        {
            get
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                if (capi == null) return null;

                return ObjectCacheUtil.GetOrCreate(Api, "dirtMoundMesh", () =>
                {
                    MeshData mesh = null;

                    Shape shape = capi.Assets.TryGet(AssetLocation.Create("shapes/block/plant/dirtmound.json", Block.Code.Domain))?.ToObject<Shape>();
                    capi.Tesselator.TesselateShape(Block, shape, out mesh);

                    return mesh;
                });
            }
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            minTemp = Block.Attributes["minTemp"].AsFloat(0f);
            maxTemp = Block.Attributes["maxTemp"].AsFloat(60f);
            changer = Api.World.GetBlockAccessorBulkUpdate(true, true);
            changer.ReadFromStagedByDefault = true;
            

            if (api is ICoreServerAPI)
            {
                growListenerId = RegisterGameTickListener(CheckGrow, 2000);
                treeFinder = api.ModLoader.GetModSystem<POIRegistry>();
                treeFinder.AddPOI(this);
            }
        }

        NatFloat nextStageDaysRnd
        {
            get
            {
                if (stage == EnumTreeGrowthStage.Seed)
                {
                    NatFloat sproutDays = NatFloat.create(EnumDistribution.UNIFORM, 1.5f, 0.5f);
                    if (Block?.Attributes != null)
                    {
                        return Block.Attributes["growthDays"].AsObject(sproutDays);
                    }
                    return sproutDays;
                }

                NatFloat matureDays = NatFloat.create(EnumDistribution.UNIFORM, 7f, 2f);
                if (Block?.Attributes != null)
                {
                    return Block.Attributes["matureDays"].AsObject(matureDays);
                }
                return matureDays;
            }
        }

        float GrowthRateMod => Api.World.Config.GetString("saplingGrowthRate").ToFloat(1);

        public string Stage => stage == EnumTreeGrowthStage.Sapling ? "sapling" : "seed";

        public Vec3d Position => Pos.ToVec3d().Add(0.5);

        public string Type => "tree";

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            stage = byItemStack?.Collectible is ItemTreeSeed ? EnumTreeGrowthStage.Seed : EnumTreeGrowthStage.Sapling;
            plantedFromSeed = stage == EnumTreeGrowthStage.Seed;
            totalHoursTillGrowth = Api.World.Calendar.TotalHours + nextStageDaysRnd.nextFloat(1, Api.World.Rand) * 24 * GrowthRateMod;
        }


        private void CheckGrow(float dt)
        {
            if (Api.World.Calendar.TotalHours < totalHoursTillGrowth) return;

            ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues);
            
            if (BotanyConfig.Loaded.HarshSaplingsEnabled)
            {
                if (conds == null) return;
                if (conds.Temperature < minTemp || conds.Temperature > maxTemp)
                {
                    if (conds.Temperature < minTemp - BotanyConfig.Loaded.TreeRevertGrowthTempThreshold || conds.Temperature > maxTemp + BotanyConfig.Loaded.TreeRevertGrowthTempThreshold)
                    {
                        totalHoursTillGrowth = Api.World.Calendar.TotalHours + (float)Api.World.Rand.NextDouble() * 72 * GrowthRateMod;
                    }

                    return;
                }
            }
            else
            {
                if (conds == null || conds.Temperature < 5)
                {
                    return;
                }

                if (conds.Temperature < 0)
                {
                    totalHoursTillGrowth = Api.World.Calendar.TotalHours + (float)Api.World.Rand.NextDouble() * 72 * GrowthRateMod;
                    return;
                }
            }

            if (stage == EnumTreeGrowthStage.Seed)
            {
                stage = EnumTreeGrowthStage.Sapling;
                totalHoursTillGrowth = Api.World.Calendar.TotalHours + nextStageDaysRnd.nextFloat(1, Api.World.Rand) * 24 * GrowthRateMod;
                MarkDirty(true);
                return;
            }

            int chunksize = Api.World.BlockAccessor.ChunkSize;
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                Vec3i dir = facing.Normali;
                int x = Pos.X + dir.X * chunksize;
                int z = Pos.Z + dir.Z * chunksize;

                // Not at world edge and chunk is not loaded? We must be at the edge of loaded chunks. Wait until more chunks are generated
                if (Api.World.BlockAccessor.IsValidPos(x, Pos.Y, z) && Api.World.BlockAccessor.GetChunkAtBlockPos(x, Pos.Y, z) == null) return;
            }

            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            string treeGenCode = block.Attributes?["treeGen"].AsString(null);

            if (treeGenCode == null)
            {
                Api.Event.UnregisterGameTickListener(growListenerId);
                return;
            }

            AssetLocation code = new AssetLocation(treeGenCode);
            ICoreServerAPI sapi = Api as ICoreServerAPI;

            ITreeGenerator gen;
            if (!sapi.World.TreeGenerators.TryGetValue(code, out gen))
            {
                Api.Event.UnregisterGameTickListener(growListenerId);
                return;
            }

            bool doubleThick = false;
            bool found = true;
            Block trunk = Api.World.GetBlock(new AssetLocation("wildfarming:trunk-grown-maple")); ;

            switch (Block.Variant["wood"])
            {
                case null:
                    found = false;
                    break;
                case "greenbamboo":
                    found = false;
                    break;
                case "brownbamboo":
                    found = false;
                    break;
                case "ferntree":
                    found = false;
                    break;
                case "crimsonkingmaple":
                    trunk = Api.World.GetBlock(new AssetLocation("wildfarming:trunk-maple"));
                    break;
                case "greenspirecypress":
                    trunk = Api.World.GetBlock(new AssetLocation("wildfarming:trunk-baldcypress"));
                    break;
                default:
                    trunk = Api.World.GetBlock(new AssetLocation("wildfarming:trunk-" + Block.Variant["wood"]));
                    doubleThick = Block.Variant["wood"] == "redwood";
                    break;

            }

            if (BotanyConfig.Loaded.LivingTreesEnabled && found)
            {
                Api.World.BlockAccessor.SetBlock(trunk.BlockId, Pos);
                float size = BotanyConfig.Loaded.SaplingToTreeSize;
                sapi.World.TreeGenerators[code].GrowTree(changer, Pos.AddCopy(0, doubleThick ? 1 : 0, 0), true, size, 0, 0);
                BlockEntityTrunk growth = Api.World.BlockAccessor.GetBlockEntity(Pos) as BlockEntityTrunk;
                if (growth != null) growth.setupTree(changer.Commit()); else changer.Commit();
            }
            else
            {
                Api.World.BlockAccessor.SetBlock(0, Pos);
                float size = 0.6f + (float)Api.World.Rand.NextDouble() * 0.5f;
                sapi.World.TreeGenerators[code].GrowTree(changer, Pos.DownCopy(), true, size, 0, 0);
                changer.Commit();
            }
        }

        public override void OnBlockRemoved()
        {
            treeFinder?.RemovePOI(this);
            base.OnBlockRemoved();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("totalHoursTillGrowth", totalHoursTillGrowth);
            tree.SetInt("growthStage", (int)stage);
            tree.SetBool("plantedFromSeed", plantedFromSeed);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            totalHoursTillGrowth = tree.GetDouble("totalHoursTillGrowth", 0);
            stage = (EnumTreeGrowthStage)tree.GetInt("growthStage", 1);
            plantedFromSeed = tree.GetBool("plantedFromSeed");
        }

        public ItemStack[] GetDrops()
        {
            if (stage == EnumTreeGrowthStage.Seed)
            {
                Item item = Api.World.GetItem(AssetLocation.Create("treeseed-" + Block.Variant["wood"], Block.Code.Domain));
                return new ItemStack[] { new ItemStack(item) };
            }
            else
            {
                return new ItemStack[] { new ItemStack(Block) };
            }
        }


        public string GetBlockName()
        {
            if (stage == EnumTreeGrowthStage.Seed)
            {
                return Lang.Get("treeseed-planted-" + Block.Variant["wood"]);
            }
            else
            {
                return Block.OnPickBlock(Api.World, Pos).GetName();
            }
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            double hoursleft = totalHoursTillGrowth - Api.World.Calendar.TotalHours;
            double daysleft = hoursleft / Api.World.Calendar.HoursPerDay;

            if (stage == EnumTreeGrowthStage.Seed)
            {
                if (daysleft <= 1)
                {
                    dsc.AppendLine(Lang.Get("Will sprout in less than a day"));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("Will sprout in about {0} days", (int)daysleft));
                }
            }
            else
            {

                if (daysleft <= 1)
                {
                    dsc.AppendLine(Lang.Get("Will mature in less than a day"));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("Will mature in about {0} days", (int)daysleft));
                }
            }

            if (BotanyConfig.Loaded.HarshSaplingsEnabled)
            {
                ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues);

                if (conds.Temperature < minTemp) dsc.AppendLine(Lang.Get("wildfarming:tree-cold"));
                else if (conds.Temperature > maxTemp) dsc.AppendLine(Lang.Get("wildfarming:tree-hot"));
            }
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (plantedFromSeed)
            {
                mesher.AddMeshData(dirtMoundMesh);
            }

            if (stage == EnumTreeGrowthStage.Seed)
            {
                return true;
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        public  bool IsSuitableFor(Entity entity, string[] diet)
        {
            if (diet == null) return false;

            return diet.Contains("Wood");
        }

        public float ConsumeOnePortion()
        {
            if (0.05f > Api.World.Rand.NextDouble())
            {
                Api.World.BlockAccessor.BreakBlock(Pos, null);
                return 1;
            }

            return 0.1f;
        }
    }
}
