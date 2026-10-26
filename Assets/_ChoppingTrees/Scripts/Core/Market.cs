using System;
using System.Collections.Generic;

namespace ChoppingTrees
{
    /// <summary>Sale prices and selling. Works for any resource in the database.</summary>
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

        public double Price(ResourceDef r) => r == null ? 0 : NumberUtil.SafePositive(r.basePrice * m_Upgrades.PriceMultiplier(r.id));

        /// <summary>What everything in stock would sell for right now.</summary>
        public double StockValue()
        {
            double v = 0;
            foreach (var r in m_Db.resources)
                if (r != null) v += Math.Floor(m_State.Get(r.id)) * Price(r);
            return NumberUtil.SafePositive(v);
        }

        /// <summary>Sells all whole units of every resource; fractions stay in stock.</summary>
        public Receipt SellAll()
        {
            var receipt = new Receipt();
            foreach (var r in m_Db.resources)
            {
                if (r == null) continue;
                double whole = Math.Floor(m_State.Get(r.id));
                if (whole < 1) continue;
                double money = whole * Price(r);
                m_State.Add(r.id, -whole);
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
