using System;
using System.Collections.Generic;
using BakeryShop.Events;

namespace BakeryShop.Core
{
    /// <summary>하루의 네 단계.</summary>
    public enum DayPhase
    {
        /// <summary>영업 준비. 영업 시계가 멈춰 있고 시간 제한이 없다.</summary>
        Preparation,

        /// <summary>영업 중. 영업 시계가 흐르고 손님이 온다.</summary>
        Open,

        /// <summary>마감. 새 손님은 오지 않고 남은 손님만 처리한다.</summary>
        Closing,

        /// <summary>정산. 하루 마무리 처리가 끝났고 리포트를 보여 준다.</summary>
        Settlement,
    }

    /// <summary>
    /// 하루 단계의 전환 규칙을 한 곳에 모은 상태 기계.
    /// 준비 → 영업 → 마감 → 정산 → (다음 날) 준비.
    /// 전환은 아래의 명령으로만 일어나고, 조건이 맞지 않으면 false를 돌려주고 아무것도 바꾸지 않는다.
    /// 다른 시스템은 단계를 직접 바꾸지 않고 DayPhaseChanged를 구독해서 자기 동작을 켜고 끈다.
    /// </summary>
    public sealed class DayCycle
    {
        private readonly struct StepEntry
        {
            public readonly int Order;
            public readonly IDayEndStep Step;

            public StepEntry(int order, IDayEndStep step)
            {
                Order = order;
                Step = step;
            }
        }

        private readonly TimeService _time;
        private readonly EventBus _bus;

        // order가 작은 것부터, 같으면 등록한 순서대로 정렬된 상태를 유지한다.
        private readonly List<StepEntry> _steps = new List<StepEntry>();

        private Func<bool> _isShopEmpty;
        private bool _hasBegun;
        private bool _isSettling;

        public DayCycle(TimeService time, EventBus bus)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            Day = 1;
            Phase = DayPhase.Preparation;
        }

        /// <summary>오늘이 며칠째인지. 1부터 시작한다.</summary>
        public int Day { get; private set; }

        public DayPhase Phase { get; private set; }

        /// <summary>지금 TrySettle이 성공하는지. PC의 '마감 정산' 버튼이 이 값으로 켜지고 꺼진다.</summary>
        public bool CanSettle => _hasBegun && Phase == DayPhase.Closing && IsShopEmpty();

        /// <summary>
        /// 하루 마무리 단계를 등록한다. order가 작은 것부터 실행되고, 같으면 등록한 순서를 따른다.
        /// 등록하는 코드의 순서가 바뀌어도 실행 순서가 바뀌지 않게 하려고 번호를 받는다.
        /// </summary>
        public void AddDayEndStep(int order, IDayEndStep step)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            if (_isSettling)
            {
                throw new InvalidOperationException("하루 마무리 처리 중에는 단계를 등록할 수 없습니다.");
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                if (ReferenceEquals(_steps[i].Step, step))
                {
                    throw new ArgumentException("이미 등록된 하루 마무리 단계입니다.", nameof(step));
                }
            }

            // 뒤에서부터 자리를 찾는다. 같은 order의 맨 뒤에 들어가므로 등록 순서가 유지된다.
            int index = _steps.Count;
            while (index > 0 && _steps[index - 1].Order > order)
            {
                index--;
            }

