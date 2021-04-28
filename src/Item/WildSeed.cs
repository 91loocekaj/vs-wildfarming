using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace WildFarming
{


    public class WildSeed : Item
    {
    
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity?.World == null) return;

            //Setting up the variables will need

            IWorldAccessor world = byEntity.World;
            Block ground = world.BlockAccessor.GetBlock(blockSel.Position);
            BlockPos onPos = blockSel.Position.UpCopy(1);
            Block taken = world.BlockAccessor.GetBlock(onPos);
            string plant = slot.Itemstack.Collectible.CodeEndWithoutParts(1);
            Block wildPlant = world.GetBlock(new AssetLocation("wildfarming:wildplant-" + plant));
            //System.Diagnostics.Debug.WriteLine(plant);
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);


            // Checking to see if we can place the plant. If not this stops the method
            if (!byEntity.World.Claims.TryAccess(byPlayer, onPos, EnumBlockAccessFlags.BuildOrBreak)) return;
            if (!ground.SideSolid[blockSel.Face.Index]) return;
            if (taken.BlockId != 0) return;
            if (ground.Fertility <= 0) return;



            // Placing the plant
            world.BlockAccessor.SetBlock(wildPlant.BlockId, onPos);

            byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), onPos.X, onPos.Y, onPos.Z, byPlayer);

            ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            if (byPlayer?.WorldData?.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }

            handling = EnumHandHandling.PreventDefault;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            Block block = world.GetBlock(new AssetLocation("wildfarming:wildplant-" + CodeEndWithoutParts(1)));

            dsc.AppendLine("Average Grow Time: " + block.Attributes["hours"].AsFloat(192f)/24);
            dsc.AppendLine("Maximum Growing Temperature: " + block.Attributes["maxTemp"].AsFloat(50f));
            dsc.AppendLine("Minimum Growing Temperature: " + block.Attributes["minTemp"].AsFloat(-5f));
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-plant",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
