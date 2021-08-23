using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace WildFarming
{
    public class BlockBehaviorScore : BlockBehavior
    {
        float scoreTime;

        public AssetLocation scoringSound;

        AssetLocation scoredBlockCode;
        Block scoredBlock;
        WorldInteraction[] interactions;

        public BlockBehaviorScore(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            if (!block.Code.Path.Contains("log-grown-pine-")) return;

            scoreTime = properties["scoreTime"].AsFloat(0);

            string code = properties["scoringSound"].AsString("game:sounds/block/chop3");
            if (code != null)
            {
                scoringSound = AssetLocation.Create(code, block.Code.Domain);
            }

        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (!block.Code.Path.Contains("log-grown-pine-")) return;
            scoredBlockCode = new AssetLocation("log-resinharvested-pine-ud");
            scoredBlock = api.World.GetBlock(scoredBlockCode);
            if (scoredBlock == null)
            {
                api.World.Logger.Warning("Unable to resolve scored block code '{0}' for block {1}. Will ignore.", scoredBlockCode, block.Code);
            }

            interactions = ObjectCacheUtil.GetOrCreate(api, "resinHarvest", () =>
            {
                List<ItemStack> knifeStacklist = new List<ItemStack>();

                foreach (Item item in api.World.Items)
                {
                    if (item.Code == null) continue;

                    if (item.Tool == EnumTool.Knife)
                    {
                        knifeStacklist.Add(new ItemStack(item));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "wildfarming:blockhelp-score",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = knifeStacklist.ToArray()
                    }
                };
            });
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            if (!block.Code.Path.Contains("log-grown-pine-") || byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible?.Tool != EnumTool.Knife) return false;

            handling = EnumHandling.PreventDefault;

            world.PlaySoundAt(scoringSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
            return true;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handled)
        {
            if (blockSel == null) return false;

            handled = EnumHandling.PreventDefault;

            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);

            if (world.Rand.NextDouble() < 0.1)
            {
                world.PlaySoundAt(scoringSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
            }

            return world.Side == EnumAppSide.Client || secondsUsed < scoreTime;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;


            if (secondsUsed > scoreTime - 0.05f && world.Side == EnumAppSide.Server)
            {
                ItemStack knife = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
                if (knife != null && knife.Collectible.Tool == EnumTool.Knife)
                {
                    knife.Collectible.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot, 15);
                    if (scoredBlock != null)
                    {
                        world.BlockAccessor.SetBlock(scoredBlock.BlockId, blockSel.Position);
                    }

                    world.PlaySoundAt(scoringSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                }
            }
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handled)
        {
            if (!block.Code.Path.Contains("log-grown-pine-")) return null;
            return interactions;
        }
    }
}
