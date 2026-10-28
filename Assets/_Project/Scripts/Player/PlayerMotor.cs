using UnityEngine;

namespace BakeryShop.Player
{
    /// <summary>
    /// 1인칭 이동과 시점. 몸통(이 오브젝트)은 좌우로만, 카메라 피벗은 위아래로만 돈다.
    /// 계산은 FirstPersonLookMath와 PlayerMovementMath에 맡기고, 여기서는 결과를 엔진에 적용만 한다.
    /// </summary>
    /// <remarks>
    /// 플레이어 이동·시점은 화면 프레임을 따르는 표현 계층이라 Time.deltaTime을 직접 쓴다.
    /// 오븐·배송·손님 같은 시뮬레이션은 3주차의 TimeService를 거친다 (docs/design/02).
    /// </remarks>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Transform _cameraPivot;

        [Header("이동")]
        [SerializeField, Min(0f)] private float _moveSpeed = 3.5f;
        [SerializeField] private float _gravity = -20f;
        [SerializeField] private float _groundedStickVelocity = -2f;

        [Header("시점")]
        [Tooltip("마우스 1픽셀당 회전 각도 (도)")]
        [SerializeField, Min(0f)] private float _lookSensitivity = 0.1f;
        [SerializeField, Range(-89f, 0f)] private float _minPitch = -80f;
        [SerializeField, Range(0f, 89f)] private float _maxPitch = 80f;

        private CharacterController _controller;
        private LookAngles _look;
        private float _verticalVelocity;

        /// <summary>몸통이 바라보는 좌우 각도. 물건을 내려놓을 때 플레이어 쪽을 향하게 하는 데 쓴다.</summary>
        public float Yaw => _look.Yaw;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            float startPitch = _cameraPivot != null
                ? FirstPersonLookMath.WrapDegrees(_cameraPivot.localEulerAngles.x)
                : 0f;
            _look = new LookAngles(FirstPersonLookMath.WrapDegrees(transform.eulerAngles.y), startPitch);
        }

        private void Update()
        {
            if (_input == null)
            {
                return;
            }

            UpdateLook();
            UpdateMovement(Time.deltaTime);
        }

        private void UpdateLook()
        {
            _look = FirstPersonLookMath.Apply(_look, _input.LookDelta, _lookSensitivity, _minPitch, _maxPitch);

            transform.rotation = Quaternion.Euler(0f, _look.Yaw, 0f);
            if (_cameraPivot != null)
            {
                _cameraPivot.localRotation = Quaternion.Euler(_look.Pitch, 0f, 0f);
            }
        }

        private void UpdateMovement(float deltaTime)
        {
            Vector3 planar = PlayerMovementMath.PlanarVelocity(_input.Move, transform.forward, transform.right, _moveSpeed);
            _verticalVelocity = PlayerMovementMath.NextVerticalVelocity(
                _verticalVelocity, _controller.isGrounded, _gravity, _groundedStickVelocity, deltaTime);

            _controller.Move((planar + Vector3.up * _verticalVelocity) * deltaTime);
        }
    }
}
