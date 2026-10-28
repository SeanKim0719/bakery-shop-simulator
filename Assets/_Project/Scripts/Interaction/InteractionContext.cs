namespace BakeryShop.Interaction
{
    /// <summary>상호작용 시점의 플레이어 상태. 대상은 이것만 보고 무엇을 할 수 있는지 판단한다.</summary>
    public readonly struct InteractionContext
    {
        public readonly IHands Hands;

        public InteractionContext(IHands hands)
        {
            Hands = hands;
        }

        /// <summary>손에 든 물건. 빈손이면 null.</summary>
        public IHoldable Held => Hands?.Held;
    }
}
