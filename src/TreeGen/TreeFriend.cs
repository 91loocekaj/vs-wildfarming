using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace WildFarming
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TreeFriend
    {
        [JsonProperty]
        float MaxTemp = 50;

        [JsonProperty]
        float MinTemp = -50;

        [JsonProperty]
        float MaxRain = 1;

        [JsonProperty]
        float MinRain = 0;

        [JsonProperty]
        public float Weight = 1;

        [JsonProperty]
        string BlockCode = "air";

        [JsonProperty]
        public bool OnGround = true;

        [JsonProperty]
        public bool OnUnderneath = false;

        [JsonProperty]
        bool ReverseSide = false;

        Block ResolvedBlock;
        string NeedsWood;

        public bool Resolve(ICoreAPI api)
        {
            ResolvedBlock = api.World.BlockAccessor.GetBlock(new AssetLocation(BlockCode));

            if (ResolvedBlock != null)
            {
                NeedsWood = ResolvedBlock.Attributes?["needsTree"].AsString(null);
                return true;
            }

            return false;
        }

        public bool CanPlant(ClimateCondition conds, string treeType)
        {
            if (conds == null) return false;

            if (conds.Temperature > MaxTemp || conds.Temperature < MinTemp || conds.WorldgenRainfall > MaxRain || conds.WorldgenRainfall < MinRain || !NeedsCertainTree(treeType)) return false;

            return true;
        }

        public bool NeedsCertainTree(string type)
        {

            if (NeedsWood == null) return true;

            return type == NeedsWood;
        }

        public bool TryToPlant(BlockPos pos, IBlockAccessor changer, BlockFacing side)
        {
            if (ResolvedBlock == null) return false;

            

            BlockPos tmpPos = pos.Copy();

            if (OnGround)
            {
                tmpPos.Add(0, 1, 0);

                Block ground = changer.GetBlock(pos);

                if (ground.Fertility > 0 && ground.SideSolid[BlockFacing.UP.Index] && changer.GetBlock(tmpPos).IsReplacableBy(ResolvedBlock))
                {
                    changer.SetBlock(ResolvedBlock.BlockId, tmpPos);
                    return true;
                }
            }
            else if (OnUnderneath)
            {
                tmpPos.Add(0, -1, 0);

                if (changer.GetBlock(pos).SideSolid[BlockFacing.DOWN.Index] && changer.GetBlock(tmpPos).BlockId == 0)
                {
                    changer.SetBlock(ResolvedBlock.BlockId, tmpPos);
                    return true;
                }
            }
            else
            {
                Block rotatedBlock = changer.GetBlock(new AssetLocation(ResolvedBlock.Code.Domain, ResolvedBlock.CodeWithoutParts(1) + "-" + (ReverseSide ? side.Opposite.Code : side.Code)));
                if (rotatedBlock == null) return false;

                tmpPos.Add(side);

                if (changer.GetBlock(pos).SideSolid[side.Index] && changer.GetBlock(tmpPos).BlockId == 0)
                {
                    changer.SetBlock(rotatedBlock.BlockId, tmpPos);
                    return true;
                }
            }

            return false;
        }
    }
}
