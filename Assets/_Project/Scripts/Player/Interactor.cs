using System;
using BakeryShop.Interaction;
using UnityEngine;

namespace BakeryShop.Player
{
    /// <summary>
    /// 매 프레임 화면 중앙으로 Raycast해서 "지금 E를 누르면 무엇을 하는지"를 정하고,
    /// 바뀔 때만 OptionChanged로 알린다 (HUD가 매 프레임 문자열을 다시 그리지 않게).
    /// 입력이 오면 같은 결정을 실행한다.
    /// </summary>
    public sealed class Interactor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Hands _hands;
        [SerializeField] private PlayerMotor _motor;

        [Tooltip("Raycast 시작점과 방향. 보통 카메라.")]
        [SerializeField] private Transform _viewOrigin;

        [SerializeField, Min(0.1f)] private float _reach = 2.5f;
        [SerializeField] private LayerMask _raycastMask = ~0;

        [Tooltip("이 값 이상으로 위를 향한 면에만 내려놓을 수 있다 (1 = 완전히 수평).")]
        [SerializeField, Range(0f, 1f)] private float _minSurfaceUpDot = 0.9f;

        [Tooltip("내려놓을 때 면에서 띄우는 높이 (m). 0이면 면과 겹쳐서 공간 검사가 실패할 수 있다.")]
        [SerializeField, Min(0f)] private float _placementLift = 0.01f;

        private InteractionOption _current;
        private Vector3 _placePosition;
        private Quaternion _placeRotation;

        public InteractionOption Current => _current;

        public event Action<InteractionOption> OptionChanged;

        private void OnEnable()
        {
            if (_input == null)
            {
                return;
            }

            _input.InteractPressed += HandleInteract;
            _input.DropPressed += HandleDrop;
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.InteractPressed -= HandleInteract;
                _input.DropPressed -= HandleDrop;
            }

            SetOption(InteractionOption.None);
        }

        private void Update()
        {
            if (_viewOrigin == null || _hands == null)
            {
                return;
            }

            if (_input != null && _input.Mode != InputMode.Gameplay)
            {
                SetOption(InteractionOption.None);
                return;
            }

            SetOption(Evaluate());
        }

        private InteractionOption Evaluate()
        {
            var context = new InteractionContext(_hands);
            IInteractable target = null;
            bool canPlace = false;

            if (Physics.Raycast(_viewOrigin.position, _viewOrigin.forward, out RaycastHit hit, _reach, _raycastMask, QueryTriggerInteraction.Ignore))
            {
                // 콜라이더가 자식 오브젝트에 있어도 찾을 수 있게 부모 방향으로 찾는다.
                target = hit.collider.GetComponentInParent<IInteractable>();

                IHoldable held = context.Held;
                if (held != null && hit.normal.y >= _minSurfaceUpDot)
                {
                    _placePosition = hit.point + Vector3.up * (held.HalfExtents.y + _placementLift);
                    _placeRotation = Quaternion.Euler(0f, CurrentYaw(), 0f);
                    canPlace = _hands.CanPlace(_placePosition, _placeRotation);
                }
            }

            return InteractionResolver.Resolve(target, context, canPlace);
        }

        private void HandleInteract()
        {
            switch (_current.Kind)
            {
                case InteractionKind.Target:
                    _current.Target.Interact(new InteractionContext(_hands));
                    break;
                case InteractionKind.Place:
                    _hands.TryPlace(_placePosition, _placeRotation);
                    break;
            }

            // 상태가 바뀌었을 수 있으니 다음 프레임을 기다리지 않고 바로 다시 판단한다.
            if (isActiveAndEnabled && _viewOrigin != null && _hands != null)
            {
                SetOption(Evaluate());
            }
        }

        private void HandleDrop()
        {
            if (_hands != null)
            {
                _hands.TryDrop(CurrentYaw());
            }
        }

        private float CurrentYaw() => _motor != null ? _motor.Yaw : transform.eulerAngles.y;

        private void SetOption(InteractionOption option)
        {
            if (option == _current)
            {
                return;
            }

            _current = option;
            OptionChanged?.Invoke(option);
        }
    }
}
