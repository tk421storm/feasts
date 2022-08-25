using Verse;
using RimWorld;
using Verse.AI;
using System.Collections.Generic;
using System;

namespace TKS_Feasts
{
    //using [DefOf] allows us to reference any Defs we've defined in XML. 
    [DefOf]
    class JobDefOf_Feasts
    {
        public static JobDef SitAndBeSociallyActive;
        public static JobDef GetFoodForFeast;
    }

    public class JobGiver_GetFoodForFeast : ThinkNode_JobGiver
    {

        public bool forceScanWholeMap;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_GetFoodForFeast jobGiver_GetFoodForFeast = (JobGiver_GetFoodForFeast)base.DeepCopy(resolve);
            jobGiver_GetFoodForFeast.forceScanWholeMap = this.forceScanWholeMap;
            return jobGiver_GetFoodForFeast;
        }

        private Thing FindFood(Pawn pawn, IntVec3 gatheringSpot)
        {
            /*
            Predicate<Thing> validator = (Thing x) => x.IngestibleNow 
            && x.def.IsNutritionGivingIngestible
            && GatheringsUtility.InGatheringArea(x.Position, gatheringSpot, pawn.Map)
            && !x.def.IsDrug
            && x.def.ingestible.preferability > FoodPreferability.RawBad
            && pawn.WillEat(x, null, true)
            && !x.IsForbidden(pawn)
            && x.IsSociallyProper(pawn)
            && pawn.CanReserve(x, 1, -1, null, false);

            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.FoodSourceNotPlantOrTree), PathEndMode.ClosestTouch, TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Deadly, false, false, false), 14f, validator, null, 0, 12, false, RegionType.Set_Passable, false);
            */
            bool allowCorpse;
            if (pawn.AnimalOrWildMan())
            {
                allowCorpse = true;
            }
            else
            {
                Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition, false);
                allowCorpse = (firstHediffOfDef != null && firstHediffOfDef.Severity > 0.4f);
            }

            Thing thing;
            ThingDef thingDef;

            bool desperate = pawn.needs.food.CurCategory == HungerCategory.Starving;
            if (!FoodUtility.TryFindBestFoodSourceFor_NewTemp(pawn, pawn, desperate, out thing, out thingDef, true, true, true, false, allowCorpse, false, pawn.IsWildMan(), this.forceScanWholeMap, false, false, FoodPreferability.Undefined))
            {
                Log.Warning("Could not find any food using TryFindBestFoodSourceFor_NewTemp!");
                return null;
            }
            if (thing is Plant && thing.def.plant.harvestedThingDef == thingDef)
            {
                //not really appropriate for the feast, we want the food item not a job
                Log.Warning("Only unharvested plant food found for feast food source - not making pawn pick");
                return null;
                //return JobMaker.MakeJob(JobDefOf.Harvest, thing);
            }
            float nutrition = FoodUtility.GetNutrition(thing, thingDef);
            Pawn_InventoryTracker pawn_InventoryTracker = thing.ParentHolder as Pawn_InventoryTracker;
            Pawn pawn3 = (pawn_InventoryTracker != null) ? pawn_InventoryTracker.pawn : null;
            if (pawn3 != null && pawn3 != pawn)
            {
                //only food found is in someone elses inventory
                Log.Warning("Only food source found is in another pawns inventory - not making them take");
                return null;
            }

