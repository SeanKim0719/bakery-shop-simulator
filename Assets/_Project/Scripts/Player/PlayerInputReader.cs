using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BakeryShop.Player
{
    /// <summary>
    /// Input System 액션을 읽어 다른 컴포넌트에 전달한다.
    /// 입력 모드(게임 / UI) 전환과 커서 잠금은 이 클래스의 SetMode 한 곳에서만 한다.
    /// 패널마다 커서를 직접 건드리면 "패널을 닫았는데 커서가 안 잠기는" 문제가 생기기 쉽다.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const string PlayerMapName = "Player";
        private const string GlobalMapName = "Global";

        [SerializeField] private InputActionAsset _actions;

        private InputActionMap _playerMap;
        private InputActionMap _globalMap;
        private InputAction _move;
        private InputAction _look;
        private InputAction _interact;
        private InputAction _drop;
        private InputAction _pause;

        /// <summary>상호작용 키(E / 좌클릭)를 누른 순간.</summary>
        public event Action InteractPressed;

        /// <summary>내려놓기 키(G)를 누른 순간.</summary>
        public event Action DropPressed;

        /// <summary>Esc. 입력 모드와 상관없이 항상 들어온다.</summary>
        public event Action PausePressed;

        public InputMode Mode { get; private set; } = InputMode.Gameplay;

        /// <summary>이동 입력 (-1 ~ 1). UI 모드에서는 0.</summary>
        public Vector2 Move => Mode == InputMode.Gameplay ? ReadVector2(_move) : Vector2.zero;

        /// <summary>이번 프레임의 마우스 이동량 (픽셀). UI 모드에서는 0.</summary>
        public Vector2 LookDelta => Mode == InputMode.Gameplay ? ReadVector2(_look) : Vector2.zero;

        private bool IsBound => _playerMap != null && _globalMap != null;

        private void Awake()
        {
            if (_actions == null)
            {
                Debug.LogError("[PlayerInputReader] InputActionAsset이 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            _playerMap = _actions.FindActionMap(PlayerMapName, throwIfNotFound: true);
            _globalMap = _actions.FindActionMap(GlobalMapName, throwIfNotFound: true);
            _move = _playerMap.FindAction("Move", throwIfNotFound: true);
            _look = _playerMap.FindAction("Look", throwIfNotFound: true);
            _interact = _playerMap.FindAction("Interact", throwIfNotFound: true);
            _drop = _playerMap.FindAction("Drop", throwIfNotFound: true);
            _pause = _globalMap.FindAction("Pause", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            if (!IsBound)
            {
                return;
            }

            _interact.performed += OnInteractPerformed;
            _drop.performed += OnDropPerformed;
            _pause.performed += OnPausePerformed;

            _globalMap.Enable();
            ApplyMode(Mode);
        }

        private void OnDisable()
        {
            if (!IsBound)
            {
                return;
            }

            _interact.performed -= OnInteractPerformed;
            _drop.performed -= OnDropPerformed;
            _pause.performed -= OnPausePerformed;

            _playerMap.Disable();
            _globalMap.Disable();
            ApplyCursor(InputMode.UI);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // 창을 벗어났다 돌아오면 OS가 커서 잠금을 풀어 둔다. 현재 모드 기준으로 다시 적용한다.
            if (hasFocus && isActiveAndEnabled && IsBound)
            {
                ApplyCursor(Mode);
            }
        }

        public void SetMode(InputMode mode)
        {
            if (Mode == mode)
            {
                return;
            }

            Mode = mode;
            if (isActiveAndEnabled && IsBound)
            {
                ApplyMode(mode);
            }
        }

        private void ApplyMode(InputMode mode)
        {
            if (mode == InputMode.Gameplay)
            {
                _playerMap.Enable();
            }
            else
            {
                _playerMap.Disable();
            }

            ApplyCursor(mode);
        }

        private static void ApplyCursor(InputMode mode)
        {
            bool gameplay = mode == InputMode.Gameplay;
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
        }

        private static Vector2 ReadVector2(InputAction action) =>
            action != null ? action.ReadValue<Vector2>() : Vector2.zero;

        private void OnInteractPerformed(InputAction.CallbackContext _) => InteractPressed?.Invoke();

        private void OnDropPerformed(InputAction.CallbackContext _) => DropPressed?.Invoke();

        private void OnPausePerformed(InputAction.CallbackContext _) => PausePressed?.Invoke();
    }
}
