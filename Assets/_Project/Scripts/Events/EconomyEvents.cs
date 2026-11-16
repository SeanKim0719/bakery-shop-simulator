using BakeryShop.Economy;

namespace BakeryShop.Events
{
    /// <summary>잔액이 바뀌었다.</summary>
    public readonly struct MoneyChanged
    {
        /// <summary>바뀐 뒤의 잔액(원).</summary>
        public readonly int Balance;

        /// <summary>변화량(원). 들어온 돈은 양수, 나간 돈은 음수.</summary>
        public readonly int Delta;

        public readonly MoneyReason Reason;

        public MoneyChanged(int balance, int delta, MoneyReason reason)
        {
            Balance = balance;
            Delta = delta;
            Reason = reason;
        }
    }

    /// <summary>평판이 바뀌었다. 범위의 끝에 막혀 값이 그대로면 발행되지 않는다.</summary>
    public readonly struct ReputationChanged
    {
        /// <summary>바뀐 뒤의 평판.</summary>
        public readonly int Value;

        /// <summary>실제로 바뀐 양. 범위에 막혔으면 요청한 양보다 작다.</summary>
        public readonly int Delta;

        public ReputationChanged(int value, int delta)
        {
            Value = value;
            Delta = delta;
        }
    }

    /// <summary>경험치를 얻었다.</summary>
    public readonly struct XpGained
    {
        public readonly int Amount;

        /// <summary>얻은 뒤의 누적 경험치.</summary>
        public readonly int TotalXp;

        public XpGained(int amount, int totalXp)
        {
            Amount = amount;
            TotalXp = totalXp;
        }
    }

    /// <summary>레벨이 올랐다. 한 번에 두 레벨이 오르면 레벨마다 한 번씩, 낮은 레벨부터 발행된다.</summary>
    public readonly struct LevelUp
    {
        public readonly int NewLevel;

        public LevelUp(int newLevel)
        {
            NewLevel = newLevel;
        }
    }
}
