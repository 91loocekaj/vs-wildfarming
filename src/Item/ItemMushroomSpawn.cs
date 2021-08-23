using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace WildFarming
{
    public class ItemMushroomSpawn : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity?.World == null) return;

            //Setting up the variables will need

            IWorldAccessor world = byEntity.World;
            Block ground = world.BlockAccessor.GetBlock(blockSel.Position);
            BlockPos onPos = blockSel.Position.UpCopy(1);
            Block taken = world.BlockAccessor.GetBlock(onPos);
            Block mushroom = world.GetBlock(new AssetLocation("game:mushroom-" + slot.Itemstack.Collectible.CodeEndWithoutParts(1) + "-harvested-free"));
            //System.Diagnostics.Debug.WriteLine(plant);
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);


            // Checking to see if we can place the plant. If not this stops the method
            if (!byEntity.World.Claims.TryAccess(byPlayer, onPos, EnumBlockAccessFlags.BuildOrBreak)) return;
            if (taken.Replaceable <= 9501) return;
            if (ground.Fertility <= 0) return;



            // Placing the plant
            world.BlockAccessor.SetBlock(mushroom.BlockId, onPos);

            byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), onPos.X, onPos.Y, onPos.Z, byPlayer);

            ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            if (byPlayer?.WorldData?.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }

            handling = EnumHandHandling.PreventDefault;
        }
    }
}
