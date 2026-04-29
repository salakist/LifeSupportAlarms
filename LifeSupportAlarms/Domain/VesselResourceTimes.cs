namespace LifeSupportAlarms.Domain
{
    // Data carrier: computed remaining times for all life support resources on a vessel.
    // Times are in seconds; double.PositiveInfinity means the resource is not applicable
    // or is effectively unlimited for this vessel.
    internal readonly struct VesselResourceTimes
    {
        internal double SuppliesLeft { get; }
        internal double ECLeft { get; }
        internal double EarliestHab { get; }
        internal double EarliestHome { get; }
        internal bool AnyHabPenalty { get; }

        internal VesselResourceTimes(
            double suppliesLeft, double ecLeft,
            double earliestHab, double earliestHome, bool anyHabPenalty)
        {
            SuppliesLeft = suppliesLeft;
            ECLeft = ecLeft;
            EarliestHab = earliestHab;
            EarliestHome = earliestHome;
            AnyHabPenalty = anyHabPenalty;
        }
    }
}
