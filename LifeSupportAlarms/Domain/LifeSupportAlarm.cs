namespace LifeSupportAlarms.Domain
{
    // Read-only DTO wrapping a KSP AlarmTypeRaw.
    // All mutation (create, delete, update) goes through AlarmRepository.
    internal sealed class LifeSupportAlarm
    {
        internal AlarmTypeRaw Raw     { get; }
        internal string       Prefix  { get; }
        internal string       Title   => Raw.title;
        internal double       Ut      => Raw.ut;
        internal uint         VesselId => Raw.vesselId;

        // Only AlarmRepository creates instances
        internal LifeSupportAlarm(AlarmTypeRaw raw, string prefix)
        {
            Raw    = raw;
            Prefix = prefix;
        }
    }
}
