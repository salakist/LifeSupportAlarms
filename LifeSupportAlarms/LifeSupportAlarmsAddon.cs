using LifeSupport;
using UnityEngine;

namespace LifeSupportAlarms
{
    // -- Scene-specific entry points ------------------------------------------

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class LifeSupportAlarmsFlightAddon : LifeSupportAlarmsCore { }

    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class LifeSupportAlarmsTrackingAddon : LifeSupportAlarmsCore { }

    // -- Register LifeSupportScenario for TrackingStation ---------------------
    // USI-LS only registers its scenario for SPACECENTER, FLIGHT, and EDITOR.
    // We patch in TRACKINGSTATION at SpaceCentre startup so that on the next
    // visit to the Tracking Station the scenario is loaded and its data is
    // available (i.e. LifeSupportScenario.Instance is non-null there).

    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class LifeSupportAlarmsScenarioRegistrar : MonoBehaviour
    {
        public void Start()
        {
            Game game = HighLogic.CurrentGame;
            if (game == null) return;

            ProtoScenarioModule psm = game.scenarios.Find(s => s.moduleName == nameof(LifeSupportScenario));
            if (psm == null)
            {
                Debug.Log("[LifeSupportAlarms] LifeSupportScenario not found in game.scenarios � USI-LS may not be installed.");
                return;
            }

            if (!psm.targetScenes.Contains(GameScenes.TRACKSTATION))
            {
                psm.targetScenes.Add(GameScenes.TRACKSTATION);
                Debug.Log("[LifeSupportAlarms] Added TRACKINGSTATION to LifeSupportScenario target scenes.");
            }
        }
    }
}
