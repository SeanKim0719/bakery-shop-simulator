using System;
using BakeryShop.Core;
using BakeryShop.Data;
using BakeryShop.Events;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    /// <summary>
    /// 씬 없이 게임 전체를 만들어 돌려 본다. GameBootstrap(MonoBehaviour)이 하는 일 중
    /// "시스템을 만들고 프레임마다 순서대로 돌리는 것"이 MonoBehaviour 밖에 있어서 가능하다.
    /// </summary>
    public class GameComposerTests
    {
        // TestContent: 시작 자금 10,000, 평판 500, 영업 09:00 ~ 18:00을 실제 360초에.
        private ContentDatabase _content;

        [SetUp]
        public void SetUp()
        {
            _content = TestContent.Create();
        }

        [Test]
        public void Compose_CreatesEverySystem()
        {
            GameContext context = GameComposer.Compose(_content, 1u, 2u);

            Assert.AreSame(_content, context.Content);
            Assert.IsNotNull(context.Bus);
            Assert.IsNotNull(context.CustomerRng);
            Assert.IsNotNull(context.LootRng);
            Assert.IsNotNull(context.Time);
            Assert.IsNotNull(context.Day);
            Assert.IsNotNull(context.Wallet);
            Assert.IsNotNull(context.Reputation);
            Assert.IsNotNull(context.Progression);
        }

        [Test]
        public void Compose_LeavesSceneReferencesEmpty()
        {
            // 씬 참조는 GameBootstrap이 붙인다. 테스트에서는 비어 있다.
            GameContext context = GameComposer.Compose(_content, 1u, 2u);

            Assert.IsNull(context.Input);
            Assert.IsNull(context.Panels);
            Assert.IsNull(context.Layout);
        }

        [Test]
        public void StartNewGame_SetsTheStartingState()
        {
            GameContext context = GameComposer.Compose(_content, 1u, 2u);

            GameComposer.StartNewGame(context);

            Assert.AreEqual(10000, context.Wallet.Balance);
            Assert.AreEqual(500, context.Reputation.Value);
            Assert.AreEqual(0, context.Progression.Xp);
            Assert.AreEqual(1, context.Progression.Level);
            Assert.AreEqual(1, context.Day.Day);
            Assert.AreEqual(DayPhase.Preparation, context.Day.Phase);
            Assert.AreEqual(540f, context.Time.ShopClockMinutes, 0.001f);
        }

        [Test]
        public void StartNewGame_PublishesNothing()
        {
            // 시작 자금은 "번 돈"이 아니다. 시작하자마자 +10,000 알림이 뜨면 안 된다.
            GameContext context = GameComposer.Compose(_content, 1u, 2u);
            int events = 0;
            context.Bus.Trace += (type, boxed) => events++;

            GameComposer.StartNewGame(context);

            Assert.AreEqual(0, events);
        }

        [Test]
        public void SameSeeds_GiveTheSameRandomNumbers()
        {
            GameContext first = GameComposer.Compose(_content, 111u, 222u);
            GameContext second = GameComposer.Compose(_content, 111u, 222u);

            Assert.AreEqual(first.CustomerRng.NextUInt(), second.CustomerRng.NextUInt());
            Assert.AreEqual(first.LootRng.NextUInt(), second.LootRng.NextUInt());
        }

        [Test]
        public void CustomerAndLootRandomNumbers_AreSeparateStreams()
        {
            GameContext context = GameComposer.Compose(_content, 111u, 222u);

            Assert.AreNotSame(context.CustomerRng, context.LootRng);
            Assert.AreNotEqual(context.CustomerRng.NextUInt(), context.LootRng.NextUInt());
        }

        [Test]
        public void UsingCustomerRandomNumbers_DoesNotChangeTheLootStream()
        {
            // 손님이 몇 명 왔는지에 따라 박스에서 나오는 것이 달라지면 안 된다.
            GameContext quiet = GameComposer.Compose(_content, 111u, 222u);
            GameContext busy = GameComposer.Compose(_content, 111u, 222u);
            for (int i = 0; i < 50; i++)
            {
                busy.CustomerRng.NextUInt();
            }

            Assert.AreEqual(quiet.LootRng.NextUInt(), busy.LootRng.NextUInt());
        }

        [TestCase(1u)]
        [TestCase(12345u)]
        [TestCase(0x5BD1E995u)]
        [TestCase(uint.MaxValue)]
        public void LootSeedFor_IsAlwaysDifferentFromTheSeed(uint seed)
        {
            Assert.AreNotEqual(seed, GameComposer.LootSeedFor(seed));
        }

        [Test]
        public void GameLoop_InPreparation_KeepsTheShopClockStill()
        {
            GameContext context = NewGame();

            for (int i = 0; i < 100; i++)
            {
                GameLoop.Tick(context, 0.1f);
            }

            Assert.AreEqual(DayPhase.Preparation, context.Day.Phase);
            Assert.AreEqual(540f, context.Time.ShopClockMinutes, 0.001f);
            Assert.AreEqual(10.0, context.Time.SimTime, 0.001);
        }

        [Test]
        public void GameLoop_RunsAWholeDay_UntilClosing()
        {
            // 씬 없이 하루의 시간이 흐른다: 개점 → 360초 → 마감.
            GameContext context = NewGame();
            DayPhase lastAnnounced = DayPhase.Preparation;
            context.Bus.Subscribe<DayPhaseChanged>(change => lastAnnounced = change.Current);

            Assert.IsTrue(context.Day.TryOpenShop());
            int ticks = (int)(360f / TimeService.MaxDeltaSeconds);
            for (int i = 0; i < ticks; i++)
            {
                GameLoop.Tick(context, TimeService.MaxDeltaSeconds);
            }

            Assert.AreEqual(DayPhase.Closing, context.Day.Phase);
            Assert.AreEqual(DayPhase.Closing, lastAnnounced);
            Assert.AreEqual(1080f, context.Time.ShopClockMinutes, 0.001f);
            Assert.IsFalse(context.Time.IsClockRunning);
        }

        [Test]
        public void GameLoop_JustBeforeClose_IsStillOpen()
        {
            GameContext context = NewGame();
            context.Day.TryOpenShop();

            int ticks = (int)(359f / TimeService.MaxDeltaSeconds);
            for (int i = 0; i < ticks; i++)
            {
                GameLoop.Tick(context, TimeService.MaxDeltaSeconds);
            }

            Assert.AreEqual(DayPhase.Open, context.Day.Phase);
        }

        [Test]
        public void GameLoop_WhilePaused_NothingMoves()
        {
            GameContext context = NewGame();
            context.Day.TryOpenShop();
            context.Time.SetPaused(true);

            for (int i = 0; i < 100; i++)
            {
                GameLoop.Tick(context, 0.25f);
            }

            Assert.AreEqual(540f, context.Time.ShopClockMinutes, 0.001f);
            Assert.AreEqual(DayPhase.Open, context.Day.Phase);
        }

        [Test]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => GameComposer.Compose(null, 1u, 2u));
            Assert.Throws<ArgumentNullException>(() => GameComposer.StartNewGame(null));
            Assert.Throws<ArgumentNullException>(() => GameLoop.Tick(null, 0.1f));
        }

        private GameContext NewGame()
        {
            GameContext context = GameComposer.Compose(_content, 1u, 2u);
            GameComposer.StartNewGame(context);
            context.Day.Begin();
            return context;
        }
    }
}
