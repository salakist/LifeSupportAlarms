using System;
using System.Collections.Generic;
using LifeSupport;
using LifeSupportAlarms.Domain;

namespace LifeSupportAlarms.Repositories
{
    // Pure read accessor over the USI-LS vessel supply data.
    // Returns only vessels that have crew and exist in FlightGlobals.
    internal sealed class VesselRepository
    {
        internal IEnumerable<TrackedVessel> GetCrewedVessels()
        {
            // LifeSupportManager.Instance uses a lazy getter -- do not null-check with Unity ==
            foreach (VesselSupplyStatus vsl in LifeSupportManager.Instance.VesselSupplyInfo)
            {
                if (vsl.NumCrew == 0) continue;

                Vessel vessel = FindVessel(vsl.VesselId);
                if (vessel == null) continue;

                yield return new TrackedVessel(vessel, vsl);
            }
        }

        // Absorbed from VesselHelpers.FindVessel
        private static Vessel FindVessel(string vesselId)
        {
            // Parse once to avoid per-vessel ToString allocation
            if (!Guid.TryParse(vesselId, out Guid gid)) return null;
            List<Vessel> vessels = FlightGlobals.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                if (vessels[i].id == gid)
                    return vessels[i];
            }
            return null;
        }
    }
}
