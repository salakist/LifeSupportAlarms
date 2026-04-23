using UnityEngine;

namespace LifeSupportAlarms
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class LifeSupportAlarmsAddon : MonoBehaviour
    {
        public void Start()
        {
            Debug.Log("[LifeSupportAlarms] Loaded");
        }
    }
}
