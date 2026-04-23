using System;
using LifeSupport;
using UnityEngine;

namespace LifeSupportAlarms
{
    // Shared logic — subclassed by the Flight and TrackingStation addons
    public class LifeSupportAlarmsCore : MonoBehaviour
    {
        private const double AlarmTolerance = 60.0;    // 1 minute
        private const double FloatTolerance = 1e-6;

        private static readonly string[] AlarmPrefixes =
            { "[USILS-Supplies]", "[USILS-EC]", "[USILS-Hab]", "[USILS-Home]", "[USILS-Grouped]" };

        public void Start()
        {
            Debug.Log("[LifeSupportAlarms] Loaded");
            InvokeRepeating("PollLifeSupport", 5f, 10f);
        }

        // ── Main poll ───────────────────────────────────────────────────────

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
                            RemoveAlarm(v.persistentId, prefix);
                return;
            }

            if (ReferenceEquals(LifeSupportScenario.Instance, null)) { Debug.Log("[LifeSupportAlarms] Poll: LifeSupportScenario.Instance is truly null"); return; }
            if (!LifeSupportScenario.Instance.settings.isLoaded()) { Debug.Log("[LifeSupportAlarms] Poll: settings not loaded"); return; }
            if (AlarmClockScenario.Instance  == null)              { Debug.Log("[LifeSupportAlarms] Poll: AlarmClockScenario.Instance is null"); return; }

            double leadTimeSecs = settings.LeadTimeHours * 3600.0;

            // LifeSupportManager.Instance auto-creates via lazy getter — do not null-check with Unity ==
            var lsm = LifeSupportManager.Instance;

            double now = Planetarium.GetUniversalTime();
            LifeSupportConfig cfg = LifeSupportScenario.Instance.settings.GetSettings();

            Debug.Log(string.Format("[LifeSupportAlarms] Poll: {0} vessel supply records, {1} FlightGlobals vessels",
                lsm.VesselSupplyInfo.Count, FlightGlobals.Vessels.Count));

