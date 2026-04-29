using System;
using System.Collections.Generic;
using LifeSupport;
using LifeSupportAlarms.Domain;
using LifeSupportAlarms.Repositories;

namespace LifeSupportAlarms.Providers
{
    // ILifeSupportProvider implementation for USI Life Support.
    // No USI-LS types as instance fields — Mono only JIT-compiles method bodies on first
    // invocation, so direct type usage inside methods is safe even when the mod is absent,
    // as long as instantiation is guarded by an AssemblyLoader check in LifeSupportAlarmsCore.
    internal sealed class UsiLsProvider : ILifeSupportProvider
    {
        private const double FloatTolerance = 1e-6;

        public bool IsAvailable =>
            LifeSupportScenario.Instance != null
            && LifeSupportScenario.Instance.settings.isLoaded();

        public IEnumerable<VesselData> GetVesselData(LifeSupportAlarmsSettings settings, double now)
        {
            LifeSupportConfig cfg = LifeSupportScenario.Instance.settings.GetSettings();

            foreach (VesselSupplyStatus supply in LifeSupportManager.Instance.VesselSupplyInfo)
            {
                if (supply.NumCrew == 0) continue;

                Vessel vessel = VesselRepository.FindVessel(supply.VesselId);
                if (vessel == null) continue;

                yield return BuildVesselData(vessel, supply, settings, cfg, now);
            }
        }

        // --- Private helpers ---------------------------------------------------------

        private static VesselData BuildVesselData(
            Vessel vessel, VesselSupplyStatus supply,
            LifeSupportAlarmsSettings settings, LifeSupportConfig cfg, double now)
        {
            double suppliesLeft = settings.EnableSuppliesAlarm
                ? ComputeSuppliesLeft(vessel, supply, cfg, now)
                : double.PositiveInfinity;

            double ecLeft = settings.EnableECAlarm
                ? ComputeECLeft(vessel, supply, cfg, now)
                : double.PositiveInfinity;

            // Returns ∞ for hab/home when no hab penalty applies — AlarmService needs no special case
            ComputeHabHome(vessel, supply, cfg, now, out double earliestHab, out double earliestHome);

            VesselData.ResourceEntry[] resources =
            [
                new(AlarmPrefixes.Supplies, "Supplies", suppliesLeft, settings.EnableSuppliesAlarm),
                new(AlarmPrefixes.EC, "Electric Charge", ecLeft, settings.EnableECAlarm),
                new(AlarmPrefixes.Hab, "Hab", earliestHab, settings.EnableHabAlarm),
                new(AlarmPrefixes.Home, "Home", earliestHome, settings.EnableHomeAlarm),
            ];

            return new VesselData(vessel.vesselName, vessel.persistentId, vessel.id, resources);
        }

        private static double ComputeSuppliesLeft(
            Vessel vessel, VesselSupplyStatus supply, LifeSupportConfig cfg, double now)
        {
            double rate = cfg.SupplyAmount * supply.NumCrew * supply.RecyclerMultiplier;
            if (rate <= FloatTolerance) return double.PositiveInfinity;
            double amount = GetResourceAmount(vessel, "Supplies");
            return amount > FloatTolerance
                ? amount / rate
                : supply.SuppliesLeft - (now - supply.LastFeeding);
        }

        private static double ComputeECLeft(
            Vessel vessel, VesselSupplyStatus supply, LifeSupportConfig cfg, double now)
        {
            double rate = cfg.ECAmount * supply.NumCrew;
            if (rate <= FloatTolerance) return double.PositiveInfinity;
            double amount = GetResourceAmount(vessel, "ElectricCharge");
            return amount > FloatTolerance
                ? amount / rate
                : supply.ECLeft - (now - supply.LastECCheck);
        }

        private static void ComputeHabHome(
            Vessel vessel, VesselSupplyStatus supply, LifeSupportConfig cfg, double now,
            out double earliestHab, out double earliestHome)
        {
            double habTotal = supply.CachedHabTime;
            earliestHab = double.PositiveInfinity;
            earliestHome = double.PositiveInfinity;
            bool anyPenalty = false;

            List<ProtoCrewMember> crew = vessel.GetVesselCrew();
            for (int i = 0; i < crew.Count; i++)
            {
                ProtoCrewMember c = crew[i];
                if (LifeSupportManager.GetNoHomeEffect(c.name) == 0) continue;
                anyPenalty = true;

                LifeSupportStatus cls = LifeSupportManager.Instance.FetchKerbal(c);

                double habLeft = habTotal - (now - cls.TimeEnteredVessel);
                if (!IsIndefinite(c, habLeft, cfg))
                    earliestHab = Math.Min(earliestHab, habLeft);

                double homeLeft = cls.MaxOffKerbinTime - now;
                if (!IsIndefinite(c, homeLeft, cfg))
                    earliestHome = Math.Min(earliestHome, homeLeft);
            }

            if (!anyPenalty)
            {
                earliestHab = double.PositiveInfinity;
                earliestHome = double.PositiveInfinity;
            }
        }

        private static double GetResourceAmount(Vessel vessel, string resName)
        {
            double amount = 0d;
            List<Part> parts = vessel.parts;
            for (int i = 0; i < parts.Count; i++)
            {
                Part p = parts[i];
                if (!p.Resources.Contains(resName)) continue;
                PartResource res = p.Resources[resName];
                if (res.flowState) amount += res.amount;
            }
            return amount;
        }

        private static bool IsIndefinite(ProtoCrewMember c, double timeLeft, LifeSupportConfig cfg) =>
            timeLeft >= cfg.PermaHabTime || (c.HasEffect("ExplorerSkill") && timeLeft >= cfg.ScoutHabTime);
    }
}
