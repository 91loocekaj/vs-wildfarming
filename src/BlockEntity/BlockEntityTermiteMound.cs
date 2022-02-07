using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace WildFarming
{
    public class BlockEntityTermiteMound : BlockEntity
    {
        double lastChecked;
        float colonySupplies;
        POIRegistry treeFinder;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 1000);
                treeFinder = api.ModLoader.GetModSystem<POIRegistry>();
            }
        }

        private void OnServerTick(float dt)
        {
            if (!BotanyConfig.Loaded.TermitesEnabled || Api.Side != EnumAppSide.Server || Api.World.Calendar.TotalHours - lastChecked < 1) return;

            lastChecked = Api.World.Calendar.TotalHours;
            bool newNest = false;

            treeFinder.WalkPois(Pos.ToVec3d().Add(0.5), 30, (poi) => {

                ITreePoi tree = poi as ITreePoi;
                if (tree == null) return true;
                colonySupplies += tree.ConsumeOnePortion();

                if (colonySupplies > 100 && !newNest)
                {
                    BlockPos swarmPos = tree.Position.AsBlockPos.Add(-2, 0, -2);

                    for (int x = 0; x < 3; x++)
                    {
                        swarmPos.X++;
                        for (int z = 0; z < 3; z++)
                        {
                            swarmPos.Z++;

                            Block candidate = Api.World.BlockAccessor.GetBlock(swarmPos);

                            if (!candidate.IsLiquid() && candidate.IsReplacableBy(Block))
                            {
                                Api.World.BlockAccessor.SetBlock(Block.Id, swarmPos);
                                newNest = true;
                                colonySupplies -= 100;
                            }

                            if (newNest) break;
                        }
                        if (newNest) break;
                        swarmPos.Z -= 3;
                    }
                }

                return true;
            
            });
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("lastChecked", lastChecked);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            lastChecked = tree.GetDouble("lastChecked", Api?.World.Calendar.TotalHours ?? 1);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            lastChecked = Api.World.Calendar.TotalHours;
        }
    }
}