            _steps.Insert(index, new StepEntry(order, step));
        }

        /// <summary>
        /// 매장에 손님이 남아 있는지 묻는 함수를 연결한다 (M4에서 CustomerSystem이 연결한다).
        /// 연결하지 않았으면 항상 비어 있는 것으로 본다.
        /// </summary>
        public void SetShopEmptyCheck(Func<bool> isEmpty)
        {
            _isShopEmpty = isEmpty;
        }

        /// <summary>
        /// 세이브에서 날짜를 되돌린다. Begin보다 먼저 불러야 하고, 이벤트를 발행하지 않는다.
        /// 저장은 하루와 하루 사이에만 하므로 단계는 항상 준비 단계다.
        /// </summary>
        public void RestoreDay(int day)
        {
            if (_hasBegun)
            {
                throw new InvalidOperationException("RestoreDay는 Begin보다 먼저 불러야 합니다.");
            }

            if (day < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(day), "날짜는 1 이상이어야 합니다.");
            }

            Day = day;
        }

        /// <summary>
        /// 준비 단계의 시작을 알린다. 모든 시스템과 뷰가 구독을 마친 뒤에 부트스트랩이 한 번 부른다.
        /// 이 발행에서는 Previous와 Current가 모두 Preparation이다.
        /// </summary>
        public void Begin()
        {
            if (_hasBegun)
            {
                throw new InvalidOperationException("Begin은 한 번만 부를 수 있습니다.");
            }

            _hasBegun = true;
            _time.ResetClock();
            _bus.Publish(new DayPhaseChanged(Day, DayPhase.Preparation, DayPhase.Preparation));
        }

        /// <summary>개점: 준비 → 영업. 영업 시계가 흐르기 시작한다.</summary>
        public bool TryOpenShop()
        {
            RequireBegun();

            if (Phase != DayPhase.Preparation)
            {
                return false;
            }

            _time.StartClock();
            ChangePhase(DayPhase.Open);
            return true;
        }

        /// <summary>
        /// 프레임마다 TimeService.Advance 다음에 부른다. 영업 중에 폐점 시각이 되면 마감 단계로 넘긴다.
        /// </summary>
        public void Tick()
        {
            RequireBegun();

            if (Phase != DayPhase.Open || !_time.HasReachedClose)
            {
                return;
            }

            _time.StopClock();
            ChangePhase(DayPhase.Closing);
        }

        /// <summary>
        /// 마감 정산: 마감 → 정산. 매장에 손님이 남아 있으면 실패한다.
        /// 순서: ① 정산 단계로 바꾸고 알림 ② 하루 마무리 단계를 order 순서로 실행 ③ 날짜 +1 ④ DayAdvanced 발행.
        /// 모두 이 호출 안에서 끝난다.
        /// </summary>
        public bool TrySettle()
        {
            RequireBegun();

            if (!CanSettle)
            {
                return false;
            }

            int endedDay = Day;
            _isSettling = true;
            try
            {
                ChangePhase(DayPhase.Settlement);

                for (int i = 0; i < _steps.Count; i++)
                {
                    _steps[i].Step.OnDayEnd(endedDay);
                }

                Day = endedDay + 1;
            }
            finally
            {
                _isSettling = false;
            }

            _bus.Publish(new DayAdvanced(Day));
            return true;
        }

        /// <summary>다음 날: 정산 → 준비. 영업 시계를 개점 시각으로 되돌린다.</summary>
        public bool StartNextDay()
        {
            RequireBegun();

            if (_isSettling)
            {
                throw new InvalidOperationException("하루 마무리 처리가 끝나기 전에는 다음 날로 넘어갈 수 없습니다.");
            }

            if (Phase != DayPhase.Settlement)
            {
                return false;
            }

            _time.ResetClock();
            ChangePhase(DayPhase.Preparation);
            return true;
        }

        private bool IsShopEmpty()
        {
            return _isShopEmpty == null || _isShopEmpty();
        }

        // 상태를 먼저 바꾸고 나서 알린다. 구독자가 Phase를 읽었을 때 이미 새 값이어야 한다.
        private void ChangePhase(DayPhase next)
        {
            DayPhase previous = Phase;
            Phase = next;
            _bus.Publish(new DayPhaseChanged(Day, previous, next));
        }

        private void RequireBegun()
        {
            if (!_hasBegun)
            {
                throw new InvalidOperationException("Begin을 먼저 불러야 합니다.");
            }
        }
    }
}
