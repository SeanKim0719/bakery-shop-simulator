using BakeryShop.Data;
using BakeryShop.Economy;
using BakeryShop.Player;
using BakeryShop.UI;
using BakeryShop.World;

namespace BakeryShop.Core
{
    /// <summary>
    /// 게임의 시스템을 담는 묶음. 서비스 로케이터처럼 아무 데서나 꺼내 쓰지 않고,
    /// 각 컴포넌트가 Init 시점에 필요한 것만 꺼내 필드로 보관한다 (docs/design/01, ADR 0003).
    /// 순수 C# 시스템은 GameComposer가 만들고, 씬 참조는 GameBootstrap이 붙인다.
    /// </summary>
    public sealed class GameContext
    {
        internal GameContext(
            ContentDatabase content,
            EventBus bus,
            Rng customerRng,
            Rng lootRng,
            TimeService time,
            DayCycle day,
            Wallet wallet,
            ReputationService reputation,
            ProgressionService progression)
        {
            Content = content;
            Bus = bus;
            CustomerRng = customerRng;
            LootRng = lootRng;
            Time = time;
            Day = day;
            Wallet = wallet;
            Reputation = reputation;
            Progression = progression;
        }

        // ---- 순수 C# 시스템 (GameComposer가 만든다) ----

        public ContentDatabase Content { get; }

        public EventBus Bus { get; }

        /// <summary>손님의 등장과 선택에 쓰는 난수.</summary>
        public Rng CustomerRng { get; }

        /// <summary>
        /// 미스터리 박스에 쓰는 난수. 손님용과 따로 둔다: 손님이 한 명 더 왔다고 박스에서 나오는 것이 바뀌면 안 된다.
        /// </summary>
        public Rng LootRng { get; }

        public TimeService Time { get; }

        public DayCycle Day { get; }

        public Wallet Wallet { get; }

        public ReputationService Reputation { get; }

        public ProgressionService Progression { get; }

        // ---- 씬 참조 (GameBootstrap이 붙인다. 테스트에서는 null) ----

        /// <summary>패널이 입력 모드를 바꿀 때 쓴다.</summary>
        public PlayerInputReader Input { get; private set; }

        public PanelHost Panels { get; private set; }

        public ShopLayout Layout { get; private set; }

        internal void AttachScene(PlayerInputReader input, PanelHost panels, ShopLayout layout)
        {
            Input = input;
            Panels = panels;
            Layout = layout;
        }
    }

    /// <summary>씬에 미리 배치되어 부트스트랩에게 GameContext를 주입받는 컴포넌트.</summary>
    /// <remarks>
    /// Init은 부트스트랩의 Awake에서 불린다. 이 시점에는 자신의 Awake와 OnEnable이 아직 실행되지 않았을 수 있으므로,
    /// Init 안에서 필요한 준비(구독, 첫 화면 갱신)를 모두 한다.
    /// </remarks>
    public interface IContextConsumer
    {
        void Init(GameContext context);
    }
}
