using UnityEngine;

namespace BakeryShop.Interaction
{
    /// <summary>
    /// 바닥에 놓여 있다가 빈손으로 집어 들 수 있는 물리 물건. 배송 박스와 빵 트레이가 이 컴포넌트를 쓴다.
    /// 박스 내용물, 트레이의 빵 같은 데이터는 3주차부터 별도 컴포넌트가 맡는다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class HoldableObject : MonoBehaviour, IInteractable, IHoldable
    {
        [SerializeField] private HoldableKind _kind = HoldableKind.Box;
        [SerializeField] private string _displayName = "박스";

        private Rigidbody _body;
        private BoxCollider _box;
        private Collider[] _colliders;

        public HoldableKind Kind => _kind;
        public string DisplayName => _displayName;
        public bool IsHeld { get; private set; }

        public Vector3 HalfExtents => Vector3.Scale(_box.size, transform.lossyScale) * 0.5f;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _box = GetComponent<BoxCollider>();
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        }

        public string GetPrompt(in InteractionContext context)
        {
            // 빈손일 때만 들 수 있다. 무언가 들고 있으면 null을 돌려주고, 판단은 Interactor의 '내려놓기(쌓기)'로 넘어간다.
            if (IsHeld || context.Hands == null || context.Held != null)
            {
                return null;
            }

            return $"{_displayName} 들기";
        }

        public void Interact(in InteractionContext context)
        {
            context.Hands?.TryPickUp(this);
        }

        public void OnPickedUp(Transform holdAnchor)
        {
            IsHeld = true;

            // 속도는 kinematic으로 바꾸기 전에 비운다 (kinematic 상태에서는 속도를 설정할 수 없다).
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.isKinematic = true;

            // 부모를 따라 움직이는 동안 보간이 켜져 있으면 이전 물리 위치와 섞여 떨려 보일 수 있다.
            _body.interpolation = RigidbodyInterpolation.None;

            // 들고 있는 동안에는 충돌을 끈다. 켜 두면 플레이어 캡슐을 밀어내거나 벽에 걸린다.
            SetCollidersEnabled(false);

            transform.SetParent(holdAnchor, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void OnDropped(Vector3 position, Quaternion rotation)
        {
            transform.SetParent(null, worldPositionStays: true);
            transform.SetPositionAndRotation(position, rotation);

            SetCollidersEnabled(true);
            _body.isKinematic = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;

            IsHeld = false;
        }

        private void SetCollidersEnabled(bool value)
        {
            foreach (Collider col in _colliders)
            {
                col.enabled = value;
            }
        }
    }
}
