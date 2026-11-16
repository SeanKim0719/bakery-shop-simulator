using System;
using System.Collections.Generic;
using BakeryShop.Core;
using BakeryShop.Data;
using BakeryShop.Events;

namespace BakeryShop.Economy
{
    /// <summary>
    /// 경험치와 레벨. 레벨이 오르면 살 수 있는 해금과 주문할 수 있는 상품이 늘어난다.
    /// 어떤 일에 경험치를 얼마 주는지는 여기가 아니라 XpRules(M4)가 정한다.
    /// </summary>
    public sealed class ProgressionService
    {
        private readonly EventBus _bus;
        private readonly IReadOnlyList<int> _thresholds;

        public ProgressionService(EventBus bus, ProgressionConfig config)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (config.XpThresholds.Count == 0)
            {
                throw new ArgumentException("경험치 기준표가 비어 있습니다.", nameof(config));
            }

            _thresholds = config.XpThresholds;
        }

        /// <summary>누적 경험치.</summary>
        public int Xp { get; private set; }

        /// <summary>레벨. 1 ~ MaxLevel.</summary>
        public int Level => LevelMath.LevelFor(Xp, _thresholds);

        public int MaxLevel => _thresholds.Count;

        /// <summary>지금 레벨이 시작된 누적 경험치. 경험치 막대의 왼쪽 끝이다.</summary>
        public int XpForCurrentLevel => _thresholds[Level - 1];

        /// <summary>다음 레벨이 되는 누적 경험치. 최고 레벨이면 -1.</summary>
        public int XpForNextLevel
        {
            get
            {
                int level = Level;
                return level < MaxLevel ? _thresholds[level] : -1;
            }
        }

        /// <summary>
        /// 경험치를 얻는다. XpGained를 먼저 알리고, 레벨이 올랐으면 LevelUp을 오른 레벨마다 순서대로 알린다.
        /// 최고 레벨에서도 경험치는 계속 쌓인다.
        /// </summary>
        public void AddXp(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "경험치는 줄어들지 않습니다.");
            }

            if (amount == 0)
            {
                return;
            }

            int previousLevel = Level;
            Xp = checked(Xp + amount);
            int newLevel = Level;

            _bus.Publish(new XpGained(amount, Xp));
            for (int level = previousLevel + 1; level <= newLevel; level++)
            {
                _bus.Publish(new LevelUp(level));
            }
        }

        /// <summary>세이브에서 경험치를 되돌린다. 이벤트를 발행하지 않는다.</summary>
        public void Restore(int xp)
        {
            if (xp < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(xp), "경험치는 0 이상이어야 합니다.");
            }

            Xp = xp;
        }
    }
}
