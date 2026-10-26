using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace ChoppingTrees.Tests
{
    /// <summary>
    /// Plays the economy with a simple simulated player to check the progression pace:
    /// the player chops the best tree they can (at half efficiency for walking, collecting and
    /// selling), sells everything, and buys the cheapest available upgrade, preferring new axes,
    /// machines and forests. Prints a milestone timeline to the test log.
    /// </summary>
    public class PacingTests
    {
        const float ChopEfficiency = 0.5f;

        static double ChopWoodPerSecond(GameModel m)
        {
            var up = m.Upgrades;
            double best = 0;
            foreach (var area in m.Db.areas)
            {
                if (!up.AreaUnlocked(area)) continue;
                foreach (var t in area.trees)
                {
                    if (t.requiredAxeTier > up.AxeTier) continue;
                    double hits = System.Math.Ceiling(t.maxHealth / up.AxeDamage);
                    double seconds = hits * up.SwingSeconds + 3.0; // + walking to the next tree
                    best = System.Math.Max(best, (t.woodReward + t.cashBonus / System.Math.Max(0.01, m.Market.Price(m.Db.Resource("wood")))) / seconds);
                }
            }
            return best * ChopEfficiency;
        }

        static readonly HashSet<string> s_Priority = new HashSet<string> { "axe_tier", "chopper_count", "sawmill_count", "area_grove", "area_ridge", "chopper_tier", "courier" };

        [Test]
        public void ProgressionPaceIsReasonable()
        {
            var db = GameDatabase.Load();
            var m = new GameModel(db, new GameState { HasPlayed = true });
            var firsts = new Dictionary<string, double>();
            var log = new StringBuilder("Pacing (simulated active play):\n");
            const float dt = 1f;
            for (double t = 0; t < 8 * 3600; t += dt)
            {
                m.Collect("wood", ChopWoodPerSecond(m) * dt);
                m.Tick(dt);
                m.Sell();
                for (int guard = 0; guard < 5; guard++)
                {
                    UpgradeDef pick = null;
                    foreach (var u in db.upgrades)
                    {
                        if (m.Upgrades.Blocker(u) != null) continue;
                        if (pick == null) { pick = u; continue; }
                        bool pu = s_Priority.Contains(u.id), pp = s_Priority.Contains(pick.id);
                        if (pu && !pp || pu == pp && m.Upgrades.NextCost(u) < m.Upgrades.NextCost(pick)) pick = u;
                    }
                    if (pick == null) break;
                    // Save up for the next priority item rather than spending everything on small upgrades.
                    if (!s_Priority.Contains(pick.id))
                    {
                        double goal = double.MaxValue;
                        foreach (var u in db.upgrades)
                            if (s_Priority.Contains(u.id) && !u.IsMaxed(m.Upgrades.Level(u)) && !m.Upgrades.IsLocked(u))
                                goal = System.Math.Min(goal, m.Upgrades.NextCost(u));
                        if (m.Upgrades.NextCost(pick) > goal * 0.25) break;
                    }
                    m.TryBuy(pick, out _);
                    string key = pick.id + " " + m.Upgrades.Level(pick);
                    if (!firsts.ContainsKey(key))
                    {
                        firsts[key] = t;
                        if (s_Priority.Contains(pick.id)) log.AppendLine($"  {t / 60.0,6:0.0} min  {pick.LevelName(m.Upgrades.Level(pick)) ?? pick.displayName} (Lv {m.Upgrades.Level(pick)})");
                    }
                }
            }
            Debug.Log(log.ToString());
            System.IO.File.WriteAllText("Temp/pacing.txt", log.ToString());
            double Time(string key) => firsts.TryGetValue(key, out var v) ? v : double.MaxValue;
            Assert.Less(Time("axe_tier 1"), 4 * 60, "first axe should come within a few minutes");
            Assert.Less(Time("chopper_count 1"), 10 * 60, "first automation should come quickly");
            Assert.Less(Time("sawmill_count 1"), 60 * 60, "sawmill within the first hour");
            Assert.Less(Time("area_grove 1"), 3 * 3600, "second forest within a few hours");
            Assert.Greater(Time("area_grove 1"), 10 * 60, "second forest shouldn't be trivial");
        }
    }
}
