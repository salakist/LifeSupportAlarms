using System;

namespace LifeSupportAlarms.Domain
{
    // Desired alarm state — what we want a KSP alarm to be.
    // Built exclusively via static factories; never wraps an existing KSP alarm.
    // Pass to AlarmRepository.Upsert to create or refresh the corresponding KSP alarm.
    internal sealed class AlarmSpec
    {
        internal string Prefix { get; }
        internal string Title { get; }
        internal double TimeLeft { get; }
        internal uint VesselPersistentId { get; }
        internal Guid VesselGuid { get; }

        private AlarmSpec(string prefix, string title, double timeLeft,
            uint vesselPersistentId, Guid vesselGuid)
        {
            Prefix = prefix;
            Title = title;
            TimeLeft = timeLeft;
            VesselPersistentId = vesselPersistentId;
            VesselGuid = vesselGuid;
        }

        // --- Factories ---------------------------------------------------------------

        // Spec for a per-resource alarm. Title derived from prefix.
        internal static AlarmSpec ForResource(TrackedVessel vessel, string prefix, double timeLeft) =>
            new(prefix, ResourceTitle(vessel.Name, prefix), timeLeft, vessel.PersistentId, vessel.Id);

        // Spec for a grouped alarm showing the earliest-expiring enabled resource.
        internal static AlarmSpec ForGrouped(TrackedVessel vessel, double timeLeft, string criticalLabel)
        {
            string title = vessel.Name + (criticalLabel.Length > 0 ? $" ({criticalLabel})" : "");
            return new(AlarmPrefixes.Grouped, title, timeLeft, vessel.PersistentId, vessel.Id);
        }

        // --- Private helpers ---------------------------------------------------------

        // Maps a known prefix to its human-readable resource label.
        // Falls back to stripping the bracket/namespace decoration for any future prefix.
        private static string ResourceTitle(string vesselName, string prefix) =>
            vesselName + " " + prefix switch
            {
                AlarmPrefixes.Supplies => "Supplies",
                AlarmPrefixes.EC => "Electric Charge",
                AlarmPrefixes.Hab => "Hab",
                AlarmPrefixes.Home => "Home",
                _ => prefix.Trim('[', ']').Replace("USILS-", "")
            };
    }
}
