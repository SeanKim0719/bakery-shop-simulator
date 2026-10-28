using BakeryShop.Player;

namespace BakeryShop.Core
{
    /// <summary>
    /// 부트스트랩이 만든 시스템을 담는 묶음. 서비스 로케이터처럼 아무 데서나 꺼내 쓰지 않고,
    /// 각 컴포넌트가 Init 시점에 필요한 것만 꺼내 필드로 보관한다 (docs/design/01, ADR 0003).
    /// 3주차부터 EventBus, TimeService, DayCycle 등이 여기에 추가된다.
    /// </summary>
    public sealed class GameContext
    {
        public GameContext(PlayerInputReader input)
        {
            Input = input;
        }

        /// <summary>패널·메뉴가 입력 모드를 바꿀 때 쓴다.</summary>
        public PlayerInputReader Input { get; }
    }

    /// <summary>씬에 미리 배치되어 부트스트랩에게 GameContext를 주입받는 컴포넌트.</summary>
    public interface IContextConsumer
    {
        void Init(GameContext context);
    }
}
