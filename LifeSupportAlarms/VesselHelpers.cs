namespace LifeSupportAlarms
{
    // KSP vessel utility methods used across the plugin.
    internal static class VesselHelpers
    {
        internal static Vessel FindVessel(string vesselId)
        {
            var vessels = FlightGlobals.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                if (vessels[i].id.ToString() == vesselId)
                    return vessels[i];
            }
            return null;
        }

        internal static double GetResourceInVessel(Vessel vessel, string resName)
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
