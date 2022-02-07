using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace WildFarming
{
    public class BlockEnhancedMushroom : BlockMushroom
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            string preference = Attributes?["needsTree"].AsString();

            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (preference != null)
            {
                dsc.AppendLine();
                dsc.AppendLine(Lang.Get("wildfarming:mushroom-" + preference));
            }
        }

        public override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (Variant["side"] != null)
            {
                BlockFacing face = BlockFacing.FromCode(Variant["side"]);
                Block hold = blockAccessor.GetBlock(pos.AddCopy(face));

                return hold is BlockLog;
            }

            return base.CanPlantStay(blockAccessor, pos);
        }
    }
}
