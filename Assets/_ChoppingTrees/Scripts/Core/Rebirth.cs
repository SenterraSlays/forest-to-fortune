using System;

namespace ChoppingTrees
{
    /// <summary>
    /// Rebirth maths. The n-th rebirth (counting from 0) costs base * growth^n, so buying several
    /// at once costs the sum of a geometric series. Each rebirth adds a fixed share to all earnings.
    /// </summary>
    public static class Rebirth
    {
        /// <summary>Earnings multiplier after <paramref name="rebirths"/> rebirths (1 + bonus * n).</summary>
        public static double Multiplier(GameConfig cfg, int rebirths) => 1 + cfg.rebirthBonus * Math.Max(0, rebirths);

        /// <summary>Total price of <paramref name="count"/> rebirths when <paramref name="done"/> are done already.</summary>
        public static double Cost(GameConfig cfg, int done, int count)
        {
            if (count <= 0) return 0;
            double g = cfg.rebirthCostGrowth, first = cfg.rebirthBaseCost * Math.Pow(g, Math.Max(0, done));
            double total = Math.Abs(g - 1) < 1e-12 ? first * count : first * (Math.Pow(g, count) - 1) / (g - 1);
            return NumberUtil.Safe(total);
        }

        /// <summary>The most rebirths <paramref name="money"/> pays for in one go.</summary>
        public static int MaxAffordable(GameConfig cfg, int done, double money)
        {
            if (money <= 0 || cfg.rebirthBaseCost <= 0) return 0;
            double g = cfg.rebirthCostGrowth, first = cfg.rebirthBaseCost * Math.Pow(g, Math.Max(0, done));
            int n = Math.Abs(g - 1) < 1e-12 ? (int)Math.Floor(money / first)
                : (int)Math.Floor(Math.Log(1 + money * (g - 1) / first) / Math.Log(g));
            n = Math.Max(0, Math.Min(n, 100000));
            // Settle floating-point edges against the exact sum.
            while (n > 0 && Cost(cfg, done, n) > money + 1e-9) n--;
            while (Cost(cfg, done, n + 1) <= money + 1e-9 && n < 100000) n++;
            return n;
        }
    }
}
