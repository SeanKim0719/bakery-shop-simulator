using System;
using BakeryShop.Core;
using BakeryShop.Data;
using BakeryShop.Events;

namespace BakeryShop.Economy
{
    /// <summary>
    /// 평판. 판매, 품절, 비싼 가격, 긴 줄 같은 사건으로 오르내리고, 하루 손님 수의 배율이 된다.
    /// 어떤 사건에 얼마를 바꾸는지는 여기가 아니라 ReputationRules(M4)가 정한다.
    /// </summary>
    public sealed class ReputationService
    {
        private const int HalfStarCount = 10;

        private readonly EventBus _bus;
        private readonly int _min;
        private readonly int _max;
        private readonly float _spawnMin;
        private readonly float _spawnMax;

        /// <param name="customers">평판에 따른 손님 수 배율의 범위(reputationSpawnMin/Max)를 읽는다.</param>
        public ReputationService(EventBus bus, ReputationConfig reputation, CustomerConfig customers)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            if (reputation == null)
            {
                throw new ArgumentNullException(nameof(reputation));
            }

            if (customers == null)
            {
                throw new ArgumentNullException(nameof(customers));
            }

            if (reputation.Max <= reputation.Min)
            {
                throw new ArgumentException("평판의 max는 min보다 커야 합니다.", nameof(reputation));
            }

            _min = reputation.Min;
            _max = reputation.Max;
            _spawnMin = customers.ReputationSpawnMin;
            _spawnMax = customers.ReputationSpawnMax;
            Value = Clamp(reputation.Start);
        }

        /// <summary>평판. 설정의 min ~ max (기본 0 ~ 1000).</summary>
        public int Value { get; private set; }

        /// <summary>별 0 ~ 5개, 반 개 단위로 내림 (기본 설정에서 200점에 별 하나).</summary>
        public float Stars
        {
            get
            {
                int halfStars = (Value - _min) * HalfStarCount / (_max - _min);
                return halfStars * 0.5f;
            }
        }

        /// <summary>하루 손님 수에 곱하는 배율. 평판이 최저일 때 spawnMin, 최고일 때 spawnMax (기본 0.7 ~ 1.3).</summary>
        public float SpawnMultiplier
        {
            get
            {
                float ratio = (Value - _min) / (float)(_max - _min);
                return _spawnMin + (_spawnMax - _spawnMin) * ratio;
            }
        }

        /// <summary>평판을 올리거나 내린다. 범위를 벗어나면 끝에서 멈추고, 값이 실제로 바뀌었을 때만 알린다.</summary>
        public void Add(int delta)
        {
            int previous = Value;
            long target = (long)previous + delta;
            int next = target < _min ? _min : target > _max ? _max : (int)target;
            if (next == previous)
            {
                return;
            }

            Value = next;
            _bus.Publish(new ReputationChanged(next, next - previous));
        }

        /// <summary>세이브에서 평판을 되돌린다. 이벤트를 발행하지 않는다.</summary>
        public void Restore(int value)
        {
            Value = Clamp(value);
        }

        private int Clamp(int value)
        {
            return value < _min ? _min : value > _max ? _max : value;
        }
    }
}
