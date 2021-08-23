using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace WildFarming
{
    public class BlockEntityTrunk : BlockEntity
    {
        //Set at runtime/from saved attributes
        Vec4i[] Tree;
        Vec4i[] Leaves;
        string[] Codes;        
        float regenPerc;
        double lastChecked;
        int growthStage;
        int currentGrowthTime;

        //Set from json attributes
        float[] nutrientsConsumedOnRegen;
        float[] nutrientsConsumedOnGrowth;
        Block deathBlock;
        int timeForNextStage;
        float maxTemp;
        float minTemp;
        float maxMoisture;
        float minMoisture;

        //For chunk checking
        int mincx, mincy, mincz, maxcx, maxcy, maxcz;

        //For block setting
        public IBulkBlockAccessor changer;



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (!BotanyConfig.Loaded.LivingTreesEnabled)
            {
                Api.World.BlockAccessor.SetBlock(deathBlock.BlockId, Pos);
                return;
            }

            RegisterGameTickListener(Regenerate, 3000);
            nutrientsConsumedOnRegen = Block.Attributes["regenNutrients"].AsArray<float>(new float[] { 5, 5, 5});
            nutrientsConsumedOnGrowth = Block.Attributes["growthNutrients"].AsArray<float>(new float[] { 25, 25, 25 });
            deathBlock = api.World.GetBlock(new AssetLocation(Block.Attributes["deathState"].AsString("game:air")));
            timeForNextStage = Block.Attributes["growthTime"].AsInt(96);

            minTemp = Block.Attributes["minTemp"].AsFloat(0f);
            maxTemp = Block.Attributes["maxTemp"].AsFloat(60f);
            minMoisture = Block.Attributes["minMoisture"].AsFloat(0f);
            //Max moisture disabled for now due to rain
            maxMoisture = 1; //Block.Attributes["maxMoisture"].AsFloat(1f);

            changer = Api.World.GetBlockAccessorBulkUpdate(true, true);
            changer.ReadFromStagedByDefault = true;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            lastChecked = Api.World.Calendar.TotalHours;
        }

        public void Regenerate(float dt)
        {
            if (Api.Side != EnumAppSide.Server || Tree == null || Tree.Length < 0 || Leaves == null || Leaves.Length < 0) return;
            ICoreServerAPI sapi = Api as ICoreServerAPI;

            //Check if all chunks are loaded
            for (int cx = mincx; cx <= maxcx; cx++)
            {
                for (int cy = mincy; cy <= maxcy; cy++)
                {
                    for (int cz = mincz; cz <= maxcz; cz++)
                    {
                        if (sapi.WorldManager.GetChunk(cx, cy, cz) == null) return;
                    }
                }
            }

            double hoursPerDay = Api.World.Calendar.HoursPerDay;
            double sinceLastChecked = Api.World.Calendar.TotalHours - lastChecked;
            if (sinceLastChecked < hoursPerDay) return;
            int daysPassed = 0;

            while (sinceLastChecked >= hoursPerDay)
            {
                sinceLastChecked -= hoursPerDay;
                lastChecked += hoursPerDay;
                daysPassed++;
            }

            BlockPos tmpPos = Pos.Copy();
            Block desiredBlock;
            Block blockThere;

            //Checking for damage to the trees
            foreach (Vec4i log in Tree)
            {
                tmpPos.Set(log.X, log.Y, log.Z);
                desiredBlock = Api.World.GetBlock(new AssetLocation(Codes[log.W]));
                blockThere = Api.World.BlockAccessor.GetBlock(tmpPos);

                if (blockThere.FirstCodePart() != desiredBlock.FirstCodePart() || blockThere.Variant["wood"] != desiredBlock.Variant["wood"] || blockThere.Variant["type"] == "placed")
                {
                    Api.World.BlockAccessor.SetBlock(deathBlock.BlockId, Pos);
                    return;
                }
            }

            //Don't do anything if too hot or too cold
            ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues);
            if (conds != null && (conds.Temperature < minTemp || conds.Temperature > maxTemp))
            {
                if (conds.Temperature < minTemp - BotanyConfig.Loaded.TreeRevertGrowthTempThreshold || conds.Temperature > maxTemp + BotanyConfig.Loaded.TreeRevertGrowthTempThreshold)
                {
                    regenPerc = 0f;
                    if (currentGrowthTime > timeForNextStage / 2) currentGrowthTime -= (int)hoursPerDay;
                }
            }

            //Needs to be on farmland to grow further and regenerate
            BlockEntityFarmland fl = Api.World.BlockAccessor.GetBlockEntity(tmpPos.Set(Pos).Down()) as BlockEntityFarmland;
            if (fl == null)
            {
                Api.World.BlockAccessor.SetBlock(deathBlock.BlockId, Pos);
                return;
            }

            //Don't do anything if too dry or too wet
            if (fl.MoistureLevel > maxMoisture || fl.MoistureLevel < minMoisture) return;

            //Check for missing leaves
            Queue<Vec4i> missingLeaves = new Queue<Vec4i>();
            Queue<Vec4i> regenedLeaves = new Queue<Vec4i>();
            HashSet<Vec4i> blockedLeaves = new HashSet<Vec4i>();

            foreach (Vec4i leaf in Leaves)
            {
                tmpPos.Set(leaf.X, leaf.Y, leaf.Z);
                desiredBlock = Api.World.GetBlock(new AssetLocation(Codes[leaf.W]));
                blockThere = Api.World.BlockAccessor.GetBlock(tmpPos);

                if (blockThere.BlockId != desiredBlock.BlockId)
                {
                    if (desiredBlock.Replaceable >= blockThere.Replaceable) blockedLeaves.Add(leaf); else missingLeaves.Enqueue(leaf);
                }
            }

            //Mark for regeneration
            for (int d = 0; d < daysPassed; d++)
            {
                for (int h = 0; h < 24; h++)
                {
                    if (missingLeaves.Count > 0 && NutrientsToRegen(fl))
                    {
                        regenPerc += 1f - Math.Max(0.05f, (float)(missingLeaves.Count + blockedLeaves.Count) / (float)Leaves.Length) * BotanyConfig.Loaded.TreeRegenMultiplier;
                        if (regenPerc > 1f)
                        {
                            regenPerc -= 1f;
                            regenedLeaves.Enqueue(missingLeaves.Dequeue());
                            fl.Nutrients[0] -= nutrientsConsumedOnRegen[0];
                            fl.Nutrients[1] -= nutrientsConsumedOnRegen[1];
                            fl.Nutrients[2] -= nutrientsConsumedOnRegen[2];
                        }
                    }

                    if (growthStage <= BotanyConfig.Loaded.MaxTreeGrowthStages && missingLeaves.Count <= 0 && blockedLeaves.Count <= 0) currentGrowthTime++;
                }
            }

            //See if it is time to grow or not
            bool growNow = false;


            while (growthStage <= BotanyConfig.Loaded.MaxTreeGrowthStages && currentGrowthTime >= timeForNextStage &&
                NutrientsToGrow(fl))
            {
                currentGrowthTime -= timeForNextStage;
                growthStage++;
                growNow = true;
                fl.Nutrients[0] -= nutrientsConsumedOnGrowth[0];
                fl.Nutrients[1] -= nutrientsConsumedOnGrowth[1];
                fl.Nutrients[2] -= nutrientsConsumedOnGrowth[2];
            }

            //If  the tree is going to grow into another stage, then we do not really need to set the regenerated leaf blocks

            if (growNow)
            {
                foreach (Vec4i leaf in Leaves)
                {
                    tmpPos.Set(leaf.X, leaf.Y, leaf.Z);
                    changer.SetBlock(0, tmpPos);
                }

                foreach (Vec4i log in Tree)
                {
                    tmpPos.Set(log.X, log.Y, log.Z);
                    changer.SetBlock(0, tmpPos);
                }

                changer.Commit();
                

                string treeGenCode = Block.Attributes?["treeGen"].AsString(null);
                if (treeGenCode == null) return;
                ITreeGenerator gen;
                AssetLocation code = new AssetLocation(treeGenCode);
                                
                if (!sapi.World.TreeGenerators.TryGetValue(code, out gen)) return;
                
                
                float size = 0.6f + (0.125f * growthStage);
                sapi.World.TreeGenerators[code].GrowTree(changer, Pos.AddCopy(0, Block.Variant["wood"] == "redwood" ? 1 : 0, 0), size, 0, 0);
                setupTree(changer.Commit());
            }
            else if (regenedLeaves.Count > 0)
            {
                while (regenedLeaves.Count > 0)
                {
                    Vec4i leaf = regenedLeaves.Dequeue();
                    desiredBlock = Api.World.GetBlock(new AssetLocation(Codes[leaf.W]));
                    tmpPos.Set(leaf.X, leaf.Y, leaf.Z);
                    changer.SetBlock(desiredBlock.BlockId, tmpPos);
                }

                changer.Commit();
            }

            MarkDirty();
            fl.MarkDirty();
        }

        public void setupTree(List<BlockUpdate> commited)
        {
            List<Vec4i> tmpLogs = new List<Vec4i>();
            List<Vec4i> tmpLeaves = new List<Vec4i>();
            List<string> codes = new List<string>();

            mincx = Pos.X;
            mincy = Pos.Y;
            mincz = Pos.Z;
            maxcx = Pos.X;
            maxcy = Pos.Y;
            maxcz = Pos.Z;

            for (int i = 0; i < commited.Count; i++)
            {
                if (commited[i].Pos.X > maxcx) maxcx = commited[i].Pos.X;
                if (commited[i].Pos.X < mincx) mincx = commited[i].Pos.X;

                if (commited[i].Pos.Y > maxcy) maxcy = commited[i].Pos.Y;
                if (commited[i].Pos.Y < mincy) mincy = commited[i].Pos.Y;

                if (commited[i].Pos.Z > maxcz) maxcz = commited[i].Pos.Z;
                if (commited[i].Pos.Z < mincz) mincz = commited[i].Pos.Z;

                Block treeBlock = Api.World.GetBlock(commited[i].NewBlockId);
                int localId = 0;
                string dAp = treeBlock.Code.Domain + ":" + treeBlock.Code.Path;

                if ((localId = codes.IndexOf(dAp)) == -1)
                {
                    codes.Add(dAp);
                    localId = codes.IndexOf(dAp);
                }
                
                if (treeBlock is BlockLeaves || treeBlock.Attributes?.IsTrue("isLeaf") == true)
                {
                    tmpLeaves.Add(new Vec4i(commited[i].Pos.X, commited[i].Pos.Y, commited[i].Pos.Z, localId));
                }
                else
                {
                    tmpLogs.Add(new Vec4i(commited[i].Pos.X, commited[i].Pos.Y, commited[i].Pos.Z, localId));
                }
            }

            ICoreServerAPI sapi = Api as ICoreServerAPI;

            if (sapi != null)
            {
                int chunksize = Api.World.BlockAccessor.ChunkSize;
                int sizeX = sapi.WorldManager.MapSizeX / chunksize;
                int sizeY = sapi.WorldManager.MapSizeY / chunksize;
                int sizeZ = sapi.WorldManager.MapSizeZ / chunksize;

                mincx = GameMath.Clamp(mincx / chunksize, 0, sizeX - 1);
                maxcx = GameMath.Clamp(maxcx / chunksize, 0, sizeX - 1);
                mincy = GameMath.Clamp(mincy / chunksize, 0, sizeY - 1);
                maxcy = GameMath.Clamp(maxcy / chunksize, 0, sizeY - 1);
                mincz = GameMath.Clamp(mincz / chunksize, 0, sizeZ - 1);
                maxcz = GameMath.Clamp(maxcz / chunksize, 0, sizeZ - 1);
            }

            Tree = tmpLogs.ToArray();
            Leaves = tmpLeaves.ToArray();
            Codes = codes.ToArray();

            Array.Sort(Tree, (a, b) =>
            {
                int ahortDiff = Math.Max(Math.Abs(a.X - Pos.X), Math.Abs(a.Z - Pos.Z));
                int bhortDiff = Math.Max(Math.Abs(b.X - Pos.X), Math.Abs(b.Z - Pos.Z));

                if (ahortDiff - bhortDiff != 0) return ahortDiff - bhortDiff;

                return (a.Y - Pos.Y) - (b.Y - Pos.Y);
            });

            Array.Sort(Leaves, (a, b) =>
            {
                int ahortDiff = Math.Max(Math.Abs(a.X - Pos.X), Math.Abs(a.Z - Pos.Z));
                int bhortDiff = Math.Max(Math.Abs(b.X - Pos.X), Math.Abs(b.Z - Pos.Z));

                if (ahortDiff - bhortDiff != 0) return ahortDiff - bhortDiff;

                return (a.Y - Pos.Y) - (b.Y - Pos.Y);
            });

            MarkDirty();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("lastChecked", lastChecked);
            tree.SetFloat("regenPerc", regenPerc);
            tree.SetInt("currentGrowthTime", currentGrowthTime);
            tree.SetInt("growthStage", growthStage);

            tree.SetInt("mincx", mincx);
            tree.SetInt("mincy", mincy);
            tree.SetInt("mincz", mincz);
            tree.SetInt("maxcx", maxcx);
            tree.SetInt("maxcy", maxcy);
            tree.SetInt("maxcz", maxcz);

            tree["blockCodes"] = new StringArrayAttribute(Codes);

            if (Tree != null && Tree.Length > 0)
            {
                ITreeAttribute logStorage = tree.GetOrAddTreeAttribute("logStorage");
                int[] logX = new int[Tree.Length];
                int[] logY = new int[Tree.Length];
                int[] logZ = new int[Tree.Length];
                int[] logW = new int[Tree.Length];

                for (int i = 0; i < Tree.Length; i++)
                {
                    logX[i] = Tree[i].X;
                    logY[i] = Tree[i].Y;
                    logZ[i] = Tree[i].Z;
                    logW[i] = Tree[i].W;
                }

                logStorage["logX"] = new IntArrayAttribute(logX);
                logStorage["logY"] = new IntArrayAttribute(logY);
                logStorage["logZ"] = new IntArrayAttribute(logZ);
                logStorage["logW"] = new IntArrayAttribute(logW);
            }

            if (Leaves != null && Leaves.Length > 0)
            {
                ITreeAttribute leavesStorage = tree.GetOrAddTreeAttribute("leavesStorage");
                int[] leavesX = new int[Leaves.Length];
                int[] leavesY = new int[Leaves.Length];
                int[] leavesZ = new int[Leaves.Length];
                int[] leavesW = new int[Leaves.Length];

                for (int i = 0; i < Leaves.Length; i++)
                {
                    leavesX[i] = Leaves[i].X;
                    leavesY[i] = Leaves[i].Y;
                    leavesZ[i] = Leaves[i].Z;
                    leavesW[i] = Leaves[i].W;
                }

                leavesStorage["leavesX"] = new IntArrayAttribute(leavesX);
                leavesStorage["leavesY"] = new IntArrayAttribute(leavesY);
                leavesStorage["leavesZ"] = new IntArrayAttribute(leavesZ);
                leavesStorage["leavesW"] = new IntArrayAttribute(leavesW);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            lastChecked = tree.GetDouble("lastChecked", worldAccessForResolve.Calendar.TotalHours);
            regenPerc = tree.GetFloat("regenPerc");
            currentGrowthTime = tree.GetInt("currentGrowthTime");
            growthStage = tree.GetInt("growthStage");

            mincx = tree.GetInt("mincx");
            maxcx = tree.GetInt("maxcx");
            mincy = tree.GetInt("mincy");
            maxcy = tree.GetInt("maxcy");
            mincz = tree.GetInt("mincz");
            maxcz = tree.GetInt("maxcz");

            List<Vec4i> logsBack = new List<Vec4i>();
            List<Vec4i> leavesBack = new List<Vec4i>();
            int[] xValues;
            int[] yValues;
            int[] zValues;
            int[] wValues;

            ITreeAttribute logs = tree.GetTreeAttribute("logStorage");
            ITreeAttribute leaves = tree.GetTreeAttribute("leavesStorage");
            Codes = (tree["blockCodes"] as StringArrayAttribute).value;

            if (logs != null)
            {
                xValues = (logs["logX"] as IntArrayAttribute)?.value;
                yValues = (logs["logY"] as IntArrayAttribute)?.value;
                zValues = (logs["logZ"] as IntArrayAttribute)?.value;
                wValues = (logs["logW"] as IntArrayAttribute)?.value;

                if (xValues != null)
                {
                    for (int i = 0; i < xValues.Length; i++)
                    {
                        logsBack.Add(new Vec4i(xValues[i], yValues[i], zValues[i], wValues[i]));
                    }

                    Tree = logsBack.ToArray();
                }
            }

            if (leaves != null)
            {
                xValues = (leaves["leavesX"] as IntArrayAttribute)?.value;
                yValues = (leaves["leavesY"] as IntArrayAttribute)?.value;
                zValues = (leaves["leavesZ"] as IntArrayAttribute)?.value;
                wValues = (leaves["leavesW"] as IntArrayAttribute)?.value;

                if (xValues != null)
                {
                    for (int i = 0; i < xValues.Length; i++)
                    {
                        leavesBack.Add(new Vec4i(xValues[i], yValues[i], zValues[i], wValues[i]));
                    }

                    Leaves = leavesBack.ToArray();
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (growthStage < BotanyConfig.Loaded.MaxTreeGrowthStages)
            {
                dsc.AppendLine(Lang.Get("wildfarming:tree-growthstage", growthStage + 1, BotanyConfig.Loaded.MaxTreeGrowthStages + 1));
            }
            else
            {
                dsc.AppendLine(Lang.Get("wildfarming:tree-mature"));
            }

            BlockEntityFarmland fl = Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy()) as BlockEntityFarmland;
            ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos);

            if (fl == null)
            {
                dsc.AppendLine(Lang.Get("wildfarming:tree-noland"));
                return;
            }

            if (conds.Temperature < minTemp) dsc.AppendLine(Lang.Get("wildfarming:tree-cold"));
            else if (conds.Temperature > maxTemp) dsc.AppendLine(Lang.Get("wildfarming:tree-hot"));
            else if (fl.MoistureLevel < minMoisture) dsc.AppendLine(Lang.Get("wildfarming:tree-dry"));
            else if (fl.MoistureLevel > maxMoisture) dsc.AppendLine(Lang.Get("wildfarming:tree-wet"));
            else if (!NutrientsToRegen(fl)) dsc.AppendLine(Lang.Get("wildfarming:tree-noregen"));
            else if (growthStage < BotanyConfig.Loaded.MaxTreeGrowthStages && !NutrientsToGrow(fl)) dsc.AppendLine(Lang.Get("wildfarming:tree-nogrow"));

            fl.GetBlockInfo(forPlayer, dsc);
        }

        public bool NutrientsToGrow(BlockEntityFarmland fl)
        {
            return fl.Nutrients[0] >= nutrientsConsumedOnGrowth[0] && fl.Nutrients[1] >= nutrientsConsumedOnGrowth[1] && fl.Nutrients[2] >= nutrientsConsumedOnGrowth[2];
        }

        public bool NutrientsToRegen(BlockEntityFarmland fl)
        {
            return fl.Nutrients[0] >= nutrientsConsumedOnRegen[0] && fl.Nutrients[1] >= nutrientsConsumedOnRegen[1] && fl.Nutrients[2] >= nutrientsConsumedOnRegen[2];
        }
    }
}
