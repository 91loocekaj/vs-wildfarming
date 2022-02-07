using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace WildFarming
{
    public class BlockMushroomSubstrate : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityMushroomSubstrate mse = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMushroomSubstrate;
            if (mse?.AlreadyGrowing != false || !(byPlayer?.InventoryManager.ActiveHotbarSlot.Itemstack?.Block is BlockMushroom)) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (mse.FirstTimeMushroomSetup(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Block))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack result = base.OnPickBlock(world, pos);

            BlockEntityMushroomSubstrate mse = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityMushroomSubstrate;
            if (mse != null)
            {
                mse.SetItemstackAttributes(result);
            }

            return result;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            string mushroom;
            if ((mushroom = inSlot?.Itemstack?.Attributes.GetString("mushroomBlockCode")) != null)
            {
                Block mushroomBlock = world.GetBlock(new AssetLocation(mushroom));
                dsc.AppendLine(Lang.Get("wildfarming:msubstrate-incubating", Lang.GetMatching("block-" + mushroomBlock.Code.Path)));
            }
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }
    }
}
