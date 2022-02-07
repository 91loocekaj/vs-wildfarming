using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace WildFarming
{
    public class BlockTrunk : Block
    {
        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            return Lang.Get("wildfarming:block-trunk-" + Variant["wood"]);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.GetBlock(new AssetLocation(Attributes["deathState"].AsString("game:air"))));
        }
    }
}
