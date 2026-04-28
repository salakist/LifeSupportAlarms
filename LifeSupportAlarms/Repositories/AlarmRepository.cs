using System;
using System.Collections.Generic;
using UnityEngine;
using LifeSupportAlarms.Domain;

namespace LifeSupportAlarms.Repositories
{
    // Pure CRUD wrapper over KSP AlarmClockScenario.
    // All alarm create/update/delete operations go through this class.
    internal sealed class AlarmRepository
    {
        private const double Tolerance = 60.0; // seconds — skip update if UT is within this margin

        internal FoundAlarm Find(uint vesselPersistentId, string prefix)
        {
            foreach (AlarmTypeBase alarm in AlarmClockScenario.Instance.alarms.Values)
            {
                AlarmTypeRaw raw = alarm as AlarmTypeRaw;
                if (raw == null) continue;
                if (raw.vesselId != vesselPersistentId) continue;
                if (raw.description != null && raw.description.StartsWith(prefix))
                    return new FoundAlarm(raw, prefix);
            }
            return null;
        }

        // Creates or refreshes the alarm described by spec. Skips the write if nothing has changed.
        internal void Upsert(AlarmSpec spec, double now, double leadTimeSecs, AlarmAction alarmAction)
        {
            // Hoist Find so the result is shared by both the early-exit delete path and the stale check
            FoundAlarm existing = Find(spec.VesselPersistentId, spec.Prefix);

            // Resource is indefinite, invalid, or already expired -- ensure no alarm exists
            if (double.IsPositiveInfinity(spec.TimeLeft) || double.IsNaN(spec.TimeLeft) || spec.TimeLeft <= 0)
            {
                if (existing != null) AlarmClockScenario.DeleteAlarm(existing.Raw);
                return;
            }

            double alarmUT = now + spec.TimeLeft - leadTimeSecs;

            // Alarm would fire in the past -- nothing useful to show
            if (alarmUT <= now)
            {
                if (existing != null) AlarmClockScenario.DeleteAlarm(existing.Raw);
                return;
            }

            AlarmActions.WarpEnum warpAction = alarmAction switch
            {
                AlarmAction.PauseGame => AlarmActions.WarpEnum.PauseGame,
                AlarmAction.KillWarp  => AlarmActions.WarpEnum.KillWarp,
                _                     => AlarmActions.WarpEnum.DoNothing
            };

            if (existing != null
                && Math.Abs(existing.Ut - alarmUT) < Tolerance
                && existing.Title == spec.Title)
                return; // already correct, no write needed

            if (existing != null)
                AlarmClockScenario.DeleteAlarm(existing.Raw);

            AlarmTypeRaw alarm = new()
            {
                description = spec.Prefix + ":" + spec.VesselGuid,
                actions     = { warp = warpAction, message = AlarmActions.MessageEnum.Yes },
                ut          = alarmUT,
                vesselId    = spec.VesselPersistentId
            };
            AlarmClockScenario.AddAlarm(alarm);
            // AddAlarm resets title to vessel name; override it after the call
            alarm.title = spec.Title;
            // Force alarm-list UI to refresh the title via a transient fake alarm
            AlarmTypeRaw fake = new()
            {
                ut      = alarm.ut + 1,
                actions = { message = AlarmActions.MessageEnum.No, deleteWhenDone = true }
            };
            AlarmClockScenario.AddAlarm(fake);
            AlarmClockScenario.DeleteAlarm(fake);
            Debug.Log($"[LifeSupportAlarms] Alarm set: '{spec.Title}' at UT {alarmUT:F0}");
        }

        internal void Delete(uint vesselPersistentId, string prefix)
        {
            FoundAlarm found = Find(vesselPersistentId, prefix);
            if (found != null)
                AlarmClockScenario.DeleteAlarm(found.Raw);
        }

        internal void Delete(FoundAlarm alarm) => Delete(alarm.VesselPersistentId, alarm.Prefix);

        internal void DeleteAll(IEnumerable<uint> vesselIds)
        {
            foreach (uint id in vesselIds)
                foreach (string prefix in AlarmPrefixes.All)
                    Delete(id, prefix);
        }
    }
}
