using System;

namespace BakeryShop.Core
{
    /// <summary>
    /// 게임의 시계 두 개.
    /// <list type="bullet">
    /// <item>SimTime: 일시정지가 아니면 항상 흐른다. 오븐, 손님의 인내심 같은 "몇 초 뒤"가 이 시계를 쓴다.</item>
    /// <item>영업 시계: 개점부터 폐점까지만 흐른다. 화면의 09:00 ~ 18:00과 시간대별 손님 수가 이 시계를 쓴다.</item>
    /// </list>
    /// Time.deltaTime을 직접 읽는 코드는 GameBootstrap 한 곳뿐이고, 나머지는 모두 여기의 SimDelta를 쓴다.
    /// 그래서 테스트에서 Advance만 불러 시간을 마음대로 흘릴 수 있다.
    /// </summary>
    public sealed class TimeService
    {
        /// <summary>한 번의 Advance에서 흐를 수 있는 최대 시간(초).</summary>
        public const float MaxDeltaSeconds = 0.25f;

        private readonly int _openMinutes;
        private readonly int _closeMinutes;
        private readonly double _clockMinutesPerSecond;

        // 영업 시계는 내부에서 double로 쌓는다. 프레임마다 작은 값을 수만 번 더하기 때문에
        // float로 쌓으면 하루가 끝날 때 폐점 시각이 몇 초씩 어긋난다.
        private double _shopClockMinutes;

        /// <param name="openMinutes">개점 시각. 자정부터의 분 (09:00 = 540).</param>
        /// <param name="closeMinutes">폐점 시각. 자정부터의 분 (18:00 = 1080).</param>
        /// <param name="openRealSeconds">개점부터 폐점까지 걸리는 실제 시간(초).</param>
        public TimeService(int openMinutes, int closeMinutes, float openRealSeconds)
        {
            if (closeMinutes <= openMinutes)
            {
                throw new ArgumentException("폐점 시각은 개점 시각보다 늦어야 합니다.", nameof(closeMinutes));
            }

            if (!(openRealSeconds > 0f) || float.IsInfinity(openRealSeconds))
            {
                throw new ArgumentException("영업 시간의 실제 길이는 0보다 커야 합니다.", nameof(openRealSeconds));
            }

            _openMinutes = openMinutes;
            _closeMinutes = closeMinutes;
            _clockMinutesPerSecond = (closeMinutes - openMinutes) / (double)openRealSeconds;
            _shopClockMinutes = openMinutes;
        }

        /// <summary>
        /// 게임이 시작된 뒤 흐른 시간(초). double인 이유: float는 10시간쯤 지나면 한 칸이 0.004초가 되어
        /// 한 프레임(0.0167초)을 제대로 더하지 못한다.
        /// </summary>
        public double SimTime { get; private set; }

        /// <summary>이번 Advance에서 흐른 시간(초). 일시정지 중에는 0.</summary>
        public float SimDelta { get; private set; }

        /// <summary>영업 시계. 자정부터의 분이고 개점 시각 이상 폐점 시각 이하다.</summary>
        public float ShopClockMinutes => (float)_shopClockMinutes;

        public bool IsPaused { get; private set; }

        public bool IsClockRunning { get; private set; }

        public bool HasReachedClose => _shopClockMinutes >= _closeMinutes;

        /// <summary>
        /// 한 프레임만큼 시간을 흘린다. 프레임마다 한 번, GameBootstrap이 부른다.
        /// 음수와 NaN은 0으로, MaxDeltaSeconds보다 큰 값은 MaxDeltaSeconds로 자른다
        /// (창을 옮기거나 멈췄다 돌아왔을 때 시간이 한꺼번에 건너뛰지 않게).
        /// </summary>
        public void Advance(float realDeltaSeconds)
        {
            if (IsPaused)
            {
                SimDelta = 0f;
                return;
            }

            SimDelta = ClampDelta(realDeltaSeconds);
            SimTime += SimDelta;

            if (IsClockRunning)
            {
                _shopClockMinutes += SimDelta * _clockMinutesPerSecond;
                if (_shopClockMinutes > _closeMinutes)
                {
                    _shopClockMinutes = _closeMinutes;
                }
            }
        }

        /// <summary>일시정지. 두 시계가 모두 멈추고 SimDelta가 바로 0이 된다.</summary>
        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            if (paused)
            {
                SimDelta = 0f;
            }
        }

        /// <summary>개점: 영업 시계가 지금 자리에서 흐르기 시작한다. 하루의 시작에는 ResetClock이 개점 시각으로 되돌려 둔다.</summary>
        public void StartClock()
        {
            IsClockRunning = true;
        }

        /// <summary>폐점: 영업 시계가 그 자리에서 멈춘다.</summary>
        public void StopClock()
        {
            IsClockRunning = false;
        }

        /// <summary>다음 날: 영업 시계를 개점 시각으로 되돌리고 멈춘다.</summary>
        public void ResetClock()
        {
            IsClockRunning = false;
            _shopClockMinutes = _openMinutes;
        }

        /// <summary>세이브에서 SimTime을 되돌린다. 저장은 하루와 하루 사이에만 하므로 영업 시계는 따로 복원하지 않는다.</summary>
        public void RestoreSimTime(double simTime)
        {
            if (double.IsNaN(simTime) || double.IsInfinity(simTime) || simTime < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(simTime), "SimTime은 0 이상의 유한한 값이어야 합니다.");
            }

            SimTime = simTime;
        }

        private static float ClampDelta(float realDeltaSeconds)
        {
            // NaN은 모든 비교가 거짓이라서 "0보다 큰가"를 먼저 물으면 음수와 함께 걸러진다.
            if (!(realDeltaSeconds > 0f))
            {
                return 0f;
            }

            return realDeltaSeconds > MaxDeltaSeconds ? MaxDeltaSeconds : realDeltaSeconds;
        }
    }
}
