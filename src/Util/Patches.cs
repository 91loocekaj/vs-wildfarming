using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace WildFarming
{
    [HarmonyPatch(typeof(TreeGen))]
    public class TreeGenModifications
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            //From Melchoir
            if (original != null)
            {
                foreach (var patched in harmony.GetPatchedMethods())
                {
                    if (patched.Name == original.Name) return false;
                }
            }

            return true;
        }

        [HarmonyPatch("getPlaceResumeState")]
        [HarmonyPrefix]
        static void StopGrowingInto(BlockPos targetPos, ref int desiredblockId, IBlockAccessor ___api)
        {
            Block check = ___api.GetBlock(targetPos);
            Block desired = ___api.GetBlock(desiredblockId);

            if (check.Replaceable == desired.Replaceable)
            {
                //System.Diagnostics.Debug.WriteLine("Stopped it");
                desiredblockId = 0;
            }
        }
    }
}