            return thing;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            PawnDuty duty = pawn.mindState.duty;
            if (duty == null)
            {
                return null;
            }
            IntVec3 cell = duty.focus.Cell;
            Thing food = FindFood(pawn, cell);
            if (food == null)
            {
                Log.Warning("Unable to find any food for " + pawn.Name + " for the feast!");
                return null;
            } else
            {
                //reserve a chair
                Thing chair;
                bool success = TKS_Feasts.Toils_Feast.TryFindChair(pawn, out chair);

                Job job = JobMaker.MakeJob(JobDefOf_Feasts.GetFoodForFeast, food, chair);
                job.count = FoodUtility.WillIngestStackCountOf(pawn, food.def, food.def.GetStatValueAbstract(StatDefOf.Nutrition, null));
                return job;
            }
        }
    }

    public class JobDriver_GetFoodForFeast : JobDriver_Ingest
    {

        private bool usingNutrientPasteDispenser;
        private bool eatingFromInventory;

        private float ChewDurationMultiplier
        {
            get
            {
                Thing ingestibleSource = this.IngestibleSource;
                if (ingestibleSource.def.ingestible != null && !ingestibleSource.def.ingestible.useEatingSpeedStat)
                {
                    return 1f;
                }
                return 1f / this.pawn.GetStatValue(StatDefOf.EatingSpeed, true);
            }
        }

        private Thing IngestibleSource
        {
            get
            {
                return this.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        private Thing ChairAtFeast
        {
            get
            {
                return this.job.GetTarget(TargetIndex.B).Thing;
            }
        }

        private IEnumerable<Toil> PrepareToIngestToils(Toil chewToil)
        {
            if (this.usingNutrientPasteDispenser)
            {
                return this.PrepareToIngestToils_Dispenser();
            }
            else
            {
                return this.PrepareToIngestToils_ToolUser(chewToil);
            }
        }

        private IEnumerable<Toil> PrepareToIngestToils_Dispenser()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Ingest.TakeMealFromDispenser(TargetIndex.A, this.pawn);
            if (!this.pawn.Drafted)
            {
                yield return TKS_Feasts.Toils_Feast.Toil_GoToChair(this.pawn, ChairAtFeast).FailOnDestroyedOrNull(TargetIndex.B);
            }
            yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
            yield break;
        }

        private IEnumerable<Toil> PrepareToIngestToils_ToolUser(Toil chewToil)
        {
            if (this.eatingFromInventory)
            {
                yield return Toils_Misc.TakeItemFromInventoryToCarrier(this.pawn, TargetIndex.A);
            }
            else
            {
                Toil gotoToPickup = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
                yield return Toils_Jump.JumpIf(gotoToPickup, () => this.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation));
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
                yield return Toils_Jump.Jump(chewToil);
                yield return gotoToPickup;
                yield return Toils_Ingest.PickupIngestible(TargetIndex.A, this.pawn);
                gotoToPickup = null;
            }
            if (!this.pawn.Drafted)
            {
                yield return TKS_Feasts.Toils_Feast.Toil_GoToChair(this.pawn, ChairAtFeast).FailOnDestroyedOrNull(TargetIndex.B);
            }
            yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
            yield break;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (this.pawn.Faction != null && !(this.IngestibleSource is Building_NutrientPasteDispenser))
            {
                Thing ingestibleSource = this.IngestibleSource;
                if (!this.pawn.Reserve(ingestibleSource, this.job, 10, FoodUtility.GetMaxAmountToPickup(ingestibleSource, this.pawn, this.job.count), null, errorOnFailed))
                {
                    return false;
                }
            }
            //reserve chair
            return this.pawn.Reserve(this.job.GetTarget(TargetIndex.B), this.job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (!this.usingNutrientPasteDispenser)
            {
                this.FailOn(() => !this.IngestibleSource.Destroyed && !this.IngestibleSource.IngestibleNow);
            }
            Toil chew = Toils_Ingest.ChewIngestible(this.pawn, this.ChewDurationMultiplier, TargetIndex.A, TargetIndex.B).FailOn((Toil x) => !this.IngestibleSource.Spawned && (this.pawn.carryTracker == null || this.pawn.carryTracker.CarriedThing != this.IngestibleSource)).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            foreach (Toil toil in this.PrepareToIngestToils(chew))
            {
                yield return toil;
            }
            //IEnumerator<Toil> enumerator = null;
            yield return chew;
            yield return Toils_Ingest.FinalizeIngest(this.pawn, TargetIndex.A);
            yield return Toils_Jump.JumpIf(chew, () => this.job.GetTarget(TargetIndex.A).Thing is Corpse && this.pawn.needs.food.CurLevelPercentage < 0.9f);
            yield break;
        }

}

    public class JobDriver_SitAndBeSociallyActive : JobDriver
    {

        private const TargetIndex FeastLocation = TargetIndex.A;
        private const int SitDuration = 500;

        private const TargetIndex chairTarget = TargetIndex.B;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.job.GetTarget(chairTarget), this.job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {

            //these are the fail conditions. If at any time during this toil the cat becomes unavailable, our toil ends.

            Thing chair = this.job.GetTarget(chairTarget).Thing;
            Toil gotoChair = TKS_Feasts.Toils_Feast.Toil_GoToChair(pawn, chair);

            yield return gotoChair;

            Toil wait = Toils_General.WaitWith(chairTarget, SitDuration);
            wait.socialMode = RandomSocialMode.SuperActive;

            yield return wait;
            Pawn_InteractionsTracker socialTracker = new Pawn_InteractionsTracker(pawn);
            if (!socialTracker.InteractedTooRecentlyToInteract())
            {
                yield return wait;
            }

            yield break;
        }

    }

    public class JobGiver_SitAndBeSociallyActive : ThinkNode_JobGiver
    {

        public IntRange ticksRange = new IntRange(300, 600);
        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_SitAndBeSociallyActive jobGiver_SitAndBeSociallyActive = (JobGiver_SitAndBeSociallyActive)base.DeepCopy(resolve);
            jobGiver_SitAndBeSociallyActive.ticksRange = this.ticksRange;
            return jobGiver_SitAndBeSociallyActive;
        }
        protected override Job TryGiveJob(Pawn pawn)
        {

            PawnDuty duty = pawn.mindState.duty;
            if (duty == null)
            {
                return null;
            }
            IntVec3 cell = duty.focus.Cell;


            //find a chair
            Thing chair;
            bool success = TKS_Feasts.Toils_Feast.TryFindChair(pawn, out chair);

            if (chair != null)
            {
                LocalTargetInfo targetInfoB = new LocalTargetInfo(chair);
                Job job = JobMaker.MakeJob(JobDefOf_Feasts.SitAndBeSociallyActive, duty.focus, targetInfoB);
                job.expiryInterval = this.ticksRange.RandomInRange;
                return job;
            }

            return null;

        }
       
    }
}