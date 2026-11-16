using System;
using System.Collections.Generic;
using BakeryShop.Core;
using BakeryShop.Data;
using BakeryShop.Economy;
using BakeryShop.Events;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class LevelMathTests
    {
        // 실제 설정의 경험치 기준표 (2026-11 기준). 경계값의 규칙을 확인하는 표본이다.
        private static readonly int[] Thresholds = { 0, 40, 130, 280, 540 };

        [TestCase(0, 1)]
        [TestCase(39, 1)]
        [TestCase(40, 2)]
        [TestCase(129, 2)]
        [TestCase(130, 3)]
        [TestCase(279, 3)]
        [TestCase(280, 4)]
        [TestCase(539, 4)]
        [TestCase(540, 5)]
        [TestCase(9999, 5)]
        public void LevelFor_Boundaries(int xp, int expectedLevel)
        {
            Assert.AreEqual(expectedLevel, LevelMath.LevelFor(xp, Thresholds));
        }

        [Test]
        public void LevelFor_NegativeXp_IsLevelOne()
        {
            Assert.AreEqual(1, LevelMath.LevelFor(-5, Thresholds));
        }

        [Test]
        public void LevelFor_SingleLevel_IsAlwaysOne()
        {
            Assert.AreEqual(1, LevelMath.LevelFor(1000, new[] { 0 }));
        }

        [Test]
        public void LevelFor_EmptyOrNull_Throws()
        {
            Assert.Throws<ArgumentException>(() => LevelMath.LevelFor(0, new int[0]));
            Assert.Throws<ArgumentNullException>(() => LevelMath.LevelFor(0, null));
        }
    }

    public class ProgressionServiceTests
    {
        // TestContent의 경험치 기준표: [0, 10, 30] (레벨 3단계).
        private EventBus _bus;
        private ProgressionService _progression;

        // "xp:5:5" (얻은 양:누적), "level:2" 같은 줄이 일어난 순서대로 쌓인다.
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _progression = new ProgressionService(_bus, TestContent.Create().Config.Progression);
            _log = new List<string>();
            _bus.Subscribe<XpGained>(e => _log.Add("xp:" + e.Amount + ":" + e.TotalXp));
            _bus.Subscribe<LevelUp>(e => _log.Add("level:" + e.NewLevel));
        }

        [Test]
        public void StartsAtLevelOne_WithNoXp()
        {
            Assert.AreEqual(0, _progression.Xp);
            Assert.AreEqual(1, _progression.Level);
            Assert.AreEqual(3, _progression.MaxLevel);
            Assert.AreEqual(0, _progression.XpForCurrentLevel);
            Assert.AreEqual(10, _progression.XpForNextLevel);
            Assert.IsEmpty(_log);
        }

        [Test]
        public void AddXp_Accumulates_AndAnnouncesEachGain()
        {
            _progression.AddXp(3);
            _progression.AddXp(4);

            Assert.AreEqual(7, _progression.Xp);
            Assert.AreEqual(1, _progression.Level);
            CollectionAssert.AreEqual(new[] { "xp:3:3", "xp:4:7" }, _log);
        }

        [Test]
        public void ReachingTheThreshold_LevelsUp()
        {
            _progression.AddXp(9);
            _log.Clear();

            _progression.AddXp(1);

            Assert.AreEqual(2, _progression.Level);
            Assert.AreEqual(10, _progression.XpForCurrentLevel);
            Assert.AreEqual(30, _progression.XpForNextLevel);
            CollectionAssert.AreEqual(new[] { "xp:1:10", "level:2" }, _log);
        }

        [Test]
        public void TwoLevelsAtOnce_AnnounceEachLevelInOrder()
        {
            // 레시피 발견처럼 큰 경험치가 한 번에 들어오면 레벨 2를 건너뛰지 않고 2, 3을 차례로 알린다.
            _progression.AddXp(30);

            Assert.AreEqual(3, _progression.Level);
            CollectionAssert.AreEqual(new[] { "xp:30:30", "level:2", "level:3" }, _log);
        }

        [Test]
        public void AtMaxLevel_XpKeepsGrowing_ButNoMoreLevelUps()
        {
            _progression.AddXp(30);
            _log.Clear();

            _progression.AddXp(100);

            Assert.AreEqual(130, _progression.Xp);
            Assert.AreEqual(3, _progression.Level);
            Assert.AreEqual(30, _progression.XpForCurrentLevel);
            Assert.AreEqual(-1, _progression.XpForNextLevel);
            CollectionAssert.AreEqual(new[] { "xp:100:130" }, _log);
        }

        [Test]
        public void LevelUpSubscriber_SeesTheNewLevel()
        {
            int levelWhenNotified = 0;
            _bus.Subscribe<LevelUp>(e => levelWhenNotified = _progression.Level);

            _progression.AddXp(10);

            Assert.AreEqual(2, levelWhenNotified);
        }

        [Test]
        public void ZeroXp_DoesNothing()
        {
            _progression.AddXp(0);

            Assert.AreEqual(0, _progression.Xp);
            Assert.IsEmpty(_log);
        }

        [Test]
        public void NegativeXp_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _progression.AddXp(-1));
        }

        [Test]
        public void Restore_SetsXpAndLevel_WithoutEvents()
        {
            _progression.Restore(15);

            Assert.AreEqual(15, _progression.Xp);
            Assert.AreEqual(2, _progression.Level);
            Assert.IsEmpty(_log);
        }

        [Test]
        public void Restore_NegativeXp_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _progression.Restore(-1));
        }

        [Test]
        public void AfterRestore_LevelUpsContinueFromTheRestoredLevel()
        {
            _progression.Restore(15);

            _progression.AddXp(15);

            CollectionAssert.AreEqual(new[] { "xp:15:30", "level:3" }, _log);
        }

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            ProgressionConfig config = TestContent.Create().Config.Progression;

            Assert.Throws<ArgumentNullException>(() => new ProgressionService(null, config));
            Assert.Throws<ArgumentNullException>(() => new ProgressionService(_bus, null));
        }

        [Test]
        public void Constructor_EmptyThresholds_Throws()
        {
            ProgressionConfig empty = TestContent.Create(json => json.Config.Progression.XpThresholds = new int[0]).Config.Progression;

            Assert.Throws<ArgumentException>(() => new ProgressionService(_bus, empty));
        }
    }
}
