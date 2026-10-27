using System;
using System.Collections.Generic;

namespace ChoppingTrees
{
    /// <summary>Sale prices and selling. Works for any resource in the database, from the yard
    /// stock, the backpack or both.</summary>
    public class Market
    {
        public struct Sale
        {
            public string resourceId;
            public double amount;
            public double money;
        }

        public class Receipt
        {
            public readonly List<Sale> lines = new List<Sale>();
            public double total;
            public bool Empty => lines.Count == 0;
        }

        readonly GameState m_State;
        readonly GameDatabase m_Db;
        readonly Upgrades m_Upgrades;

        public Market(GameState state, GameDatabase db, Upgrades upgrades)
        {
            m_State = state;
            m_Db = db;
            m_Upgrades = upgrades;
        }

        public double Price(ResourceDef r) => r == null ? 0 : NumberUtil.SafePositive(r.basePrice * m_Upgrades.PriceMultiplier(r.id) * m_Upgrades.SellMultiplier * RebirthMultiplier);

        /// <summary>Earnings multiplier from rebirths (applies to every sale).</summary>
        public double RebirthMultiplier => Rebirth.Multiplier(m_Db.config, m_State.Rebirths);

        /// <summary>What the chosen stock would sell for right now.</summary>
        public double StockValue(bool backpack = true, bool yard = true)
        {
            double v = 0;
            foreach (var r in m_Db.resources)
            {
                if (r == null) continue;
                if (yard) v += Math.Floor(m_State.Get(r.id)) * Price(r);
                if (backpack) v += Math.Floor(m_State.Carried(r.id)) * Price(r);
            }
            return NumberUtil.SafePositive(v);
        }

        /// <summary>Sells all whole units of every resource from the chosen stock; fractions stay.
        /// Each resource gets one receipt line, whichever stock it came from.</summary>
        public Receipt SellAll(bool backpack = true, bool yard = true)
        {
            var receipt = new Receipt();
            foreach (var r in m_Db.resources)
            {
                if (r == null) continue;
                double fromYard = yard ? Math.Floor(m_State.Get(r.id)) : 0;
                double fromPack = backpack ? Math.Floor(m_State.Carried(r.id)) : 0;
                double whole = fromYard + fromPack;
                if (whole < 1) continue;
                double money = whole * Price(r);
                if (fromYard > 0) m_State.Add(r.id, -fromYard);
                if (fromPack > 0) m_State.AddCarried(r.id, -fromPack);
                receipt.lines.Add(new Sale { resourceId = r.id, amount = whole, money = money });
                receipt.total = NumberUtil.Add(receipt.total, money);
            }
            if (!receipt.Empty)
            {
                m_State.Money = NumberUtil.Add(m_State.Money, receipt.total);
                m_State.TotalEarned = NumberUtil.Add(m_State.TotalEarned, receipt.total);
            }
            return receipt;
        }
    }
}
