using System;
using System.Collections.Generic;

namespace BakeryShop.Core
{
    /// <summary>
    /// 재현할 수 있는 난수 (xorshift32). 상태가 uint 하나라서 세이브에 그대로 저장하고 복원한다.
    /// UnityEngine.Random이나 System.Random 대신 쓴다: 시드를 고정하면 테스트가 매번 같은 결과를 내고,
    /// 손님용과 박스용을 따로 두면 손님 수가 달라져도 박스에서 나오는 결과가 바뀌지 않는다.
    /// </summary>
    public sealed class Rng
    {
        // xorshift는 상태가 0이면 영원히 0만 낸다. 0이 들어오면 이 값으로 바꾼다.
        private const uint ZeroReplacement = 0x9E3779B9u;

        // 2의 -24제곱. uint의 위쪽 24비트를 0 이상 1 미만의 float로 바꿀 때 쓴다.
        // float의 가수부가 24비트라서, 이보다 많은 비트를 쓰면 반올림으로 1.0이 나올 수 있다.
        private const float Inverse24Bit = 1f / 16777216f;

        private uint _state;

        public Rng(uint seed)
        {
            _state = seed == 0u ? ZeroReplacement : seed;
        }

        /// <summary>지금 상태. 세이브에 저장한다.</summary>
        public uint State => _state;

        /// <summary>저장해 둔 상태로 되돌린다. 이후의 수열이 저장한 시점부터 이어진다.</summary>
        public void Restore(uint state)
        {
            _state = state == 0u ? ZeroReplacement : state;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>0 이상 1 미만. 1.0은 나오지 않는다.</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * Inverse24Bit;
        }

        /// <summary>minInclusive 이상 maxInclusive 이하의 정수. 양 끝을 포함한다.</summary>
        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
            {
                throw new ArgumentException("maxInclusive가 minInclusive보다 작습니다.");
            }

            long count = (long)maxInclusive - minInclusive + 1;
            long offset = (long)(NextFloat() * count);
            if (offset >= count)
            {
                offset = count - 1;
            }

            return (int)(minInclusive + offset);
        }

        /// <summary>
        /// probability의 확률로 참. 확률이 0 이하이거나 1 이상이어도 난수를 하나 소비한다
        /// (확률 값에 따라 이후의 수열이 달라지지 않게).
        /// </summary>
        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        /// <summary>
        /// 가중치에 비례한 확률로 인덱스 하나를 뽑는다. 가중치가 0 이하인 항목은 뽑히지 않는다.
        /// 가중치의 합이 0 이하이면 -1을 돌려주고 난수를 소비하지 않는다.
        /// 밸런스 시뮬레이터의 pick_weighted와 같은 규칙이다.
        /// </summary>
        public int PickWeighted(IReadOnlyList<float> weights)
        {
            if (weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }

            float total = 0f;
            int lastPositive = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0f)
                {
                    total += weights[i];
                    lastPositive = i;
                }
            }

            if (lastPositive < 0)
            {
                return -1;
            }

            float x = NextFloat() * total;
            float accumulated = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f)
                {
                    continue;
                }

                accumulated += weights[i];
                if (x < accumulated)
                {
                    return i;
                }
            }

            // 부동소수 오차로 x가 누적 합과 같아지는 드문 경우.
            return lastPositive;
        }
    }
}
