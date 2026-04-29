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

        // Spec for a per-resource alarm. Title and prefix taken from the ResourceEntry.
        internal static AlarmSpec ForResource(VesselData vessel, VesselData.ResourceEntry entry) =>
            new(entry.AlarmPrefix, vessel.Name + " " + entry.ResourceLabel,
                entry.SecondsLeft, vessel.PersistentId, vessel.VesselGuid);

        // Spec for a grouped alarm showing the earliest-expiring enabled resource.
        internal static AlarmSpec ForGrouped(VesselData vessel, double timeLeft, string criticalLabel)
        {
            string title = vessel.Name + (criticalLabel.Length > 0 ? $" ({criticalLabel})" : "");
            return new(AlarmPrefixes.Grouped, title, timeLeft, vessel.PersistentId, vessel.VesselGuid);
        }
    }
}
