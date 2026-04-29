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

        // Sync all alarms for one vessel based on computed resource times and current settings.
        internal void Sync(TrackedVessel vessel, VesselResourceTimes times,
            LifeSupportAlarmsSettings settings, double now, double leadTimeSecs)
        {
            if (settings.GroupAlarmsByVessel)
                SyncGrouped(vessel, times, settings, now, leadTimeSecs);
            else
                SyncIndividual(vessel, times, settings, now, leadTimeSecs);
        }

        private void SyncGrouped(TrackedVessel vessel, VesselResourceTimes times,
            LifeSupportAlarmsSettings settings, double now, double leadTimeSecs)
        {
            AlarmAction alarmAction = (AlarmAction)settings.AlarmAction;
            // Remove all per-resource alarms; maintain one grouped alarm per vessel
            foreach (string prefix in AlarmPrefixes.AllResources)
                _repo.Delete(vessel.PersistentId, prefix);

            (bool enabled, double time, string label)[] candidates =
            [
                (settings.EnableSuppliesAlarm, times.SuppliesLeft, "Supplies"),
                (settings.EnableECAlarm, times.ECLeft, "Electric Charge"),
                (settings.EnableHabAlarm, times.EarliestHab, "Hab"),
                (settings.EnableHomeAlarm, times.EarliestHome, "Home"),
            ];

            double earliest = double.PositiveInfinity;
            string criticalLabel = "";
            foreach ((bool enabled, double time, string label) in candidates)
                if (enabled && time < earliest) { earliest = time; criticalLabel = label; }

            if (earliest < double.PositiveInfinity)
                _repo.Upsert(AlarmSpec.ForGrouped(vessel, earliest, criticalLabel),
                    now, leadTimeSecs, alarmAction);
            else
                _repo.Delete(vessel.PersistentId, AlarmPrefixes.Grouped);
        }

        private void SyncIndividual(TrackedVessel vessel, VesselResourceTimes times,
            LifeSupportAlarmsSettings settings, double now, double leadTimeSecs)
        {
            AlarmAction alarmAction = (AlarmAction)settings.AlarmAction;
            // Remove grouped alarm; maintain one alarm per resource type
            _repo.Delete(vessel.PersistentId, AlarmPrefixes.Grouped);

            UpsertOrDelete(AlarmSpec.ForResource(vessel, AlarmPrefixes.Supplies, times.SuppliesLeft),
                settings.EnableSuppliesAlarm, now, leadTimeSecs, alarmAction);
            UpsertOrDelete(AlarmSpec.ForResource(vessel, AlarmPrefixes.EC, times.ECLeft),
                settings.EnableECAlarm, now, leadTimeSecs, alarmAction);

            if (times.AnyHabPenalty)
            {
                UpsertOrDelete(AlarmSpec.ForResource(vessel, AlarmPrefixes.Hab, times.EarliestHab),
                    settings.EnableHabAlarm, now, leadTimeSecs, alarmAction);
                UpsertOrDelete(AlarmSpec.ForResource(vessel, AlarmPrefixes.Home, times.EarliestHome),
                    settings.EnableHomeAlarm, now, leadTimeSecs, alarmAction);
            }
            else
            {
                _repo.Delete(vessel.PersistentId, AlarmPrefixes.Hab);
                _repo.Delete(vessel.PersistentId, AlarmPrefixes.Home);
            }
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
