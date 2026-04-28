using System;
using UnityEngine;

namespace LifeSupportAlarms
{
    // Creates, updates, and removes KSP alarm clock entries for life support expiry.
    internal static class AlarmManager
    {
        private const double AlarmTolerance = 60.0; // 1 minute

        internal static void SetOrRefreshGroupedAlarm(Vessel vessel, double timeLeft, string criticalLabel,
            double now, double leadTimeSecs, int alarmAction)
        {
            string expectedTitle = vessel.vesselName + (criticalLabel.Length > 0 ? $" ({criticalLabel})" : "");
            SetOrRefreshAlarmCore("[USILS-Grouped]", vessel, expectedTitle, timeLeft, now, leadTimeSecs, alarmAction);
        }

        internal static void SetOrRefreshAlarm(string prefix, Vessel vessel, double timeLeft, double now,
            double leadTimeSecs, int alarmAction)
        {
            string label         = prefix.Trim('[', ']').Replace("USILS-", "").Replace("EC", "Electric Charge");
            string expectedTitle = vessel.vesselName + " " + label;
            SetOrRefreshAlarmCore(prefix, vessel, expectedTitle, timeLeft, now, leadTimeSecs, alarmAction);
        }

        private static void SetOrRefreshAlarmCore(string prefix, Vessel vessel, string expectedTitle,
            double timeLeft, double now, double leadTimeSecs, int alarmAction)
        {
            // Indefinite, NaN, or already expired ? ensure no alarm exists
            if (double.IsPositiveInfinity(timeLeft) || double.IsNaN(timeLeft) || timeLeft <= 0)
            {
                RemoveAlarm(vessel.persistentId, prefix);
                return;
            }

            double alarmUT = now + timeLeft - leadTimeSecs;

            // Alarm would fire in the past ? nothing useful to show
            if (alarmUT <= now)
            {
                RemoveAlarm(vessel.persistentId, prefix);
                return;
            }

            AlarmActions.WarpEnum warpAction = alarmAction switch
            {
                2 => AlarmActions.WarpEnum.PauseGame,
                1 => AlarmActions.WarpEnum.KillWarp,
                _ => AlarmActions.WarpEnum.DoNothing
            };

            AlarmTypeRaw existing = FindAlarm(vessel.persistentId, prefix);
            if (existing != null && Math.Abs(existing.ut - alarmUT) < AlarmTolerance && existing.title == expectedTitle)
                return; // already correct, skip update

            if (existing != null)
                AlarmClockScenario.DeleteAlarm(existing);

            AlarmTypeRaw alarm = new()
            {
                description = prefix + ":" + vessel.id,
                actions     = { warp = warpAction, message = AlarmActions.MessageEnum.Yes },
                ut          = alarmUT,
                vesselId    = vessel.persistentId
            };
            AlarmClockScenario.AddAlarm(alarm);
            // AddAlarm resets title to vessel name; set it after the call
            alarm.title = expectedTitle;
            // The alarm list UI doesn't update titles on the fly — force a refresh
            // by adding and immediately deleting a fake alarm (same trick used by AlarmEnhancements)
            AlarmTypeRaw fake = new() { ut = alarm.ut + 1, actions = { message = AlarmActions.MessageEnum.No, deleteWhenDone = true } };
            AlarmClockScenario.AddAlarm(fake);
            AlarmClockScenario.DeleteAlarm(fake);
            Debug.Log($"[LifeSupportAlarms] Alarm set: '{expectedTitle}' at UT {alarmUT:F0}");
        }

        internal static AlarmTypeRaw FindAlarm(uint vesselPersistentId, string prefix)
        {
            foreach (AlarmTypeBase alarm in AlarmClockScenario.Instance.alarms.Values)
            {
                AlarmTypeRaw raw = alarm as AlarmTypeRaw;
                if (raw == null) continue;
                if (raw.vesselId != vesselPersistentId) continue;
                if (raw.description != null && raw.description.StartsWith(prefix)) return raw;
            }
            return null;
        }

        internal static void RemoveAlarm(uint vesselPersistentId, string prefix)
        {
            AlarmTypeRaw found = FindAlarm(vesselPersistentId, prefix);
            if (found != null)
                AlarmClockScenario.DeleteAlarm(found);
        }
    }
}
