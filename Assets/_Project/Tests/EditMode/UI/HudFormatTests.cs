using BakeryShop.Core;
using BakeryShop.UI;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class HudFormatTests
    {
        [TestCase(1, DayPhase.Preparation, "1일차 · 준비 중")]
        [TestCase(3, DayPhase.Open, "3일차 · 영업 중")]
        [TestCase(3, DayPhase.Closing, "3일차 · 마감")]
        [TestCase(12, DayPhase.Settlement, "12일차 · 정산")]
        public void DayLine_ShowsDayAndPhase(int day, DayPhase phase, string expected)
        {
            Assert.AreEqual(expected, HudFormat.DayLine(day, phase));
        }

        [TestCase(540f, "09:00")]
        [TestCase(549.9f, "09:00")]
        [TestCase(550f, "09:10")]
        [TestCase(599.99f, "09:50")]
        [TestCase(600f, "10:00")]
        [TestCase(767f, "12:40")]
        [TestCase(1079.9f, "17:50")]
        [TestCase(1080f, "18:00")]
        public void Clock_RoundsDownToTenMinutes(float minutes, string expected)
        {
            Assert.AreEqual(expected, HudFormat.Clock(minutes));
        }

        [Test]
        public void Clock_NegativeOrNaN_IsMidnight()
        {
            Assert.AreEqual("00:00", HudFormat.Clock(-5f));
            Assert.AreEqual("00:00", HudFormat.Clock(float.NaN));
        }

        [Test]
        public void ClockBucket_ChangesOnlyEveryTenMinutes()
        {
            // HUD는 이 값이 바뀔 때만 글자를 다시 만든다. 하루(540분)에 54번이다.
            Assert.AreEqual(HudFormat.ClockBucket(540f), HudFormat.ClockBucket(549.9f));
            Assert.AreNotEqual(HudFormat.ClockBucket(549.9f), HudFormat.ClockBucket(550f));
            Assert.AreEqual(54, HudFormat.ClockBucket(1080f) - HudFormat.ClockBucket(540f));
        }

        [TestCase(1, "Lv.1")]
        [TestCase(5, "Lv.5")]
        public void LevelLabel_Formats(int level, string expected)
        {
            Assert.AreEqual(expected, HudFormat.LevelLabel(level));
        }

        [TestCase(0, 40, "0/40")]
        [TestCase(57, 130, "57/130")]
        [TestCase(9999, -1, "MAX")]
        public void XpLine_ShowsTotalAndNextThreshold(int xp, int next, string expected)
        {
            Assert.AreEqual(expected, HudFormat.XpLine(xp, next));
        }

        [TestCase(0, 0, 40, 0f)]
        [TestCase(20, 0, 40, 0.5f)]
        [TestCase(40, 40, 130, 0f)]
        [TestCase(85, 40, 130, 0.5f)]
        [TestCase(130, 40, 130, 1f)]
        [TestCase(600, 540, -1, 1f)]
        public void XpFill_IsTheProgressWithinTheCurrentLevel(int xp, int current, int next, float expected)
        {
            Assert.AreEqual(expected, HudFormat.XpFill(xp, current, next), 0.0001f);
        }

        [Test]
        public void XpFill_StaysWithinZeroAndOne()
        {
            Assert.AreEqual(0f, HudFormat.XpFill(10, 40, 130), 0.0001f);
            Assert.AreEqual(1f, HudFormat.XpFill(500, 40, 130), 0.0001f);
            Assert.AreEqual(1f, HudFormat.XpFill(50, 40, 40), 0.0001f);
        }

        [TestCase(0f, 0)]
        [TestCase(0.5f, 1)]
        [TestCase(2.5f, 5)]
        [TestCase(3f, 6)]
        [TestCase(5f, 10)]
        [TestCase(9f, 10)]
        [TestCase(-1f, 0)]
        public void LitHalfPips_TwoPerStar(float stars, int expected)
        {
            Assert.AreEqual(expected, HudFormat.LitHalfPips(stars));
        }
    }
}
