using UnityEngine;

namespace BakeryShop.Player
{
    /// <summary>이동 속도 계산. CharacterController와 분리해 단위 테스트한다.</summary>
    public static class PlayerMovementMath
    {
        /// <summary>
        /// 입력과 몸통 방향으로 수평 이동 속도(m/s)를 구한다.
        /// 대각선 입력이 더 빨라지지 않게 입력 크기를 1로 제한하고, 방향 벡터의 위아래 성분은 버린다.
        /// </summary>
        public static Vector3 PlanarVelocity(Vector2 input, Vector3 forward, Vector3 right, float speed)
        {
            input = Vector2.ClampMagnitude(input, 1f);

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return (forward * input.y + right * input.x) * speed;
        }

        /// <summary>
        /// 다음 프레임의 수직 속도(m/s).
        /// 땅에 붙어 있을 때는 작은 아래 방향 속도를 유지한다. 0으로 두면 CharacterController가
        /// 땅에서 살짝 떠 isGrounded가 프레임마다 깜빡이고, 내리막에서 통통 튄다.
        /// </summary>
        public static float NextVerticalVelocity(float current, bool grounded, float gravity, float groundedStickVelocity, float deltaTime)
        {
            if (grounded && current <= 0f)
            {
                return groundedStickVelocity;
            }

            return current + gravity * deltaTime;
        }
    }
}
