using System;
using LifeSupport;
using UnityEngine;

namespace LifeSupportAlarms
{
    // Shared MonoBehaviour logic -- subclassed by the scene-specific KSPAddon stubs.
    // Responsible only for the poll loop: guard clauses, vessel iteration, and
    // delegating computation and alarm dispatch to the static helper classes.
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

            // LifeSupportManager.Instance auto-creates via lazy getter -- do not null-check with Unity ==
            var lsm = LifeSupportManager.Instance;

            double now = Planetarium.GetUniversalTime();
            LifeSupportConfig cfg = LifeSupportScenario.Instance.settings.GetSettings();

            foreach (VesselSupplyStatus vsl in lsm.VesselSupplyInfo)
            {
                if (vsl.NumCrew == 0) continue;

                Vessel vessel = VesselHelpers.FindVessel(vsl.VesselId);
                if (vessel == null) continue;

                VesselResourceTimes times = ComputeResourceTimes(vessel, vsl, cfg, settings, now);
                AlarmManager.SyncAlarmsForVessel(vessel, times, settings, now, leadTimeSecs);
            }
        }

        private static VesselResourceTimes ComputeResourceTimes(
            Vessel vessel, VesselSupplyStatus vsl,
            LifeSupportConfig cfg, LifeSupportAlarmsSettings settings, double now)
        {
            double suppliesLeft = double.PositiveInfinity;
            if (settings.EnableSuppliesAlarm)
            {
                double suppliesPerSec = cfg.SupplyAmount * vsl.NumCrew * vsl.RecyclerMultiplier;
                suppliesLeft = ResourceTimeCalculator.ComputeSuppliesTime(vessel, vsl, now, suppliesPerSec);
            }

            double ecLeft = double.PositiveInfinity;
            if (settings.EnableECAlarm)
            {
                double ecPerSec = cfg.ECAmount * vsl.NumCrew;
                ecLeft = ResourceTimeCalculator.ComputeECTime(vessel, vsl, now, ecPerSec);
            }

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

            return new VesselResourceTimes(suppliesLeft, ecLeft, earliestHab, earliestHome, anyHabPenalty);
        }
    }
}
