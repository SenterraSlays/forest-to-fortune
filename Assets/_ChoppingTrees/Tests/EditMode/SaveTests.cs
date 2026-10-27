using System;
using System.IO;
using NUnit.Framework;

namespace ChoppingTrees.Tests
{
    /// <summary>Save files: round trips, backups, damaged and missing files.</summary>
    public class SaveTests
    {
        string m_OldDir, m_Dir;

        [SetUp]
        public void SetUp()
        {
            m_OldDir = SaveSystem.Directory;
            m_Dir = Path.Combine(Path.GetTempPath(), "ct_save_tests_" + Guid.NewGuid().ToString("N"));
            SaveSystem.Directory = m_Dir;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Directory = m_OldDir;
            SaveSystem.CurrentSlot = 0;
            if (Directory.Exists(m_Dir)) Directory.Delete(m_Dir, true);
        }

        static GameState Sample()
        {
            var s = new GameState { Money = 1234.5, TreesFelled = 42, TutorialStep = 3, HasPlayed = true, PlaySeconds = 99 };
            s.Set("wood", 77.25);
            s.Set("planks", 3);
            s.SetCarried("wood", 12.5);
            s.Rebirths = 4;
            s.SkillPoints = 3;
            s.SkillPointsEarned = 4;
            s.SetLevel("axe_tier", 2);
            s.SetLevel("chopper_count", 3);
            return s;
        }

        [Test]
        public void RoundTripKeepsProgress()
        {
            Assert.IsTrue(SaveSystem.Save(Sample()));
            var loaded = SaveSystem.Load(out var problem);
            Assert.IsNull(problem);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1234.5, loaded.Money, 1e-9);
            Assert.AreEqual(77.25, loaded.Get("wood"), 1e-9);
            Assert.AreEqual(3, loaded.Get("planks"), 1e-9);
            Assert.AreEqual(12.5, loaded.Carried("wood"), 1e-9);
            Assert.AreEqual(4, loaded.Rebirths);
            Assert.AreEqual(3, loaded.SkillPoints);
            Assert.AreEqual(4, loaded.SkillPointsEarned);
            Assert.AreEqual(2, loaded.Level("axe_tier"));
            Assert.AreEqual(3, loaded.Level("chopper_count"));
            Assert.AreEqual(42, loaded.TreesFelled);
            Assert.AreEqual(3, loaded.TutorialStep);
            Assert.IsTrue(loaded.HasPlayed);
            Assert.Less((DateTime.UtcNow - loaded.LastSavedUtc).TotalSeconds, 60);
        }

        [Test]
        public void SlotsAreSeparate()
        {
            SaveSystem.CurrentSlot = 1;
            var b = Sample();
            b.Money = 77;
            Assert.IsTrue(SaveSystem.Save(b));
            Assert.IsTrue(SaveSystem.SlotHasSave(1));
            Assert.IsFalse(SaveSystem.SlotHasSave(0));
            Assert.IsFalse(SaveSystem.SlotHasSave(2));
            StringAssert.EndsWith("progress_B.json", SaveSystem.SavePath);
            Assert.AreEqual(77, SaveSystem.Peek(1).money, 1e-9, "the save screen can read a slot without loading it");
            Assert.IsNull(SaveSystem.Peek(2));

            SaveSystem.CurrentSlot = 0;
            Assert.IsNull(SaveSystem.Load(out _), "slot A is still empty");
            SaveSystem.Delete(1);
            Assert.IsFalse(SaveSystem.SlotHasSave(1));
        }

        [Test]
        public void OldSingleSaveBecomesSlotA()
        {
            Directory.CreateDirectory(m_Dir);
            File.WriteAllText(Path.Combine(m_Dir, SaveSystem.LegacySaveFile), "{\"version\":2,\"money\":321,\"hasPlayed\":true}");
            SaveSystem.CurrentSlot = 0;
            var s = SaveSystem.Load(out var problem);
            Assert.IsNull(problem);
            Assert.AreEqual(321, s.Money, 1e-9);
            Assert.IsTrue(File.Exists(SaveSystem.SlotPath(0)));
            Assert.IsFalse(File.Exists(Path.Combine(m_Dir, SaveSystem.LegacySaveFile)));
        }

