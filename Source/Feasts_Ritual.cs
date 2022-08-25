using RimWorld;
using Verse;
using Verse.AI.Group;
using System.Collections.Generic;

namespace TKS_Feasts
{
	[DefOf]
	class ThingDefOf_Feasts
    {
		public static ThingDef FeastSpot;
    }

    class StageEndTrigger_AllPawnsArrived : StageEndTrigger
    {
        protected virtual bool Trigger (LordJob_Ritual ritual)
        {
			foreach (Pawn pawn in ritual.assignments.Participants)
			{
				if (pawn.GetRoom() != ritual.GetRoom)
				{
					Log.Message("not starting the ritual beacuse " + pawn.Name + " is not in the room yet");
					return false;
				}
			}
			return true;
        }

		public override Trigger MakeTrigger(LordJob_Ritual ritual, TargetInfo spot, IEnumerable<TargetInfo> foci, RitualStage stage)
		{
			return new Trigger_Custom((TriggerSignal signal) => this.Trigger(ritual));
		}

	}

	public class RitualBehaviorWorker_Feast: RitualBehaviorWorker
    {
		public RitualBehaviorWorker_Feast()
		{
		}
		public RitualBehaviorWorker_Feast(RitualBehaviorDef def) : base(def)
		{
		}
		public override void Tick(LordJob_Ritual ritual)
		{
			base.Tick(ritual);
		}
		public override void Cleanup(LordJob_Ritual ritual)
		{
			base.Cleanup(ritual);
		}
		public override string CanStartRitualNow(TargetInfo target, Precept_Ritual ritual, Pawn selectedPawn = null, Dictionary<string, Pawn> forcedForRole = null)
		{
			return base.CanStartRitualNow(target, ritual, selectedPawn, forcedForRole);
		}
		/*
		protected override LordJob CreateLordJob(TargetInfo target, Pawn organizer, Precept_Ritual ritual, RitualObligation obligation, RitualRoleAssignments assignments)
		{
			Pawn organizer2 = assignments.AssignedPawns("speaker").First<Pawn>();
			return new LordJob_Joinable_Speech(target, organizer2, ritual, this.def.stages, assignments, false);
		}
		*/
	}

	public class RitualObligationTargetWorker_FeastSpot : RitualObligationTargetFilter
	{
		// Token: 0x06005EEF RID: 24303 RVA: 0x0020F7B3 File Offset: 0x0020D9B3
		public RitualObligationTargetWorker_FeastSpot()
		{
		}

		// Token: 0x06005EF0 RID: 24304 RVA: 0x0020F7BB File Offset: 0x0020D9BB
		public RitualObligationTargetWorker_FeastSpot(RitualObligationTargetFilterDef def) : base(def)
		{
		}

		// Token: 0x06005EF1 RID: 24305 RVA: 0x0020F9E5 File Offset: 0x0020DBE5
		public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map)
		{
			List<Thing> feastSpot = map.listerThings.ThingsOfDef(ThingDefOf_Feasts.FeastSpot);
			int num;
			for (int i = 0; i < feastSpot.Count; i = num + 1)
			{
				yield return feastSpot[i];
				num = i;
			}
			for (int i = 0; i < map.gatherSpotLister.activeSpots.Count; i = num + 1)
			{
				yield return map.gatherSpotLister.activeSpots[i].parent;
				num = i;
			}
			yield break;
		}

		// Token: 0x06005EF2 RID: 24306 RVA: 0x0020F9F8 File Offset: 0x0020DBF8
		protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation)
		{
			if (!target.HasThing)
			{
				return false;
			}
			Thing thing = target.Thing;
			if (this.def.colonistThingsOnly && (thing.Faction == null || !thing.Faction.IsPlayer))
			{
				return false;
			}
			if (thing.def == ThingDefOf_Feasts.FeastSpot)
			{
				return true;
			}
			/*
			CompGatherSpot compGatherSpot = thing.TryGetComp<CompGatherSpot>();
			if (compGatherSpot != null && compGatherSpot.Active)
			{
				return true;
			}*/
			return false;
			
		}

		// Token: 0x06005EF3 RID: 24307 RVA: 0x0020FA79 File Offset: 0x0020DC79
		public override IEnumerable<string> GetTargetInfos(RitualObligation obligation)
		{
			yield return "RitualTargetFeastSpotInfo".Translate();
			yield break;
		}
	}
}
