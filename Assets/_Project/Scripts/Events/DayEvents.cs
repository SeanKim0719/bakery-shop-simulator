using BakeryShop.Core;

namespace BakeryShop.Events
{
    /// <summary>
    /// 하루 단계가 바뀌었다. 게임을 시작하거나 불러온 직후에는 Previous와 Current가 모두 Preparation으로 한 번 발행된다.
    /// </summary>
    public readonly struct DayPhaseChanged
    {
        /// <summary>단계가 속한 날. 정산 단계에서는 방금 끝난 날이고, 그다음 준비 단계에서는 새 날이다.</summary>
        public readonly int Day;
        public readonly DayPhase Previous;
        public readonly DayPhase Current;

        public DayPhaseChanged(int day, DayPhase previous, DayPhase current)
        {
            Day = day;
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>
    /// 하루 마무리 처리가 모두 끝나고 날짜가 올라갔다. 이 시점의 상태가 "다음 날 준비 단계의 시작"이고, 세이브가 여기서 저장한다.
    /// </summary>
    public readonly struct DayAdvanced
    {
        public readonly int NewDay;

        public DayAdvanced(int newDay)
        {
            NewDay = newDay;
        }
    }
}
