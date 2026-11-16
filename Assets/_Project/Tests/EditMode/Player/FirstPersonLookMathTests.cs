using BakeryShop.Player;
using NUnit.Framework;
using UnityEngine;

namespace BakeryShop.Tests
{
    public class FirstPersonLookMathTests
    {
        private const float Tolerance = 1e-4f;
        private const float Sensitivity = 0.1f;

        private static LookAngles Apply(LookAngles start, Vector2 delta) =>
            FirstPersonLookMath.Apply(start, delta, Sensitivity, minPitch: -80f, maxPitch: 80f);

        [Test]
        public void MouseRight_TurnsRight()
        {
            LookAngles result = Apply(new LookAngles(0f, 0f), new Vector2(100f, 0f));

            Assert.AreEqual(10f, result.Yaw, Tolerance);
            Assert.AreEqual(0f, result.Pitch, Tolerance);
        }

        [Test]
        public void MouseUp_LooksUp()
        {
            // Unity에서 x축 회전이 음수면 위를 본다.
            LookAngles result = Apply(new LookAngles(0f, 0f), new Vector2(0f, 100f));

            Assert.AreEqual(-10f, result.Pitch, Tolerance);
        }

        [Test]
        public void Pitch_IsClampedAtLimits()
        {
            Assert.AreEqual(-80f, Apply(new LookAngles(0f, 0f), new Vector2(0f, 5000f)).Pitch, Tolerance);
            Assert.AreEqual(80f, Apply(new LookAngles(0f, 0f), new Vector2(0f, -5000f)).Pitch, Tolerance);
        }

        [Test]
        public void Yaw_WrapsAroundInsteadOfGrowingForever()
        {
            LookAngles result = Apply(new LookAngles(179f, 0f), new Vector2(20f, 0f));

            Assert.AreEqual(-179f, result.Yaw, Tolerance);
        }

        [TestCase(350f, -10f)]
        [TestCase(10f, 10f)]
        [TestCase(180f, 180f)]
        [TestCase(-180f, 180f)]
        [TestCase(540f, 180f)]
        [TestCase(-190f, 170f)]
        [TestCase(0f, 0f)]
        public void WrapDegrees_MapsToSignedRange(float input, float expected)
        {
            Assert.AreEqual(expected, FirstPersonLookMath.WrapDegrees(input), Tolerance);
        }

        [Test]
        public void StartingPitchFromUnityEuler_IsNotClampedToTheWrongSide()
        {
            // 카메라가 10도 위를 보고 시작하면 localEulerAngles.x는 350이다.
            // 변환하지 않으면 350이 maxPitch(80)로 잘려 시작하자마자 바닥을 보게 된다.
            var start = new LookAngles(0f, FirstPersonLookMath.WrapDegrees(350f));

            LookAngles result = Apply(start, Vector2.zero);

            Assert.AreEqual(-10f, result.Pitch, Tolerance);
        }
    }
}
