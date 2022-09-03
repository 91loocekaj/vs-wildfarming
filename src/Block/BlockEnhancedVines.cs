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
            BlockPos apos = pos.AddCopy(VineFacing.Opposite);
            Block block = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlock(apos).Id);

            return block.CanAttachBlockAt(world.BlockAccessor, this, apos, VineFacing) || world.BlockAccessor.GetBlock(pos.UpCopy()) is BlockVines;
        }
    }
}
