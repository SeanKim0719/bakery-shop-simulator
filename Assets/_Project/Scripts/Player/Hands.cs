using System;
using BakeryShop.Interaction;
using UnityEngine;

namespace BakeryShop.Player
{
    /// <summary>
    /// 플레이어가 들고 있는 물건 하나를 관리한다. 들기, 지정한 자리에 내려놓기, 앞에 떨어뜨리기.
    /// - 내려놓기 전에 그 자리가 비어 있는지 검사해서 벽·다른 물건·플레이어 몸에 박히지 않게 한다.
    /// - 들고 있는 동안 앞이 막히면 물건을 몸 쪽으로 당겨 벽·가구에 파고들지 않게 한다.
    /// </summary>
    public sealed class Hands : MonoBehaviour, IHands
    {
        [Tooltip("들고 있는 물건이 따라갈 위치. 카메라의 자식이어야 한다 (부모가 시야 기준이 된다).")]
        [SerializeField] private Transform _holdAnchor;

        [Header("내려놓기 공간 검사")]
        [Tooltip("내려놓을 자리를 막는 레이어")]
        [SerializeField] private LayerMask _placementBlockers = ~0;

        [Tooltip("공간 검사 때 물건 크기에 곱하는 비율. 1보다 조금 작게 해서 바닥·벽에 딱 붙은 경우를 겹침으로 보지 않게 한다.")]
        [SerializeField, Range(0.5f, 1f)] private float _overlapTolerance = 0.95f;

        [Header("들고 있을 때 벽 피하기")]
        [Tooltip("들고 있는 물건을 막는 레이어")]
        [SerializeField] private LayerMask _holdObstacleMask = ~0;

        [Tooltip("막혔을 때 물체와 남겨 둘 틈 (m)")]
        [SerializeField, Min(0f)] private float _obstacleGap = 0.02f;

        [Tooltip("아무리 막혀도 카메라에서 이 거리보다 가까이 당기지 않는다 (m)")]
        [SerializeField, Min(0.05f)] private float _minHoldDistance = 0.4f;

        [Tooltip("막힘이 풀렸을 때 원래 거리로 돌아가는 속도 (m/s)")]
        [SerializeField, Min(0.1f)] private float _returnSpeed = 3f;

        private CharacterController _body;
        private Vector3 _restDirection;
        private float _restDistance;
        private float _currentDistance;

        public IHoldable Held { get; private set; }

        /// <summary>손에 든 물건이 바뀔 때 (빈손이 되면 null).</summary>
        public event Action<IHoldable> HeldChanged;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();

            if (_holdAnchor != null)
            {
                Vector3 rest = _holdAnchor.localPosition;
                _restDistance = rest.magnitude;
                _restDirection = _restDistance > 0f ? rest / _restDistance : Vector3.forward;
                _currentDistance = _restDistance;
            }
        }

        private void LateUpdate()
        {
            // 플레이어 이동·시점(Update)이 끝난 뒤에 계산해야 이번 프레임 카메라 위치 기준으로 막힘을 판단한다.
            UpdateHoldDistance(Time.deltaTime);
        }

        public bool TryPickUp(IHoldable item)
        {
            if (item == null || Held != null || _holdAnchor == null)
            {
                return false;
            }

            Held = item;
            item.OnPickedUp(_holdAnchor);
            HeldChanged?.Invoke(item);
            return true;
        }

        /// <summary>손에 든 물건을 이 자세로 놓을 공간이 비어 있는지.</summary>
        /// <param name="rotation">좌우 회전만 있는 자세여야 한다.</param>
        public bool CanPlace(Vector3 position, Quaternion rotation)
        {
            if (Held == null)
            {
                return false;
            }

            Vector3 halfExtents = Held.HalfExtents;

            // 1. 플레이어 몸: 스킨까지 포함한 캡슐과 직접 비교한다 (PlacementMath 참고).
            if (OverlapsPlayer(position, rotation, halfExtents))
            {
                return false;
            }

            // 2. 벽·가구·다른 물건: 물리 쿼리. 들고 있는 물건은 충돌이 꺼져 있어서 자기 자신은 걸리지 않는다.
            return !Physics.CheckBox(position, halfExtents * _overlapTolerance, rotation, _placementBlockers, QueryTriggerInteraction.Ignore);
        }

        public bool TryPlace(Vector3 position, Quaternion rotation)
        {
            if (!CanPlace(position, rotation))
            {
                return false;
            }

            IHoldable item = Held;
            Held = null;
            item.OnDropped(position, rotation);
            ResetHoldDistance();
            HeldChanged?.Invoke(null);
            return true;
        }

        /// <summary>
        /// 들고 있는 위치에서 그대로 놓아 떨어뜨린다. 벽 속이나 몸과 겹치는 위치면 놓지 않는다.
        /// </summary>
        /// <param name="yaw">물건이 바라볼 좌우 각도. 보통 플레이어가 바라보는 방향.</param>
        public bool TryDrop(float yaw)
        {
            if (Held == null || _holdAnchor == null)
            {
                return false;
            }

            return TryPlace(_holdAnchor.position, Quaternion.Euler(0f, yaw, 0f));
        }

        private bool OverlapsPlayer(Vector3 position, Quaternion rotation, Vector3 halfExtents)
        {
            if (_body == null)
            {
                return false;
            }

            Vector3 capsuleCenter = transform.TransformPoint(_body.center);
            float inflatedRadius = _body.radius + _body.skinWidth;
            return PlacementMath.BoxOverlapsUprightCapsule(
                position, rotation.eulerAngles.y, halfExtents,
                capsuleCenter, _body.height, inflatedRadius);
        }

        private void UpdateHoldDistance(float deltaTime)
        {
            if (_holdAnchor == null)
            {
                return;
            }

            Transform view = _holdAnchor.parent;
            if (Held == null || view == null)
            {
                ResetHoldDistance();
                return;
            }

            // 물건 크기의 상자를 카메라에서 원래 위치 방향으로 밀어 보고, 막히는 곳 바로 앞에서 멈춘다.
            // 시작 지점에서 이미 겹친 콜라이더(플레이어 자신의 캡슐)는 BoxCast가 무시한다.
            Vector3 direction = view.TransformDirection(_restDirection);
            float blockedAt = float.PositiveInfinity;
            if (Physics.BoxCast(view.position, Held.HalfExtents, direction, out RaycastHit hit, view.rotation,
                    _restDistance, _holdObstacleMask, QueryTriggerInteraction.Ignore))
            {
                blockedAt = hit.distance - _obstacleGap;
            }

            _currentDistance = HoldDistanceMath.Next(_currentDistance, _restDistance, blockedAt, _minHoldDistance, _returnSpeed, deltaTime);
            _holdAnchor.localPosition = _restDirection * _currentDistance;
        }

        private void ResetHoldDistance()
        {
            _currentDistance = _restDistance;
            if (_holdAnchor != null)
            {
                _holdAnchor.localPosition = _restDirection * _restDistance;
            }
        }

        private void OnDrawGizmos()
        {
            // 들고 있을 때 "몸으로 보는 캡슐(스킨 포함)"을 Scene 뷰에 노란 선으로 그린다.
            if (Held == null || _body == null)
            {
                return;
            }

            Vector3 center = transform.TransformPoint(_body.center);
            float radius = _body.radius + _body.skinWidth;
            float segmentHalf = Mathf.Max(0f, _body.height * 0.5f - radius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center + Vector3.up * segmentHalf, radius);
            Gizmos.DrawWireSphere(center - Vector3.up * segmentHalf, radius);
        }
    }
}
