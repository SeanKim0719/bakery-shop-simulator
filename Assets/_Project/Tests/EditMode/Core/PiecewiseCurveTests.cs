using System;
using BakeryShop.Core;
using BakeryShop.Data;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class PiecewiseCurveTests
    {
        private const float Tolerance = 0.001f;

        private static readonly CurvePoint[] Simple =
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(10f, 100f),
            new CurvePoint(20f, 50f),
        };

        // content.json의 학생 손님 가격 민감도 (2026-11 기준). x = 표시가 ÷ 시세, y = 구매 확률.
        // 수치가 바뀌어도 이 테스트는 고칠 필요가 없다. 보간 규칙을 확인하는 표본일 뿐이다.
        private static readonly CurvePoint[] StudentSensitivity =
        {
            new CurvePoint(0.7f, 1.0f),
            new CurvePoint(1.0f, 0.95f),
            new CurvePoint(1.1f, 0.85f),
            new CurvePoint(1.2f, 0.6f),
            new CurvePoint(1.35f, 0.25f),
            new CurvePoint(1.5f, 0.05f),
            new CurvePoint(1.7f, 0.0f),
        };

        // content.json의 시간대 곡선 (2026-11 기준). x = 영업 시계(분), y = 손님이 오는 정도.
        private static readonly CurvePoint[] SpawnCurve =
        {
            new CurvePoint(540f, 0.6f),
            new CurvePoint(600f, 0.8f),
            new CurvePoint(660f, 1.2f),
            new CurvePoint(720f, 1.6f),
            new CurvePoint(780f, 1.0f),
            new CurvePoint(840f, 0.7f),
            new CurvePoint(900f, 0.8f),
            new CurvePoint(960f, 1.2f),
            new CurvePoint(1020f, 1.5f),
            new CurvePoint(1080f, 1.5f),
        };

        [TestCase(0f, 0f)]
        [TestCase(10f, 100f)]
        [TestCase(20f, 50f)]
        public void Evaluate_OnAPoint_ReturnsItsY(float x, float expected)
        {
            Assert.AreEqual(expected, PiecewiseCurve.Evaluate(Simple, x), Tolerance);
        }

        [TestCase(5f, 50f)]
        [TestCase(2.5f, 25f)]
        [TestCase(15f, 75f)]
        public void Evaluate_BetweenPoints_Interpolates(float x, float expected)
        {
            Assert.AreEqual(expected, PiecewiseCurve.Evaluate(Simple, x), Tolerance);
        }

        [Test]
        public void Evaluate_LeftOfRange_ReturnsFirstY()
        {
            Assert.AreEqual(0f, PiecewiseCurve.Evaluate(Simple, -100f), Tolerance);
        }

        [Test]
        public void Evaluate_RightOfRange_ReturnsLastY()
        {
            Assert.AreEqual(50f, PiecewiseCurve.Evaluate(Simple, 999f), Tolerance);
        }

        [Test]
        public void Evaluate_SinglePoint_IsConstant()
        {
            var single = new[] { new CurvePoint(3f, 7f) };

            Assert.AreEqual(7f, PiecewiseCurve.Evaluate(single, -1f), Tolerance);
            Assert.AreEqual(7f, PiecewiseCurve.Evaluate(single, 3f), Tolerance);
            Assert.AreEqual(7f, PiecewiseCurve.Evaluate(single, 10f), Tolerance);
        }

        [TestCase(1.0f, 0.95f)]
        [TestCase(1.15f, 0.725f)]
        [TestCase(1.25f, 0.4833f)]
        [TestCase(0.5f, 1.0f)]
        [TestCase(3.0f, 0.0f)]
        public void Evaluate_StudentSensitivity_MatchesSimulator(float priceRatio, float expected)
        {
            Assert.AreEqual(expected, PiecewiseCurve.Evaluate(StudentSensitivity, priceRatio), Tolerance);
        }

        [Test]
        public void Evaluate_EmptyOrNull_Throws()
        {
            Assert.Throws<ArgumentException>(() => PiecewiseCurve.Evaluate(new CurvePoint[0], 1f));
            Assert.Throws<ArgumentNullException>(() => PiecewiseCurve.Evaluate(null, 1f));
        }

        [Test]
        public void Mean_OfConstantCurve_IsThatValue()
        {
            var flat = new[] { new CurvePoint(0f, 4f), new CurvePoint(10f, 4f) };

            Assert.AreEqual(4f, PiecewiseCurve.Mean(flat, 2f, 9f), Tolerance);
        }

        [Test]
        public void Mean_OfStraightLine_IsMidpointValue()
        {
            var line = new[] { new CurvePoint(0f, 0f), new CurvePoint(10f, 100f) };

            Assert.AreEqual(50f, PiecewiseCurve.Mean(line, 0f, 10f), Tolerance);
            Assert.AreEqual(30f, PiecewiseCurve.Mean(line, 2f, 4f), Tolerance);
        }

        [Test]
        public void Mean_AcrossABreakpoint_AddsBothSides()
        {
            // 0~10: 넓이 500, 10~20: 넓이 750 → 평균 62.5
            Assert.AreEqual(62.5f, PiecewiseCurve.Mean(Simple, 0f, 20f), Tolerance);
        }

        [Test]
        public void Mean_BeyondTheRange_ExtendsEndValues()
        {
            // −10~0은 0으로, 20~30은 50으로 이어진다: (0 + 500 + 750 + 500) ÷ 40
            Assert.AreEqual(43.75f, PiecewiseCurve.Mean(Simple, -10f, 30f), Tolerance);
        }

        [Test]
        public void Mean_OfSpawnCurveOverOpenHours_MatchesSimulator()
        {
            Assert.AreEqual(1.0944f, PiecewiseCurve.Mean(SpawnCurve, 540f, 1080f), Tolerance);
        }

        [Test]
        public void Mean_EmptyInterval_ReturnsValueAtFrom()
        {
            Assert.AreEqual(50f, PiecewiseCurve.Mean(Simple, 5f, 5f), Tolerance);
            Assert.AreEqual(50f, PiecewiseCurve.Mean(Simple, 5f, 1f), Tolerance);
        }
    }
}
