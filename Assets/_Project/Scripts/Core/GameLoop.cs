using System;

namespace BakeryShop.Core
{
    /// <summary>
    /// 한 프레임의 갱신 순서. 시스템마다 Update를 따로 두지 않고 여기서 정해진 순서로 한 번씩 부른다 (docs/design/01의 "갱신 순서").
    /// 순서가 코드 한 곳에 적혀 있어서 "오븐이 먼저 도는지 시간이 먼저 도는지"에 따라 결과가 달라지는 문제가 없다.
    /// </summary>
    public static class GameLoop
    {
        public static void Tick(GameContext context, float realDeltaSeconds)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // 1. 이번 프레임에 흐른 시간을 정한다 (일시정지면 0).
            context.Time.Advance(realDeltaSeconds);

            // 2. 폐점 시각이 되었는지 본다.
            context.Day.Tick();

            // 3. 배송 도착 (M3-03)
            // 4. 오븐 (M3-06)
            // 5. 손님 (M4-05)
        }
    }
}
