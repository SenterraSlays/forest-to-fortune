using System;
using System.Collections.Generic;

namespace ChoppingTrees
{
    /// <summary>
    /// All game rules in one place, with no scene dependencies: state, upgrades, market and
    /// production, plus the events the scene listens to. <see cref="Game"/> owns one instance.
    /// </summary>
    public class GameModel
    {
        public const string Wood = "wood";

        public readonly GameDatabase Db;
        public readonly GameState State;
        public readonly Upgrades Upgrades;
        public readonly Market Market;
        public readonly Production Production;

        /// <summary>An upgrade level was bought.</summary>
        public event Action<UpgradeDef> Purchased;
        /// <summary>Resources were sold (bool = automatic sale).</summary>
        public event Action<Market.Receipt, bool> Sold;
        /// <summary>The player collected resources by hand (id, amount).</summary>
        public event Action<string, double> Collected;
        /// <summary>A tree was felled by the player.</summary>
        public event Action<TreeType> TreeFelled;

        float m_AutoSellTimer;

        public GameModel(GameDatabase db, GameState state)
        {
            Db = db;
            State = state ?? new GameState();
            if (State.Money <= 0 && !State.HasPlayed) State.Money = db.config.startingMoney;
            Upgrades = new Upgrades(State, db);
            Market = new Market(State, db, Upgrades);
            Production = new Production(State, db, Upgrades);
        }

        /// <summary>Real-time update: production, play time and automatic selling.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0) return;
            Production.Advance(dt);
            State.PlaySeconds += dt;
            float interval = Upgrades.AutoSellSeconds;
            if (interval > 0)
            {
                m_AutoSellTimer += dt;
                if (m_AutoSellTimer >= interval)
                {
                    m_AutoSellTimer = 0;
                    Sell(true);
                }
            }
            else m_AutoSellTimer = 0;
        }

        public float AutoSellProgress => Upgrades.AutoSellSeconds > 0 ? m_AutoSellTimer / Upgrades.AutoSellSeconds : 0;

        public void Collect(string resourceId, double amount)
        {
            amount = NumberUtil.SafePositive(amount);
            if (amount <= 0) return;
            State.Add(resourceId, amount);
            if (resourceId == Wood) State.TotalWoodChopped = NumberUtil.Add(State.TotalWoodChopped, amount);
            Collected?.Invoke(resourceId, amount);
        }

        public void AddMoney(double amount)
        {
            amount = NumberUtil.SafePositive(amount);
            State.Money = NumberUtil.Add(State.Money, amount);
            State.TotalEarned = NumberUtil.Add(State.TotalEarned, amount);
        }

        public void NotifyTreeFelled(TreeType t)
        {
            State.TreesFelled++;
            TreeFelled?.Invoke(t);
        }

        public bool TryBuy(UpgradeDef u, out string failure)
        {
            if (!Upgrades.TryBuy(u, out failure)) return false;
            Purchased?.Invoke(u);
            return true;
        }

        public Market.Receipt Sell(bool automatic = false)
        {
            var r = Market.SellAll();
            if (!r.Empty) Sold?.Invoke(r, automatic);
            return r;
        }

        public class OfflineReport
        {
            public double awaySeconds;
            public double creditedSeconds;
            public Dictionary<string, double> gained = new Dictionary<string, double>();
            public bool Capped => creditedSeconds + 0.5 < awaySeconds;
        }

        /// <summary>
        /// Grants machine output for the time since the last save, capped by the offline limit.
        /// Clock changes that move time backwards give nothing.
        /// </summary>
        public OfflineReport ApplyOffline(DateTime nowUtc)
        {
            var report = new OfflineReport();
            double away = (nowUtc - State.LastSavedUtc).TotalSeconds;
            if (double.IsNaN(away) || away <= 0) return report;
            report.awaySeconds = away;
            double cap = Upgrades.OfflineCapHours * 3600.0;
            report.creditedSeconds = Math.Min(away, cap);
            if (!Production.ProducesAnything) return report;
            report.gained = Production.Advance(report.creditedSeconds * Db.config.offlineEfficiency);
            return report;
        }
    }
}
