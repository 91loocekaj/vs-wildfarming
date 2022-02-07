using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace WildFarming
{
    public class BESeaweed : BlockEntity
    {
        private double plantedAt;
        private double blossomAt;
        private double growthTime = 12;
        private float minTemp;
        private float maxTemp;
        long growthTick;
        Block waterBlock;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            waterBlock = api.World.BlockAccessor.GetBlock(new AssetLocation("game:water-still-7"));
            growthTick = RegisterGameTickListener(growthMonitior, 3000);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            plantedAt = Api.World.Calendar.TotalHours;

            if (block.Attributes == null)
            {
                minTemp = 1;
                maxTemp = 50f;
                blossomAt = Api.World.Calendar.TotalHours + growthTime;

                return;
            }

            if (Api.Side == EnumAppSide.Server) blossomAt = Api.World.Calendar.TotalHours + ((block.Attributes["hours"].AsDouble(growthTime) * 0.75) + ((block.Attributes["hours"].AsDouble(growthTime) * 1.25) - (block.Attributes["hours"].AsDouble(growthTime) * 0.75)) * Api.World.Rand.NextDouble());
            minTemp = block.Attributes["minTemp"].AsFloat(-5f);
            maxTemp = block.Attributes["maxTemp"].AsFloat(50f);
        }

        public void growthMonitior(float dt)
        {
            //Determines if the plant is ready to blossom
            if (Api.World.BlockAccessor.GetBlock(Pos.UpCopy()).Id != waterBlock.Id || Api.World.BlockAccessor.GetBlock(Pos.UpCopy(2)).Id != waterBlock.Id) return;
            

            if (blossomAt > Api.World.Calendar.TotalHours) return;
            float temperature = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Temperature;

            if (temperature < minTemp || temperature > maxTemp)
            {
                blossomAt += 18;
                return;
            }

            Block self = Api.World.BlockAccessor.GetBlock(Pos);
            AssetLocation plantCode = self.CodeWithPart("section", 1);
            Block plant = Api.World.GetBlock(plantCode);

            if (plant == null) return;

            Api.World.BlockAccessor.SetBlock(plant.Id, Pos);
            Api.World.BlockAccessor.SetBlock(self.Id, Pos.UpCopy());
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            //Displays how much time is left
            double daysleft = (blossomAt - Api.World.Calendar.TotalHours) / Api.World.Calendar.HoursPerDay;
            if (daysleft >= 1)
            {
                dsc.AppendLine((int)daysleft + " days until mature.");
            }
            else
            {
                dsc.AppendLine("Less than one day until mature.");
            }

            if (Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Temperature > maxTemp) dsc.AppendLine("Too hot to grow!");
            if (Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Temperature < minTemp) dsc.AppendLine("Too cold to grow!");
            
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("plantedAt", plantedAt);
            tree.SetDouble("blossomAt", blossomAt);
            tree.SetDouble("growthTime", growthTime);
            tree.SetFloat("minTemp", minTemp);
            tree.SetFloat("maxTemp", maxTemp);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            plantedAt = tree.GetDouble("plantedAt");
            blossomAt = tree.GetDouble("blossomAt");
            growthTime = tree.GetDouble("growthTime");
            minTemp = tree.GetFloat("minTemp");
            maxTemp = tree.GetFloat("maxTemp");
        }
    }
}
