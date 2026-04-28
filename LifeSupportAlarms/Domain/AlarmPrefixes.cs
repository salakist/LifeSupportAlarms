namespace LifeSupportAlarms.Domain
{
    // Canonical alarm prefix strings used to identify and categorise USI-LS alarms.
    // Stored in KSP AlarmTypeRaw.description as "<Prefix>:<VesselGuid>".
    internal static class AlarmPrefixes
    {
        internal const string Supplies = "[USILS-Supplies]";
        internal const string EC       = "[USILS-EC]";
        internal const string Hab      = "[USILS-Hab]";
        internal const string Home     = "[USILS-Home]";
        internal const string Grouped  = "[USILS-Grouped]";

        internal static readonly string[] AllResources =
            [Supplies, EC, Hab, Home];

        internal static readonly string[] All =
            [Supplies, EC, Hab, Home, Grouped];
    }
}
