using UnityEngine;

namespace BakeryShop.Player
{
    /// <summary>
    /// 내려놓을 상자가 플레이어 몸(세워진 캡슐)과 겹치는지 계산한다.
    /// </summary>
    /// <remarks>
    /// 물리 쿼리(Physics.CheckBox)는 CharacterController의 캡슐(반지름)만 보고 스킨(skinWidth)은 보지 않는다.
    /// 게다가 상자는 0.95배로 줄여 검사한다. 그래서 쿼리로는 "안 겹침"이어도 상자가 몸 가장자리나 스킨 안쪽에
    /// 들어올 수 있고, 이때 CharacterController가 상자 위로 올라타는 것으로 추정한다 (트러블슈팅 가설 1).
    /// 플레이어 몸만큼은 쿼리에 맡기지 않고 스킨까지 포함한 크기로 직접 계산해서 막는다.
    /// </remarks>
    public static class PlacementMath
    {
        /// <summary>
        /// 세워진 상자(좌우 회전만 있음)와 세워진 캡슐이 겹치는지.
        /// 둘 다 세워져 있으므로 거리를 수평(XZ) 거리와 수직(Y) 간격으로 나눠 구할 수 있다.
        /// </summary>
        /// <param name="boxCenter">상자 중심 (월드).</param>
        /// <param name="boxYawDegrees">상자의 좌우 회전 (도).</param>
        /// <param name="boxHalfExtents">회전 전 기준 반 크기.</param>
        /// <param name="capsuleCenter">캡슐 중심 (월드).</param>
        /// <param name="capsuleHeight">캡슐 전체 높이 (양 끝 반구 포함).</param>
        /// <param name="capsuleRadius">캡슐 반지름. CharacterController라면 반지름 + 스킨을 넣는다.</param>
        public static bool BoxOverlapsUprightCapsule(
            Vector3 boxCenter, float boxYawDegrees, Vector3 boxHalfExtents,
            Vector3 capsuleCenter, float capsuleHeight, float capsuleRadius)
        {
            // 캡슐 = 수직 선분에서 반지름 이내인 점들. 선분 끝은 양 끝 반구의 중심이다.
            float segmentHalf = Mathf.Max(0f, capsuleHeight * 0.5f - capsuleRadius);
            float segmentBottom = capsuleCenter.y - segmentHalf;
            float segmentTop = capsuleCenter.y + segmentHalf;

            // 수직 간격: 선분의 높이 구간과 상자의 높이 구간 사이 틈. 겹치면 0.
            float boxBottom = boxCenter.y - boxHalfExtents.y;
            float boxTop = boxCenter.y + boxHalfExtents.y;
            float gapY = Mathf.Max(0f, Mathf.Max(boxBottom - segmentTop, segmentBottom - boxTop));

            // 수평 거리: 캡슐 축을 상자 기준 좌표로 옮겨 상자 사각형까지의 최단 거리를 구한다.
            float dx = capsuleCenter.x - boxCenter.x;
            float dz = capsuleCenter.z - boxCenter.z;
            float radians = boxYawDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            float localX = dx * cos - dz * sin;
            float localZ = dx * sin + dz * cos;

            float outsideX = Mathf.Max(0f, Mathf.Abs(localX) - boxHalfExtents.x);
            float outsideZ = Mathf.Max(0f, Mathf.Abs(localZ) - boxHalfExtents.z);

            float distanceSquared = outsideX * outsideX + outsideZ * outsideZ + gapY * gapY;
            return distanceSquared < capsuleRadius * capsuleRadius;
        }
    }
}
