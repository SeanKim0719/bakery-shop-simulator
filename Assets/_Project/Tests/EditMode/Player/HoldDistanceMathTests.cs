using BakeryShop.Player;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void MinimumZero_BlockedAtStart_FoldsCompletely()
        {
            // Hands는 최소 거리 0으로 쓴다. 접은 자세조차 막혀 있으면(거리 0 이하) 완전히 접는다.
            float next = HoldDistanceMath.Next(Rest, Rest, -0.02f, 0f, ReturnSpeed, Dt);

            Assert.AreEqual(0f, next, Tolerance);
        }

        // --- 경로 위의 위치 ---

        private static readonly Vector3 Tucked = new Vector3(0f, -0.5f, 0.2f);
        private static readonly Vector3 Extended = new Vector3(0f, -0.3f, 0.9f);

        [Test]
        public void PoseOnPath_ZeroDistance_IsTuckedPose()
        {
            AssertVector(Tucked, HoldDistanceMath.PoseOnPath(Tucked, Extended, 0f));
        }

        [Test]
        public void PoseOnPath_FullLength_IsRestPose()
        {
            float length = Vector3.Distance(Tucked, Extended);

            AssertVector(Extended, HoldDistanceMath.PoseOnPath(Tucked, Extended, length));
        }

        [Test]
        public void PoseOnPath_HalfLength_IsMidpoint()
        {
            float length = Vector3.Distance(Tucked, Extended);

            AssertVector((Tucked + Extended) * 0.5f, HoldDistanceMath.PoseOnPath(Tucked, Extended, length * 0.5f));
        }

        [Test]
        public void PoseOnPath_OutOfRange_StaysOnPath()
        {
            AssertVector(Tucked, HoldDistanceMath.PoseOnPath(Tucked, Extended, -1f));
            AssertVector(Extended, HoldDistanceMath.PoseOnPath(Tucked, Extended, 10f));
        }

        [Test]
        public void PoseOnPath_SamePoints_ReturnsRest()
        {
            AssertVector(Extended, HoldDistanceMath.PoseOnPath(Extended, Extended, 0.3f));
        }

        [Test]
        public void PoseOnPath_BlockedPose_StaysBelowTheViewCenter()
        {
            // 회귀 테스트 (트러블슈팅 #2): 눈 쪽으로 당기던 때에는 0.5m 상자의 윗면이 화면 중앙보다 위로 올라와
            // 조준점을 가렸다. 접는 경로에서는 어느 지점에서도 상자 윗면(중심 + 0.25m)이 화면 중앙(y = 0)보다 아래다.
            const float boxHalfHeight = 0.25f;
            float length = Vector3.Distance(Tucked, Extended);

            for (int i = 0; i <= 10; i++)
            {
                Vector3 pose = HoldDistanceMath.PoseOnPath(Tucked, Extended, length * i / 10f);

                Assert.Less(pose.y + boxHalfHeight, 0f, $"경로의 {i * 10}% 지점");
            }
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance);
            Assert.AreEqual(expected.y, actual.y, Tolerance);
            Assert.AreEqual(expected.z, actual.z, Tolerance);
        }
    }
}
