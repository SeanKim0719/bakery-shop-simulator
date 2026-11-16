using System;
using BakeryShop.Core;
using BakeryShop.Player;
using UnityEngine;

namespace BakeryShop.UI
{
    /// <summary>
    /// 패널을 여닫는 한 곳. 패널이 열리면 입력을 UI 모드로(커서 풀림), 닫히면 게임 모드로(커서 잠김) 바꾼다.
    /// 패널마다 커서를 직접 건드리면 "패널을 닫았는데 커서가 안 잠긴다"나 "패널이 두 개 겹쳐 열린다" 같은 문제가 생긴다.
    /// 그래서 패널은 스스로 열리지 않고 항상 여기를 거친다.
    /// </summary>
    public sealed class PanelHost : MonoBehaviour, IContextConsumer
    {
        private PlayerInputReader _input;

        /// <summary>지금 열려 있는 패널. 없으면 null.</summary>
        public IPanel Current { get; private set; }

        public bool IsOpen => Current != null;

        public void Init(GameContext context)
        {
            _input = context.Input;
        }

        /// <summary>패널을 연다. 다른 패널이 열려 있으면 먼저 닫는다.</summary>
        public void Open(IPanel panel)
        {
            if (panel == null)
            {
                throw new ArgumentNullException(nameof(panel));
            }

            if (ReferenceEquals(Current, panel))
            {
                return;
            }

            if (Current != null)
            {
                Current.Hide();
            }

            Current = panel;
            panel.Show();
            SetMode(InputMode.UI);
        }

        public void Close()
        {
            if (Current == null)
            {
                return;
            }

            IPanel closing = Current;
            Current = null;
            closing.Hide();
            SetMode(InputMode.Gameplay);
        }

        /// <summary>
        /// Esc가 눌렸을 때 부른다. 패널이 열려 있었으면 true를 돌려주고 (닫을 수 있는 패널이면 닫는다),
        /// 열린 패널이 없었으면 false를 돌려준다 (그러면 부른 쪽이 일시정지 메뉴를 연다).
        /// </summary>
        public bool HandleEscape()
        {
            if (Current == null)
            {
                return false;
            }

            if (Current.CanCloseWithEscape)
            {
                Close();
            }

            return true;
        }

        private void SetMode(InputMode mode)
        {
            if (_input != null)
            {
                _input.SetMode(mode);
            }
        }
    }
}
