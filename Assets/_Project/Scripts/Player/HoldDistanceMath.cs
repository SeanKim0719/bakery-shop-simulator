using UnityEngine;

namespace BakeryShop.Player
{
    /// <summary>
    /// 들고 있는 물건의 위치 계산. 물건은 "접은 자세"(화면 아래쪽, 몸 가까이)와 "편 자세"(평소 위치)를 잇는
    /// 선 위에서만 움직인다. 앞이 막히면 접은 쪽으로 물러나 벽·가구에 파고들지 않게 한다.
    /// </summary>
    /// <remarks>
    /// 처음에는 눈 → 물건 방향으로 당겼다. 그 선의 끝이 눈이라서 막힐수록 물건이 화면을 가렸고,
    /// 탁자에 올려놓으려고 다가가면 앞이 보이지 않았다 (트러블슈팅 #2). 그래서 당기는 방향을 화면 아래쪽으로 바꿨다.
    /// </remarks>
    public static class HoldDistanceMath
    {
        /// <summary>
        /// 다음 프레임의 거리. 거리는 접은 자세에서 편 자세 쪽으로 나와 있는 길이다 (0 = 완전히 접음, rest = 완전히 폄).
        /// 막히면 그 자리로 즉시 물러나고(한 프레임이라도 파고들지 않게), 막힘이 풀리면 일정 속도로 돌아간다
        /// (순간이동처럼 튀어 보이지 않게).
        /// </summary>
        /// <param name="current">지금 거리 (m).</param>
        /// <param name="rest">아무것도 막지 않을 때의 거리 = 경로의 전체 길이 (m).</param>
        /// <param name="blockedAt">앞을 막는 물체 앞에서 멈출 수 있는 거리. 막히지 않았으면 float.PositiveInfinity.</param>
        /// <param name="min">아무리 막혀도 이보다 더 물러나지 않는다. 접은 자세까지 허용하려면 0.</param>
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

        /// <summary>
        /// 접은 자세에서 편 자세 쪽으로 distance만큼 간 위치. 경로의 양 끝을 벗어나지 않는다.
        /// </summary>
        public static Vector3 PoseOnPath(Vector3 tucked, Vector3 rest, float distance)
        {
            float length = Vector3.Distance(tucked, rest);
            if (length < 1e-6f)
            {
                return rest;
            }

            return Vector3.Lerp(tucked, rest, Mathf.Clamp01(distance / length));
        }
    }
}
