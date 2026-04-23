using System;
using LifeSupport;
using UnityEngine;

namespace LifeSupportAlarms
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class LifeSupportAlarmsAddon : MonoBehaviour
    {
        private const double LeadTimeSecs   = 21600.0; // 6 hours
        private const double AlarmTolerance = 60.0;    // 1 minute
        private const double FloatTolerance = 1e-6;

        public void Start()
        {
            Debug.Log("[LifeSupportAlarms] Loaded");
            InvokeRepeating("PollLifeSupport", 5f, 10f);
        }

        // ── Main poll ───────────────────────────────────────────────────────

        private void PollLifeSupport()
        {
            if (LifeSupportManager.Instance == null) return;
            if (LifeSupportScenario.Instance == null) return;
            if (AlarmClockScenario.Instance  == null) return;

            double now = Planetarium.GetUniversalTime();
            LifeSupportConfig cfg = LifeSupportScenario.Instance.settings.GetSettings();

            foreach (VesselSupplyStatus vsl in LifeSupportManager.Instance.VesselSupplyInfo)
            {
                if (vsl.NumCrew == 0) continue;

                Vessel vessel = FindVessel(vsl.VesselId);
                if (vessel == null) continue;

                // Supplies
                double suppliesPerSec = cfg.SupplyAmount * vsl.NumCrew * vsl.RecyclerMultiplier;
                double suppliesLeft   = ComputeSuppliesTime(vessel, vsl, now, suppliesPerSec);
                SetOrRefreshAlarm("[USILS-Supplies]", vessel, suppliesLeft, now);

                // EC
                double ecPerSec = cfg.ECAmount * vsl.NumCrew;
                double ecLeft   = ComputeECTime(vessel, vsl, now, ecPerSec);
                SetOrRefreshAlarm("[USILS-EC]", vessel, ecLeft, now);

                // Hab and Home — computed per-crew, alarmed on the earliest expiry
                bool   anyHabPenalty = false;
                double earliestHab   = double.PositiveInfinity;
                double earliestHome  = double.PositiveInfinity;
                // CachedHabTime is set by GetTotalHabTime (internal) each time USI-LS polls
                double habTotal      = vsl.CachedHabTime;

                var crew = vessel.GetVesselCrew();
                for (int i = 0; i < crew.Count; i++)
                {
                    ProtoCrewMember c = crew[i];
                    if (LifeSupportManager.GetNoHomeEffect(c.name) == 0) continue;
                    anyHabPenalty = true;

                    LifeSupportStatus cls = LifeSupportManager.Instance.FetchKerbal(c);

                    double habLeft = habTotal - (now - cls.TimeEnteredVessel);
                    if (!IsIndefinite(c, habLeft, cfg))
                        earliestHab = Math.Min(earliestHab, habLeft);

                    double homeLeft = cls.MaxOffKerbinTime - now;
                    if (!IsIndefinite(c, homeLeft, cfg))
                        earliestHome = Math.Min(earliestHome, homeLeft);
                }

                if (anyHabPenalty)
                {
                    SetOrRefreshAlarm("[USILS-Hab]",  vessel, earliestHab,  now);
                    SetOrRefreshAlarm("[USILS-Home]", vessel, earliestHome, now);
                }
                else
                {
                    RemoveAlarm(vessel.persistentId, "[USILS-Hab]");
                    RemoveAlarm(vessel.persistentId, "[USILS-Home]");
                }
            }
        }

        // ── Time computation helpers ────────────────────────────────────────

        private double ComputeSuppliesTime(Vessel vessel, VesselSupplyStatus vsl, double now, double ratePerSec)
        {
            if (ratePerSec <= FloatTolerance) return double.PositiveInfinity;
            double amount = GetResourceInVessel(vessel, "Supplies");
            if (amount <= FloatTolerance)
                return vsl.SuppliesLeft - (now - vsl.LastFeeding);
            return amount / ratePerSec;
        }

        private double ComputeECTime(Vessel vessel, VesselSupplyStatus vsl, double now, double ratePerSec)
        {
            if (ratePerSec <= FloatTolerance) return double.PositiveInfinity;
            double amount = GetResourceInVessel(vessel, "ElectricCharge");
            if (amount <= FloatTolerance)
                return vsl.ECLeft - (now - vsl.LastECCheck);
            return amount / ratePerSec;
        }

        // Returns true when the remaining time is so large USI-LS treats it as indefinite
        private bool IsIndefinite(ProtoCrewMember c, double timeLeft, LifeSupportConfig cfg)
        {
            if (timeLeft >= cfg.PermaHabTime) return true;
            if (c.HasEffect("ExplorerSkill") && timeLeft >= cfg.ScoutHabTime) return true;
            return false;
        }

        // ── Alarm lifecycle ─────────────────────────────────────────────────

        private void SetOrRefreshAlarm(string prefix, Vessel vessel, double timeLeft, double now)
        {
            // Indefinite, NaN, or already expired → ensure no alarm exists
            if (double.IsPositiveInfinity(timeLeft) || double.IsNaN(timeLeft) || timeLeft <= 0)
            {
                RemoveAlarm(vessel.persistentId, prefix);
                return;
            }

            double alarmUT = now + timeLeft - LeadTimeSecs;

            // Alarm would fire in the past → nothing useful to show
            if (alarmUT <= now)
            {
                RemoveAlarm(vessel.persistentId, prefix);
                return;
            }

            AlarmTypeRaw existing = FindAlarm(vessel.persistentId, prefix);
            if (existing != null && Math.Abs(existing.ut - alarmUT) < AlarmTolerance)
                return; // already correct, skip update

            if (existing != null)
                AlarmClockScenario.DeleteAlarm(existing);

            string label = prefix.Trim('[', ']').Replace("USILS-", "");
            AlarmTypeRaw alarm = new AlarmTypeRaw
            {
                title       = vessel.vesselName + " \u2013 " + label,
                description = prefix + ":" + vessel.id,
                actions     = { warp = AlarmActions.WarpEnum.KillWarp, message = AlarmActions.MessageEnum.Yes },
                ut          = alarmUT,
                vesselId    = vessel.persistentId
            };
            AlarmClockScenario.AddAlarm(alarm);
            Debug.Log(string.Format("[LifeSupportAlarms] Alarm set: {0} for {1} at UT {2:F0}",
                prefix, vessel.vesselName, alarmUT));
        }

        private AlarmTypeRaw FindAlarm(uint vesselPersistentId, string prefix)
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

        private void RemoveAlarm(uint vesselPersistentId, string prefix)
        {
            AlarmTypeRaw found = FindAlarm(vesselPersistentId, prefix);
            if (found != null)
                AlarmClockScenario.DeleteAlarm(found);
        }

        // ── KSP helpers ─────────────────────────────────────────────────────

        private Vessel FindVessel(string vesselId)
        {
            var vessels = FlightGlobals.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                if (vessels[i].id.ToString() == vesselId)
                    return vessels[i];
            }
            return null;
        }

        private double GetResourceInVessel(Vessel vessel, string resName)
        {
            if (vessel == null) return 0d;
            double amount = 0d;
            var parts = vessel.parts;
            for (int i = 0; i < parts.Count; i++)
            {
                Part p = parts[i];
                if (!p.Resources.Contains(resName)) continue;
                PartResource res = p.Resources[resName];
                if (res.flowState) amount += res.amount;
            }
            return amount;
        }
    }
}
