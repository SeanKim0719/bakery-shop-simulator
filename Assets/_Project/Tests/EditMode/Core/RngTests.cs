using System;
using BakeryShop.Core;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class RngTests
    {
        [Test]
        public void SeedOne_ProducesKnownXorshift32Sequence()
        {
            // xorshift32 (13, 17, 5)의 알려진 값. 구현의 시프트 순서나 방향이 틀리면 여기서 바로 드러난다.
            var rng = new Rng(1u);

            Assert.AreEqual(270369u, rng.NextUInt());
            Assert.AreEqual(67634689u, rng.NextUInt());
            Assert.AreEqual(2647435461u, rng.NextUInt());
        }

        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new Rng(12345u);
            var b = new Rng(12345u);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new Rng(1u);
            var b = new Rng(2u);

            Assert.AreNotEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void RestoringSavedState_ContinuesTheSameSequence()
        {
            var original = new Rng(777u);
            for (int i = 0; i < 10; i++)
            {
                original.NextUInt();
            }

            uint saved = original.State;
            var restored = new Rng(1u);
            restored.Restore(saved);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(original.NextUInt(), restored.NextUInt());
            }
        }

        [Test]
        public void ZeroSeed_DoesNotGetStuck()
        {
            var rng = new Rng(0u);

            uint first = rng.NextUInt();
            uint second = rng.NextUInt();

            Assert.AreNotEqual(0u, first);
            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void RestoringZero_DoesNotGetStuck()
        {
            var rng = new Rng(5u);
            rng.Restore(0u);

            Assert.AreNotEqual(0u, rng.NextUInt());
        }

        [Test]
        public void NextFloat_IsAlwaysInZeroToOneExclusive()
        {
            var rng = new Rng(42u);

            for (int i = 0; i < 100000; i++)
            {
                float value = rng.NextFloat();
                Assert.IsTrue(value >= 0f && value < 1f, "값: " + value);
            }
        }

        [Test]
        public void NextFloat_MeanIsAboutHalf()
        {
            var rng = new Rng(42u);
            const int samples = 100000;
            double sum = 0.0;

            for (int i = 0; i < samples; i++)
            {
                sum += rng.NextFloat();
            }

            Assert.AreEqual(0.5, sum / samples, 0.01);
        }

        [Test]
        public void Range_IncludesBothEnds_AndNothingElse()
        {
            var rng = new Rng(9u);
            var counts = new int[3];

            for (int i = 0; i < 10000; i++)
            {
                int value = rng.Range(3, 5);
                Assert.IsTrue(value >= 3 && value <= 5, "값: " + value);
                counts[value - 3]++;
            }

            // 세 값이 고르게 나온다 (각 3,333회 안팎).
            foreach (int count in counts)
            {
                Assert.Greater(count, 3000);
                Assert.Less(count, 3700);
            }
        }

        [Test]
        public void Range_SingleValue_ReturnsIt()
        {
            var rng = new Rng(9u);

            Assert.AreEqual(4, rng.Range(4, 4));
        }

        [Test]
        public void Range_NegativeBounds_Work()
        {
            var rng = new Rng(9u);

            for (int i = 0; i < 1000; i++)
            {
                int value = rng.Range(-2, 1);
                Assert.IsTrue(value >= -2 && value <= 1, "값: " + value);
            }
        }

        [Test]
        public void Range_MaxBelowMin_Throws()
        {
            var rng = new Rng(9u);

            Assert.Throws<ArgumentException>(() => rng.Range(5, 3));
        }

        [Test]
        public void Chance_FollowsProbability()
        {
            var rng = new Rng(2024u);
            const int samples = 100000;
            int hits = 0;

            for (int i = 0; i < samples; i++)
            {
                if (rng.Chance(0.3f))
                {
                    hits++;
                }
            }

            Assert.AreEqual(0.3, hits / (double)samples, 0.01);
        }

        [Test]
        public void Chance_ZeroNeverHits_OneAlwaysHits()
        {
            var rng = new Rng(3u);

            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(rng.Chance(0f));
                Assert.IsTrue(rng.Chance(1f));
            }
        }

        [Test]
        public void Chance_AlwaysConsumesOneNumber()
        {
            // 확률 값과 상관없이 수열이 한 칸씩만 나아가야, 확률을 바꿔도 그 뒤의 결과가 흔들리지 않는다.
            var a = new Rng(11u);
            var b = new Rng(11u);

            a.Chance(0f);
            b.NextUInt();

            Assert.AreEqual(a.State, b.State);
        }

        [Test]
        public void PickWeighted_FollowsWeights()
        {
            var rng = new Rng(55u);
            var weights = new[] { 1f, 3f };
            const int samples = 100000;
            int first = 0;

            for (int i = 0; i < samples; i++)
            {
                if (rng.PickWeighted(weights) == 0)
                {
                    first++;
                }
            }

            Assert.AreEqual(0.25, first / (double)samples, 0.01);
        }

        [Test]
        public void PickWeighted_ZeroWeightIsNeverPicked()
        {
            var rng = new Rng(55u);
            var weights = new[] { 0f, 1f, 0f };

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(1, rng.PickWeighted(weights));
            }
        }

        [Test]
        public void PickWeighted_EmptyOrAllZero_ReturnsMinusOne_WithoutConsuming()
        {
            var rng = new Rng(55u);
            uint before = rng.State;

            Assert.AreEqual(-1, rng.PickWeighted(new float[0]));
            Assert.AreEqual(-1, rng.PickWeighted(new[] { 0f, 0f }));
            Assert.AreEqual(before, rng.State);
        }

        [Test]
        public void PickWeighted_NegativeWeightIsTreatedAsZero()
        {
            var rng = new Rng(55u);
            var weights = new[] { -5f, 2f };

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(1, rng.PickWeighted(weights));
            }
        }

        [Test]
        public void PickWeighted_Null_Throws()
        {
            var rng = new Rng(55u);

            Assert.Throws<ArgumentNullException>(() => rng.PickWeighted(null));
        }
    }
}