        [Test]
        public void MissingSaveLoadsNothing()
        {
            Assert.IsFalse(SaveSystem.HasSave);
            Assert.IsNull(SaveSystem.Load(out var problem));
            Assert.IsNull(problem);
        }

        [Test]
        public void DamagedSaveFallsBackToBackup()
        {
            var first = Sample();
            SaveSystem.Save(first);
            var second = Sample();
            second.Money = 9999;
            SaveSystem.Save(second); // first becomes the .bak
            File.WriteAllText(SaveSystem.SavePath, "{ this is not json");
            var loaded = SaveSystem.Load(out var problem);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1234.5, loaded.Money, 1e-9);
            StringAssert.Contains("backup", problem);
        }

        [Test]
        public void GarbageWithoutBackupIsReportedNotThrown()
        {
            Directory.CreateDirectory(m_Dir);
            File.WriteAllText(SaveSystem.SavePath, "\0\0garbage");
            GameState loaded = null;
            Assert.DoesNotThrow(() => loaded = SaveSystem.Load(out _));
            Assert.IsNull(loaded);
        }

        [Test]
        public void InvalidNumbersAreSanitisedOnLoad()
        {
            Directory.CreateDirectory(m_Dir);
            File.WriteAllText(SaveSystem.SavePath, "{\"version\":1,\"money\":-50,\"resources\":[{\"id\":\"wood\",\"value\":-3}],\"upgrades\":[{\"id\":\"axe_tier\",\"level\":-2}],\"treesFelled\":-1}");
            var s = SaveSystem.Load(out _);
            Assert.IsNotNull(s);
            Assert.AreEqual(0, s.Money);
            Assert.AreEqual(0, s.Get("wood"));
            Assert.AreEqual(0, s.Level("axe_tier"));
            Assert.AreEqual(0, s.TreesFelled);
        }

        [Test]
        public void OlderVersionsAreMigrated()
        {
            var d = new SaveData { version = 0, money = 5, resources = null, upgrades = null };
            SaveSystem.Migrate(d);
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
            Assert.IsNotNull(d.resources);
            Assert.IsNotNull(d.upgrades);
            Assert.IsNotNull(d.backpack);
        }

        [Test]
        public void VersionOneStockStaysInTheYard()
        {
            // 1.1 saves had one stock and no backpack; it all stays sellable in the yard.
            Directory.CreateDirectory(m_Dir);
            File.WriteAllText(SaveSystem.SavePath, "{\"version\":1,\"money\":10,\"resources\":[{\"id\":\"wood\",\"value\":500}],\"upgrades\":[]}");
            var s = SaveSystem.Load(out var problem);
            Assert.IsNull(problem);
            Assert.AreEqual(500, s.Get("wood"), 1e-9);
            Assert.AreEqual(0, s.CarriedTotal, 1e-9);
        }

        [Test]
        public void OldSettingsGoBackToNativeResolution()
        {
            // A 1.1 settings file has no version field and may hold a resolution capped at 1080p.
            var old = UnityEngine.JsonUtility.FromJson<SettingsData>("{\"resolutionWidth\":1920,\"resolutionHeight\":1080,\"upscaler\":3}");
            Assert.AreEqual(0, old.version);
            var d = GameSettings.Migrate(old);
            Assert.AreEqual(0, d.resolutionWidth);
            Assert.AreEqual(0, d.resolutionHeight);
            Assert.AreEqual(UpscalerSetting.DLSS, d.upscaler, "other settings are kept");
            Assert.AreEqual(SettingsData.CurrentVersion, d.version);

            // A size chosen in the new version is kept.
            var fresh = new SettingsData { version = SettingsData.CurrentVersion, resolutionWidth = 1920, resolutionHeight = 1080 };
            GameSettings.Migrate(fresh);
            Assert.AreEqual(1920, fresh.resolutionWidth);
        }

        [Test]
        public void DeleteRemovesSaveAndBackup()
        {
            SaveSystem.Save(Sample());
            SaveSystem.Save(Sample());
            Assert.IsTrue(SaveSystem.HasSave);
            SaveSystem.Delete();
            Assert.IsFalse(SaveSystem.HasSave);
        }
    }
}
