using System.Collections.Generic;
using UnityEngine;
using LifeSupportAlarms.Domain;
using LifeSupportAlarms.Providers;
using LifeSupportAlarms.Repositories;
using LifeSupportAlarms.Services;

namespace LifeSupportAlarms
{
    // Shared MonoBehaviour base -- subclassed by the scene-specific KSPAddon stubs.
    // Responsible only for the poll loop: guards, provider iteration, and delegating to AlarmService.
    public class LifeSupportAlarmsCore : MonoBehaviour
    {
        private ILifeSupportProvider[] _providers;
        private AlarmService _alarmService;

        public void Start()
        {
            Debug.Log("[LifeSupportAlarms] Loaded");

            List<ILifeSupportProvider> providers = [];
            foreach (AssemblyLoader.LoadedAssembly a in AssemblyLoader.loadedAssemblies)
                if (a.name == "USILifeSupport") { providers.Add(new UsiLsProvider()); break; }
            _providers = [.. providers];

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

            double now = Planetarium.GetUniversalTime();
            double leadTimeSecs = settings.LeadTimeHours * 3600.0;
            AlarmAction alarmAction = (AlarmAction)settings.AlarmAction;
            bool grouped = settings.GroupAlarmsByVessel;

            foreach (ILifeSupportProvider provider in _providers)
            {
                if (!provider.IsAvailable) continue;
                foreach (VesselData vessel in provider.GetVesselData(settings, now))
                    _alarmService.Sync(vessel, now, leadTimeSecs, alarmAction, grouped);
            }
        }

        private static bool ValidatePrerequisites(LifeSupportAlarmsSettings settings) =>
            settings != null && AlarmClockScenario.Instance != null;
    }
}
