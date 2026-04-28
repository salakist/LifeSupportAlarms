using System;
using LifeSupport;
using UnityEngine;

namespace LifeSupportAlarms
{
    // Shared MonoBehaviour logic — subclassed by the scene-specific KSPAddon stubs.
    // Responsible only for the poll loop and orchestrating the other static helpers.
    public class LifeSupportAlarmsCore : MonoBehaviour
    {
        private static readonly string[] AlarmPrefixes =
            ["[USILS-Supplies]", "[USILS-EC]", "[USILS-Hab]", "[USILS-Home]", "[USILS-Grouped]"];

        public void Start()
        {
            Debug.Log("[LifeSupportAlarms] Loaded");
            InvokeRepeating("PollLifeSupport", 5f, 10f);
        }

        private void PollLifeSupport()
        {
            LifeSupportAlarmsSettings settings = LifeSupportAlarmsSettings.Instance;
            if (settings == null) return;

            // When alarms are disabled, clear any we previously created and stop
            if (!settings.EnableAlarms)
            {
                if (AlarmClockScenario.Instance != null)
                    foreach (string prefix in AlarmPrefixes)
                        foreach (Vessel v in FlightGlobals.Vessels)
                            AlarmManager.RemoveAlarm(v.persistentId, prefix);
                return;
            }

            if (ReferenceEquals(LifeSupportScenario.Instance, null)) return;
            if (!LifeSupportScenario.Instance.settings.isLoaded())       return;
            if (AlarmClockScenario.Instance  == null)                     return;

            double leadTimeSecs = settings.LeadTimeHours * 3600.0;

            // LifeSupportManager.Instance auto-creates via lazy getter — do not null-check with Unity ==
            var lsm = LifeSupportManager.Instance;

            double now = Planetarium.GetUniversalTime();
            LifeSupportConfig cfg = LifeSupportScenario.Instance.settings.GetSettings();

            foreach (VesselSupplyStatus vsl in lsm.VesselSupplyInfo)
            {
                if (vsl.NumCrew == 0) continue;

                Vessel vessel = VesselHelpers.FindVessel(vsl.VesselId);
                if (vessel == null) continue;

                // Supplies
                double suppliesLeft = double.PositiveInfinity;
                if (settings.EnableSuppliesAlarm)
                {
                    double suppliesPerSec = cfg.SupplyAmount * vsl.NumCrew * vsl.RecyclerMultiplier;
                    suppliesLeft = ResourceTimeCalculator.ComputeSuppliesTime(vessel, vsl, now, suppliesPerSec);
                }

                // EC
                double ecLeft = double.PositiveInfinity;
                if (settings.EnableECAlarm)
                {
                    double ecPerSec = cfg.ECAmount * vsl.NumCrew;
                    ecLeft = ResourceTimeCalculator.ComputeECTime(vessel, vsl, now, ecPerSec);
                }

                // Hab and Home — computed per-crew, alarmed on the earliest expiry
                bool   anyHabPenalty = false;
                double earliestHab   = double.PositiveInfinity;
                double earliestHome  = double.PositiveInfinity;
                double habTotal      = vsl.CachedHabTime;

                var crew = vessel.GetVesselCrew();
                for (int i = 0; i < crew.Count; i++)
                {
                    ProtoCrewMember c = crew[i];
                    if (LifeSupportManager.GetNoHomeEffect(c.name) == 0) continue;
                    anyHabPenalty = true;

                    LifeSupportStatus cls = LifeSupportManager.Instance.FetchKerbal(c);

                    double habLeft = habTotal - (now - cls.TimeEnteredVessel);
                    if (!ResourceTimeCalculator.IsIndefinite(c, habLeft, cfg))
                        earliestHab = Math.Min(earliestHab, habLeft);

                    double homeLeft = cls.MaxOffKerbinTime - now;
                    if (!ResourceTimeCalculator.IsIndefinite(c, homeLeft, cfg))
                        earliestHome = Math.Min(earliestHome, homeLeft);
                }

                if (!anyHabPenalty)
                {
                    earliestHab  = double.PositiveInfinity;
                    earliestHome = double.PositiveInfinity;
                }

                if (settings.GroupAlarmsByVessel)
                {
                    // Remove all individual alarms
                    AlarmManager.RemoveAlarm(vessel.persistentId, "[USILS-Supplies]");
                    AlarmManager.RemoveAlarm(vessel.persistentId, "[USILS-EC]");
                    AlarmManager.RemoveAlarm(vessel.persistentId, "[USILS-Hab]");
                    AlarmManager.RemoveAlarm(vessel.persistentId, "[USILS-Home]");

                    // Find earliest enabled resource
                    double earliest = double.PositiveInfinity;
                    string criticalLabel = "";
                    if (settings.EnableSuppliesAlarm  && suppliesLeft  < earliest) { earliest = suppliesLeft;  criticalLabel = "Supplies"; }
                    if (settings.EnableECAlarm         && ecLeft        < earliest) { earliest = ecLeft;        criticalLabel = "Electric Charge"; }
                    if (settings.EnableHabAlarm        && earliestHab   < earliest) { earliest = earliestHab;   criticalLabel = "Hab"; }
                    if (settings.EnableHomeAlarm       && earliestHome  < earliest) { earliest = earliestHome;  criticalLabel = "Home"; }

                    AlarmManager.SetOrRefreshGroupedAlarm(vessel, earliest, criticalLabel, now, leadTimeSecs, settings.AlarmAction);
                }
                else
                {
                    // Remove grouped alarm
                    AlarmManager.RemoveAlarm(vessel.persistentId, "[USILS-Grouped]");

                    // Supplies
                    if (settings.EnableSuppliesAlarm)
                        AlarmManager.SetOrRefreshAlarm("[USILS-Supplies]", vessel, suppliesLeft, now, leadTimeSecs, settings.AlarmAction);
                    else
                        AlarmManager.RemoveAlarm(vessel.persistentId, "[USILS-Supplies]");

                    // EC
                    if (settings.EnableECAlarm)
                        AlarmManager.SetOrRefreshAlarm("[USILS-EC]", vessel, ecLeft, now, leadTimeSecs, settings.AlarmAction);
                    else
                        AlarmManager.RemoveAlarm(vessel.persistentId, "[USILS-EC]");

                    // Hab
                    if (anyHabPenalty)
                    {
                        if (settings.EnableHabAlarm)
                            AlarmManager.SetOrRefreshAlarm("[USILS-Hab]",  vessel, earliestHab,  now, leadTimeSecs, settings.AlarmAction);
                        else
                            AlarmManager.RemoveAlarm(vessel.persistentId, "[USILS-Hab]");

                        if (settings.EnableHomeAlarm)
                            AlarmManager.SetOrRefreshAlarm("[USILS-Home]", vessel, earliestHome, now, leadTimeSecs, settings.AlarmAction);
                        else
                            AlarmManager.RemoveAlarm(vessel.persistentId, "[USILS-Home]");
                    }
                    else
                    {
                        AlarmManager.RemoveAlarm(vessel.persistentId, "[USILS-Hab]");
                        AlarmManager.RemoveAlarm(vessel.persistentId, "[USILS-Home]");
                    }
                }
            }
        }
    }
}
