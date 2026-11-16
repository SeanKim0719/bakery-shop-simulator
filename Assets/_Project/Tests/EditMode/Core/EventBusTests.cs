using System;
using System.Collections.Generic;
using BakeryShop.Core;
using NUnit.Framework;

namespace BakeryShop.Tests
{
    public class EventBusTests
    {
        private struct Ping
        {
            public int Value;
        }

        private struct Pong
        {
            public int Value;
        }

        private EventBus _bus;
        private List<string> _calls;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _calls = new List<string>();
        }

        [Test]
        public void Subscribed_ReceivesPublishedValue()
        {
            int received = 0;
            _bus.Subscribe<Ping>(e => received = e.Value);

            _bus.Publish(new Ping { Value = 7 });

            Assert.AreEqual(7, received);
        }

        [Test]
        public void OtherEventType_IsNotReceived()
        {
            int pings = 0;
            _bus.Subscribe<Ping>(e => pings++);

            _bus.Publish(new Pong { Value = 1 });

            Assert.AreEqual(0, pings);
        }

        [Test]
        public void NoSubscribers_PublishDoesNothing()
        {
            Assert.DoesNotThrow(() => _bus.Publish(new Ping { Value = 1 }));
        }

        [Test]
        public void Unsubscribed_IsNotCalledAnymore()
        {
            int count = 0;
            Action<Ping> handler = e => count++;
            _bus.Subscribe(handler);
            _bus.Publish(new Ping());

            _bus.Unsubscribe(handler);
            _bus.Publish(new Ping());

            Assert.AreEqual(1, count);
            Assert.AreEqual(0, _bus.SubscriberCount<Ping>());
        }

