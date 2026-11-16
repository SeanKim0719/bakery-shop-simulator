using System;
using BakeryShop.Data;
using BakeryShop.Economy;

namespace BakeryShop.Core
{
    /// <summary>
    /// 시스템을 만들고 서로 연결한다. MonoBehaviour 없이 동작해서, 테스트에서도 씬 없이 게임 전체를 만들 수 있다.
    /// 만드는 순서가 곧 의존의 순서다: 뒤에 만드는 것이 앞에 만든 것을 받는다.
    /// </summary>
    public static class GameComposer
    {
        // 박스용 시드를 손님용 시드에서 만들 때 섞는 값. 0이 아니면 두 시드는 항상 다르다.
        private const uint LootSeedMix = 0x5BD1E995u;

        public static GameContext Compose(ContentDatabase content, uint customerSeed, uint lootSeed)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            GameConfig config = content.Config;
            if (config == null)
            {
                throw new ArgumentException("콘텐츠에 설정(GameConfig)이 없습니다.", nameof(content));
            }

            var bus = new EventBus();
            var time = new TimeService(config.Day.OpenMinutes, config.Day.CloseMinutes, config.Day.OpenRealSeconds);
            var day = new DayCycle(time, bus);
            var wallet = new Wallet(bus, 0);
            var reputation = new ReputationService(bus, config.Reputation, config.Customers);
            var progression = new ProgressionService(bus, config.Progression);

            return new GameContext(
                content,
                bus,
                new Rng(customerSeed),
                new Rng(lootSeed),
                time,
                day,
                wallet,
                reputation,
                progression);
        }

        /// <summary>
        /// 새 게임의 처음 상태를 채운다 (시작 자금 등). 이벤트를 발행하지 않는다: 아직 아무도 구독하지 않았고,
        /// "돈을 벌었다"는 알림이 뜰 일도 아니다.
        /// </summary>
        public static void StartNewGame(GameContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            GameConfig config = context.Content.Config;
            context.Wallet.Restore(config.Economy.StartMoney);
            context.Reputation.Restore(config.Reputation.Start);
            context.Progression.Restore(0);
        }

        /// <summary>시드 하나에서 박스용 시드를 만든다. 손님용과 박스용이 같은 수열을 내지 않게 한다.</summary>
        public static uint LootSeedFor(uint seed)
        {
            return seed ^ LootSeedMix;
        }
    }
}
