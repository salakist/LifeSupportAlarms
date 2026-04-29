using System.Collections.Generic;

namespace LifeSupportAlarms.Domain
{
    internal interface ILifeSupportProvider
    {
        bool IsAvailable { get; }
        IEnumerable<VesselData> GetVesselData(LifeSupportAlarmsSettings settings, double now);
    }
}
