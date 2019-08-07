﻿using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;

namespace BurnItForFuel
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        private static readonly Type patchType = typeof(HarmonyPatches);

        static HarmonyPatches()
        {
            
            HarmonyInstance harmonyInstance = HarmonyInstance.Create("JPT_BurnItForFuel");

            harmonyInstance.Patch(original: AccessTools.Method(type: typeof(RefuelWorkGiverUtility), name: "CanRefuel"), 
                prefix: new HarmonyMethod(type: patchType, name: nameof(Inhibitor)), postfix: new HarmonyMethod(type: patchType, name: nameof(CanRefuel_Postfix)), transpiler: null);

            harmonyInstance.Patch(original: AccessTools.Method(type: typeof(RefuelWorkGiverUtility), name: "FindBestFuel"),
                prefix: new HarmonyMethod(type: patchType, name: nameof(Inhibitor)), postfix: new HarmonyMethod(type: patchType, name: nameof(FindBestFuel_Postfix)), transpiler: null);

            harmonyInstance.Patch(original: AccessTools.Method(type: typeof(RefuelWorkGiverUtility), name: "FindAllFuel"),
                prefix: new HarmonyMethod(type: patchType, name: nameof(Inhibitor)), postfix: new HarmonyMethod(type: patchType, name: nameof(FindAllFuel_Postfix)), transpiler: null);

        }

        public static bool Inhibitor(MethodInfo __originalMethod)
        {
            //Log.Message("Preventing vanilla " + __originalMethod);
            return true;
        }

        public static void CanRefuel_Postfix(object __instance, Pawn pawn, Thing t, bool forced, ref bool __result)
        {
            __result = CanRefuel(pawn, t, forced);
        }

        public static bool CanRefuel(Pawn pawn, Thing t, bool forced = false)
        {
            CompRefuelable compRefuelable = t.TryGetComp<CompRefuelable>();
            if (compRefuelable == null || compRefuelable.IsFull)
            {
                return false;
            }
            bool flag = !forced;
            if (flag && !compRefuelable.ShouldAutoRefuelNow)
            {
                return false;
            }
            if (!t.IsForbidden(pawn))
            {
                LocalTargetInfo target = t;
                if (pawn.CanReserve(target, 1, -1, null, forced))
                {
                    if (t.Faction != pawn.Faction)
                    {
                        return false;
                    }

                    ThingFilter fuelFilter = new ThingFilter();
                    fuelFilter = t.TryGetComp<CompSelectFuel>().fuelSettings.filter;
                    if (FindBestFuel(pawn, t) == null)
                    {
                        JobFailReason.Is("NoFuelToRefuel".Translate(fuelFilter.Summary), null);
                        return false;
                    }
                    if (t.TryGetComp<CompRefuelable>().Props.atomicFueling && FindAllFuel(pawn, t) == null)
                    {

                        JobFailReason.Is("NoFuelToRefuel".Translate(fuelFilter.Summary), null);
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        public static void FindBestFuel_Postfix(Pawn pawn, Thing refuelable, ref Thing __result)
        {
            __result = FindBestFuel(pawn, refuelable);
        }

        private static Thing FindBestFuel(Pawn pawn, Thing refuelable)
        {
            ThingFilter filter = new ThingFilter();
            filter = refuelable.TryGetComp<CompSelectFuel>().fuelSettings.filter;
            Predicate<Thing> predicate = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1, -1, null, false) && filter.Allows(x);
            IntVec3 position = pawn.Position;
            Map map = pawn.Map;
            ThingRequest bestThingRequest = filter.BestThingRequest;
            PathEndMode peMode = PathEndMode.ClosestTouch;
            TraverseParms traverseParams = TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false);
            Predicate<Thing> validator = predicate;
            return GenClosest.ClosestThingReachable(position, map, bestThingRequest, peMode, traverseParams, 9999f, validator, null, 0, -1, false, RegionType.Set_Passable, false);
        }

        public static void FindAllFuel_Postfix(Pawn pawn, Thing refuelable, ref List<Thing> __result, MethodInfo __originalMethod)
        {
            __result = FindAllFuel(pawn, refuelable);
        }

        private static List<Thing> FindAllFuel(Pawn pawn, Thing refuelable)
        {
            int quantity = refuelable.TryGetComp<CompRefuelable>().GetFuelCountToFullyRefuel();
            ThingFilter filter = new ThingFilter();
            filter = refuelable.TryGetComp<CompSelectFuel>().fuelSettings.filter;
            Predicate<Thing> validator = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1, -1, null, false) && filter.Allows(x);
            IntVec3 position = refuelable.Position;
            Region region = position.GetRegion(pawn.Map, RegionType.Set_Passable);
            TraverseParms traverseParams = TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false);
            RegionEntryPredicate entryCondition = (Region from, Region r) => r.Allows(traverseParams, false);
            List<Thing> chosenThings = new List<Thing>();
            int accumulatedQuantity = 0;
            RegionProcessor regionProcessor = delegate (Region r)
            {
                List<Thing> list = r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));
                for (int i = 0; i < list.Count; i++)
                {
                    Thing thing = list[i];
                    if (validator(thing))
                    {
                        if (!chosenThings.Contains(thing))
                        {
                            if (ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, r, PathEndMode.ClosestTouch, pawn))
                            {
                                chosenThings.Add(thing);
                                accumulatedQuantity += thing.stackCount;
                                if (accumulatedQuantity >= quantity)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            };
            RegionTraverser.BreadthFirstTraverse(region, entryCondition, regionProcessor, 99999, RegionType.Set_Passable);
            if (accumulatedQuantity >= quantity)
            {
                return chosenThings;
            }
            return null;
        }

    }
}

