using BakeryShop.Player;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class HoldDistanceMathTests
    {
        private const float Tolerance = 1e-4f;
        private const float Rest = 0.95f;
        private const float Min = 0.4f;
        private const float ReturnSpeed = 3f;
        private const float Dt = 0.02f;
        private const float NotBlocked = float.PositiveInfinity;

        private static float Next(float current, float blockedAt) =>
            HoldDistanceMath.Next(current, Rest, blockedAt, Min, ReturnSpeed, Dt);

        [Test]
        public void NotBlocked_StaysAtRest()
        {
            Assert.AreEqual(Rest, Next(Rest, NotBlocked), Tolerance);
        }

        [Test]
        public void Blocked_PullsInImmediately()
        {
            // 한 프레임이라도 벽에 파고들지 않도록 천천히가 아니라 즉시 당긴다.
            Assert.AreEqual(0.6f, Next(Rest, 0.6f), Tolerance);
        }

        [Test]
        public void BlockedVeryClose_StopsAtMinimum()
        {
            Assert.AreEqual(Min, Next(Rest, 0.1f), Tolerance);
        }

        [Test]
        public void BlockedBehindCamera_StopsAtMinimum()
        {
            // 막힘 거리가 음수로 계산돼도(틈을 빼서) 최소 거리 아래로 내려가지 않는다.
            Assert.AreEqual(Min, Next(Rest, -0.05f), Tolerance);
        }

        [Test]
        public void Unblocked_ReturnsGraduallyNotInstantly()
        {
            float next = Next(0.5f, NotBlocked);

            Assert.AreEqual(0.5f + ReturnSpeed * Dt, next, Tolerance);
        }

        [Test]
        public void Returning_NeverOvershootsRest()
        {
            Assert.AreEqual(Rest, Next(Rest - 0.01f, NotBlocked), Tolerance);
        }

        [Test]
        public void BlockedFartherThanCurrent_ReturnsGraduallyToThatPoint()
        {
            // 벽에서 조금 물러난 경우: 막힘 지점까지는 천천히 따라간다.
            float next = Next(0.5f, 0.8f);

            Assert.AreEqual(0.5f + ReturnSpeed * Dt, next, Tolerance);
        }
    }
}
