using Verse;
using RimWorld;
using Verse.AI;
using Verse.AI.Group;
using System.Collections.Generic;

namespace TKS_Feasts
{
    public static class Toils_Feast
    {
        public static Danger MaxDangerDining => Danger.None;

        private static readonly Dictionary<Pair<Pawn, Region>, bool> dangerousRegionsCache = new Dictionary<Pair<Pawn, Region>, bool>();

            //shamelessly stolen from Gastronomy
            public static bool BaseChairValidator(Pawn pawn, Thing t)
        {
            if (t.def.building == null || !t.def.building.isSittable) return false;

            if (t.IsForbidden(pawn)) return false;

            if (!pawn.CanReserve(t)) return false;

            if (!t.IsSociallyProper(pawn)) return false;

            if (t.IsBurning()) return false;

            if (t.HostileTo(pawn)) return false;

            if (t.Position.GetDangerFor(pawn, t.Map) > MaxDangerDining) return false;
            return true;
        }

        //shamelessly stolen from Gastronomy
        public static bool IsRegionDangerous(Pawn pawn, Danger maxDanger, Region region = null)
        {
            if (region == null)
            {
                region = pawn.GetRegion();
            }

            var key = new Pair<Pawn, Region>(pawn, region);
            if (dangerousRegionsCache.TryGetValue(key, out bool result)) return result;

            var isRegionDangerous = region.DangerFor(pawn) > maxDanger;
            dangerousRegionsCache.Add(key, isRegionDangerous);

            return isRegionDangerous;
        }

        //shamelessly stolen from Gastronomy
        public static T FailOnDangerous<T>(this T f, Danger maxDanger) where T : IJobEndable
        {
            JobCondition OnRegionDangerous()
            {
                Pawn pawn = f.GetActor();
                var check = IsRegionDangerous(pawn, maxDanger, pawn.GetRegion());
                if (!check) return JobCondition.Ongoing;
                Log.Message($"{pawn.NameShortColored} failed {pawn.CurJobDef.label} because of danger ({pawn.GetRegion().DangerFor(pawn)})");
                return JobCondition.Incompletable;
            }

            f.AddEndCondition(OnRegionDangerous);
            return f;
        }

        public static void FailOnChairNoLongerUsable(this Toil toil, Thing chair)
        {
            toil.FailOn(() => chair == null || !chair.Spawned || chair.Map != toil.actor.Map);
            toil.FailOn(() => chair.IsBurning());
            toil.FailOn(() => HealthAIUtility.ShouldSeekMedicalRest(toil.actor) || HealthAIUtility.ShouldSeekMedicalRestUrgent(toil.actor));
            toil.FailOn(() => toil.actor.IsColonist && !toil.actor.CurJob.ignoreForbidden && !toil.actor.Downed && chair.IsForbidden(toil.actor));
        }

        public static bool TryFindChair(Pawn pawn, out Thing chair)
        {
            LocalTargetInfo roomSpot = pawn.mindState.duty.focus;
            Room feastRoom = GridsUtility.GetRoom(roomSpot.Cell, pawn.Map);

            chair = null;

            CellRect roomRect = feastRoom.ExtentsClose;
            if (roomRect.Area == 0)
            {
                return false;
            }
            List<Thing> allBuildings = feastRoom.ContainedAndAdjacentThings;

            //bool reserved = false;
            foreach (Thing b in allBuildings)
            {

                if (b.GetType().IsSubclassOf(typeof(Verse.Building)) || b.GetType() == typeof(Verse.Building))
                {
                    if (BaseChairValidator(pawn, b))
                    {
                        chair = b;
                        return true;
                    }
                }
            }

            Log.Warning("Failed to find a seat for pawn " + pawn.Name);
            return false;
        }

        public static Toil Toil_GoToChair(Pawn pawn, Thing chair)
        {
            Toil gotoChair = new Toil();
            gotoChair.initAction = delegate ()
            {
                if (chair == null)
                {
                    pawn.jobs.curDriver.ReadyForNextToil();
                    return;
                }
                pawn.pather.StartPath(chair.Position, PathEndMode.OnCell);
                if (pawn.Position == chair.Position)
                {
                    pawn.jobs.curDriver.ReadyForNextToil();
                    return;
                }
            };
            gotoChair.tickAction = delegate ()
            {
                pawn.pather.StartPath(chair.Position, PathEndMode.OnCell);

            };
            gotoChair.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            gotoChair.FailOnChairNoLongerUsable(chair);
            return gotoChair;
        }

        public static Toil Toil_GoToFeast(Pawn pawn, TargetIndex feastSpot)
        {

            var toil = new Toil();
            toil.initAction = () =>
            {
                Pawn actor = toil.actor;
                IntVec3 targetPosition = IntVec3.Invalid;

                var diningSpot = (Thing)actor.CurJob.GetTarget(feastSpot).Thing;

                Thing chair = null;
                var foundChair = TryFindChair(actor, out chair);
            };

            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            return toil;
        }
    }
}
