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
            Assert.AreEqual(25, axe.CostForLevel(0));
            Assert.AreEqual(300, axe.CostForLevel(1));
            var stone = m_Db.Upgrade("axe_sharpen");
            Assert.AreEqual(stone.baseCost, stone.CostForLevel(0), 1e-9);
            Assert.AreEqual(stone.baseCost * Math.Pow(stone.costGrowth, 3), stone.CostForLevel(3), 1e-6);
            var chopper = m_Db.Upgrade("chopper_count");
            Assert.AreEqual(100, chopper.CostForLevel(0));
            Assert.AreEqual(600, chopper.CostForLevel(1));
        }

        [Test]
        public void PurchaseNeedsMoneyAndRequirements()
        {
            var m = NewModel(20);
            var axe = m_Db.Upgrade("axe_tier");
            Assert.IsFalse(m.TryBuy(axe, out var why));
            StringAssert.Contains("money", why);
            Assert.AreEqual(20, m.State.Money);
            Assert.AreEqual(0, m.Upgrades.Level(axe));

            m.State.Money = 1000;
            Assert.IsTrue(m.TryBuy(axe, out _));
            Assert.AreEqual(975, m.State.Money, 1e-9);
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
        public void RebirthCostsGrowAndCanBeBoughtInBulk()
        {
            var cfg = m_Db.config;
            Assert.AreEqual(5000, cfg.rebirthBaseCost, 1e-9);
            Assert.AreEqual(1.03, cfg.rebirthCostGrowth, 1e-9);
            var m = NewModel(0);
            Assert.AreEqual(5000, m.RebirthCost(), 1e-6);
            // Several at once cost exactly the sum of the single prices.
            double sum = 0;
            for (int i = 0; i < 5; i++) sum += 5000 * Math.Pow(1.03, i);
            Assert.AreEqual(sum, m.RebirthCost(5), 1e-6);
            Assert.AreEqual(0, m.MaxRebirths());
            m.State.Money = sum;
            Assert.AreEqual(5, m.MaxRebirths(), "exactly enough for five");
            m.State.Money = sum - 0.01;
            Assert.AreEqual(4, m.MaxRebirths());
            Assert.IsFalse(m.TryRebirth(5, out var why));
            StringAssert.StartsWith("Need", why);
            Assert.AreEqual(0, m.State.Rebirths);

            m.State.Money = sum;
            Assert.IsTrue(m.TryRebirth(5, out _));
            Assert.AreEqual(5, m.State.Rebirths);
            Assert.AreEqual(5000 * Math.Pow(1.03, 5), m.RebirthCost(), 1e-6, "the next one continues the series");
        }

        [Test]
        public void RebirthResetsProgressButKeepsStatsAndBoostsEarnings()
        {
            var m = NewModel(1e9);
            Buy(m, m_Db, "axe_tier", 2);
            Buy(m, m_Db, "chopper_count", 3);
            Buy(m, m_Db, "area_grove");
            m.State.Add("wood", 500);
            m.Collect("wood", 20);
            m.State.TreesFelled = 77;
            m.State.TotalEarned = 12345;
            m.State.TutorialDone = true;
            double plainPrice = m.Market.Price(m_Db.Resource("wood"));
            double wanted = 0;
            Assert.IsTrue(m.TryRebirth(3, out _));
            Assert.AreEqual(m_Db.config.startingMoney, m.State.Money, 1e-9);
            Assert.AreEqual(0, m.Upgrades.AxeTier);
            Assert.AreEqual(0, m.Upgrades.MachineCount("chopper"));
            Assert.IsFalse(m.Upgrades.AreaUnlocked(Array.Find(m_Db.areas, a => a.id == "grove")));
            Assert.IsFalse(m.Production.ProducesAnything);
            Assert.AreEqual(wanted, m.State.Get("wood"));
            Assert.AreEqual(0, m.State.CarriedTotal);
            Assert.AreEqual(77, m.State.TreesFelled);
            Assert.AreEqual(12345, m.State.TotalEarned, 1e-9);
            Assert.IsTrue(m.State.TutorialDone);
            // +10% per rebirth on everything sold.
            Assert.AreEqual(1.3, m.Market.RebirthMultiplier, 1e-9);
            Assert.AreEqual(m_Db.Resource("wood").basePrice * 1.3, m.Market.Price(m_Db.Resource("wood")), 1e-9);
            Assert.Greater(plainPrice, 0);
            m.Collect("wood", 10);
            Assert.AreEqual(13, m.Sell().total, 1e-9);
        }

        [Test]
        public void RebirthsGiveSkillPointsForLegacyPerksThatAreKept()
        {
            var m = NewModel(1e9);
            var axe = m_Db.Upgrade("heirloom_axe");
            Assert.IsTrue(axe.legacy);
            Assert.IsFalse(m.TryBuy(axe, out var why), "Legacy perks cost skill points, not money");
            StringAssert.Contains("skill points", why);

            Assert.IsTrue(m.TryRebirth(10, out _));
            Assert.AreEqual(10 * m_Db.config.rebirthSkillPoints, m.State.SkillPoints);

            // Buying applies the head start immediately: the axe and free choppers appear now.
            Buy(m, m_Db, "heirloom_axe", 2);
            int p = m_Db.config.rebirthSkillPoints;
            Assert.AreEqual(3, p, "three skill points per rebirth");
            Assert.AreEqual(10 * p - 2 - 4, m.State.SkillPoints, "costs 2 + 4 skill points");
            Assert.AreEqual(2, m.Upgrades.AxeTier);
            Buy(m, m_Db, "starter_kit");
            Assert.AreEqual(1, m.Upgrades.MachineCount("chopper"));
            Buy(m, m_Db, "head_start");
            Assert.AreEqual(500, m.Upgrades.StartingMoney, 1e-9);
            Assert.AreEqual(10 * p - 9, m.State.SkillPoints);

            // The next rebirth keeps the perks and grants the head start again.
            m.State.Money = 1e9;
            Buy(m, m_Db, "axe_tier"); // a normal upgrade: this one resets
            Assert.IsTrue(m.TryRebirth(1, out _));
            Assert.AreEqual(2, m.Upgrades.Level(axe), "Legacy perks are kept");
            Assert.AreEqual(2, m.Upgrades.AxeTier, "the heirloom axe, not the tier bought last run");
            Assert.AreEqual(1, m.Upgrades.MachineCount("chopper"));
            Assert.AreEqual(m_Db.config.startingMoney + 500, m.State.Money, 1e-9);
            Assert.AreEqual(11 * p - 9, m.State.SkillPoints);
            Assert.AreEqual(11 * p, m.State.SkillPointsEarned);

            Buy(m, m_Db, "deep_pockets");
            Assert.AreEqual(Upgrades.CapacityForTier(m_Db.config, 0) * (1 + m_Db.Upgrade("deep_pockets").effectPerLevel), m.BackpackCapacity, 1e-6);
        }

        [Test]
        public void ForestCharterOpensForestsEveryRunAndOldSavesGetTheirPoints()
        {
            var s = new GameState { HasPlayed = true, Rebirths = 15 };
            var m = new GameModel(m_Db, s);
            Assert.AreEqual(15, m.State.SkillPoints, "rebirths from before skill points existed still pay out");
            Buy(m, m_Db, "forest_charter", 2);
            Assert.IsTrue(m.Upgrades.AreaUnlocked(Array.Find(m_Db.areas, a => a.id == "grove")));
            Assert.IsTrue(m.Upgrades.AreaUnlocked(Array.Find(m_Db.areas, a => a.id == "ridge")));
            m.State.Money = 1e9;
            Assert.IsTrue(m.TryRebirth(1, out _));
            Assert.IsTrue(m.Upgrades.AreaUnlocked(Array.Find(m_Db.areas, a => a.id == "ridge")));
            var again = new GameModel(m_Db, m.State);
            Assert.AreEqual(m.State.SkillPoints, again.State.SkillPoints, "points are only owed once");
        }

        [Test]
        public void LogMagnetAddsRangePerLevel()
        {
            Assert.IsNull(m_Db.Upgrade("courier"), "the Wood Courier was replaced by the Log Magnet");
            var magnet = m_Db.Upgrade("magnet");
            Assert.AreEqual(10, magnet.maxLevel);
            Assert.AreEqual(500, magnet.CostForLevel(0), 1e-9);
            var m = NewModel(1e9);
            float baseRange = m_Db.config.pickupMagnetRadius;
            Assert.AreEqual(baseRange, m.Upgrades.MagnetRadius, 1e-5);
            Assert.IsFalse(m.TryBuy(magnet, out _), "the magnet branches off Market Contacts");
            m.TryBuy(m_Db.Upgrade("wood_price"), out _);
            for (int i = 0; i < 10; i++) Assert.IsTrue(m.TryBuy(magnet, out _));
            Assert.AreEqual(baseRange + 10 * magnet.effectPerLevel, m.Upgrades.MagnetRadius, 1e-4);
            Assert.IsFalse(m.TryBuy(magnet, out _), "10 levels at most");
            Assert.AreEqual(0, m.Upgrades.AutoSellSeconds, "nothing sells automatically any more");
        }

        static void Buy(GameModel m, GameDatabase db, string id, int levels = 1)
        {
            for (int i = 0; i < levels; i++) Assert.IsTrue(m.TryBuy(db.Upgrade(id), out var why), $"{id}: {why}");
        }

        [Test]
        public void NewPerksChangeTheirNumbers()
        {
            var m = NewModel(1e12);
            Buy(m, m_Db, "axe_tier", 4);
            Buy(m, m_Db, "axe_sharpen");
            Buy(m, m_Db, "axe_speed");
            Buy(m, m_Db, "backpack");

            Buy(m, m_Db, "clean_cuts", 2);
            Assert.AreEqual(1 + 2 * m_Db.Upgrade("clean_cuts").effectPerLevel, m.Upgrades.WoodYieldMultiplier, 1e-5);
            Buy(m, m_Db, "trail_boots");
            Assert.AreEqual(1 + m_Db.Upgrade("trail_boots").effectPerLevel, m.Upgrades.MoveSpeedMultiplier, 1e-5);
            Buy(m, m_Db, "lucky_strike", 3);
            Assert.AreEqual(3 * m_Db.Upgrade("lucky_strike").effectPerLevel, m.Upgrades.CritChance, 1e-5);

            // Haggling raises every price on top of the per-resource buyers.
            double before = m.Market.Price(m_Db.Resource("wood"));
            Buy(m, m_Db, "wood_price", 2);
            double withContacts = m.Market.Price(m_Db.Resource("wood"));
            Buy(m, m_Db, "haggling", 2);
            Assert.AreEqual(withContacts * (1 + 2 * m_Db.Upgrade("haggling").effectPerLevel), m.Market.Price(m_Db.Resource("wood")), 1e-6);
            Assert.Greater(withContacts, before);
            Buy(m, m_Db, "lucky_finds");
            Assert.AreEqual(m_Db.Upgrade("lucky_finds").effectPerLevel, m.Upgrades.LuckyFindChance, 1e-5);

            // Spare Parts discounts machines, and only machines.
            var chopper = m_Db.Upgrade("chopper_count");
            Buy(m, m_Db, "chopper_count");
            Buy(m, m_Db, "foreman");
            double full = chopper.CostForLevel(1);
            Buy(m, m_Db, "spare_parts", 2);
            Assert.AreEqual(full * (1 - 2 * m_Db.Upgrade("spare_parts").effectPerLevel), m.Upgrades.NextCost(chopper), 1e-3);
            var stone = m_Db.Upgrade("axe_sharpen");
            Assert.AreEqual(stone.CostForLevel(m.Upgrades.Level(stone)), m.Upgrades.NextCost(stone), 1e-6);
            double money = m.State.Money;
            Buy(m, m_Db, "chopper_count");
            Assert.AreEqual(full * (1 - 2 * m_Db.Upgrade("spare_parts").effectPerLevel), money - m.State.Money, 1e-3, "the discounted price is what you pay");

            Buy(m, m_Db, "area_grove");
            Buy(m, m_Db, "fertile_soil");
            Assert.AreEqual(1f / (1f + m_Db.Upgrade("fertile_soil").effectPerLevel), m.Upgrades.RegrowthTimeMultiplier, 1e-5);
            Buy(m, m_Db, "area_ridge");
            Buy(m, m_Db, "gold_prospector");
            Assert.AreEqual(1 + m_Db.Upgrade("gold_prospector").effectPerLevel, m.Upgrades.TreeCashMultiplier, 1e-5);
        }

        [Test]
        public void EveryBranchHasPlentyOfUpgrades()
        {
            foreach (UpgradeCategory c in new[] { UpgradeCategory.Axe, UpgradeCategory.Automation, UpgradeCategory.Economy, UpgradeCategory.Forest })
            {
                int n = System.Array.FindAll(m_Db.upgrades, u => !u.legacy && u.category == c).Length;
                Assert.GreaterOrEqual(n, 11, $"{c} has {n} upgrades");
            }
        }

        [Test]
        public void LateGamePerksChangeTheirNumbers()
        {
            var m = NewModel(1e15);
            Assert.AreEqual(2f, m.Upgrades.CritMultiplier, 1e-6);
            Buy(m, m_Db, "axe_tier");
            Buy(m, m_Db, "axe_sharpen", 5);
            Buy(m, m_Db, "axe_speed", 3);
            Buy(m, m_Db, "lucky_strike", 5);
            Buy(m, m_Db, "lucky_blade");
            Buy(m, m_Db, "power_swing", 2);
            Assert.AreEqual(2f + 2 * m_Db.Upgrade("power_swing").effectPerLevel, m.Upgrades.CritMultiplier, 1e-5);
            Buy(m, m_Db, "steady_rhythm");
            Buy(m, m_Db, "cleave", 3);
            Assert.AreEqual(3 * m_Db.Upgrade("cleave").effectPerLevel, m.Upgrades.FellChance, 1e-5);

            // Bulk Buyer: every upgrade but machines and Legacy perks gets cheaper.
            Buy(m, m_Db, "wood_price", 2);
            Buy(m, m_Db, "haggling");
            Buy(m, m_Db, "lucky_finds", 3);
            Buy(m, m_Db, "fortunes_favor");
            var honed = m_Db.Upgrade("honed_edge");
            var chopper = m_Db.Upgrade("chopper_count");
            Buy(m, m_Db, "bulk_buyer", 5);
            double off = 5 * m_Db.Upgrade("bulk_buyer").effectPerLevel;
            Assert.AreEqual(honed.CostForLevel(0) * (1 - off), m.Upgrades.NextCost(honed), 1e-3);
            Assert.AreEqual(chopper.CostForLevel(0), m.Upgrades.NextCost(chopper), 1e-3, "machines use Spare Parts instead");
            var legacy = m_Db.Upgrade("titan_grip");
            Assert.AreEqual(legacy.CostForLevel(0), m.Upgrades.NextCost(legacy), 1e-6, "skill point prices never change");
        }

        [Test]
        public void PrecisionSawsGetMorePlanksFromTheSameWood()
        {
            var m = NewModel(1e12);
            Buy(m, m_Db, "chopper_count");
            Buy(m, m_Db, "sawmill_count");
            Buy(m, m_Db, "sawmill_speed");
            m.State.Set("wood", 1e6);
            m.Production.Advance(10);
            double plain = m.State.Get("planks");
            m.State.Set("planks", 0);
            Buy(m, m_Db, "precision_saws", 2);
            m.Production.Advance(10);
            Assert.AreEqual(plain * (1 + 2 * m_Db.Upgrade("precision_saws").effectPerLevel), m.State.Get("planks"), 1e-6);
            Assert.AreEqual(m.Production.GrossRate("planks") * 10, m.State.Get("planks"), 1e-6, "rates match what is produced");
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
            var seenLegacy = new System.Collections.Generic.HashSet<Vector2>();
            foreach (var u in m_Db.upgrades)
            {
                Assert.IsFalse(string.IsNullOrEmpty(u.icon) || u.icon == "dot", $"{u.id} has no icon");
                if (u.legacy)
                {
                    // The Legacy tree is its own screen.
                    Assert.IsTrue(seenLegacy.Add(u.treePosition), $"{u.id} shares a Legacy tree position with another perk");
                    Assert.That(u.treePosition.x, Is.InRange(-7.5f, 7.5f), u.id);
                    Assert.That(u.treePosition.y, Is.InRange(1f, 9.5f), u.id);
                    Assert.AreNotEqual(UpgradeCategory.Legacy, u.category, $"{u.id} needs a Legacy tree branch");
                    continue;
                }
                Assert.IsTrue(seen.Add(u.treePosition), $"{u.id} shares a tree position with another node");
                Assert.That(u.treePosition.x, Is.InRange(-11.5f, 14f), u.id);
                Assert.That(u.treePosition.y, Is.InRange(0f, 13.5f), u.id);
            }
            // Roots (no prerequisites) are the trunk's three branches.
            var roots = System.Array.FindAll(m_Db.upgrades, u => !u.legacy && (u.requires == null || u.requires.Length == 0));
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
