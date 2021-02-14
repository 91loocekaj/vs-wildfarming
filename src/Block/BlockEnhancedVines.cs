using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace WildFarming
{
    public class BlockEnhancedVines : BlockVines
    {
        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!CanVineStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
                world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
                return;
            }
        }

        bool CanVineStay(IWorldAccessor world, BlockPos pos)
        {
            BlockFacing facing = GetOrientation();
            Block block = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(pos.AddCopy(facing.Opposite)));

            return block.CanAttachBlockAt(world.BlockAccessor, this, pos, facing) || world.BlockAccessor.GetBlock(pos.UpCopy()) is BlockVines;
        }
    }
}
