using System;
using BakeryShop.Interaction;
using UnityEngine;

namespace BakeryShop.Player
{
    /// <summary>
    /// 플레이어가 들고 있는 물건 하나를 관리한다. 들기, 지정한 자리에 내려놓기, 앞에 떨어뜨리기.
    /// 내려놓기 전에 그 자리가 비어 있는지 검사해서 벽이나 다른 물건 속에 박히지 않게 한다.
    /// </summary>
    public sealed class Hands : MonoBehaviour, IHands
    {
        [Tooltip("들고 있는 물건이 따라갈 위치. 보통 카메라 앞 아래쪽의 빈 오브젝트.")]
        [SerializeField] private Transform _holdAnchor;

        [Tooltip("내려놓을 자리를 막는 레이어")]
        [SerializeField] private LayerMask _placementBlockers = ~0;

        [Tooltip("공간 검사 때 물건 크기에 곱하는 비율. 1보다 조금 작게 해서 바닥에 딱 붙은 경우를 겹침으로 보지 않게 한다.")]
        [SerializeField, Range(0.5f, 1f)] private float _overlapTolerance = 0.95f;

        public IHoldable Held { get; private set; }

        /// <summary>손에 든 물건이 바뀔 때 (빈손이 되면 null).</summary>
        public event Action<IHoldable> HeldChanged;

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
        public bool CanPlace(Vector3 position, Quaternion rotation)
        {
            if (Held == null)
            {
                return false;
            }

            // 들고 있는 물건은 충돌이 꺼져 있어서 검사에 자기 자신이 걸리지 않는다.
            // 플레이어 캡슐은 걸리므로 발밑에 내려놓아 플레이어 몸에 박히는 일도 막힌다.
            Vector3 halfExtents = Held.HalfExtents * _overlapTolerance;
            return !Physics.CheckBox(position, halfExtents, rotation, _placementBlockers, QueryTriggerInteraction.Ignore);
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
            HeldChanged?.Invoke(null);
            return true;
        }

        /// <summary>
        /// 들고 있는 위치에서 그대로 놓아 떨어뜨린다. 벽 속처럼 공간이 막혀 있으면 놓지 않는다.
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
    }
}
