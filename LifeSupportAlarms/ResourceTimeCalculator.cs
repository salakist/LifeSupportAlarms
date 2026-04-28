using LifeSupport;

namespace LifeSupportAlarms
{
    // Computes remaining resource time for a vessel from current USI-LS data.
    internal static class ResourceTimeCalculator
    {
        private const double FloatTolerance = 1e-6;

        internal static double ComputeSuppliesTime(Vessel vessel, VesselSupplyStatus vsl, double now, double ratePerSec)
        {
            if (ratePerSec <= FloatTolerance) return double.PositiveInfinity;
            double amount = VesselHelpers.GetResourceInVessel(vessel, "Supplies");
            if (amount <= FloatTolerance)
                return vsl.SuppliesLeft - (now - vsl.LastFeeding);
            return amount / ratePerSec;
        }

        internal static double ComputeECTime(Vessel vessel, VesselSupplyStatus vsl, double now, double ratePerSec)
        {
            if (ratePerSec <= FloatTolerance) return double.PositiveInfinity;
            double amount = VesselHelpers.GetResourceInVessel(vessel, "ElectricCharge");
            if (amount <= FloatTolerance)
                return vsl.ECLeft - (now - vsl.LastECCheck);
            return amount / ratePerSec;
        }

        // Returns true when the remaining time is so large USI-LS treats it as indefinite.
        internal static bool IsIndefinite(ProtoCrewMember c, double timeLeft, LifeSupportConfig cfg)
        {
            if (timeLeft >= cfg.PermaHabTime) return true;
            if (c.HasEffect("ExplorerSkill") && timeLeft >= cfg.ScoutHabTime) return true;
            return false;
        }
    }
}
