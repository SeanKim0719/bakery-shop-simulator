using BakeryShop.Economy;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace BakeryShop.Core
{
    /// <summary>
    /// 개발용 단축키. 에디터와 개발 빌드에서만 동작한다 (출시 빌드에서는 컴포넌트가 있어도 아무 일도 하지 않는다).
    /// F5: 영업 시계를 폐점 1분 전으로 / F6: 돈 +100,000 / F7: 경험치 +50
    /// 하루를 끝까지 기다리지 않고 마감·정산·레벨 업을 확인하려고 둔다.
    /// </summary>
    public sealed class DevCheats : MonoBehaviour, IContextConsumer
    {
        private const int MoneyAmount = 100000;
        private const int XpAmount = 50;
        private const float MinutesBeforeClose = 1f;

        // 건너뛰기가 끝나지 않을 때(일시정지 등)를 대비한 반복 한도.
        private const int MaxSkipTicks = 10000;

        private GameContext _context;

        public void Init(GameContext context)
        {
            _context = context;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (_context == null || keyboard == null)
            {
                return;
            }

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                SkipToJustBeforeClose();
            }

            if (keyboard.f6Key.wasPressedThisFrame)
            {
                _context.Wallet.Add(MoneyAmount, MoneyReason.Subsidy);
            }

            if (keyboard.f7Key.wasPressedThisFrame)
            {
                _context.Progression.AddXp(XpAmount);
            }
        }

        // 시계만 고쳐 쓰지 않고 GameLoop를 빠르게 여러 번 돌린다.
        // 그 사이의 일(오븐, 손님)도 실제로 흘러간 것처럼 처리되어, 건너뛴 뒤의 상태가 앞뒤가 맞는다.
        private void SkipToJustBeforeClose()
        {
            if (_context.Day.Phase != DayPhase.Open)
            {
                Debug.Log("[DevCheats] F5는 영업 중에만 동작합니다.");
                return;
            }

            float target = _context.Content.Config.Day.CloseMinutes - MinutesBeforeClose;
            int ticks = 0;
            while (_context.Time.ShopClockMinutes < target && _context.Day.Phase == DayPhase.Open && ticks < MaxSkipTicks)
            {
                GameLoop.Tick(_context, TimeService.MaxDeltaSeconds);
                ticks++;
            }

            Debug.Log("[DevCheats] 영업 시계를 폐점 " + MinutesBeforeClose + "분 전으로 건너뛰었습니다 (" + ticks + "틱).");
        }
#endif
    }
}
