using System;
using System.Collections.Generic;
using BakeryShop.Core;
using BakeryShop.Data;
using BakeryShop.Economy;
using BakeryShop.Events;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class ReputationServiceTests
    {
        // TestContent의 평판: 시작 500, 범위 0 ~ 1000, 손님 수 배율 0.7 ~ 1.3 (실제 설정과 같은 값).
        private EventBus _bus;
        private ReputationService _reputation;
        private List<ReputationChanged> _events;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = TestContent.Create().Config;
            _bus = new EventBus();
            _reputation = new ReputationService(_bus, config.Reputation, config.Customers);
            _events = new List<ReputationChanged>();
            _bus.Subscribe<ReputationChanged>(e => _events.Add(e));
        }

        [Test]
        public void StartsAtTheConfiguredValue()
        {
            Assert.AreEqual(500, _reputation.Value);
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void Add_ChangesTheValue_AndAnnouncesIt()
        {
            _reputation.Add(-10);

            Assert.AreEqual(490, _reputation.Value);
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(490, _events[0].Value);
            Assert.AreEqual(-10, _events[0].Delta);
        }

        [Test]
        public void Add_StopsAtTheTop()
        {
            _reputation.Restore(990);

            _reputation.Add(20);

            Assert.AreEqual(1000, _reputation.Value);
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(10, _events[0].Delta);
        }

        [Test]
        public void Add_StopsAtTheBottom()
        {
            _reputation.Restore(3);

            _reputation.Add(-10);

            Assert.AreEqual(0, _reputation.Value);
            Assert.AreEqual(-3, _events[0].Delta);
        }

        [Test]
        public void Add_WithNoActualChange_AnnouncesNothing()
        {
            _reputation.Restore(1000);

            _reputation.Add(5);
            _reputation.Add(0);

            Assert.AreEqual(1000, _reputation.Value);
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void Add_HugeDelta_DoesNotOverflow()
        {
            _reputation.Add(int.MaxValue);
            Assert.AreEqual(1000, _reputation.Value);

            _reputation.Add(int.MinValue);
            Assert.AreEqual(0, _reputation.Value);
        }

        [TestCase(0, 0f)]
        [TestCase(199, 0.5f)]
        [TestCase(200, 1f)]
        [TestCase(500, 2.5f)]
        [TestCase(599, 2.5f)]
        [TestCase(600, 3f)]
        [TestCase(999, 4.5f)]
        [TestCase(1000, 5f)]
        public void Stars_AreHalfSteps_RoundedDown(int value, float expected)
        {
            _reputation.Restore(value);

            Assert.AreEqual(expected, _reputation.Stars, 0.0001f);
        }

        [TestCase(0, 0.7f)]
        [TestCase(250, 0.85f)]
        [TestCase(500, 1.0f)]
        [TestCase(1000, 1.3f)]
        public void SpawnMultiplier_ScalesWithReputation(int value, float expected)
        {
            _reputation.Restore(value);

            Assert.AreEqual(expected, _reputation.SpawnMultiplier, 0.0001f);
        }

        [Test]
        public void Restore_SetsTheValue_WithoutEvents()
        {
            _reputation.Restore(720);

            Assert.AreEqual(720, _reputation.Value);
            Assert.AreEqual(0, _events.Count);
        }

        [TestCase(-50, 0)]
        [TestCase(5000, 1000)]
        public void Restore_OutOfRange_IsClamped(int saved, int expected)
        {
            _reputation.Restore(saved);

            Assert.AreEqual(expected, _reputation.Value);
        }

        [Test]
        public void Subscriber_SeesTheNewValue()
        {
            int valueWhenNotified = 0;
            _bus.Subscribe<ReputationChanged>(e => valueWhenNotified = _reputation.Value);

            _reputation.Add(20);

            Assert.AreEqual(520, valueWhenNotified);
        }

        [Test]
        public void OtherRanges_AreRespected()
        {
            // 범위와 배율은 코드에 적혀 있지 않고 설정에서 온다.
            GameConfig config = TestContent.Create(json =>
            {
                json.Config.Reputation.Min = 100;
                json.Config.Reputation.Max = 300;
                json.Config.Reputation.Start = 200;
                json.Config.Customers.ReputationSpawnMin = 0.5f;
                json.Config.Customers.ReputationSpawnMax = 2f;
            }).Config;
            var reputation = new ReputationService(new EventBus(), config.Reputation, config.Customers);

            Assert.AreEqual(200, reputation.Value);
            Assert.AreEqual(2.5f, reputation.Stars, 0.0001f);
            Assert.AreEqual(1.25f, reputation.SpawnMultiplier, 0.0001f);

            reputation.Add(-500);
            Assert.AreEqual(100, reputation.Value);
            Assert.AreEqual(0.5f, reputation.SpawnMultiplier, 0.0001f);
        }

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            GameConfig config = TestContent.Create().Config;

            Assert.Throws<ArgumentNullException>(() => new ReputationService(null, config.Reputation, config.Customers));
            Assert.Throws<ArgumentNullException>(() => new ReputationService(_bus, null, config.Customers));
            Assert.Throws<ArgumentNullException>(() => new ReputationService(_bus, config.Reputation, null));
        }
    }
}
