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
            Assert.AreEqual(0.6, m.State.Get("wood"), 1e-9);
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
                model.Collect("wood", 5);
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
            Assert.IsTrue(m.TryBuy(m_Db.Upgrade("courier"), out _));
            m.State.Money = 0;
            m.Collect("wood", 50);
            float interval = m.Upgrades.AutoSellSeconds;
            Assert.Greater(interval, 0f);
            m.Tick(interval * 0.5f);
            Assert.AreEqual(0, m.State.Money);
            m.Tick(interval * 0.6f);
            Assert.Greater(m.State.Money, 0);
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
