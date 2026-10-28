using BakeryShop.Player;
using NUnit.Framework;
using UnityEngine;

namespace BakeryShop.Tests
{
    public class PlayerMovementMathTests
    {
        private const float Tolerance = 1e-4f;
        private const float Speed = 3.5f;

        [Test]
        public void NoInput_NoMovement()
        {
            Vector3 velocity = PlayerMovementMath.PlanarVelocity(Vector2.zero, Vector3.forward, Vector3.right, Speed);

            Assert.AreEqual(0f, velocity.magnitude, Tolerance);
        }

        [Test]
        public void Forward_MovesAlongBodyForwardAtFullSpeed()
        {
            Vector3 velocity = PlayerMovementMath.PlanarVelocity(new Vector2(0f, 1f), Vector3.forward, Vector3.right, Speed);

            Assert.AreEqual(Speed, velocity.z, Tolerance);
            Assert.AreEqual(0f, velocity.x, Tolerance);
        }

        [Test]
        public void Diagonal_IsNotFasterThanStraight()
        {
            Vector3 velocity = PlayerMovementMath.PlanarVelocity(new Vector2(1f, 1f), Vector3.forward, Vector3.right, Speed);

            Assert.AreEqual(Speed, velocity.magnitude, Tolerance);
        }

        [Test]
        public void TiltedForward_StaysOnGroundPlane()
        {
            Vector3 tilted = new Vector3(0f, 0.6f, 0.8f);

            Vector3 velocity = PlayerMovementMath.PlanarVelocity(new Vector2(0f, 1f), tilted, Vector3.right, Speed);

            Assert.AreEqual(0f, velocity.y, Tolerance);
            Assert.AreEqual(Speed, velocity.magnitude, Tolerance);
        }

        [Test]
        public void UsesGivenBodyBasis()
        {
            // 몸통이 오른쪽(+x)을 보고 있으면 앞으로 가기는 +x 방향이다.
            Vector3 velocity = PlayerMovementMath.PlanarVelocity(new Vector2(0f, 1f), Vector3.right, Vector3.back, Speed);

            Assert.AreEqual(Speed, velocity.x, Tolerance);
            Assert.AreEqual(0f, velocity.z, Tolerance);
        }

        [Test]
        public void Grounded_KeepsSmallDownwardVelocity()
        {
            float next = PlayerMovementMath.NextVerticalVelocity(-15f, grounded: true, gravity: -20f, groundedStickVelocity: -2f, deltaTime: 0.016f);

            Assert.AreEqual(-2f, next, Tolerance);
        }

        [Test]
        public void Airborne_AccumulatesGravity()
        {
            float next = PlayerMovementMath.NextVerticalVelocity(-1f, grounded: false, gravity: -20f, groundedStickVelocity: -2f, deltaTime: 0.1f);

            Assert.AreEqual(-3f, next, Tolerance);
        }

        [Test]
        public void GroundedButMovingUp_IsNotSnappedDown()
        {
            // 나중에 점프나 밀려 올라가는 경우를 위해, 위로 움직이는 중에는 땅에 붙이지 않는다.
            float next = PlayerMovementMath.NextVerticalVelocity(5f, grounded: true, gravity: -20f, groundedStickVelocity: -2f, deltaTime: 0.1f);

            Assert.AreEqual(3f, next, Tolerance);
        }
    }
}
