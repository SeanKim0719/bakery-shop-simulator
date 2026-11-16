using System;
using System.Collections.Generic;
using BakeryShop.Core;
using BakeryShop.Events;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class DayCycleTests
    {
        private const int Open = 540;
        private const int Close = 1080;
        private const float OpenRealSeconds = 360f;

        private TimeService _time;
        private EventBus _bus;
        private DayCycle _cycle;

        // 일어난 일을 순서대로 적는다. "phase:1:Preparation>Open", "advanced:2", "step:oven:1" 같은 줄이 쌓인다.
        private List<string> _log;

        private sealed class RecordingStep : IDayEndStep
        {
            private readonly string _name;
            private readonly List<string> _log;

            public RecordingStep(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            public void OnDayEnd(int day)
            {
                _log.Add("step:" + _name + ":" + day);
            }
        }

        private sealed class ActionStep : IDayEndStep
        {
            private readonly Action<int> _action;

            public ActionStep(Action<int> action)
            {
                _action = action;
            }

            public void OnDayEnd(int day)
            {
                _action(day);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _time = new TimeService(Open, Close, OpenRealSeconds);
            _bus = new EventBus();
            _cycle = new DayCycle(_time, _bus);
            _log = new List<string>();

            _bus.Subscribe<DayPhaseChanged>(e => _log.Add("phase:" + e.Day + ":" + e.Previous + ">" + e.Current));
            _bus.Subscribe<DayAdvanced>(e => _log.Add("advanced:" + e.NewDay));
        }

        /// <summary>영업 시계를 폐점 시각까지 흘리고 Tick을 부른다.</summary>
        private void RunUntilClose()
        {
            int steps = (int)(OpenRealSeconds / TimeService.MaxDeltaSeconds);
            for (int i = 0; i < steps; i++)
            {
                _time.Advance(TimeService.MaxDeltaSeconds);
                _cycle.Tick();
            }
        }

        private void GoToClosing()
        {
            _cycle.Begin();
            _cycle.TryOpenShop();
            RunUntilClose();
            _log.Clear();
        }

        [Test]
        public void StartsOnDayOne_InPreparation()
        {
            Assert.AreEqual(1, _cycle.Day);
            Assert.AreEqual(DayPhase.Preparation, _cycle.Phase);
            Assert.IsEmpty(_log);
        }

        [Test]
        public void Begin_AnnouncesPreparation()
        {
            _cycle.Begin();

            CollectionAssert.AreEqual(new[] { "phase:1:Preparation>Preparation" }, _log);
            Assert.IsFalse(_time.IsClockRunning);
        }

        [Test]
        public void Begin_Twice_Throws()
        {
            _cycle.Begin();

            Assert.Throws<InvalidOperationException>(() => _cycle.Begin());
        }

        [Test]
        public void CommandsBeforeBegin_Throw()
        {
            // 구독이 끝나기 전에 단계가 바뀌면 뷰가 알림을 놓친다. 부트스트랩의 순서 실수를 바로 드러낸다.
            Assert.Throws<InvalidOperationException>(() => _cycle.TryOpenShop());
            Assert.Throws<InvalidOperationException>(() => _cycle.Tick());
            Assert.Throws<InvalidOperationException>(() => _cycle.TrySettle());
            Assert.Throws<InvalidOperationException>(() => _cycle.StartNextDay());
        }

        [Test]
        public void TryOpenShop_InPreparation_OpensAndStartsTheClock()
        {
            _cycle.Begin();
            _log.Clear();

            bool opened = _cycle.TryOpenShop();

            Assert.IsTrue(opened);
            Assert.AreEqual(DayPhase.Open, _cycle.Phase);
            Assert.IsTrue(_time.IsClockRunning);
            CollectionAssert.AreEqual(new[] { "phase:1:Preparation>Open" }, _log);
        }

        [Test]
        public void TryOpenShop_WhileOpen_FailsWithoutEvent()
        {
            _cycle.Begin();
            _cycle.TryOpenShop();
            _log.Clear();

            bool opened = _cycle.TryOpenShop();

            Assert.IsFalse(opened);
            Assert.AreEqual(DayPhase.Open, _cycle.Phase);
            Assert.IsEmpty(_log);
        }

        [Test]
        public void Tick_BeforeClose_KeepsOpen()
        {
            _cycle.Begin();
            _cycle.TryOpenShop();
            _log.Clear();

            _time.Advance(0.25f);
            _cycle.Tick();

            Assert.AreEqual(DayPhase.Open, _cycle.Phase);
            Assert.IsEmpty(_log);
        }

        [Test]
        public void Tick_AtClose_MovesToClosing_AndStopsTheClock()
        {
            _cycle.Begin();
            _cycle.TryOpenShop();
            _log.Clear();

            RunUntilClose();

            Assert.AreEqual(DayPhase.Closing, _cycle.Phase);
            Assert.IsFalse(_time.IsClockRunning);
            Assert.AreEqual(Close, _time.ShopClockMinutes, 0.001f);
            CollectionAssert.AreEqual(new[] { "phase:1:Open>Closing" }, _log);
        }

        [Test]
        public void Tick_InClosing_DoesNotAnnounceAgain()
        {
            GoToClosing();

            _time.Advance(0.25f);
            _cycle.Tick();
            _cycle.Tick();

            Assert.IsEmpty(_log);
        }

        [Test]
        public void Tick_InPreparation_DoesNothing()
        {
            _cycle.Begin();
            _log.Clear();

            _time.Advance(0.25f);
            _cycle.Tick();

            Assert.AreEqual(DayPhase.Preparation, _cycle.Phase);
            Assert.IsEmpty(_log);
        }

        [Test]
        public void TrySettle_WhileOpen_Fails()
        {
            _cycle.Begin();
            _cycle.TryOpenShop();
            _log.Clear();

            Assert.IsFalse(_cycle.CanSettle);
            Assert.IsFalse(_cycle.TrySettle());
            Assert.AreEqual(DayPhase.Open, _cycle.Phase);
            Assert.AreEqual(1, _cycle.Day);
            Assert.IsEmpty(_log);
        }

        [Test]
        public void TrySettle_InPreparation_Fails()
        {
            _cycle.Begin();

            Assert.IsFalse(_cycle.TrySettle());
            Assert.AreEqual(DayPhase.Preparation, _cycle.Phase);
        }

        [Test]
        public void TrySettle_WithCustomersInside_Fails_ThenSucceedsWhenEmpty()
        {
            bool empty = false;
            _cycle.SetShopEmptyCheck(() => empty);
            GoToClosing();

            Assert.IsFalse(_cycle.CanSettle);
            Assert.IsFalse(_cycle.TrySettle());
            Assert.AreEqual(DayPhase.Closing, _cycle.Phase);
            Assert.IsEmpty(_log);

            empty = true;

            Assert.IsTrue(_cycle.CanSettle);
            Assert.IsTrue(_cycle.TrySettle());
            Assert.AreEqual(DayPhase.Settlement, _cycle.Phase);
        }

        [Test]
        public void TrySettle_WithoutEmptyCheck_TreatsShopAsEmpty()
        {
            GoToClosing();

            Assert.IsTrue(_cycle.CanSettle);
            Assert.IsTrue(_cycle.TrySettle());
        }

        [Test]
        public void TrySettle_RunsPhaseChange_ThenSteps_ThenAdvancesTheDay()
        {
            _cycle.AddDayEndStep(10, new RecordingStep("oven", _log));
            _cycle.AddDayEndStep(20, new RecordingStep("rent", _log));
            GoToClosing();

            _cycle.TrySettle();

            CollectionAssert.AreEqual(
                new[]
                {
                    "phase:1:Closing>Settlement",
                    "step:oven:1",
                    "step:rent:1",
                    "advanced:2",
                },
                _log);
            Assert.AreEqual(2, _cycle.Day);
        }

        [Test]
        public void DayEndSteps_RunByOrder_NotByRegistration()
        {
            // 임대료(50)를 먼저 등록하고 오븐 비우기(20)를 나중에 등록해도 오븐이 먼저 실행된다.
            _cycle.AddDayEndStep(50, new RecordingStep("rent", _log));
            _cycle.AddDayEndStep(20, new RecordingStep("oven", _log));
            _cycle.AddDayEndStep(80, new RecordingStep("report", _log));
            _cycle.AddDayEndStep(10, new RecordingStep("hands", _log));
            GoToClosing();

            _cycle.TrySettle();

            CollectionAssert.AreEqual(
                new[]
                {
                    "phase:1:Closing>Settlement",
                    "step:hands:1",
                    "step:oven:1",
                    "step:rent:1",
                    "step:report:1",
                    "advanced:2",
                },
                _log);
        }

        [Test]
        public void DayEndSteps_WithSameOrder_RunInRegistrationOrder()
        {
            _cycle.AddDayEndStep(30, new RecordingStep("shelf", _log));
            _cycle.AddDayEndStep(30, new RecordingStep("tray", _log));
            _cycle.AddDayEndStep(30, new RecordingStep("extra", _log));
            GoToClosing();

            _cycle.TrySettle();

            CollectionAssert.AreEqual(
                new[]
                {
                    "phase:1:Closing>Settlement",
                    "step:shelf:1",
                    "step:tray:1",
                    "step:extra:1",
                    "advanced:2",
                },
                _log);
        }

        [Test]
        public void DuringDayEndSteps_DayIsStillTheEndedDay()
        {
            int dayInsideStep = 0;
            DayPhase phaseInsideStep = DayPhase.Preparation;
            _cycle.AddDayEndStep(
                10,
                new ActionStep(day =>
                {
                    dayInsideStep = _cycle.Day;
                    phaseInsideStep = _cycle.Phase;
                }));
            GoToClosing();

            _cycle.TrySettle();

            Assert.AreEqual(1, dayInsideStep);
            Assert.AreEqual(DayPhase.Settlement, phaseInsideStep);
        }

        [Test]
        public void DayAdvancedSubscriber_SeesTheNewDay()
        {
            // 세이브가 이 이벤트에서 저장한다. 저장되는 날짜는 새 날이어야 한다.
            int dayWhenAdvanced = 0;
            _bus.Subscribe<DayAdvanced>(e => dayWhenAdvanced = _cycle.Day);
            GoToClosing();

            _cycle.TrySettle();

            Assert.AreEqual(2, dayWhenAdvanced);
        }

        [Test]
        public void PhaseChangedSubscriber_SeesTheNewPhase()
        {
            DayPhase phaseWhenNotified = DayPhase.Settlement;
            _bus.Subscribe<DayPhaseChanged>(e => phaseWhenNotified = _cycle.Phase);
            _cycle.Begin();

            _cycle.TryOpenShop();

            Assert.AreEqual(DayPhase.Open, phaseWhenNotified);
        }

        [Test]
        public void TrySettle_Twice_SecondFails()
        {
            _cycle.AddDayEndStep(10, new RecordingStep("oven", _log));
            GoToClosing();
            _cycle.TrySettle();
            _log.Clear();

            Assert.IsFalse(_cycle.TrySettle());
            Assert.AreEqual(2, _cycle.Day);
            Assert.IsEmpty(_log);
        }

        [Test]
        public void StartNextDay_InSettlement_ReturnsToPreparation_WithClockReset()
        {
            GoToClosing();
            _cycle.TrySettle();
            _log.Clear();

            bool started = _cycle.StartNextDay();

            Assert.IsTrue(started);
            Assert.AreEqual(DayPhase.Preparation, _cycle.Phase);
            Assert.AreEqual(2, _cycle.Day);
            Assert.AreEqual(Open, _time.ShopClockMinutes, 0.001f);
            Assert.IsFalse(_time.IsClockRunning);
            Assert.IsFalse(_time.HasReachedClose);
            CollectionAssert.AreEqual(new[] { "phase:2:Settlement>Preparation" }, _log);
        }

        [Test]
        public void StartNextDay_OutsideSettlement_Fails()
        {
            _cycle.Begin();
            _log.Clear();

            Assert.IsFalse(_cycle.StartNextDay());

            _cycle.TryOpenShop();
            Assert.IsFalse(_cycle.StartNextDay());

            Assert.AreEqual(1, _cycle.Day);
        }

        [Test]
        public void StartNextDay_FromInsideADayEndStep_Throws()
        {
            // 마무리 처리가 끝나기 전에 다음 날로 넘어가면 남은 단계가 준비 단계에서 실행된다.
            _cycle.AddDayEndStep(10, new ActionStep(day => _cycle.StartNextDay()));
            GoToClosing();

            Assert.Throws<InvalidOperationException>(() => _cycle.TrySettle());
        }

        [Test]
        public void AddDayEndStep_FromInsideADayEndStep_Throws()
        {
            _cycle.AddDayEndStep(10, new ActionStep(day => _cycle.AddDayEndStep(99, new RecordingStep("late", _log))));
            GoToClosing();

            Assert.Throws<InvalidOperationException>(() => _cycle.TrySettle());
        }

        [Test]
        public void TwoDaysInARow_KeepTheSameOrder()
        {
            _cycle.AddDayEndStep(20, new RecordingStep("rent", _log));
            _cycle.AddDayEndStep(10, new RecordingStep("oven", _log));
            _cycle.Begin();

            for (int day = 1; day <= 2; day++)
            {
                Assert.IsTrue(_cycle.TryOpenShop());
                RunUntilClose();
                Assert.IsTrue(_cycle.TrySettle());
                Assert.IsTrue(_cycle.StartNextDay());
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "phase:1:Preparation>Preparation",
                    "phase:1:Preparation>Open",
                    "phase:1:Open>Closing",
                    "phase:1:Closing>Settlement",
                    "step:oven:1",
                    "step:rent:1",
                    "advanced:2",
                    "phase:2:Settlement>Preparation",
                    "phase:2:Preparation>Open",
                    "phase:2:Open>Closing",
                    "phase:2:Closing>Settlement",
                    "step:oven:2",
                    "step:rent:2",
                    "advanced:3",
                    "phase:3:Settlement>Preparation",
                },
                _log);
            Assert.AreEqual(3, _cycle.Day);
            Assert.AreEqual(DayPhase.Preparation, _cycle.Phase);
        }

        [Test]
        public void SimTime_KeepsRunning_AcrossDays()
        {
            // 영업 시계는 날마다 되돌아가지만 SimTime은 이어진다.
            _cycle.Begin();
            _cycle.TryOpenShop();
            RunUntilClose();
            _cycle.TrySettle();
            _cycle.StartNextDay();

            Assert.AreEqual(OpenRealSeconds, _time.SimTime, 0.001);
        }

        [Test]
        public void RestoreDay_BeforeBegin_SetsTheDay_WithoutEvents()
        {
            _cycle.RestoreDay(7);

            Assert.AreEqual(7, _cycle.Day);
            Assert.IsEmpty(_log);

            _cycle.Begin();

            CollectionAssert.AreEqual(new[] { "phase:7:Preparation>Preparation" }, _log);
        }

        [Test]
        public void RestoreDay_AfterBegin_Throws()
        {
            _cycle.Begin();

            Assert.Throws<InvalidOperationException>(() => _cycle.RestoreDay(3));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RestoreDay_BelowOne_Throws(int day)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _cycle.RestoreDay(day));
        }

        [Test]
        public void AddDayEndStep_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _cycle.AddDayEndStep(10, null));
        }

        [Test]
        public void AddDayEndStep_SameStepTwice_Throws()
        {
            var step = new RecordingStep("oven", _log);
            _cycle.AddDayEndStep(10, step);

            Assert.Throws<ArgumentException>(() => _cycle.AddDayEndStep(20, step));
        }

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new DayCycle(null, _bus));
            Assert.Throws<ArgumentNullException>(() => new DayCycle(_time, null));
        }
    }
}
