using System;
using BakeryShop.Core;
using BakeryShop.Events;

namespace BakeryShop.Economy
{
    /// <summary>돈이 들어오고 나간 이유. 일일 리포트가 이 값으로 항목을 나눈다.</summary>
    public enum MoneyReason
    {
        Sale,
        Order,
        Unlock,
        Rent,
        Resell,
        Subsidy,
    }

    /// <summary>
    /// 잔액. 금액은 원 단위 int로만 다룬다 (float는 거스름돈 계산에서 오차가 생긴다).
    /// 지출이 잔액을 넘으면 실패한다. 임대료만 ForceSpend로 음수를 허용하고, 하루 마무리의 지원금이 메운다.
    /// </summary>
    public sealed class Wallet
    {
        private readonly EventBus _bus;

        public Wallet(EventBus bus, int startMoney)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            Balance = startMoney;
        }

        /// <summary>잔액(원). 임대료를 내지 못한 날에는 음수일 수 있다.</summary>
        public int Balance { get; private set; }

        public bool CanAfford(int amount)
        {
            RequireNotNegative(amount);
            return amount <= Balance;
        }

        public void Add(int amount, MoneyReason reason)
        {
            RequireNotNegative(amount);
            if (amount == 0)
            {
                return;
            }

            // 잔액이 int의 범위를 넘으면 조용히 음수로 돌아가지 않고 예외가 나게 한다.
            Balance = checked(Balance + amount);
            _bus.Publish(new MoneyChanged(Balance, amount, reason));
        }

        /// <summary>잔액이 모자라면 false를 돌려주고 아무것도 바꾸지 않는다.</summary>
        public bool TrySpend(int amount, MoneyReason reason)
        {
            if (!CanAfford(amount))
            {
                return false;
            }

            Spend(amount, reason);
            return true;
        }

        /// <summary>잔액이 모자라도 뺀다 (잔액이 음수가 될 수 있다). 임대료 전용이다.</summary>
        public void ForceSpend(int amount, MoneyReason reason)
        {
            RequireNotNegative(amount);
            Spend(amount, reason);
        }

        /// <summary>세이브에서 잔액을 되돌린다. 이벤트를 발행하지 않는다.</summary>
        public void Restore(int balance)
        {
            Balance = balance;
        }

        private void Spend(int amount, MoneyReason reason)
        {
            if (amount == 0)
            {
                return;
            }

            Balance = checked(Balance - amount);
            _bus.Publish(new MoneyChanged(Balance, -amount, reason));
        }

        private static void RequireNotNegative(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "금액은 0 이상이어야 합니다. 나가는 돈은 TrySpend로 처리합니다.");
            }
        }
    }
}
