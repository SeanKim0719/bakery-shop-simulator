using System;
using BakeryShop.Interaction;
using UnityEngine;

namespace BakeryShop.Player
{
    /// <summary>
    /// 플레이어가 들고 있는 물건 하나를 관리한다. 들기, 지정한 자리에 내려놓기, 앞에 떨어뜨리기.
    /// - 내려놓기 전에 그 자리가 비어 있는지 검사해서 벽·다른 물건·플레이어 몸에 박히지 않게 한다.
    /// - 들고 있는 동안 앞이 막히면 물건을 화면 아래쪽·몸 가까이로 접어 들어 벽·가구에 파고들지 않게 한다.
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

        [Tooltip("앞이 막혔을 때 물건을 접어 드는 위치 (카메라 기준, m). 화면 아래쪽이고 몸에 가까워야 한다. "
            + "눈 쪽으로 당기면 물건이 화면을 가린다.")]
        [SerializeField] private Vector3 _tuckedLocalPosition = new Vector3(0f, -0.5f, 0.2f);

        [Tooltip("막혔을 때 물체와 남겨 둘 틈 (m)")]
        [SerializeField, Min(0f)] private float _obstacleGap = 0.02f;

        [Tooltip("막힘이 풀렸을 때 원래 위치로 돌아가는 속도 (m/s)")]
        [SerializeField, Min(0.1f)] private float _returnSpeed = 3f;

        // 한 번의 BoxCast에서 받을 수 있는 최대 충돌 수. 넘치면 일부를 놓치지만 매장 규모에서는 충분하다.
        private readonly RaycastHit[] _castHits = new RaycastHit[16];

        private CharacterController _body;

        // 편 자세: 아무것도 막지 않을 때의 위치 (HoldAnchor의 처음 위치). 접은 자세: _tuckedLocalPosition.
        // _extension은 접은 자세에서 편 자세 쪽으로 나와 있는 거리. 0이면 완전히 접은 상태, _pathLength면 완전히 편 상태.
        private Vector3 _restLocalPosition;
        private float _pathLength;
        private float _extension;

        public IHoldable Held { get; private set; }

        /// <summary>손에 든 물건이 바뀔 때 (빈손이 되면 null).</summary>
        public event Action<IHoldable> HeldChanged;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();

            if (_holdAnchor != null)
            {
                _restLocalPosition = _holdAnchor.localPosition;
                _pathLength = Vector3.Distance(_tuckedLocalPosition, _restLocalPosition);
                _extension = _pathLength;
            }
        }

        private void LateUpdate()
        {
            // 플레이어 이동·시점(Update)이 끝난 뒤에 계산해야 이번 프레임 카메라 위치 기준으로 막힘을 판단한다.
            UpdateHoldPose(Time.deltaTime);
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
            ResetHoldPose();
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

        private void UpdateHoldPose(float deltaTime)
        {
            if (_holdAnchor == null)
            {
                return;
            }

            Transform view = _holdAnchor.parent;
            if (Held == null || view == null)
            {
                ResetHoldPose();
                return;
            }

            // 접은 자세에서 편 자세까지 물건 크기의 상자를 밀어 보고, 처음 막히는 곳 바로 앞에서 멈춘다.
            // 눈 → 물건 방향으로 당기지 않는 이유: 그 선의 끝은 눈이라서, 막힐수록 물건이 화면을 가린다
            // (탁자에 올려놓으려고 다가가면 앞이 보이지 않던 문제). 접은 자세는 화면 아래쪽이라 시야를 가리지 않는다.
            Vector3 tuckedWorld = view.TransformPoint(_tuckedLocalPosition);
            Vector3 restWorld = view.TransformPoint(_restLocalPosition);
            float blockedAt = FirstObstacleAlongPath(tuckedWorld, restWorld, Held.HalfExtents, view.rotation);

            _extension = HoldDistanceMath.Next(_extension, _pathLength, blockedAt, 0f, _returnSpeed, deltaTime);
            _holdAnchor.localPosition = HoldDistanceMath.PoseOnPath(_tuckedLocalPosition, _restLocalPosition, _extension);
        }

        /// <summary>
        /// from에서 to까지 상자를 밀었을 때 처음 막히는 거리 (틈을 뺀 값). 막히지 않으면 float.PositiveInfinity.
        /// 출발 자리에서 이미 겹쳐 있는 물체는 거리 0으로 돌아오므로, 접은 자세조차 막혀 있으면 0 이하가 된다.
        /// </summary>
        private float FirstObstacleAlongPath(Vector3 from, Vector3 to, Vector3 halfExtents, Quaternion orientation)
        {
            Vector3 path = to - from;
            float length = path.magnitude;
            if (length < 1e-4f)
            {
                return float.PositiveInfinity;
            }

            int count = Physics.BoxCastNonAlloc(from, halfExtents, path / length, _castHits, orientation,
                length, _holdObstacleMask, QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider hit = _castHits[i].collider;

                // 플레이어 자신의 캡슐은 장애물이 아니다. 접은 자세는 몸 가까이라 항상 캡슐과 겹친다.
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, _castHits[i].distance);
            }

            return float.IsPositiveInfinity(nearest) ? nearest : nearest - _obstacleGap;
        }

        private void ResetHoldPose()
        {
            _extension = _pathLength;
            if (_holdAnchor != null)
            {
                _holdAnchor.localPosition = _restLocalPosition;
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
