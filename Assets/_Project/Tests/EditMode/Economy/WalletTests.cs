using System;
using System.Collections.Generic;
using BakeryShop.Core;
using BakeryShop.Economy;
using BakeryShop.Events;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class WalletTests
    {
        private EventBus _bus;
        private Wallet _wallet;
        private List<MoneyChanged> _events;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _wallet = new Wallet(_bus, 10000);
            _events = new List<MoneyChanged>();
            _bus.Subscribe<MoneyChanged>(e => _events.Add(e));
        }

        [Test]
        public void StartsWithTheStartMoney_WithoutEvents()
        {
            Assert.AreEqual(10000, _wallet.Balance);
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void Add_IncreasesTheBalance_AndAnnouncesIt()
        {
            _wallet.Add(3600, MoneyReason.Sale);

            Assert.AreEqual(13600, _wallet.Balance);
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(13600, _events[0].Balance);
            Assert.AreEqual(3600, _events[0].Delta);
            Assert.AreEqual(MoneyReason.Sale, _events[0].Reason);
        }

        [Test]
        public void TrySpend_WithinBalance_Succeeds()
        {
            bool spent = _wallet.TrySpend(6000, MoneyReason.Order);

            Assert.IsTrue(spent);
            Assert.AreEqual(4000, _wallet.Balance);
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(4000, _events[0].Balance);
            Assert.AreEqual(-6000, _events[0].Delta);
            Assert.AreEqual(MoneyReason.Order, _events[0].Reason);
        }

        [Test]
        public void TrySpend_ExactlyTheBalance_Succeeds()
        {
            Assert.IsTrue(_wallet.TrySpend(10000, MoneyReason.Unlock));
            Assert.AreEqual(0, _wallet.Balance);
        }

        [Test]
        public void TrySpend_MoreThanTheBalance_Fails_AndChangesNothing()
        {
            bool spent = _wallet.TrySpend(10001, MoneyReason.Unlock);

            Assert.IsFalse(spent);
            Assert.AreEqual(10000, _wallet.Balance);
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void ForceSpend_CanGoNegative()
        {
            // 임대료는 돈이 모자라도 빠진다. 이어지는 지원금이 잔액을 채운다.
            _wallet.ForceSpend(15000, MoneyReason.Rent);

            Assert.AreEqual(-5000, _wallet.Balance);
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(-5000, _events[0].Balance);
            Assert.AreEqual(-15000, _events[0].Delta);
            Assert.AreEqual(MoneyReason.Rent, _events[0].Reason);
        }

        [Test]
        public void TrySpend_WithNegativeBalance_Fails()
        {
            _wallet.ForceSpend(15000, MoneyReason.Rent);
            _events.Clear();

            Assert.IsFalse(_wallet.TrySpend(1, MoneyReason.Order));
            Assert.AreEqual(0, _events.Count);
        }

        [TestCase(0, true)]
        [TestCase(9999, true)]
        [TestCase(10000, true)]
        [TestCase(10001, false)]
        public void CanAfford_ComparesWithTheBalance(int amount, bool expected)
        {
            Assert.AreEqual(expected, _wallet.CanAfford(amount));
        }

        [Test]
        public void ZeroAmounts_DoNothing_AndAnnounceNothing()
        {
            _wallet.Add(0, MoneyReason.Sale);
            Assert.IsTrue(_wallet.TrySpend(0, MoneyReason.Order));
            _wallet.ForceSpend(0, MoneyReason.Rent);

            Assert.AreEqual(10000, _wallet.Balance);
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void NegativeAmounts_Throw()
        {
            // 음수를 더해서 돈을 빼는 길을 막는다. 나가는 돈은 항상 Spend를 거쳐야 사유가 남는다.
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.Add(-1, MoneyReason.Sale));
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.TrySpend(-1, MoneyReason.Order));
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.ForceSpend(-1, MoneyReason.Rent));
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.CanAfford(-1));
            Assert.AreEqual(10000, _wallet.Balance);
        }

        [Test]
        public void Restore_SetsTheBalance_WithoutEvents()
        {
            _wallet.Restore(123456);

            Assert.AreEqual(123456, _wallet.Balance);
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void Restore_AcceptsANegativeBalance()
        {
            _wallet.Restore(-3000);

            Assert.AreEqual(-3000, _wallet.Balance);
        }

        [Test]
        public void Subscriber_SeesTheNewBalance()
        {
            int balanceWhenNotified = 0;
            _bus.Subscribe<MoneyChanged>(e => balanceWhenNotified = _wallet.Balance);

            _wallet.Add(500, MoneyReason.Resell);

            Assert.AreEqual(10500, balanceWhenNotified);
        }

        [Test]
        public void Add_BeyondIntRange_ThrowsInsteadOfWrapping()
        {
            _wallet.Restore(int.MaxValue - 10);

            Assert.Throws<OverflowException>(() => _wallet.Add(100, MoneyReason.Sale));
            Assert.AreEqual(int.MaxValue - 10, _wallet.Balance);
        }

        [Test]
        public void Constructor_NullBus_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Wallet(null, 0));
        }
    }
}
