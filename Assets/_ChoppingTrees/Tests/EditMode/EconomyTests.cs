using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ChoppingTrees.Tests
{
    /// <summary>Rules of the economy: costs, purchases, selling, production and offline progress.</summary>
    public class EconomyTests
    {
        GameDatabase m_Db;

        [SetUp]
        public void SetUp()
        {
            m_Db = GameDatabase.Load();
            Assert.IsNotNull(m_Db, "GameDatabase missing from Resources (run Tools > Chopping Trees > 5. Build Game Data)");
        }

        GameModel NewModel(double money = 0)
        {
            var s = new GameState { Money = money, HasPlayed = true };
            return new GameModel(m_Db, s);
        }

        [Test]
        public void NumberFormatting()
        {
            Assert.AreEqual("0", NumberUtil.Format(0));
            Assert.AreEqual("999", NumberUtil.Format(999.9));
            Assert.AreEqual("1,234", NumberUtil.Format(1234));
            Assert.AreEqual("99,999", NumberUtil.Format(99999));
            Assert.AreEqual("100.0K", NumberUtil.Format(100000));
            Assert.AreEqual("999.9K", NumberUtil.Format(999999));
            Assert.AreEqual("1.50M", NumberUtil.Format(1.5e6));
            Assert.AreEqual("$2.50", NumberUtil.Money(2.5));
            Assert.AreEqual("0", NumberUtil.Format(double.NaN));
            StringAssert.Contains("e", NumberUtil.Format(1e60));
            Assert.AreEqual("+0.60/s", NumberUtil.Rate(0.6));
        }

        [Test]
        public void SafeArithmeticNeverProducesInvalidValues()
        {
            Assert.AreEqual(0, NumberUtil.Safe(double.NaN));
            Assert.AreEqual(NumberUtil.Max, NumberUtil.Safe(double.PositiveInfinity));
            Assert.AreEqual(NumberUtil.Max, NumberUtil.Add(NumberUtil.Max, NumberUtil.Max));
            var s = new GameState();
            s.Set("wood", -5);
            Assert.AreEqual(0, s.Get("wood"));
            s.Add("wood", double.PositiveInfinity);
            Assert.IsFalse(double.IsInfinity(s.Get("wood")));
        }

        [Test]
        public void CostsFollowExplicitPricesThenFormula()
        {
            var axe = m_Db.Upgrade("axe_tier");
            Assert.AreEqual(50, axe.CostForLevel(0));
            Assert.AreEqual(600, axe.CostForLevel(1));
            var stone = m_Db.Upgrade("axe_sharpen");
            Assert.AreEqual(stone.baseCost, stone.CostForLevel(0), 1e-9);
            Assert.AreEqual(stone.baseCost * Math.Pow(stone.costGrowth, 3), stone.CostForLevel(3), 1e-6);
            var chopper = m_Db.Upgrade("chopper_count");
            Assert.AreEqual(200, chopper.CostForLevel(0));
            Assert.AreEqual(1200, chopper.CostForLevel(1));
        }

        [Test]
        public void PurchaseNeedsMoneyAndRequirements()
        {
            var m = NewModel(40);
            var axe = m_Db.Upgrade("axe_tier");
            Assert.IsFalse(m.TryBuy(axe, out var why));
            StringAssert.Contains("money", why);
            Assert.AreEqual(40, m.State.Money);
            Assert.AreEqual(0, m.Upgrades.Level(axe));

            m.State.Money = 1000;
            Assert.IsTrue(m.TryBuy(axe, out _));
            Assert.AreEqual(950, m.State.Money, 1e-9);
            Assert.AreEqual(1, m.Upgrades.AxeTier);
            Assert.AreEqual(m_Db.config.axeTierDamage[1], m.Upgrades.AxeDamage, 1e-5);

            var motors = m_Db.Upgrade("chopper_speed");
            Assert.IsFalse(m.TryBuy(motors, out why), "motors need an Auto-Chopper first");
            StringAssert.StartsWith("Requires", why);
        }

        [Test]
        public void MaxedUpgradesCannotBeBought()
        {
            var m = NewModel(1e12);
            var axe = m_Db.Upgrade("axe_tier");
            for (int i = 0; i < axe.maxLevel; i++) Assert.IsTrue(m.TryBuy(axe, out _));
            Assert.IsFalse(m.TryBuy(axe, out var why));
            Assert.AreEqual("Maxed", why);
            Assert.AreEqual(axe.maxLevel, m.Upgrades.Level(axe));
        }

        [Test]
        public void SellingSellsWholeUnitsAndAppliesPriceUpgrades()
        {
            var m = NewModel(0);
            m.Collect("wood", 10.6);
            var r = m.Sell();
            Assert.AreEqual(10, r.total, 1e-9);
            Assert.AreEqual(0.6, m.State.Carried("wood"), 1e-9);
            m.State.Money = 1000;
            Assert.IsTrue(m.TryBuy(m_Db.Upgrade("wood_price"), out _));
            m.Collect("wood", 10);
            double before = m.State.Money;
            m.Sell();
            Assert.AreEqual(10 * (1 + m_Db.Upgrade("wood_price").effectPerLevel), m.State.Money - before, 1e-6);
        }

        [Test]
        public void MachinesProduceExactlyWhatTheyPromise()
        {
            var m = NewModel(1e9);
            Assert.IsFalse(m.Production.ProducesAnything);
            m.TryBuy(m_Db.Upgrade("chopper_count"), out _);
            m.TryBuy(m_Db.Upgrade("chopper_count"), out _);
            var chopper = m_Db.Machine("chopper");
            Assert.AreEqual(2 * chopper.baseRate, m.Production.GrossRate("wood"), 1e-9);
            m.Production.Advance(10);
            Assert.AreEqual(2 * chopper.baseRate * 10, m.State.Get("wood"), 1e-9);
        }

        [Test]
        public void ProductionIsFrameRateIndependent()
        {
            var a = NewModel(1e9);
            var b = NewModel(1e9);
            foreach (var model in new[] { a, b })
            {
                model.TryBuy(m_Db.Upgrade("chopper_count"), out _);
                model.TryBuy(m_Db.Upgrade("chopper_count"), out _);
                model.TryBuy(m_Db.Upgrade("chopper_count"), out _);
                model.TryBuy(m_Db.Upgrade("sawmill_count"), out _);
                model.State.Add("wood", 5);
            }
            a.Production.Advance(20);
            for (int i = 0; i < 2000; i++) b.Production.Advance(0.01);
            Assert.AreEqual(a.State.Get("planks"), b.State.Get("planks"), 1e-6);
            Assert.AreEqual(a.State.Get("wood"), b.State.Get("wood"), 1e-6);
        }

        [Test]
        public void SawmillNeverUsesWoodItDoesNotHave()
        {
            var m = NewModel(1e9);
            m.TryBuy(m_Db.Upgrade("chopper_count"), out _);
            m.TryBuy(m_Db.Upgrade("sawmill_count"), out _);
            m.Production.Advance(100);
            Assert.GreaterOrEqual(m.State.Get("wood"), 0);
            // The sawmill can only turn the chopper's supply into planks.
            double supply = m_Db.Machine("chopper").baseRate;
            Assert.AreEqual(100 * supply / m_Db.Machine("sawmill").inputPerOutput, m.State.Get("planks"), 1e-6);
        }

        [Test]
        public void OfflineProgressIsCappedAndIgnoresClockGoingBackwards()
        {
            var m = NewModel(1e9);
            m.TryBuy(m_Db.Upgrade("chopper_count"), out _);
            m.State.Money = 0;
            var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
            m.State.LastSavedUtc = now.AddHours(-5);
            var r = m.ApplyOffline(now);
            Assert.AreEqual(5 * 3600, r.awaySeconds, 1e-6);
            Assert.AreEqual(m_Db.config.baseOfflineHours * 3600, r.creditedSeconds, 1e-6);
            Assert.IsTrue(r.Capped);
            Assert.AreEqual(m_Db.Machine("chopper").baseRate * r.creditedSeconds * m_Db.config.offlineEfficiency, m.State.Get("wood"), 1e-6);

            var m2 = NewModel(1e9);
            m2.TryBuy(m_Db.Upgrade("chopper_count"), out _);
            m2.State.LastSavedUtc = now.AddHours(3);
            var r2 = m2.ApplyOffline(now);
            Assert.AreEqual(0, r2.creditedSeconds);
            Assert.AreEqual(0, m2.State.Get("wood"));
        }

        [Test]
        public void AreasUnlockThroughTheirUpgrade()
        {
            var m = NewModel(1e9);
            var grove = Array.Find(m_Db.areas, a => a.id == "grove");
            Assert.IsFalse(m.Upgrades.AreaUnlocked(grove));
            var up = m_Db.AreaUpgradeFor("grove");
            Assert.IsFalse(m.TryBuy(up, out _), "needs the Lumberjack's Axe first");
            var axe = m_Db.Upgrade("axe_tier");
            m.TryBuy(axe, out _);
            m.TryBuy(axe, out _);
            Assert.IsTrue(m.TryBuy(up, out _));
            Assert.IsTrue(m.Upgrades.AreaUnlocked(grove));
        }

        [Test]
        public void AutoSellRunsOnItsInterval()
        {
            var m = NewModel(1e9);
            m.TryBuy(m_Db.Upgrade("chopper_count"), out _);
            m.TryBuy(m_Db.Upgrade("chopper_count"), out _);
            Assert.IsFalse(m.TryBuy(m_Db.Upgrade("courier"), out _), "the courier branches off Market Contacts");
            m.TryBuy(m_Db.Upgrade("wood_price"), out _);
            Assert.IsTrue(m.TryBuy(m_Db.Upgrade("courier"), out _));
            m.State.Money = 0;
            m.State.Add("wood", 50);
            m.Collect("wood", 20);
            float interval = m.Upgrades.AutoSellSeconds;
            Assert.Greater(interval, 0f);
            m.Tick(interval * 0.5f);
            Assert.AreEqual(0, m.State.Money);
            m.Tick(interval * 0.6f);
            Assert.Greater(m.State.Money, 0);
            Assert.Less(m.State.Get("wood"), 1, "the courier sells the yard stock");
            Assert.AreEqual(20, m.State.Carried("wood"), 1e-9, "but never the backpack");
        }

        [Test]
        public void BackpackLimitsWhatThePlayerCarries()
        {
            var m = NewModel(0);
            double cap = m_Db.config.backpackCapacity[0];
            Assert.AreEqual(cap, m.BackpackCapacity, 1e-9);
            int filled = 0;
            m.BackpackFilled += () => filled++;

            Assert.AreEqual(cap, m.Collect("wood", cap + 25), 1e-9, "only what fits is taken");
            Assert.IsTrue(m.BackpackFull);
            Assert.AreEqual(0, m.Collect("wood", 5), "a full backpack takes nothing");
            Assert.AreEqual(1, filled, "the full event fires once per fill");
            Assert.AreEqual(0, m.State.Get("wood"), "hand-collected wood never goes to the yard");
            Assert.AreEqual(cap, m.State.TotalWoodChopped, 1e-9);

            var r = m.Sell();
            Assert.AreEqual(cap * m.Market.Price(m_Db.Resource("wood")), r.total, 1e-6);
            Assert.AreEqual(0, m.State.CarriedTotal, 1e-9);
            Assert.IsFalse(m.BackpackFull);

            m.State.Money = 1e9;
            var pack = m_Db.Upgrade("backpack");
            Assert.IsFalse(m.TryBuy(pack, out var why), "the backpack branches off the Steel Axe");
            StringAssert.StartsWith("Requires", why);
            m.TryBuy(m_Db.Upgrade("axe_tier"), out _);
            Assert.IsTrue(m.TryBuy(pack, out _));
            Assert.AreEqual(m_Db.config.backpackCapacity[1], m.BackpackCapacity, 1e-9);
            Assert.AreEqual(pack.LevelName(1), m.Upgrades.BackpackName);
            for (int i = 1; i < pack.maxLevel; i++) Assert.IsTrue(m.TryBuy(pack, out _));
            Assert.AreEqual(m_Db.config.backpackCapacity[pack.maxLevel], m.BackpackCapacity, 1e-9, "every level has a capacity");
        }

        [Test]
        public void MachinesFillTheYardNotTheBackpack()
        {
            var m = NewModel(1e9);
            m.TryBuy(m_Db.Upgrade("chopper_count"), out _);
            m.Collect("wood", 1e6);
            Assert.IsTrue(m.BackpackFull);
            m.Production.Advance(10);
            Assert.AreEqual(m_Db.Machine("chopper").baseRate * 10, m.State.Get("wood"), 1e-9, "a full backpack doesn't stop the machines");
            Assert.AreEqual(m.BackpackCapacity, m.State.CarriedTotal, 1e-9);
            // Selling by hand empties both.
            m.Sell();
            Assert.Less(m.State.Get("wood"), 1);
            Assert.Less(m.State.CarriedTotal, 1);
        }

        [Test]
        public void SkillTreeIsConnectedAndLaidOut()
        {
            var seen = new System.Collections.Generic.HashSet<Vector2>();
            foreach (var u in m_Db.upgrades)
            {
                Assert.IsFalse(string.IsNullOrEmpty(u.icon) || u.icon == "dot", $"{u.id} has no icon");
                Assert.IsTrue(seen.Add(u.treePosition), $"{u.id} shares a tree position with another node");
                Assert.That(u.treePosition.x, Is.InRange(-4.4f, 4.4f), u.id);
                Assert.That(u.treePosition.y, Is.InRange(0f, 5.2f), u.id);
            }
            // Roots (no prerequisites) are the trunk's three branches.
            var roots = System.Array.FindAll(m_Db.upgrades, u => u.requires == null || u.requires.Length == 0);
            CollectionAssert.AreEquivalent(new[] { "axe_tier", "chopper_count", "wood_price" }, System.Array.ConvertAll(roots, u => u.id));
        }

        [Test]
        public void EveryUpgradeIsWellFormed()
        {
            foreach (var u in m_Db.upgrades)
            {
                Assert.IsNotNull(u, "null upgrade in database");
                Assert.Greater(u.maxLevel, 0, u.id);
                for (int lv = 0; lv < u.maxLevel; lv++) Assert.Greater(u.CostForLevel(lv), 0, $"{u.id} level {lv}");
                if (u.requires != null)
                    foreach (var r in u.requires) Assert.IsNotNull(m_Db.Upgrade(r.upgradeId), $"{u.id} requires unknown {r.upgradeId}");
                if (u.effect == UpgradeEffect.MachineCount || u.effect == UpgradeEffect.MachineSpeed || u.effect == UpgradeEffect.MachineTier)
                    Assert.IsNotNull(m_Db.Machine(u.targetId), $"{u.id} targets unknown machine {u.targetId}");
                if (u.effect == UpgradeEffect.ResourcePrice) Assert.IsNotNull(m_Db.Resource(u.targetId), u.id);
            }
            foreach (var t in m_Db.trees) Assert.LessOrEqual(t.requiredAxeTier, m_Db.config.axeTierDamage.Length - 1, t.id);
        }
    }
}
