using System;
using System.Collections.Generic;
using System.Linq;

namespace ChoppingTrees
{
    /// <summary>
    /// Automated production, kept apart from machine visuals. Uses a continuous model, so one
    /// call for N seconds gives the same result as many small calls adding up to N seconds:
    /// producers add rate * t, then each processor converts min(capacity * t, available input).
    /// That makes it frame-rate independent and lets offline time use the same code.
    /// </summary>
    public class Production
    {
        readonly GameState m_State;
        readonly GameDatabase m_Db;
        readonly Upgrades m_Upgrades;
        readonly MachineDef[] m_Ordered;

        public Production(GameState state, GameDatabase db, Upgrades upgrades)
        {
            m_State = state;
            m_Db = db;
            m_Upgrades = upgrades;
            m_Ordered = db.machines.Where(m => m != null).OrderBy(m => m.order).ToArray();
        }

        /// <summary>Producer: output/s for all owned machines. Processor: input/s they can consume.</summary>
        public double Capacity(MachineDef m)
        {
            int count = m_Upgrades.MachineCount(m.id);
            if (count <= 0) return 0;
            return NumberUtil.SafePositive(m.baseRate * count * m_Upgrades.MachineMultiplier(m.id));
        }

        /// <summary>Output per second of one machine of this type (for the upgrade menu).</summary>
        public double RatePerMachine(MachineDef m)
        {
            double r = m.baseRate * m_Upgrades.MachineMultiplier(m.id);
            return m.kind == MachineKind.Processor ? r * OutputPerInput(m) : r;
        }

        /// <summary>Advances production by <paramref name="seconds"/>; returns what was gained.</summary>
        public Dictionary<string, double> Advance(double seconds)
        {
            var gained = new Dictionary<string, double>();
            seconds = NumberUtil.SafePositive(seconds);
            if (seconds <= 0) return gained;
            foreach (var m in m_Ordered)
            {
                double cap = Capacity(m) * seconds;
                if (cap <= 0 || m.output == null) continue;
                if (m.kind == MachineKind.Producer)
                {
                    m_State.Add(m.output.id, cap);
                    Accumulate(gained, m.output.id, cap);
                }
                else if (m.input != null)
                {
                    double available = m_State.Get(m.input.id);
                    double used = Math.Min(cap, available);
                    if (used <= 0) continue;
                    double made = used * OutputPerInput(m);
                    m_State.Add(m.input.id, -used);
                    m_State.Add(m.output.id, made);
                    Accumulate(gained, m.input.id, -used);
                    Accumulate(gained, m.output.id, made);
                }
            }
            return gained;
        }

        /// <summary>Gross output per second of a resource from producers (the HUD "production" figure).</summary>
        public double GrossRate(string resourceId)
        {
            double r = 0;
            foreach (var m in m_Ordered)
                if (m.output != null && m.output.id == resourceId)
                    r += m.kind == MachineKind.Producer ? Capacity(m) : Capacity(m) * OutputPerInput(m);
            return r;
        }

        /// <summary>Expected net change per second, accounting for processors starving when stock runs out.</summary>
        public double NetRate(string resourceId)
        {
            var rates = new Dictionary<string, double>();
            foreach (var m in m_Ordered)
            {
                double cap = Capacity(m);
                if (cap <= 0 || m.output == null) continue;
                if (m.kind == MachineKind.Producer) Accumulate(rates, m.output.id, cap);
                else if (m.input != null)
                {
                    double inflow = rates.TryGetValue(m.input.id, out var v) ? v : 0;
                    double used = m_State.Get(m.input.id) > 1 ? cap : Math.Min(cap, Math.Max(0, inflow));
                    Accumulate(rates, m.input.id, -used);
                    Accumulate(rates, m.output.id, used * OutputPerInput(m));
                }
            }
            return rates.TryGetValue(resourceId, out var r) ? r : 0;
        }

        /// <summary>Output per unit of input for a processor, including yield upgrades.</summary>
        public double OutputPerInput(MachineDef m) => (1 + m_Upgrades.ProcessorYield(m.id)) / Math.Max(1e-9, m.inputPerOutput);

        public bool ProducesAnything
        {
            get
            {
                foreach (var m in m_Ordered) if (m_Upgrades.MachineCount(m.id) > 0) return true;
                return false;
            }
        }

        static void Accumulate(Dictionary<string, double> d, string id, double v) =>
            d[id] = NumberUtil.Safe((d.TryGetValue(id, out var x) ? x : 0) + v);
    }
}
