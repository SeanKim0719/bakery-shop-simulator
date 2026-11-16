using System;
using BakeryShop.Core;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class TimeServiceTests
    {
        // content.json의 기본값: 09:00 ~ 18:00을 실제 6분에. 1초에 1.5분이 흐른다.
        private const int Open = 540;
        private const int Close = 1080;
        private const float OpenRealSeconds = 360f;
        private const float Tolerance = 0.0001f;

        private static TimeService CreateDefault()
        {
            return new TimeService(Open, Close, OpenRealSeconds);
        }

        /// <summary>Advance는 한 번에 0.25초까지만 흐르므로, 긴 시간은 잘게 나누어 흘린다.</summary>
        private static void AdvanceSeconds(TimeService time, float seconds)
        {
            int steps = (int)Math.Round(seconds / TimeService.MaxDeltaSeconds);
            for (int i = 0; i < steps; i++)
            {
                time.Advance(TimeService.MaxDeltaSeconds);
            }
        }

        [Test]
        public void StartsAtZero_WithClockStoppedAtOpen()
        {
            TimeService time = CreateDefault();

            Assert.AreEqual(0.0, time.SimTime, 1e-9);
            Assert.AreEqual(0f, time.SimDelta, Tolerance);
            Assert.AreEqual(Open, time.ShopClockMinutes, Tolerance);
            Assert.IsFalse(time.IsPaused);
            Assert.IsFalse(time.IsClockRunning);
            Assert.IsFalse(time.HasReachedClose);
        }

        [Test]
        public void Advance_WithClockStopped_OnlyMovesSimTime()
        {
            TimeService time = CreateDefault();

            time.Advance(0.1f);

            Assert.AreEqual(0.1, time.SimTime, 1e-6);
            Assert.AreEqual(0.1f, time.SimDelta, Tolerance);
            Assert.AreEqual(Open, time.ShopClockMinutes, Tolerance);
        }

        [Test]
        public void RunningClock_AdvancesOneAndAHalfMinutesPerSecond()
        {
            TimeService time = CreateDefault();
            time.StartClock();

            AdvanceSeconds(time, 10f);

            Assert.AreEqual(Open + 15f, time.ShopClockMinutes, Tolerance);
            Assert.IsTrue(time.IsClockRunning);
        }

        [Test]
        public void RunningClock_ReachesCloseAfterTheFullDay()
        {
            TimeService time = CreateDefault();
            time.StartClock();

            AdvanceSeconds(time, OpenRealSeconds);

            Assert.AreEqual(Close, time.ShopClockMinutes, Tolerance);
            Assert.IsTrue(time.HasReachedClose);
        }

        [Test]
        public void RunningClock_HasNotReachedClose_JustBeforeTheEnd()
        {
            TimeService time = CreateDefault();
            time.StartClock();

            AdvanceSeconds(time, OpenRealSeconds - 1f);

            Assert.IsFalse(time.HasReachedClose);
            Assert.AreEqual(Close - 1.5f, time.ShopClockMinutes, Tolerance);
        }

        [Test]
        public void RunningClock_DoesNotPassClose()
        {
            TimeService time = CreateDefault();
            time.StartClock();

            AdvanceSeconds(time, OpenRealSeconds + 60f);

            Assert.AreEqual(Close, time.ShopClockMinutes, Tolerance);
            Assert.AreEqual(OpenRealSeconds + 60.0, time.SimTime, 1e-6);
        }

        [Test]
        public void ClockAccumulatesSmallFrames_WithoutDrift()
        {
            // 60fps로 하루를 보내도(21,600프레임) 폐점 시각이 어긋나지 않는다.
            TimeService time = CreateDefault();
            time.StartClock();
            const float frame = 1f / 60f;

            for (int i = 0; i < 21600 - 60; i++)
            {
                time.Advance(frame);
            }

            // 폐점 1초 전: 1078.5분
            Assert.AreEqual(Close - 1.5f, time.ShopClockMinutes, 0.01f);
            Assert.IsFalse(time.HasReachedClose);
        }

        [Test]
        public void StopClock_HoldsTheClock_ButSimTimeKeepsRunning()
        {
            TimeService time = CreateDefault();
            time.StartClock();
            AdvanceSeconds(time, 10f);

            time.StopClock();
            AdvanceSeconds(time, 10f);

            Assert.IsFalse(time.IsClockRunning);
            Assert.AreEqual(Open + 15f, time.ShopClockMinutes, Tolerance);
            Assert.AreEqual(20.0, time.SimTime, 1e-6);
        }

        [Test]
        public void ResetClock_ReturnsToOpen_AndStops()
        {
            TimeService time = CreateDefault();
            time.StartClock();
            AdvanceSeconds(time, OpenRealSeconds);

            time.ResetClock();

            Assert.AreEqual(Open, time.ShopClockMinutes, Tolerance);
            Assert.IsFalse(time.IsClockRunning);
            Assert.IsFalse(time.HasReachedClose);
        }

        [Test]
        public void ResetClock_DoesNotTouchSimTime()
        {
            // SimTime은 날이 바뀌어도 이어진다. 전날 넣은 반죽의 "언제 끝나는지"가 유지되어야 한다.
            TimeService time = CreateDefault();
            AdvanceSeconds(time, 5f);

            time.ResetClock();

            Assert.AreEqual(5.0, time.SimTime, 1e-6);
        }

        [Test]
        public void Paused_NothingMoves()
        {
            TimeService time = CreateDefault();
            time.StartClock();
            AdvanceSeconds(time, 10f);

            time.SetPaused(true);
            time.Advance(0.1f);
            time.Advance(0.1f);

            Assert.IsTrue(time.IsPaused);
            Assert.AreEqual(0f, time.SimDelta, Tolerance);
            Assert.AreEqual(10.0, time.SimTime, 1e-6);
            Assert.AreEqual(Open + 15f, time.ShopClockMinutes, Tolerance);
        }

        [Test]
        public void Pausing_ZeroesSimDeltaImmediately()
        {
            // 같은 프레임 안에서 일시정지 뒤에 SimDelta를 읽는 코드가 시간을 한 번 더 흘리지 않게.
            TimeService time = CreateDefault();
            time.Advance(0.1f);

            time.SetPaused(true);

            Assert.AreEqual(0f, time.SimDelta, Tolerance);
        }

        [Test]
        public void Unpausing_ResumesBothClocks()
        {
            TimeService time = CreateDefault();
            time.StartClock();
            time.SetPaused(true);
            time.Advance(0.2f);

            time.SetPaused(false);
            time.Advance(0.2f);

            Assert.AreEqual(0.2, time.SimTime, 1e-6);
            Assert.AreEqual(Open + 0.3f, time.ShopClockMinutes, Tolerance);
            Assert.IsTrue(time.IsClockRunning);
        }

        [Test]
        public void LargeDelta_IsCappedAtAQuarterSecond()
        {
            TimeService time = CreateDefault();

            time.Advance(1f);

            Assert.AreEqual(0.25f, time.SimDelta, Tolerance);
            Assert.AreEqual(0.25, time.SimTime, 1e-6);
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDelta_IsTreatedAsZero(float delta)
        {
            TimeService time = CreateDefault();
            time.StartClock();

            time.Advance(delta);

            Assert.AreEqual(0f, time.SimDelta, Tolerance);
            Assert.AreEqual(0.0, time.SimTime, 1e-9);
            Assert.AreEqual(Open, time.ShopClockMinutes, Tolerance);
        }

        [Test]
        public void RestoreSimTime_ContinuesFromTheSavedValue()
        {
            TimeService time = CreateDefault();

            time.RestoreSimTime(1234.5);
            time.Advance(0.25f);

            Assert.AreEqual(1234.75, time.SimTime, 1e-6);
        }

        [Test]
        public void SimTime_KeepsFramePrecision_AfterManyHours()
        {
            // float였다면 36,000초 근처의 한 칸이 0.0039초라서 1/60초가 0.0156이나 0.0195로 더해진다.
            TimeService time = CreateDefault();
            time.RestoreSimTime(36000.0);
            const float frame = 1f / 60f;

            time.Advance(frame);

            Assert.AreEqual(frame, time.SimTime - 36000.0, 1e-6);
        }

        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void RestoreSimTime_InvalidValue_Throws(double value)
        {
            TimeService time = CreateDefault();

            Assert.Throws<ArgumentOutOfRangeException>(() => time.RestoreSimTime(value));
        }

        [TestCase(540, 540, 360f)]
        [TestCase(1080, 540, 360f)]
        [TestCase(540, 1080, 0f)]
        [TestCase(540, 1080, -5f)]
        [TestCase(540, 1080, float.NaN)]
        public void Constructor_InvalidArguments_Throws(int open, int close, float openRealSeconds)
        {
            Assert.Throws<ArgumentException>(() => new TimeService(open, close, openRealSeconds));
        }
    }
}
