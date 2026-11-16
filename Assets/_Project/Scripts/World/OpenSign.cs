using System;
using BakeryShop.Core;
using BakeryShop.Events;
using BakeryShop.Interaction;
using TMPro;
using UnityEngine;

namespace BakeryShop.World
{
    /// <summary>
    /// 문 옆의 영업 팻말. 준비 단계에 빈손으로 E를 누르면 영업을 시작한다.
    /// 하루를 시작하는 방법을 메뉴 버튼이 아니라 매장 안의 물건으로 둔 것은, 준비를 마치고 문 쪽으로 걸어가 팻말을 돌리는 동작이
    /// "이제 연다"는 결정이 되게 하려는 것이다.
    /// </summary>
    public sealed class OpenSign : MonoBehaviour, IContextConsumer, IInteractable
    {
        private const string OpenPrompt = "영업 시작";

        // URP Lit 재질의 색 속성 이름.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private TMP_Text _label;
        [SerializeField] private Renderer _board;
        [SerializeField] private Color _preparingColor = new Color(0.75f, 0.25f, 0.2f);
        [SerializeField] private Color _openColor = new Color(0.2f, 0.6f, 0.3f);
        [SerializeField] private Color _closedColor = new Color(0.35f, 0.35f, 0.38f);

        private DayCycle _day;
        private EventBus _bus;
        private Action<DayPhaseChanged> _onPhaseChanged;
        private MaterialPropertyBlock _block;

        public void Init(GameContext context)
        {
            _day = context.Day;
            _bus = context.Bus;
            _onPhaseChanged = OnPhaseChanged;
            _bus.Subscribe(_onPhaseChanged);
            Show(_day.Phase);
        }

        private void OnDestroy()
        {
            if (_bus != null && _onPhaseChanged != null)
            {
                _bus.Unsubscribe(_onPhaseChanged);
            }
        }

        public string GetPrompt(in InteractionContext context)
        {
            // 영업을 시작할 수 있을 때만 안내가 뜬다. 영업 중에는 팻말을 봐도 할 일이 없다.
            bool canOpen = _day != null && _day.Phase == DayPhase.Preparation && context.Held == null;
            return canOpen ? OpenPrompt : null;
        }

        public void Interact(in InteractionContext context)
        {
            if (_day != null)
            {
                _day.TryOpenShop();
            }
        }

        private void OnPhaseChanged(DayPhaseChanged change)
        {
            Show(change.Current);
        }

        private void Show(DayPhase phase)
        {
            string text;
            Color color;
            switch (phase)
            {
                case DayPhase.Preparation:
                    text = "준비 중";
                    color = _preparingColor;
                    break;
                case DayPhase.Open:
                    text = "영업 중";
                    color = _openColor;
                    break;
                default:
                    text = "영업 종료";
                    color = _closedColor;
                    break;
            }

            if (_label != null)
            {
                _label.text = text;
            }

            if (_board != null)
            {
                // 재질 에셋을 건드리지 않고 이 오브젝트의 색만 바꾼다.
                if (_block == null)
                {
                    _block = new MaterialPropertyBlock();
                }

                _board.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _board.SetPropertyBlock(_block);
            }
        }
    }
}
