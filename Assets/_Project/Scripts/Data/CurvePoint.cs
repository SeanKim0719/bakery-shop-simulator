using System;

namespace BakeryShop.Data
{
    /// <summary>
    /// 꺾은선의 점 하나. content.json의 [[x, y], …] 목록이 이 타입의 배열로 들어온다.
    /// AnimationCurve 대신 쓰는 이유는 ADR 0009: JSON에 그대로 적을 수 있고, 시뮬레이터와 결과가 같다.
    /// </summary>
    [Serializable]
    public struct CurvePoint
    {
        public float X;
        public float Y;

        public CurvePoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
