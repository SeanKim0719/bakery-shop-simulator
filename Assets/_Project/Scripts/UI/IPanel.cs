namespace BakeryShop.UI
{
    /// <summary>화면을 차지하고 마우스로 조작하는 창 (오븐, 발주 PC, 정산 리포트 …). PanelHost가 한 번에 하나만 연다.</summary>
    public interface IPanel
    {
        /// <summary>Esc로 닫을 수 있는지. 정산 리포트처럼 버튼으로만 넘어가야 하는 패널은 false.</summary>
        bool CanCloseWithEscape { get; }

        void Show();

        void Hide();
    }
}
