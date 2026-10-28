using UnityEngine;

namespace BakeryShop.Player
{
    /// <summary>1인칭 시점 각도. Yaw는 몸통(좌우), Pitch는 카메라(상하, 양수가 아래).</summary>
    public readonly struct LookAngles
    {
        public readonly float Yaw;
        public readonly float Pitch;

        public LookAngles(float yaw, float pitch)
        {
            Yaw = yaw;
            Pitch = pitch;
        }
    }

    /// <summary>시점 계산. 엔진 상태 없이 입력과 각도만 다뤄서 단위 테스트한다.</summary>
    public static class FirstPersonLookMath
    {
        /// <summary>
        /// 마우스 이동량을 시점 각도에 반영한다.
        /// 마우스 delta는 이미 "이번 프레임에 움직인 양"이라 deltaTime을 곱하지 않는다.
        /// </summary>
        /// <param name="delta">마우스 이동량 (픽셀). 위로 움직이면 y가 양수.</param>
        /// <param name="sensitivity">픽셀당 회전 각도 (도).</param>
        public static LookAngles Apply(LookAngles current, Vector2 delta, float sensitivity, float minPitch, float maxPitch)
        {
            float yaw = WrapDegrees(current.Yaw + delta.x * sensitivity);

            // Unity에서 x축 회전이 양수면 아래를 본다. 마우스를 위로 올리면 위를 봐야 하므로 뺀다.
            float pitch = Mathf.Clamp(current.Pitch - delta.y * sensitivity, minPitch, maxPitch);

            return new LookAngles(yaw, pitch);
        }

        /// <summary>
        /// 각도를 (-180, 180] 범위로 바꾼다.
        /// Unity의 localEulerAngles는 0~360으로 돌려주므로(-10도 → 350도), 시작 각도를 읽을 때 변환해야
        /// Pitch 제한이 엉뚱하게 걸리지 않는다.
        /// </summary>
        public static float WrapDegrees(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f)
            {
                degrees -= 360f;
            }
            else if (degrees <= -180f)
            {
                degrees += 360f;
            }

            return degrees;
        }
    }
}
