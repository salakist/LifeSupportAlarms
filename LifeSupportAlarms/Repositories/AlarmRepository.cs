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

        private static readonly string[] AllPrefixes =
            ["[USILS-Supplies]", "[USILS-EC]", "[USILS-Hab]", "[USILS-Home]", "[USILS-Grouped]"];

        internal LifeSupportAlarm Find(uint vesselPersistentId, string prefix)
        {
            foreach (AlarmTypeBase alarm in AlarmClockScenario.Instance.alarms.Values)
            {
                AlarmTypeRaw raw = alarm as AlarmTypeRaw;
                if (raw == null) continue;
                if (raw.vesselId != vesselPersistentId) continue;
                if (raw.description != null && raw.description.StartsWith(prefix))
                    return new LifeSupportAlarm(raw, prefix);
            }
            return null;
        }

        // Creates or refreshes a single alarm. Skips the write if nothing has changed.
        internal void Upsert(TrackedVessel vessel, string prefix, string title,
            double timeLeft, double now, double leadTimeSecs, int alarmAction)
        {
            // Resource is indefinite, invalid, or already expired -- ensure no alarm exists
            if (double.IsPositiveInfinity(timeLeft) || double.IsNaN(timeLeft) || timeLeft <= 0)
            {
                Delete(vessel.PersistentId, prefix);
                return;
            }

            double alarmUT = now + timeLeft - leadTimeSecs;

            // Alarm would fire in the past -- nothing useful to show
            if (alarmUT <= now)
            {
                Delete(vessel.PersistentId, prefix);
                return;
            }

            AlarmActions.WarpEnum warpAction = alarmAction switch
            {
                2 => AlarmActions.WarpEnum.PauseGame,
                1 => AlarmActions.WarpEnum.KillWarp,
                _ => AlarmActions.WarpEnum.DoNothing
            };

            LifeSupportAlarm existing = Find(vessel.PersistentId, prefix);
            if (existing != null
                && Math.Abs(existing.Ut - alarmUT) < Tolerance
                && existing.Title == title)
                return; // already correct, no write needed

            if (existing != null)
                AlarmClockScenario.DeleteAlarm(existing.Raw);

            AlarmTypeRaw alarm = new()
            {
                description = prefix + ":" + vessel.Id,
                actions     = { warp = warpAction, message = AlarmActions.MessageEnum.Yes },
                ut          = alarmUT,
                vesselId    = vessel.PersistentId
            };
            AlarmClockScenario.AddAlarm(alarm);
            // AddAlarm resets title to vessel name; override it after the call
            alarm.title = title;
            // Force alarm-list UI to refresh the title via a transient fake alarm
            AlarmTypeRaw fake = new()
            {
                ut      = alarm.ut + 1,
                actions = { message = AlarmActions.MessageEnum.No, deleteWhenDone = true }
            };
            AlarmClockScenario.AddAlarm(fake);
            AlarmClockScenario.DeleteAlarm(fake);
            Debug.Log($"[LifeSupportAlarms] Alarm set: '{title}' at UT {alarmUT:F0}");
        }

        internal void Delete(uint vesselPersistentId, string prefix)
        {
            LifeSupportAlarm found = Find(vesselPersistentId, prefix);
            if (found != null)
                AlarmClockScenario.DeleteAlarm(found.Raw);
        }

        internal void DeleteAll(IEnumerable<uint> vesselIds)
        {
            foreach (uint id in vesselIds)
                foreach (string prefix in AllPrefixes)
                    Delete(id, prefix);
        }
    }
}
