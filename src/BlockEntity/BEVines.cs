using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace WildFarming
{
    public class BEVines : BlockEntity
    {
        private double plantedAt;
        private double blossomAt;
        private double growthTime = 12;
        private float minTemp;
        private float maxTemp;
        RoomRegistry rmaker;
        bool greenhouse;
        long growthTick;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            rmaker = api.ModLoader.GetModSystem<RoomRegistry>();
            growthTick = RegisterGameTickListener(growthMonitior, 3000);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            plantedAt = Api.World.Calendar.TotalHours;
            if (Api.Side == EnumAppSide.Server) blossomAt = Api.World.Calendar.TotalHours + ((block.Attributes["hours"].AsDouble(growthTime) * 0.75) + ((block.Attributes["hours"].AsDouble(growthTime) * 1.25) - (block.Attributes["hours"].AsDouble(growthTime) * 0.75)) * Api.World.Rand.NextDouble());
            minTemp = block.Attributes["minTemp"].AsFloat(-5f);
            maxTemp = block.Attributes["maxTemp"].AsFloat(50f);
        }

        public void growthMonitior(float dt)
        {
            //Determines if the plant is ready to blossom
            if (Api.World.BlockAccessor.GetBlock(Pos.DownCopy()).Id != 0) return;
            Room room = rmaker?.GetRoomForPosition(Pos);
            greenhouse = (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0);

            if (blossomAt > Api.World.Calendar.TotalHours) return;
            float temperature = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Temperature;

            if ((temperature < minTemp || temperature > maxTemp) && !greenhouse)
            {
                blossomAt += 18;
                return;
            }

            Block self = Api.World.BlockAccessor.GetBlock(Pos);
            AssetLocation plantCode = self.CodeWithPart("section", 1);
            Block plant = Api.World.GetBlock(plantCode);

            if (plant == null) return;

            Api.World.BlockAccessor.SetBlock(plant.Id, Pos);
            Api.World.BlockAccessor.SetBlock(self.Id, Pos.DownCopy());
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

            if (Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Temperature > maxTemp && !greenhouse) dsc.AppendLine("Too hot to grow!");
            if (Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Temperature < minTemp && !greenhouse) dsc.AppendLine("Too cold to grow!");
            if (greenhouse) dsc.AppendLine("Greenhouse bonus!");
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
