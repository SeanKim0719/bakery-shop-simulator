using System;
using BakeryShop.Core;
using BakeryShop.Economy;
using BakeryShop.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BakeryShop.UI
{
    /// <summary>
    /// 화면 위쪽의 상시 표시: 날짜와 단계, 시계, 돈, 평판, 레벨.
    /// 값이 바뀌었다는 이벤트를 받았을 때만 글자를 다시 만든다 (매 프레임 문자열을 만들지 않는다).
    /// 시계만 예외로 매 프레임 읽되, 표시가 바뀌는 10분 단위에서만 글자를 고친다 (docs/design/10의 HUD 표).
    /// </summary>
    public sealed class HudView : MonoBehaviour, IContextConsumer
    {
        [Header("왼쪽 위")]
        [SerializeField] private TMP_Text _dayLabel;
        [SerializeField] private TMP_Text _clockLabel;

        [Header("오른쪽 위")]
        [SerializeField] private TMP_Text _moneyLabel;
        [SerializeField] private TMP_Text _moneyDeltaLabel;
        [SerializeField] private Image[] _reputationHalfPips = new Image[0];
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private RectTransform _xpFill;
        [SerializeField] private TMP_Text _xpLabel;

        [Header("모양")]
        [SerializeField] private Color _pipOnColor = new Color(1f, 0.82f, 0.25f);
        [SerializeField] private Color _pipOffColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color _gainColor = new Color(0.55f, 0.9f, 0.55f);
        [SerializeField] private Color _lossColor = new Color(1f, 0.55f, 0.5f);

        [Tooltip("돈이 바뀌었을 때 변화량(+3,600)이 떠 있는 시간(초).")]
        [SerializeField, Min(0f)] private float _moneyDeltaSeconds = 1.5f;

        private EventBus _bus;
        private TimeService _time;
        private DayCycle _day;
        private Wallet _wallet;
        private ReputationService _reputation;
        private ProgressionService _progression;

        private Action<DayPhaseChanged> _onPhaseChanged;
        private Action<MoneyChanged> _onMoneyChanged;
        private Action<ReputationChanged> _onReputationChanged;
        private Action<XpGained> _onXpGained;
        private Action<LevelUp> _onLevelUp;

        private int _shownClockBucket = -1;
        private float _moneyDeltaTimer;

        public void Init(GameContext context)
        {
            _bus = context.Bus;
            _time = context.Time;
            _day = context.Day;
            _wallet = context.Wallet;
            _reputation = context.Reputation;
            _progression = context.Progression;

            _onPhaseChanged = change => ShowDay();
            _onMoneyChanged = ShowMoneyChange;
            _onReputationChanged = change => ShowReputation();
            _onXpGained = gained => ShowProgression();
            _onLevelUp = levelUp => ShowProgression();

            _bus.Subscribe(_onPhaseChanged);
            _bus.Subscribe(_onMoneyChanged);
            _bus.Subscribe(_onReputationChanged);
            _bus.Subscribe(_onXpGained);
            _bus.Subscribe(_onLevelUp);

            // 첫 화면. 이벤트는 "바뀌었을 때"만 오므로 처음 값은 직접 읽는다.
            ShowDay();
            ShowClock();
            ShowMoney();
            ShowReputation();
            ShowProgression();
            SetActive(_moneyDeltaLabel, false);
        }

        private void OnDestroy()
        {
            if (_bus == null)
            {
                return;
            }

            _bus.Unsubscribe(_onPhaseChanged);
            _bus.Unsubscribe(_onMoneyChanged);
            _bus.Unsubscribe(_onReputationChanged);
            _bus.Unsubscribe(_onXpGained);
            _bus.Unsubscribe(_onLevelUp);
        }

        private void Update()
        {
            if (_time == null)
            {
                return;
            }

            ShowClock();

            if (_moneyDeltaTimer > 0f)
            {
                _moneyDeltaTimer -= Time.deltaTime;
                if (_moneyDeltaTimer <= 0f)
                {
                    SetActive(_moneyDeltaLabel, false);
                }
            }
        }

        private void ShowDay()
        {
            SetText(_dayLabel, HudFormat.DayLine(_day.Day, _day.Phase));
        }

        private void ShowClock()
        {
            int bucket = HudFormat.ClockBucket(_time.ShopClockMinutes);
            if (bucket == _shownClockBucket)
            {
                return;
            }

            _shownClockBucket = bucket;
            SetText(_clockLabel, HudFormat.Clock(_time.ShopClockMinutes));
        }

        private void ShowMoney()
        {
            SetText(_moneyLabel, MoneyFormat.Won(_wallet.Balance));
        }

        private void ShowMoneyChange(MoneyChanged change)
        {
            SetText(_moneyLabel, MoneyFormat.Won(change.Balance));

            if (_moneyDeltaLabel != null)
            {
                _moneyDeltaLabel.text = MoneyFormat.Signed(change.Delta);
                _moneyDeltaLabel.color = change.Delta >= 0 ? _gainColor : _lossColor;
                SetActive(_moneyDeltaLabel, true);
                _moneyDeltaTimer = _moneyDeltaSeconds;
            }
        }

        private void ShowReputation()
        {
            int lit = HudFormat.LitHalfPips(_reputation.Stars);
            for (int i = 0; i < _reputationHalfPips.Length; i++)
            {
                if (_reputationHalfPips[i] != null)
                {
                    _reputationHalfPips[i].color = i < lit ? _pipOnColor : _pipOffColor;
                }
            }
        }

        private void ShowProgression()
        {
            SetText(_levelLabel, HudFormat.LevelLabel(_progression.Level));
            SetText(_xpLabel, HudFormat.XpLine(_progression.Xp, _progression.XpForNextLevel));

            if (_xpFill != null)
            {
                float fill = HudFormat.XpFill(_progression.Xp, _progression.XpForCurrentLevel, _progression.XpForNextLevel);
                _xpFill.anchorMax = new Vector2(fill, 1f);
            }
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        private static void SetActive(TMP_Text label, bool active)
        {
            if (label != null && label.gameObject.activeSelf != active)
            {
                label.gameObject.SetActive(active);
            }
        }
    }
}
