using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace WildFarming
{
    public class BlockEntityTrunk : BlockEntity, ITreePoi
    {
        //Set at runtime/from saved attributes
        Vec4i[] Tree;
        Vec4i[] Leaves;
        string[] Codes;        
        float regenPerc;
        float logRecovery;
        double lastChecked;
        int growthStage;
        int currentGrowthTime;
        int currentRepopTime;
        POIRegistry treeFinder;
        TreeFriend[] repopBuddies;
        float friendsWeight;
        BlockFacing[] rndFaces = { BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.EAST, BlockFacing.WEST};
        string treeFamily;
        GasHelper gasPlug;

        //Leaves for health calculation
        int BrokenLeaves;
        int DiseasedParts;

        int CurrentHealthyParts
        {
            get
            {
                if (Tree == null) return 0;
                int trueCount = Tree.Length - DiseasedParts;

                return trueCount < 0 ? 0 : trueCount;
            }
        }

        int CurrentLeaves
        {
            get
            {
                if (Leaves == null) return 0;
                int trueCount = Leaves.Length - BrokenLeaves;

                return trueCount < 0 ? 0 : trueCount;
            }
        }

        float CurrentHealth
        {
            get 
            { 
                float leavesHealth = (float)CurrentLeaves / (float)Leaves.Length;
                float treeHealth = (float)CurrentHealthyParts / (float)Tree.Length;
                return (0.5f * leavesHealth) + (0.5f * treeHealth);
            }
        }

        //Set from json attributes
        Block deathBlock;
        int timeForNextStage;
        float maxTemp;
        float minTemp;

        //For chunk checking
        int mincx, mincy, mincz, maxcx, maxcy, maxcz;

        //For block setting
        public IBulkBlockAccessor changer;

        public string Stage => growthStage >= BotanyConfig.Loaded.MaxTreeGrowthStages ? "mature" : "young-" + growthStage;

        public Vec3d Position => Pos.ToVec3d().Add(0.5);

        public string Type => "tree";

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            RegisterGameTickListener(Regenerate, 3000);

            gasPlug = api.ModLoader.GetModSystem<GasHelper>();

            deathBlock = api.World.GetBlock(new AssetLocation(Block.Attributes["deathState"].AsString("game:air")));
            timeForNextStage = Block.Attributes["growthTime"].AsInt(96);

            minTemp = Block.Attributes["minTemp"].AsFloat(0f);
            maxTemp = Block.Attributes["maxTemp"].AsFloat(60f);

            TreeFriend[] jsonFriends = Block.Attributes["treeFriends"].AsObject<TreeFriend[]>();
            
            if (jsonFriends != null && jsonFriends.Length > 0)
            {
                List<TreeFriend> checkFriends = new List<TreeFriend>();

                for (int i = 0; i < jsonFriends.Length; i++)
                {
                    bool resolved = jsonFriends[i].Resolve(api);

                    if (resolved)
                    {
                        checkFriends.Add(jsonFriends[i]);
                        friendsWeight += jsonFriends[i].Weight;
                    }
                }

                repopBuddies = checkFriends.ToArray();
            }
            else repopBuddies = new TreeFriend[0];

            treeFamily = WildFarming.GetTreeFamily(Block.Attributes?["treeGen"].AsString(null));

            if (api.Side == EnumAppSide.Server)
            {
                treeFinder = api.ModLoader.GetModSystem<POIRegistry>();
                treeFinder.AddPOI(this);
            }
            
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
            if (!BotanyConfig.Loaded.LivingTreesEnabled)
            {
                Api.World.BlockAccessor.SetBlock(deathBlock.BlockId, Pos);
                treeFinder.RemovePOI(this);
                return;
            }

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
            bool growNow = false;
            List<ClimateCondition> dailyConds = new List<ClimateCondition>();

            //Find out how many days have passed and get climate for those days
            while (sinceLastChecked >= hoursPerDay)
            {
                sinceLastChecked -= hoursPerDay;
                lastChecked += hoursPerDay;
                daysPassed++;
                dailyConds.Add(Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, lastChecked));
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
                    treeFinder.RemovePOI(this);
                    return;
                }
            }

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

            BrokenLeaves = missingLeaves.Count + blockedLeaves.Count;

            //Mark for regeneration
            for (int d = 0; d < daysPassed; d++)
            {
                if (dailyConds[d].Temperature < minTemp || dailyConds[d].Temperature > maxTemp)
                {
                    //Do not do anything if too or too hot
                    if (dailyConds[d].Temperature < minTemp - BotanyConfig.Loaded.TreeRevertGrowthTempThreshold || dailyConds[d].Temperature > maxTemp + BotanyConfig.Loaded.TreeRevertGrowthTempThreshold)
                    {
                        regenPerc = 0f;
                        if (currentGrowthTime > timeForNextStage / 2) currentGrowthTime -= (int)hoursPerDay;
                    }
                }
                else
                {
                    for (int h = 0; h < 24; h++)
                    {
                        if (missingLeaves.Count > 0 || DiseasedParts > 0)
                        {
                            regenPerc += Math.Max(0.01f, CurrentHealth) * BotanyConfig.Loaded.TreeRegenMultiplier;
                            if (regenPerc > 1f)
                            {
                                regenPerc -= 1f;
                                if (DiseasedParts > 0)
                                {
                                    logRecovery++;
                                    if (logRecovery >= 5)
                                    {
                                        DiseasedParts--;
                                        logRecovery = 0;
                                    }
                                }
                                else if (CurrentLeaves < Leaves.Length)
                                {
                                    regenedLeaves.Enqueue(missingLeaves.Dequeue());
                                    BrokenLeaves--;
                                }
                            }
                        }

                        if (CurrentHealth > 0.85)
                        {
                            if (growthStage < BotanyConfig.Loaded.MaxTreeGrowthStages) currentGrowthTime++;
                            else currentRepopTime++;

                            while (growthStage <= BotanyConfig.Loaded.MaxTreeGrowthStages && currentGrowthTime >= timeForNextStage)
                            {
                                currentGrowthTime -= timeForNextStage;
                                growthStage++;
                                growNow = true;
                            }
                        }
                    }
                }
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
                sapi.World.TreeGenerators[code].GrowTree(changer, Pos.AddCopy(0, Block.Variant["wood"] == "redwood" ? 1 : 0, 0), true, size);
                setupTree(changer.Commit());
            }
            else if (regenedLeaves.Count > 0)
            {
                //We did not grow up, so let's regen
                while (regenedLeaves.Count > 0)
                {
                    Vec4i leaf = regenedLeaves.Dequeue();
                    desiredBlock = Api.World.GetBlock(new AssetLocation(Codes[leaf.W]));
                    tmpPos.Set(leaf.X, leaf.Y, leaf.Z);
                    changer.SetBlock(desiredBlock.BlockId, tmpPos);
                }

                changer.Commit();
            }

            if (currentRepopTime >= 24)
            {
                bool sapped = false;
                int matureDay = dailyConds.Count;

                while (currentRepopTime >= 24)
                {
                    currentRepopTime -= 24;
                    matureDay--;
                    int foilTriesPerDay = BotanyConfig.Loaded.TreeFoilageTriesPerDay;

                    //Plant foilage

                    if (repopBuddies.Length > 0 && BotanyConfig.Loaded.TreeFoilageChance >= Api.World.Rand.NextDouble())
                    {
                        TreeFriend pop = GetRandomFriend(Api.World.Rand, dailyConds[matureDay]);

                        if (pop != null)
                        {
                            while (foilTriesPerDay > 0)
                            {
                                bool foilPlanted = false;

                                if (pop.OnGround)
                                {
                                    int foilX = Api.World.Rand.Next(-BotanyConfig.Loaded.GrownTreeRepopMinimum + 1, BotanyConfig.Loaded.GrownTreeRepopMinimum);
                                    int foilZ = Api.World.Rand.Next(-BotanyConfig.Loaded.GrownTreeRepopMinimum + 1, BotanyConfig.Loaded.GrownTreeRepopMinimum);
                                    tmpPos.Set(Pos);
                                    tmpPos.Add(foilX, -BotanyConfig.Loaded.GrownTreeRepopVertSearch, foilZ);

                                    for (int f = tmpPos.Y; f < BotanyConfig.Loaded.GrownTreeRepopVertSearch; f++)
                                    {
                                        tmpPos.Y += 1;
                                        if (pop.TryToPlant(tmpPos, changer, null))
                                        {
                                            Block floor = changer.GetBlock(new AssetLocation("game:forestfloor-" + Api.World.Rand.Next(8)));
                                            changer.SetBlock(floor.BlockId, tmpPos);
                                            foilPlanted = true;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    Vec4i randomLog = Tree[Api.World.Rand.Next(Tree.Length)];
                                    tmpPos.Set(randomLog.X, randomLog.Y, randomLog.Z);

                                    if (pop.OnUnderneath) pop.TryToPlant(tmpPos, changer, null);
                                    else
                                    {
                                        rndFaces.Shuffle(Api.World.Rand);

                                        foreach (BlockFacing side in rndFaces)
                                        {
                                            if (pop.TryToPlant(tmpPos, changer, side))
                                            {
                                                foilPlanted = true;
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (foilPlanted) foilTriesPerDay = 0; else foilTriesPerDay--;
                            }

                        }
                    }

                    //Plant a sapling
                    if (!sapped && Api.World.Rand.NextDouble() <= BotanyConfig.Loaded.TreeRepopChance)
                    {
                        //Plant sapling
                        Block plant = Api.World.GetBlock(new AssetLocation("sapling-" + Block.Variant["wood"] + "-free"));

                        bool whichside = Api.World.Rand.NextDouble() > 0.5;
                        int side = Api.World.Rand.NextDouble() > 0.5 ? -BotanyConfig.Loaded.GrownTreeRepopMinimum : BotanyConfig.Loaded.GrownTreeRepopMinimum;
                        int sidepos = Api.World.Rand.Next(-BotanyConfig.Loaded.GrownTreeRepopMinimum, BotanyConfig.Loaded.GrownTreeRepopMinimum + 1);
                        tmpPos.Set(Pos);
                        tmpPos.Add(whichside ? side : sidepos, -BotanyConfig.Loaded.GrownTreeRepopVertSearch, !whichside ? side : sidepos);

                        bool groundCheck = false;

                        IPointOfInterest found = treeFinder.GetNearestPoi(tmpPos.ToVec3d().Add(0.5, BotanyConfig.Loaded.GrownTreeRepopVertSearch, 0.5), BotanyConfig.Loaded.GrownTreeRepopMinimum, (poi) =>
                        {
                            if (poi == this || !(poi is ITreePoi)) return false;
                            return true;
                        });

                        if (found == null)
                        {
                            for (int f = tmpPos.Y; f < BotanyConfig.Loaded.GrownTreeRepopVertSearch; f++)
                            {
                                tmpPos.Y += 1;
                                Block foilSearch = Api.World.BlockAccessor.GetBlock(tmpPos);
                                if (foilSearch == null) continue;

                                if (groundCheck)
                                {
                                    if (foilSearch.IsReplacableBy(plant))
                                    {

                                        Api.World.BlockAccessor.SetBlock(plant.BlockId, tmpPos);
                                        sapped = true;
                                        break;
                                    }
                                    else groundCheck = foilSearch.Fertility > 0 && foilSearch.SideSolid[BlockFacing.UP.Index];
                                }
                                else
                                {
                                    groundCheck = foilSearch.Fertility > 0 && foilSearch.SideSolid[BlockFacing.UP.Index];
                                }
                            }
                        }
                    }
                }

                changer.Commit();
            }

            gasPlug?.CollectGases(Pos, (growthStage + 1) * 10, new string[] { "silicadust", "coaldust", "carbondioxide", "carbonmonoxide", "sulfurdioxide", "nitrogendioxide"});

            MarkDirty();
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
            BrokenLeaves = 0;
            DiseasedParts = 0;

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

        private TreeFriend GetRandomFriend(Random rand, ClimateCondition conds)
        {
            TreeFriend result = null;
            int tries = 20;

            while (result == null && tries > 0)
            {
                double rndTarget = rand.NextDouble() * (double)friendsWeight;
                repopBuddies.Shuffle(rand);
                tries--;

                foreach (TreeFriend friend in repopBuddies)
                {
                    rndTarget -= friend.Weight;

                    if (rndTarget <= 0)
                    {
                        if (friend.CanPlant(conds, treeFamily)) result = friend;

                        break;
                    }
                }
            }

            return result;
        }

        public void DestroyTree(int amount)
        {
            if (Tree == null) return;
            DiseasedParts = GameMath.Clamp(DiseasedParts + amount, 0, Tree.Length);
            MarkDirty();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("lastChecked", lastChecked);
            tree.SetFloat("regenPerc", regenPerc);
            tree.SetFloat("logRecovery", logRecovery);
            tree.SetInt("currentGrowthTime", currentGrowthTime);
            tree.SetInt("currentRepopTime", currentRepopTime);
            tree.SetInt("growthStage", growthStage);
            tree.SetInt("BrokenLeaves", BrokenLeaves);
            tree.SetInt("DestroyedLeaves", DiseasedParts);

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
            logRecovery = tree.GetFloat("logRecovery");
            currentGrowthTime = tree.GetInt("currentGrowthTime");
            currentRepopTime = tree.GetInt("currentRepopTime");
            growthStage = tree.GetInt("growthStage");
            BrokenLeaves = tree.GetInt("BrokenLeaves");
            DiseasedParts = tree.GetInt("DestroyedLeaves");


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

            if (Leaves == null) return;

            dsc.AppendLine(Lang.Get("wildfarming:tree-health", (CurrentHealth * 100).ToString("#.#"), 100));

            if (growthStage < BotanyConfig.Loaded.MaxTreeGrowthStages)
            {
                dsc.AppendLine(Lang.Get("wildfarming:tree-growthstage", growthStage + 1, BotanyConfig.Loaded.MaxTreeGrowthStages + 1));
            }
            else
            {
                dsc.AppendLine(Lang.Get("wildfarming:tree-mature"));
            }

            ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues);

            if (conds.Temperature < minTemp) dsc.AppendLine(Lang.Get("wildfarming:tree-cold"));
            else if (conds.Temperature > maxTemp) dsc.AppendLine(Lang.Get("wildfarming:tree-hot"));
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (Api.Side == EnumAppSide.Server) treeFinder?.RemovePOI(this);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api.Side == EnumAppSide.Server) treeFinder?.RemovePOI(this);
        }

        public bool IsSuitableFor(Entity entity)
        {
            if (CurrentHealthyParts < 1) return false;
            string[] diet = entity.Properties.Attributes?["blockDiet"]?.AsArray<string>();
            if (diet == null) return false;

            return diet.Contains("Wood");
        }

        public float ConsumeOnePortion()
        {
            DestroyTree(1);
            return 1;
        }
    }
}
