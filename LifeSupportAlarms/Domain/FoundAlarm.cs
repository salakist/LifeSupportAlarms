namespace LifeSupportAlarms.Domain
{
    // Wraps an existing KSP alarm found by AlarmRepository.Find().
    // Only AlarmRepository constructs instances of this type.
    internal sealed class FoundAlarm
    {
        internal AlarmTypeRaw Raw              { get; }
        internal string       Prefix           { get; }
        internal string       Title            => Raw.title;
        internal double       Ut               => Raw.ut;
        internal uint         VesselPersistentId => Raw.vesselId;

        internal FoundAlarm(AlarmTypeRaw raw, string prefix)
        {
            Raw    = raw;
            Prefix = prefix;
        }
    }
}
