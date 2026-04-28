using System;

namespace LifeSupportAlarms.Domain
{
    // Represents a single life-support alarm — either a desired spec (Raw == null, built via
    // factory methods) or an existing KSP alarm found by AlarmRepository (Raw != null).
    // AlarmRepository is the only class that sets or reads Raw.
    internal sealed class LifeSupportAlarm
    {
        // --- Identity ----------------------------------------------------------------

        internal string Prefix             { get; }
        internal string Title              { get; }
        internal double TimeLeft           { get; }
        internal uint   VesselPersistentId { get; }
        internal Guid   VesselGuid         { get; }

        // Set only when this instance was returned by AlarmRepository.Find().
        // Null when this is a spec produced by a factory method.
        internal AlarmTypeRaw Raw { get; private set; }

        // Convenience: title read from an existing KSP alarm (Raw != null)
        internal string ExistingTitle => Raw?.title;
        internal double ExistingUt    => Raw?.ut ?? double.NaN;

        // --- Private constructor (all paths go through factories) --------------------

        private LifeSupportAlarm(string prefix, string title, double timeLeft,
            uint vesselPersistentId, Guid vesselGuid)
        {
            Prefix             = prefix;
            Title              = title;
            TimeLeft           = timeLeft;
            VesselPersistentId = vesselPersistentId;
            VesselGuid         = vesselGuid;
        }

        // --- Factories ---------------------------------------------------------------

        // Spec for a per-resource alarm. Title derived from prefix.
        internal static LifeSupportAlarm ForResource(TrackedVessel vessel, string prefix, double timeLeft) =>
            new(prefix, ResourceTitle(vessel.Name, prefix), timeLeft, vessel.PersistentId, vessel.Id);

        // Spec for a grouped alarm showing the earliest-expiring enabled resource.
        internal static LifeSupportAlarm ForGrouped(TrackedVessel vessel, double timeLeft, string criticalLabel)
        {
            string title = vessel.Name + (criticalLabel.Length > 0 ? $" ({criticalLabel})" : "");
            return new("[USILS-Grouped]", title, timeLeft, vessel.PersistentId, vessel.Id);
        }

        // Used by AlarmRepository.Find() to wrap an existing KSP alarm.
        internal static LifeSupportAlarm FromExisting(AlarmTypeRaw raw, string prefix) =>
            new(prefix, raw.title, double.NaN, raw.vesselId, Guid.Empty) { Raw = raw };

        // --- Private helpers ---------------------------------------------------------

        // e.g. "[USILS-EC]" + "Kerbin Station 1" => "Kerbin Station 1 Electric Charge"
        private static string ResourceTitle(string vesselName, string prefix) =>
            vesselName + " " + prefix.Trim('[', ']').Replace("USILS-", "").Replace("EC", "Electric Charge");
    }
}
