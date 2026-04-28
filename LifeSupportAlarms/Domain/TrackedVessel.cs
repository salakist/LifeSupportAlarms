using System;
using System.Collections.Generic;
using LifeSupport;
using LifeSupportAlarms;

namespace LifeSupportAlarms.Domain
{
    // Domain object wrapping a KSP Vessel and its USI-LS supply status.
    // Owns all resource-time computation (was ResourceTimeCalculator + ComputeResourceTimes in Core).
    internal sealed class TrackedVessel
    {
        private const double FloatTolerance = 1e-6;

        private readonly Vessel             _vessel;
        private readonly VesselSupplyStatus _supply;

        internal TrackedVessel(Vessel vessel, VesselSupplyStatus supply)
        {
            _vessel = vessel;
            _supply = supply;
        }

        internal string Name         => _vessel.vesselName;
        internal uint   PersistentId => _vessel.persistentId;
        internal Guid   Id           => _vessel.id;

        internal VesselResourceTimes GetResourceTimes(
            LifeSupportAlarmsSettings settings,
            LifeSupportConfig cfg,
            double now)
        {
            double suppliesLeft = double.PositiveInfinity;
            if (settings.EnableSuppliesAlarm)
            {
                double rate = cfg.SupplyAmount * _supply.NumCrew * _supply.RecyclerMultiplier;
                suppliesLeft = ComputeSuppliesLeft(rate, now);
            }

            double ecLeft = double.PositiveInfinity;
            if (settings.EnableECAlarm)
            {
                double rate = cfg.ECAmount * _supply.NumCrew;
                ecLeft = ComputeECLeft(rate, now);
            }

            ComputeHabHome(cfg, now,
                out double earliestHab, out double earliestHome, out bool anyHabPenalty);

            return new VesselResourceTimes(suppliesLeft, ecLeft, earliestHab, earliestHome, anyHabPenalty);
        }

        // --- Private computation helpers -------------------------------------------------

        private double ComputeSuppliesLeft(double ratePerSec, double now)
        {
            if (ratePerSec <= FloatTolerance) return double.PositiveInfinity;
            double amount = GetResourceAmount("Supplies");
            return amount > FloatTolerance
                ? amount / ratePerSec
                : _supply.SuppliesLeft - (now - _supply.LastFeeding);
        }

        private double ComputeECLeft(double ratePerSec, double now)
        {
            if (ratePerSec <= FloatTolerance) return double.PositiveInfinity;
            double amount = GetResourceAmount("ElectricCharge");
            return amount > FloatTolerance
                ? amount / ratePerSec
                : _supply.ECLeft - (now - _supply.LastECCheck);
        }

        private void ComputeHabHome(LifeSupportConfig cfg, double now,
            out double earliestHab, out double earliestHome, out bool anyPenalty)
        {
            double habTotal = _supply.CachedHabTime;
            earliestHab  = double.PositiveInfinity;
            earliestHome = double.PositiveInfinity;
            anyPenalty   = false;

            List<ProtoCrewMember> crew = _vessel.GetVesselCrew();
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
                earliestHab  = double.PositiveInfinity;
                earliestHome = double.PositiveInfinity;
            }
        }

        // Absorbed from VesselHelpers.GetResourceInVessel
        private double GetResourceAmount(string resName)
        {
            double amount = 0d;
            List<Part> parts = _vessel.parts;
            for (int i = 0; i < parts.Count; i++)
            {
                Part p = parts[i];
                if (!p.Resources.Contains(resName)) continue;
                PartResource res = p.Resources[resName];
                if (res.flowState) amount += res.amount;
            }
            return amount;
        }

        // Absorbed from ResourceTimeCalculator.IsIndefinite
        private static bool IsIndefinite(ProtoCrewMember c, double timeLeft, LifeSupportConfig cfg)
        {
            if (timeLeft >= cfg.PermaHabTime) return true;
            if (c.HasEffect("ExplorerSkill") && timeLeft >= cfg.ScoutHabTime) return true;
            return false;
        }
    }
}
