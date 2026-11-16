using System;
using System.Collections.Generic;

namespace BakeryShop.Economy
{
    /// <summary>경험치와 레벨의 관계. 레벨은 저장하지 않고 경험치에서 계산한다 (두 값이 어긋날 일이 없다).</summary>
    public static class LevelMath
    {
        /// <summary>
        /// 누적 경험치가 thresholds[i] 이상인 가장 큰 i에 1을 더한 값.
        /// thresholds는 레벨별 누적 기준이고 커지는 순서다 (기본 [0, 40, 130, 280, 540]).
        /// 결과는 1 ~ thresholds의 길이.
        /// </summary>
        public static int LevelFor(int xp, IReadOnlyList<int> thresholds)
        {
            if (thresholds == null)
            {
                throw new ArgumentNullException(nameof(thresholds));
            }

            if (thresholds.Count == 0)
            {
                throw new ArgumentException("경험치 기준표가 비어 있습니다.", nameof(thresholds));
            }

            int level = 1;
            for (int i = 1; i < thresholds.Count; i++)
            {
                if (xp < thresholds[i])
                {
                    break;
                }

                level = i + 1;
            }

            return level;
        }
    }
}
