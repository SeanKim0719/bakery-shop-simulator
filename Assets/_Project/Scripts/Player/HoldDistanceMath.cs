using UnityEngine;

namespace BakeryShop.Player
{
    /// <summary>
    /// 들고 있는 물건과 카메라 사이 거리 계산. 앞이 막히면 물건을 몸 쪽으로 당겨 벽·가구에 파고들지 않게 한다.
    /// </summary>
    public static class HoldDistanceMath
    {
        /// <summary>
        /// 다음 프레임의 거리.
        /// 막히면 그 자리로 즉시 당기고(한 프레임이라도 파고들지 않게), 막힘이 풀리면 일정 속도로 원래 거리로 돌아간다
        /// (순간이동처럼 튀어 보이지 않게).
        /// </summary>
        /// <param name="current">지금 거리 (m).</param>
        /// <param name="rest">아무것도 없을 때의 거리 (m).</param>
        /// <param name="blockedAt">앞을 막는 물체 앞에서 멈출 수 있는 거리. 막히지 않았으면 float.PositiveInfinity.</param>
        /// <param name="min">아무리 막혀도 이보다 가까이 당기지 않는다. 화면을 다 가리거나 카메라 안으로 들어오는 것을 막는다.</param>
        /// <param name="returnSpeed">막힘이 풀렸을 때 돌아가는 속도 (m/s).</param>
        public static float Next(float current, float rest, float blockedAt, float min, float returnSpeed, float deltaTime)
        {
            float target = Mathf.Clamp(Mathf.Min(rest, blockedAt), min, rest);

            if (target < current)
            {
                return target;
            }

            return Mathf.MoveTowards(current, target, returnSpeed * deltaTime);
        }
    }
}
