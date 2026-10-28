using UnityEngine;

namespace BakeryShop.Interaction
{
    public enum HoldableKind
    {
        Box,
        Tray,
    }

    /// <summary>플레이어가 손에 들 수 있는 물건 (배송 박스, 빵 트레이).</summary>
    public interface IHoldable
    {
        HoldableKind Kind { get; }
        string DisplayName { get; }

        /// <summary>회전 전 기준의 월드 반 크기. 내려놓을 공간이 비어 있는지 검사할 때 쓴다.</summary>
        Vector3 HalfExtents { get; }

        /// <summary>손에 들렸을 때. 물리를 끄고 holdAnchor를 따라가게 한다.</summary>
        void OnPickedUp(Transform holdAnchor);

        /// <summary>내려놓였을 때. 주어진 자세로 옮긴 뒤 물리를 다시 켠다.</summary>
        void OnDropped(Vector3 position, Quaternion rotation);
    }

    /// <summary>
    /// 상호작용 대상이 플레이어 손에 접근할 때 쓰는 최소 인터페이스.
    /// 대상은 Hands 컴포넌트 전체가 아니라 이것만 알면 되고, 테스트에서는 가짜 구현으로 대체한다.
    /// </summary>
    public interface IHands
    {
        IHoldable Held { get; }
        bool TryPickUp(IHoldable item);
    }
}
