using System;
using System.Collections.Generic;

namespace LifeSupportAlarms.Repositories
{
    // Shared vessel-lookup utilities used by all ILifeSupportProvider implementations.
    internal static class VesselRepository
    {
        internal static Vessel FindVessel(Guid id)
        {
            List<Vessel> vessels = FlightGlobals.Vessels;
            for (int i = 0; i < vessels.Count; i++)
                if (vessels[i].id == id) return vessels[i];
            return null;
        }

        // Convenience overload for providers that store vessel IDs as strings (e.g. USI-LS).
        internal static Vessel FindVessel(string vesselId) =>
            Guid.TryParse(vesselId, out Guid gid) ? FindVessel(gid) : null;
    }
}
