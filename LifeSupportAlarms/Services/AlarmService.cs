using System.Collections.Generic;
using LifeSupportAlarms;
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
            {
                // Remove all per-resource alarms; maintain one grouped alarm per vessel
                _repo.Delete(vessel.PersistentId, "[USILS-Supplies]");
                _repo.Delete(vessel.PersistentId, "[USILS-EC]");
                _repo.Delete(vessel.PersistentId, "[USILS-Hab]");
                _repo.Delete(vessel.PersistentId, "[USILS-Home]");

                double earliest      = double.PositiveInfinity;
                string criticalLabel = "";
                if (settings.EnableSuppliesAlarm && times.SuppliesLeft  < earliest) { earliest = times.SuppliesLeft;  criticalLabel = "Supplies"; }
                if (settings.EnableECAlarm        && times.ECLeft        < earliest) { earliest = times.ECLeft;        criticalLabel = "Electric Charge"; }
                if (settings.EnableHabAlarm       && times.EarliestHab  < earliest) { earliest = times.EarliestHab;   criticalLabel = "Hab"; }
                if (settings.EnableHomeAlarm      && times.EarliestHome < earliest) { earliest = times.EarliestHome;  criticalLabel = "Home"; }

                string title = vessel.Name + (criticalLabel.Length > 0 ? $" ({criticalLabel})" : "");
                _repo.Upsert(vessel, "[USILS-Grouped]", title, earliest, now, leadTimeSecs, settings.AlarmAction);
            }
            else
            {
                // Remove grouped alarm; maintain one alarm per resource type
                _repo.Delete(vessel.PersistentId, "[USILS-Grouped]");

                UpsertOrDelete(vessel, "[USILS-Supplies]", times.SuppliesLeft,
                    settings.EnableSuppliesAlarm, now, leadTimeSecs, settings.AlarmAction);
                UpsertOrDelete(vessel, "[USILS-EC]", times.ECLeft,
                    settings.EnableECAlarm, now, leadTimeSecs, settings.AlarmAction);

                if (times.AnyHabPenalty)
                {
                    UpsertOrDelete(vessel, "[USILS-Hab]",  times.EarliestHab,
                        settings.EnableHabAlarm,  now, leadTimeSecs, settings.AlarmAction);
                    UpsertOrDelete(vessel, "[USILS-Home]", times.EarliestHome,
                        settings.EnableHomeAlarm, now, leadTimeSecs, settings.AlarmAction);
                }
                else
                {
                    _repo.Delete(vessel.PersistentId, "[USILS-Hab]");
                    _repo.Delete(vessel.PersistentId, "[USILS-Home]");
                }
            }
        }

        // Remove all LifeSupportAlarms-managed alarms from every known vessel.
        internal void ClearAll()
        {
            if (AlarmClockScenario.Instance == null) return;

            var ids = new List<uint>();
            foreach (Vessel v in FlightGlobals.Vessels)
                ids.Add(v.persistentId);
            _repo.DeleteAll(ids);
        }

        // --- Private helpers -------------------------------------------------------------

        private void UpsertOrDelete(TrackedVessel vessel, string prefix, double timeLeft,
            bool enabled, double now, double leadTimeSecs, int alarmAction)
        {
            if (enabled)
                _repo.Upsert(vessel, prefix, TitleFor(vessel.Name, prefix),
                    timeLeft, now, leadTimeSecs, alarmAction);
            else
                _repo.Delete(vessel.PersistentId, prefix);
        }

        // Derives a human-readable alarm title from a vessel name and alarm prefix.
        // e.g. "Kerbin Station 1" + "[USILS-EC]" => "Kerbin Station 1 Electric Charge"
        private static string TitleFor(string vesselName, string prefix) =>
            vesselName + " " + prefix.Trim('[', ']').Replace("USILS-", "").Replace("EC", "Electric Charge");
    }
}
