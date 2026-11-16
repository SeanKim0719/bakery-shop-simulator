using System;
using System.Collections.Generic;
using BakeryShop.Data;

namespace BakeryShop.Core
{
    /// <summary>
    /// 점 목록을 직선으로 이은 곡선의 계산. 가격 민감도와 시간대별 손님 수에 쓴다.
    /// 점은 X가 커지는 순서로 들어 있어야 한다 (ContentValidator가 확인한다).
    /// 밸런스 시뮬레이터(Tools/balance/simulate.py)의 curve와 같은 규칙이다.
    /// </summary>
    public static class PiecewiseCurve
    {
        /// <summary>
        /// x에서의 값. 첫 점보다 왼쪽은 첫 점의 Y, 마지막 점보다 오른쪽은 마지막 점의 Y, 그 사이는 직선 보간.
        /// </summary>
        public static float Evaluate(IReadOnlyList<CurvePoint> points, float x)
        {
            RequirePoints(points);

            if (x <= points[0].X)
            {
                return points[0].Y;
            }

            for (int i = 1; i < points.Count; i++)
            {
                CurvePoint right = points[i];
                if (x <= right.X)
                {
                    CurvePoint left = points[i - 1];
                    float width = right.X - left.X;
                    if (width <= 0f)
                    {
                        return right.Y;
                    }

                    return left.Y + (right.Y - left.Y) * ((x - left.X) / width);
                }
            }

            return points[points.Count - 1].Y;
        }

        /// <summary>
        /// from ~ to 구간의 평균값 (구간의 넓이 ÷ 길이). 구간이 점들의 범위를 벗어나면 양 끝의 Y가 이어진다고 본다.
        /// to가 from 이하이면 from에서의 값을 돌려준다.
        /// </summary>
        public static float Mean(IReadOnlyList<CurvePoint> points, float from, float to)
        {
            RequirePoints(points);

            if (to <= from)
            {
                return Evaluate(points, from);
            }

            // 곡선은 점과 점 사이에서 직선이므로, 구간을 점의 X에서 잘라 사다리꼴로 더하면 정확한 넓이가 된다.
            double area = 0.0;
            float previousX = from;
            float previousY = Evaluate(points, from);

            for (int i = 0; i < points.Count; i++)
            {
                float x = points[i].X;
                if (x <= from)
                {
                    continue;
                }

                if (x >= to)
                {
                    break;
                }

                float y = points[i].Y;
                area += (x - previousX) * (double)(previousY + y) * 0.5;
                previousX = x;
                previousY = y;
            }

            area += (to - previousX) * (double)(previousY + Evaluate(points, to)) * 0.5;
            return (float)(area / (to - from));
        }

        private static void RequirePoints(IReadOnlyList<CurvePoint> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Count == 0)
            {
                throw new ArgumentException("곡선에 점이 하나 이상 있어야 합니다.", nameof(points));
            }
        }
    }
}
