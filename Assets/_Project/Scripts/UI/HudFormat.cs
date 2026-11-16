using System.Globalization;
using BakeryShop.Core;

namespace BakeryShop.UI
{
    /// <summary>HUD에 보이는 글자와 수치를 만드는 계산. 화면 없이 테스트한다.</summary>
    public static class HudFormat
    {
        /// <summary>시계가 바뀌는 단위(분). 분 단위로 바꾸면 1초에 한 번 넘게 깜빡여서 읽기 어렵다.</summary>
        public const int ClockStepMinutes = 10;

        /// <summary>평판 표시의 반 칸 수 (별 5개 × 2).</summary>
        public const int HalfPipCount = 10;

        /// <summary>"3일차 · 영업 중"</summary>
        public static string DayLine(int day, DayPhase phase)
        {
            return day.ToString(CultureInfo.InvariantCulture) + "일차 · " + PhaseName(phase);
        }

        public static string PhaseName(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Preparation:
                    return "준비 중";
                case DayPhase.Open:
                    return "영업 중";
                case DayPhase.Closing:
                    return "마감";
                default:
                    return "정산";
            }
        }

        /// <summary>시계가 속한 10분 칸의 번호. 이 값이 바뀔 때만 글자를 다시 만든다.</summary>
        public static int ClockBucket(float shopClockMinutes)
        {
            if (!(shopClockMinutes > 0f))
            {
                return 0;
            }

            return (int)(shopClockMinutes / ClockStepMinutes);
        }

        /// <summary>자정부터의 분을 "HH:MM"으로. 10분 단위로 내림한다 (12:47 → "12:40").</summary>
        public static string Clock(float shopClockMinutes)
        {
            int minutes = ClockBucket(shopClockMinutes) * ClockStepMinutes;
            int hours = minutes / 60;
            int rest = minutes % 60;
            return hours.ToString("00", CultureInfo.InvariantCulture) + ":" + rest.ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>"Lv.2"</summary>
        public static string LevelLabel(int level)
        {
            return "Lv." + level.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>"57/130". 최고 레벨이면 (다음 기준이 -1) "MAX".</summary>
        public static string XpLine(int xp, int xpForNextLevel)
        {
            if (xpForNextLevel < 0)
            {
                return "MAX";
            }

            return xp.ToString(CultureInfo.InvariantCulture) + "/" + xpForNextLevel.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>경험치 막대가 찬 정도 (0 ~ 1). 지금 레벨의 시작부터 다음 레벨까지의 구간에서 계산한다.</summary>
        public static float XpFill(int xp, int xpForCurrentLevel, int xpForNextLevel)
        {
            if (xpForNextLevel < 0)
            {
                return 1f;
            }

            int span = xpForNextLevel - xpForCurrentLevel;
            if (span <= 0)
            {
                return 1f;
            }

            float fill = (xp - xpForCurrentLevel) / (float)span;
            return fill < 0f ? 0f : fill > 1f ? 1f : fill;
        }

        /// <summary>별 수(0 ~ 5, 반 개 단위)를 켜진 반 칸의 수(0 ~ 10)로.</summary>
        public static int LitHalfPips(float stars)
        {
            int lit = (int)(stars * 2f + 0.5f);
            return lit < 0 ? 0 : lit > HalfPipCount ? HalfPipCount : lit;
        }
    }
}