            foreach (VesselSupplyStatus vsl in lsm.VesselSupplyInfo)
            {
                if (vsl.NumCrew == 0) continue;

                Vessel vessel = FindVessel(vsl.VesselId);
                if (vessel == null) continue;

                // Supplies
                double suppliesLeft = double.PositiveInfinity;
                if (settings.EnableSuppliesAlarm)
                {
                    double suppliesPerSec = cfg.SupplyAmount * vsl.NumCrew * vsl.RecyclerMultiplier;
                    suppliesLeft = ComputeSuppliesTime(vessel, vsl, now, suppliesPerSec);
                }

                // EC
                double ecLeft = double.PositiveInfinity;
                if (settings.EnableECAlarm)
                {
                    double ecPerSec = cfg.ECAmount * vsl.NumCrew;
                    ecLeft = ComputeECTime(vessel, vsl, now, ecPerSec);
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
                    if (!IsIndefinite(c, habLeft, cfg))
                        earliestHab = Math.Min(earliestHab, habLeft);

                    double homeLeft = cls.MaxOffKerbinTime - now;
                    if (!IsIndefinite(c, homeLeft, cfg))
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
                    RemoveAlarm(vessel.persistentId, "[USILS-Supplies]");
                    RemoveAlarm(vessel.persistentId, "[USILS-EC]");
                    RemoveAlarm(vessel.persistentId, "[USILS-Hab]");
                    RemoveAlarm(vessel.persistentId, "[USILS-Home]");

                    // Find earliest enabled resource
                    double earliest = double.PositiveInfinity;
                    string criticalLabel = "";
                    if (settings.EnableSuppliesAlarm  && suppliesLeft  < earliest) { earliest = suppliesLeft;  criticalLabel = "Supplies"; }
                    if (settings.EnableECAlarm         && ecLeft        < earliest) { earliest = ecLeft;        criticalLabel = "Electric Charge"; }
                    if (settings.EnableHabAlarm        && earliestHab   < earliest) { earliest = earliestHab;   criticalLabel = "Hab"; }
                    if (settings.EnableHomeAlarm       && earliestHome  < earliest) { earliest = earliestHome;  criticalLabel = "Home"; }

                    SetOrRefreshGroupedAlarm(vessel, earliest, criticalLabel, now, leadTimeSecs, settings.AlarmAction);
                }
                else
                {
                    // Remove grouped alarm
                    RemoveAlarm(vessel.persistentId, "[USILS-Grouped]");

                    // Supplies
                    if (settings.EnableSuppliesAlarm)
                        SetOrRefreshAlarm("[USILS-Supplies]", vessel, suppliesLeft, now, leadTimeSecs, settings.AlarmAction);
                    else
                        RemoveAlarm(vessel.persistentId, "[USILS-Supplies]");

                    // EC
                    if (settings.EnableECAlarm)
                        SetOrRefreshAlarm("[USILS-EC]", vessel, ecLeft, now, leadTimeSecs, settings.AlarmAction);
                    else
                        RemoveAlarm(vessel.persistentId, "[USILS-EC]");

                    // Hab
                    if (anyHabPenalty)
                    {
                        if (settings.EnableHabAlarm)
                            SetOrRefreshAlarm("[USILS-Hab]",  vessel, earliestHab,  now, leadTimeSecs, settings.AlarmAction);
                        else
                            RemoveAlarm(vessel.persistentId, "[USILS-Hab]");

                        if (settings.EnableHomeAlarm)
                            SetOrRefreshAlarm("[USILS-Home]", vessel, earliestHome, now, leadTimeSecs, settings.AlarmAction);
                        else
                            RemoveAlarm(vessel.persistentId, "[USILS-Home]");
                    }
                    else
                    {
                        RemoveAlarm(vessel.persistentId, "[USILS-Hab]");
                        RemoveAlarm(vessel.persistentId, "[USILS-Home]");
                    }
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

        private void SetOrRefreshGroupedAlarm(Vessel vessel, double timeLeft, string criticalLabel,
            double now, double leadTimeSecs, int alarmAction)
        {
            string expectedTitle = vessel.vesselName + (criticalLabel.Length > 0 ? " (" + criticalLabel + ")" : "");
            SetOrRefreshAlarmCore("[USILS-Grouped]", vessel, expectedTitle, timeLeft, now, leadTimeSecs, alarmAction);
        }

        private void SetOrRefreshAlarm(string prefix, Vessel vessel, double timeLeft, double now,
            double leadTimeSecs, int alarmAction)
        {
            string label         = prefix.Trim('[', ']').Replace("USILS-", "").Replace("EC", "Electric Charge");
            string expectedTitle = vessel.vesselName + " " + label;
            SetOrRefreshAlarmCore(prefix, vessel, expectedTitle, timeLeft, now, leadTimeSecs, alarmAction);
        }

        private void SetOrRefreshAlarmCore(string prefix, Vessel vessel, string expectedTitle,
            double timeLeft, double now, double leadTimeSecs, int alarmAction)
        {
            // Indefinite, NaN, or already expired → ensure no alarm exists
            if (double.IsPositiveInfinity(timeLeft) || double.IsNaN(timeLeft) || timeLeft <= 0)
            {
                RemoveAlarm(vessel.persistentId, prefix);
                return;
            }

            double alarmUT = now + timeLeft - leadTimeSecs;

            // Alarm would fire in the past → nothing useful to show
            if (alarmUT <= now)
            {
                RemoveAlarm(vessel.persistentId, prefix);
                return;
            }

            AlarmActions.WarpEnum warpAction = alarmAction == 0
                ? AlarmActions.WarpEnum.DoNothing
                : AlarmActions.WarpEnum.KillWarp;

            AlarmTypeRaw existing = FindAlarm(vessel.persistentId, prefix);
            if (existing != null && Math.Abs(existing.ut - alarmUT) < AlarmTolerance && existing.title == expectedTitle)
                return; // already correct, skip update

            if (existing != null)
                AlarmClockScenario.DeleteAlarm(existing);

            AlarmTypeRaw alarm = new AlarmTypeRaw
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
            AlarmTypeRaw fake = new AlarmTypeRaw { ut = alarm.ut + 1, actions = { message = AlarmActions.MessageEnum.No, deleteWhenDone = true } };
            AlarmClockScenario.AddAlarm(fake);
            AlarmClockScenario.DeleteAlarm(fake);
            Debug.Log(string.Format("[LifeSupportAlarms] Alarm set: '{0}' at UT {1:F0}", expectedTitle, alarmUT));
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

    // ── Scene-specific entry points ──────────────────────────────────────────

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class LifeSupportAlarmsFlightAddon : LifeSupportAlarmsCore { }

    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class LifeSupportAlarmsTrackingAddon : LifeSupportAlarmsCore { }

    // ── Register LifeSupportScenario for TrackingStation ─────────────────────
    // USI-LS only registers its scenario for SPACECENTER, FLIGHT, and EDITOR.
    // We patch in TRACKINGSTATION at SpaceCentre startup so that on the next
    // visit to the Tracking Station the scenario is loaded and its data is
    // available (i.e. LifeSupportScenario.Instance is non-null there).

    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class LifeSupportAlarmsScenarioRegistrar : MonoBehaviour
    {
        public void Start()
        {
            var game = HighLogic.CurrentGame;
            if (game == null) return;

            var psm = game.scenarios.Find(s => s.moduleName == typeof(LifeSupportScenario).Name);
            if (psm == null)
            {
                Debug.Log("[LifeSupportAlarms] LifeSupportScenario not found in game.scenarios — USI-LS may not be installed.");
                return;
            }

            if (!psm.targetScenes.Contains(GameScenes.TRACKSTATION))
            {
                psm.targetScenes.Add(GameScenes.TRACKSTATION);
                Debug.Log("[LifeSupportAlarms] Added TRACKINGSTATION to LifeSupportScenario target scenes.");
            }
            else
            {
                Debug.Log("[LifeSupportAlarms] TRACKINGSTATION already in LifeSupportScenario target scenes.");
            }
        }
    }
}
