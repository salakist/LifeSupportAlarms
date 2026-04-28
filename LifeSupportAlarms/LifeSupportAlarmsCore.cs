using LifeSupport;
using UnityEngine;
using LifeSupportAlarms.Domain;
using LifeSupportAlarms.Repositories;
using LifeSupportAlarms.Services;

namespace LifeSupportAlarms
{
    // Shared MonoBehaviour base -- subclassed by the scene-specific KSPAddon stubs.
    // Responsible only for the poll loop: guards, vessel iteration, and delegating
    // to VesselRepository and AlarmService.
    public class LifeSupportAlarmsCore : MonoBehaviour
    {
        private VesselRepository _vesselRepo;
        private AlarmService     _alarmService;

        public void Start()
        {
            Debug.Log("[LifeSupportAlarms] Loaded");
            _vesselRepo   = new VesselRepository();
            _alarmService = new AlarmService(new AlarmRepository());
            InvokeRepeating("PollLifeSupport", 5f, 10f);
        }

        private void PollLifeSupport()
        {
            LifeSupportAlarmsSettings settings = LifeSupportAlarmsSettings.Instance;
            if (!ValidatePrerequisites(settings)) return;

            if (!settings.EnableAlarms)
            {
                _alarmService.ClearAll();
                return;
            }

            double now          = Planetarium.GetUniversalTime();
            double leadTimeSecs = settings.LeadTimeHours * 3600.0;
            LifeSupportConfig cfg = LifeSupportScenario.Instance.settings.GetSettings();

            foreach (TrackedVessel vessel in _vesselRepo.GetCrewedVessels())
            {
                VesselResourceTimes times = vessel.GetResourceTimes(settings, cfg, now);
                _alarmService.Sync(vessel, times, settings, now, leadTimeSecs);
            }
        }

        private static bool ValidatePrerequisites(LifeSupportAlarmsSettings settings)
        {
            if (settings == null) return false;
            if (ReferenceEquals(LifeSupportScenario.Instance, null)) return false;
            if (!LifeSupportScenario.Instance.settings.isLoaded())   return false;
            if (AlarmClockScenario.Instance == null)                 return false;
            return true;
        }
    }
}
