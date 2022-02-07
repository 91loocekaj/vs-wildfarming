using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace WildFarming
{
    public class WildPlantBlockEntity : BlockEntity
    {
        private double plantedAt;
        private double blossomAt;
        private double growthTime = 72;
        private float minTemp;
        private float maxTemp;
        RoomRegistry rmaker;
        bool greenhouse;

        public override void Initialize(ICoreAPI api)
        {
            // Registers the updater
            base.Initialize(api);
            RegisterGameTickListener(UpdateStep, 1200);
            rmaker = api.ModLoader.GetModSystem<RoomRegistry>();
        }

        public override void OnBlockPlaced(ItemStack byItemStack)
        {
            //Sets up the properties
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            plantedAt = Api.World.Calendar.TotalHours;
            if (Api.Side == EnumAppSide.Server) blossomAt = Api.World.Calendar.TotalHours + ((block.Attributes["hours"].AsDouble(growthTime) * 0.75) + ((block.Attributes["hours"].AsDouble(growthTime) * 1.25) - (block.Attributes["hours"].AsDouble(growthTime) * 0.75)) * Api.World.Rand.NextDouble());
            minTemp = block.Attributes["minTemp"].AsFloat(-5f);
            maxTemp = block.Attributes["maxTemp"].AsFloat(50f);
        }

        public void UpdateStep(float step)
        {
            //Determines if the plant is ready to blossom
            if (Api.Side != EnumAppSide.Server) return;
            Room room = rmaker?.GetRoomForPosition(Pos);
            greenhouse = (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0);

            if (blossomAt > Api.World.Calendar.TotalHours) return;
            ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues);

            if (conds == null) return;

            if (BotanyConfig.Loaded.HarshWildPlants && (conds.Temperature < minTemp || conds.Temperature > maxTemp) && !greenhouse)
            {
                blossomAt += 18;
                return;
            }

            string plantCode = Block.CodeEndWithoutParts(1);
            if (plantCode == null) Api.World.BlockAccessor.BreakBlock(Pos, null);
            else
            {
                Block plant = Api.World.GetBlock(new AssetLocation("game:" + plantCode));

                Api.World.BlockAccessor.SetBlock(plant.Id, Pos);
            }

            MarkDirty();
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

            if (!BotanyConfig.Loaded.HarshWildPlants) return;

            if (Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Temperature > maxTemp && !greenhouse) dsc.AppendLine("Too hot to grow!");
            if (Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues).Temperature < minTemp && !greenhouse) dsc.AppendLine("Too cold to grow!");
            if (greenhouse) dsc.AppendLine("Greenhouse bonus!");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            // Saves Properties
            base.ToTreeAttributes(tree);
            tree.SetDouble("plantedAt", plantedAt);
            tree.SetDouble("blossomAt", blossomAt);
            tree.SetDouble("growthTime", growthTime);
            tree.SetFloat("minTemp", minTemp);
            tree.SetFloat("maxTemp", maxTemp);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            // Gets Properties
            base.FromTreeAttributes(tree, worldAccessForResolve);
            plantedAt = tree.GetDouble("plantedAt");
            blossomAt = tree.GetDouble("blossomAt");
            growthTime = tree.GetDouble("growthTime");
            minTemp = tree.GetFloat("minTemp");
            maxTemp = tree.GetFloat("maxTemp");
        }
    }
}