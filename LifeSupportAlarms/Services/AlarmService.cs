using System.Collections.Generic;
using LifeSupportAlarms.Domain;
using LifeSupportAlarms.Repositories;

namespace LifeSupportAlarms.Services
{
    // Owns the grouped-vs-individual alarm dispatch logic for a single vessel.
    // Sits between LifeSupportAlarmsCore (loop) and AlarmRepository (CRUD).
    internal sealed class AlarmService
    {
        private readonly AlarmRepository _repo;

        internal AlarmService(AlarmRepository repo) => _repo = repo;

        // Sync all alarms for one vessel based on provider-supplied resource data.
        internal void Sync(VesselData vessel, double now, double leadTimeSecs,
            AlarmAction alarmAction, bool grouped)
        {
            if (grouped)
                SyncGrouped(vessel, now, leadTimeSecs, alarmAction);
            else
                SyncIndividual(vessel, now, leadTimeSecs, alarmAction);
        }

        private void SyncGrouped(VesselData vessel, double now, double leadTimeSecs,
            AlarmAction alarmAction)
        {
            // Remove all per-resource alarms; maintain one grouped alarm per vessel
            foreach (string prefix in AlarmPrefixes.AllResources)
                _repo.Delete(vessel.PersistentId, prefix);

            double earliest = double.PositiveInfinity;
            string criticalLabel = "";
            foreach (VesselData.ResourceEntry entry in vessel.Resources)
                if (entry.Enabled && entry.SecondsLeft < earliest)
                {
                    earliest = entry.SecondsLeft;
                    criticalLabel = entry.ResourceLabel;
                }

            if (earliest < double.PositiveInfinity)
                _repo.Upsert(AlarmSpec.ForGrouped(vessel, earliest, criticalLabel),
                    now, leadTimeSecs, alarmAction);
            else
                _repo.Delete(vessel.PersistentId, AlarmPrefixes.Grouped);
        }

        private void SyncIndividual(VesselData vessel, double now, double leadTimeSecs,
            AlarmAction alarmAction)
        {
            // Remove grouped alarm; maintain one alarm per resource type
            _repo.Delete(vessel.PersistentId, AlarmPrefixes.Grouped);

            foreach (VesselData.ResourceEntry entry in vessel.Resources)
                UpsertOrDelete(AlarmSpec.ForResource(vessel, entry), entry.Enabled,
                    now, leadTimeSecs, alarmAction);
        }

        // Remove all LifeSupportAlarms-managed alarms from every known vessel.
        internal void ClearAll()
        {
            if (AlarmClockScenario.Instance == null) return;

            List<uint> ids = [];
            foreach (Vessel v in FlightGlobals.Vessels)
                ids.Add(v.persistentId);
            _repo.DeleteAll(ids);
        }

        // --- Private helpers -------------------------------------------------------------

        private void UpsertOrDelete(AlarmSpec spec, bool enabled,
            double now, double leadTimeSecs, AlarmAction alarmAction)
        {
            if (enabled)
                _repo.Upsert(spec, now, leadTimeSecs, alarmAction);
            else
                _repo.Delete(spec.VesselPersistentId, spec.Prefix);
        }
    }
}
