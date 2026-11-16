namespace BakeryShop.Core
{
    /// <summary>
    /// 하루가 끝날 때 한 번 실행되는 처리 (오븐 비우기, 빵 나이 먹이기, 임대료 …).
    /// DayCycle.AddDayEndStep에 순서 번호와 함께 등록하면 정산에 들어가는 순간 번호가 작은 것부터 불린다.
    /// </summary>
    public interface IDayEndStep
    {
        /// <param name="day">방금 끝난 날. 날짜는 모든 단계가 끝난 뒤에 올라간다.</param>
        void OnDayEnd(int day);
    }
}
