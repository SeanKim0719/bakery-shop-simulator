namespace BakeryShop.Interaction
{
    /// <summary>
    /// 플레이어가 바라보고 상호작용할 수 있는 대상 (박스, 오븐, 선반, 계산대, PC 등).
    /// 플레이어 코드는 대상의 종류를 모르고, 대상이 "지금 손에 든 것으로 무엇을 할 수 있는지"를 스스로 판단한다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 지금 상황에서 할 수 있는 행동의 안내 문구. null 또는 빈 문자열이면 할 수 있는 게 없다는 뜻이다
        /// (프롬프트를 숨기고 상호작용도 막는다). 판단과 안내를 한 메서드에서 만들어 둘이 어긋나지 않게 한다.
        /// </summary>
        string GetPrompt(in InteractionContext context);

        /// <summary>GetPrompt가 문구를 돌려준 상황에서만 호출된다.</summary>
        void Interact(in InteractionContext context);
    }
}