        [Test]
        public void Handlers_AreCalledInSubscriptionOrder()
        {
            _bus.Subscribe<Ping>(e => _calls.Add("a"));
            _bus.Subscribe<Ping>(e => _calls.Add("b"));
            _bus.Subscribe<Ping>(e => _calls.Add("c"));

            _bus.Publish(new Ping());

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, _calls);
        }

        [Test]
        public void SameHandlerSubscribedTwice_IsCalledOnce()
        {
            int count = 0;
            Action<Ping> handler = e => count++;
            _bus.Subscribe(handler);
            _bus.Subscribe(handler);

            _bus.Publish(new Ping());

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, _bus.SubscriberCount<Ping>());
        }

        [Test]
        public void UnsubscribingUnknownHandler_IsIgnored()
        {
            _bus.Subscribe<Ping>(e => _calls.Add("a"));

            Assert.DoesNotThrow(() => _bus.Unsubscribe<Ping>(e => { }));
            Assert.DoesNotThrow(() => _bus.Unsubscribe<Pong>(e => { }));

            Assert.AreEqual(1, _bus.SubscriberCount<Ping>());
        }

        [Test]
        public void HandlerUnsubscribingItself_OthersStillCalled()
        {
            Action<Ping> selfRemoving = null;
            selfRemoving = e =>
            {
                _calls.Add("self");
                _bus.Unsubscribe(selfRemoving);
            };
            _bus.Subscribe<Ping>(e => _calls.Add("before"));
            _bus.Subscribe(selfRemoving);
            _bus.Subscribe<Ping>(e => _calls.Add("after"));

            _bus.Publish(new Ping());
            _bus.Publish(new Ping());

            CollectionAssert.AreEqual(new[] { "before", "self", "after", "before", "after" }, _calls);
            Assert.AreEqual(2, _bus.SubscriberCount<Ping>());
        }

        [Test]
        public void HandlerRemovedDuringPublish_IsSkippedInThatPublish()
        {
            Action<Ping> later = e => _calls.Add("later");
            _bus.Subscribe<Ping>(e =>
            {
                _calls.Add("first");
                _bus.Unsubscribe(later);
            });
            _bus.Subscribe(later);

            _bus.Publish(new Ping());

            CollectionAssert.AreEqual(new[] { "first" }, _calls);
        }

        [Test]
        public void HandlerAddedDuringPublish_IsCalledFromNextPublish()
        {
            bool added = false;
            _bus.Subscribe<Ping>(e =>
            {
                _calls.Add("first");
                if (!added)
                {
                    added = true;
                    _bus.Subscribe<Ping>(inner => _calls.Add("added"));
                }
            });

            _bus.Publish(new Ping());
            CollectionAssert.AreEqual(new[] { "first" }, _calls);

            _bus.Publish(new Ping());
            CollectionAssert.AreEqual(new[] { "first", "first", "added" }, _calls);
        }

        [Test]
        public void PublishingAnotherEventInsideHandler_ReachesItsSubscribers()
        {
            _bus.Subscribe<Ping>(e =>
            {
                _calls.Add("ping " + e.Value);
                _bus.Publish(new Pong { Value = e.Value + 1 });
            });
            _bus.Subscribe<Pong>(e => _calls.Add("pong " + e.Value));

            _bus.Publish(new Ping { Value = 1 });

            CollectionAssert.AreEqual(new[] { "ping 1", "pong 2" }, _calls);
        }

        [Test]
        public void PublishingSameTypeInsideHandler_IsDeliveredNested()
        {
            _bus.Subscribe<Ping>(e =>
            {
                _calls.Add("a" + e.Value);
                if (e.Value < 2)
                {
                    _bus.Publish(new Ping { Value = e.Value + 1 });
                }
            });
            _bus.Subscribe<Ping>(e => _calls.Add("b" + e.Value));

            _bus.Publish(new Ping { Value = 1 });

            // 안쪽 발행(값 2)이 끝난 뒤에 바깥 발행(값 1)의 나머지 구독자가 불린다.
            CollectionAssert.AreEqual(new[] { "a1", "a2", "b2", "b1" }, _calls);
        }

        [Test]
        public void UnsubscribeInsideNestedPublish_KeepsOuterPublishConsistent()
        {
            Action<Ping> removed = e => _calls.Add("removed" + e.Value);
            _bus.Subscribe<Ping>(e =>
            {
                _calls.Add("a" + e.Value);
                if (e.Value == 1)
                {
                    _bus.Publish(new Ping { Value = 2 });
                }
                else
                {
                    _bus.Unsubscribe(removed);
                }
            });
            _bus.Subscribe(removed);
            _bus.Subscribe<Ping>(e => _calls.Add("c" + e.Value));

            _bus.Publish(new Ping { Value = 1 });
            _bus.Publish(new Ping { Value = 3 });

            CollectionAssert.AreEqual(new[] { "a1", "a2", "c2", "c1", "a3", "c3" }, _calls);
            Assert.AreEqual(2, _bus.SubscriberCount<Ping>());
        }

        [Test]
        public void HandlerException_Propagates_AndBusKeepsWorking()
        {
            bool shouldThrow = true;
            Action<Ping> toRemove = e => _calls.Add("removed");
            _bus.Subscribe<Ping>(e =>
            {
                if (shouldThrow)
                {
                    _bus.Unsubscribe(toRemove);
                    throw new InvalidOperationException("boom");
                }

                _calls.Add("ok");
            });
            _bus.Subscribe(toRemove);

            Assert.Throws<InvalidOperationException>(() => _bus.Publish(new Ping()));

            // 예외가 난 뒤에도 발행 깊이가 되돌아와 있어야, 발행 중에 해제한 구독자가 정리되고 다음 발행이 정상으로 돈다.
            shouldThrow = false;
            _bus.Publish(new Ping());

            CollectionAssert.AreEqual(new[] { "ok" }, _calls);
            Assert.AreEqual(1, _bus.SubscriberCount<Ping>());
        }

        [Test]
        public void Trace_ReceivesTypeAndValue_BeforeHandlers()
        {
            _bus.Trace = (type, value) => _calls.Add("trace " + type.Name + " " + ((Ping)value).Value);
            _bus.Subscribe<Ping>(e => _calls.Add("handler " + e.Value));

            _bus.Publish(new Ping { Value = 5 });

            CollectionAssert.AreEqual(new[] { "trace Ping 5", "handler 5" }, _calls);
        }

        [Test]
        public void Trace_ReceivesEventsWithoutSubscribers()
        {
            Type seen = null;
            _bus.Trace = (type, value) => seen = type;

            _bus.Publish(new Pong { Value = 1 });

            Assert.AreEqual(typeof(Pong), seen);
        }

        [Test]
        public void NullHandler_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _bus.Subscribe<Ping>(null));
            Assert.Throws<ArgumentNullException>(() => _bus.Unsubscribe<Ping>(null));
        }
    }
}
