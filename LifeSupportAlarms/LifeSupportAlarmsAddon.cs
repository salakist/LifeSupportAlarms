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

        private int _pollCount = 0;

        private void PollLifeSupport()
        {
            _pollCount++;
            bool verbose = (_pollCount <= 3) || (_pollCount % 30 == 0); // log first 3 + every 5 min

            if (LifeSupportScenario.Instance == null)                   { if (verbose) Debug.Log("[LifeSupportAlarms] PollLifeSupport: LifeSupportScenario.Instance is null"); return; }
            if (!LifeSupportScenario.Instance.settings.isLoaded())       { if (verbose) Debug.Log("[LifeSupportAlarms] PollLifeSupport: LifeSupportScenario settings not loaded yet"); return; }
            if (AlarmClockScenario.Instance  == null)                    { if (verbose) Debug.Log("[LifeSupportAlarms] PollLifeSupport: AlarmClockScenario.Instance is null"); return; }

            // LifeSupportManager.Instance auto-creates via lazy getter — do not null-check with Unity ==
            var lsm = LifeSupportManager.Instance;

            double now = Planetarium.GetUniversalTime();
            LifeSupportConfig cfg = LifeSupportScenario.Instance.settings.GetSettings();

            var vesselInfoList = lsm.VesselSupplyInfo;
            if (verbose) Debug.Log(string.Format("[LifeSupportAlarms] Poll #{0}: {1} tracked vessel(s)", _pollCount, vesselInfoList.Count));

            foreach (VesselSupplyStatus vsl in vesselInfoList)
            {
                if (verbose) Debug.Log(string.Format("[LifeSupportAlarms]   Vessel '{0}' id={1} NumCrew={2} SuppliesLeft={3:F1} ECLeft={4:F1} CachedHabTime={5:F0}",
                    vsl.VesselName, vsl.VesselId, vsl.NumCrew, vsl.SuppliesLeft, vsl.ECLeft, vsl.CachedHabTime));

                if (vsl.NumCrew == 0) { if (verbose) Debug.Log("[LifeSupportAlarms]     -> skipped (NumCrew=0)"); continue; }

                Vessel vessel = FindVessel(vsl.VesselId);
                if (vessel == null) { if (verbose) Debug.Log("[LifeSupportAlarms]     -> skipped (vessel not found in FlightGlobals)"); continue; }

                // Supplies
                double suppliesPerSec = cfg.SupplyAmount * vsl.NumCrew * vsl.RecyclerMultiplier;
                double suppliesLeft   = ComputeSuppliesTime(vessel, vsl, now, suppliesPerSec);
                if (verbose) Debug.Log(string.Format("[LifeSupportAlarms]     Supplies: rate={0:F6}/s left={1:F0}s", suppliesPerSec, suppliesLeft));
                SetOrRefreshAlarm("[USILS-Supplies]", vessel, suppliesLeft, now);

                // EC
                double ecPerSec = cfg.ECAmount * vsl.NumCrew;
                double ecLeft   = ComputeECTime(vessel, vsl, now, ecPerSec);
                if (verbose) Debug.Log(string.Format("[LifeSupportAlarms]     EC: rate={0:F4}/s left={1:F0}s", ecPerSec, ecLeft));
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
                Debug.Log(string.Format("[LifeSupportAlarms] {0} {1}: timeLeft={2} -> no alarm (indefinite/expired)", prefix, vessel.vesselName, timeLeft));
                RemoveAlarm(vessel.persistentId, prefix);
                return;
            }

            double alarmUT = now + timeLeft - LeadTimeSecs;

            // Alarm would fire in the past → nothing useful to show
            if (alarmUT <= now)
            {
                Debug.Log(string.Format("[LifeSupportAlarms] {0} {1}: alarmUT={2:F0} <= now={3:F0} (timeLeft={4:F0}s < leadTime) -> no alarm", prefix, vessel.vesselName, alarmUT, now, timeLeft));
                RemoveAlarm(vessel.persistentId, prefix);
                return;
            }

            string trimmed       = prefix.Trim('[', ']');
            string noPrefix      = trimmed.Replace("USILS-", "");
            string label         = noPrefix.Replace("EC", "Electric Charge");
            string expectedTitle = vessel.vesselName + " " + label;
            Debug.Log(string.Format("[LifeSupportAlarms] DEBUG title build: prefix='{0}' trimmed='{1}' noPrefix='{2}' label='{3}' expectedTitle='{4}'",
                prefix, trimmed, noPrefix, label, expectedTitle));

            AlarmTypeRaw existing = FindAlarm(vessel.persistentId, prefix);
            if (existing != null && Math.Abs(existing.ut - alarmUT) < AlarmTolerance && existing.title == expectedTitle)
                return; // already correct, skip update

            if (existing != null)
                AlarmClockScenario.DeleteAlarm(existing);

            AlarmTypeRaw alarm = new AlarmTypeRaw
            {
                description = prefix + ":" + vessel.id,
                actions     = { warp = AlarmActions.WarpEnum.KillWarp, message = AlarmActions.MessageEnum.Yes },
                ut          = alarmUT,
                vesselId    = vessel.persistentId
            };
            AlarmClockScenario.AddAlarm(alarm);
            // AddAlarm resets title to vessel name; set it after the call
            alarm.title = expectedTitle;
            Debug.Log(string.Format("[LifeSupportAlarms] Alarm set: title='{0}' at UT {1:F0}", alarm.title, alarmUT));
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
