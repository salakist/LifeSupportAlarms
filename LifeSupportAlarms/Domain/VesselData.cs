using System;
using System.Collections.Generic;

namespace LifeSupportAlarms.Domain
{
    // Data carrier: vessel identity and a list of life support resource states.
    // Returned by ILifeSupportProvider.GetVesselData; consumed by AlarmService.
    internal sealed class VesselData
    {
        internal string Name { get; }
        internal uint PersistentId { get; }
        internal Guid VesselGuid { get; }
        internal IReadOnlyList<ResourceEntry> Resources { get; }

        internal VesselData(string name, uint persistentId, Guid vesselGuid,
            IReadOnlyList<ResourceEntry> resources)
        {
            Name = name;
            PersistentId = persistentId;
            VesselGuid = vesselGuid;
            Resources = resources;
        }

        // One entry per tracked resource type.
        // SecondsLeft == double.PositiveInfinity means not applicable or effectively unlimited.
        internal readonly struct ResourceEntry
        {
            internal string AlarmPrefix { get; }
            internal string ResourceLabel { get; }
            internal double SecondsLeft { get; }
            internal bool Enabled { get; }

            internal ResourceEntry(string alarmPrefix, string resourceLabel,
                double secondsLeft, bool enabled)
            {
                AlarmPrefix = alarmPrefix;
                ResourceLabel = resourceLabel;
                SecondsLeft = secondsLeft;
                Enabled = enabled;
            }
        }
    }
}
