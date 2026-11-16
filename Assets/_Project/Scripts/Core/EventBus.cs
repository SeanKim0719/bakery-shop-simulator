using System;
using System.Collections.Generic;

namespace BakeryShop.Core
{
    /// <summary>
    /// 시스템과 뷰가 서로를 모른 채 "일어난 일"을 주고받는 통로.
    /// 이벤트는 struct이고, 타입마다 구독자 목록이 따로 있다. 전역 static이 아니라 부트스트랩이 하나를 만들어 주입한다
    /// (docs/design/01, ADR 0003).
    /// </summary>
    /// <remarks>
    /// 발행하는 도중에 구독·해제가 일어나도 안전하다. 발행을 시작할 때 있던 구독자만 호출하고,
    /// 도중에 해제된 구독자는 건너뛰며, 도중에 구독한 구독자는 다음 발행부터 받는다.
    /// 구독자가 던진 예외는 삼키지 않는다 (개발 중에 바로 드러나야 한다).
    /// </remarks>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, IChannel> _channels = new Dictionary<Type, IChannel>();

        /// <summary>
        /// 개발용. 발행되는 모든 이벤트의 타입과 값을 구독자보다 먼저 받는다 (이벤트 로그 화면에서 사용).
        /// null이면 이벤트를 object로 바꾸지 않으므로 박싱이 일어나지 않는다.
        /// </summary>
        public Action<Type, object> Trace;

        /// <summary>같은 핸들러를 두 번 구독하면 한 번만 등록된다.</summary>
        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            GetOrCreateChannel<T>().Add(handler);
        }

        /// <summary>등록하지 않은 핸들러를 해제하면 아무 일도 일어나지 않는다.</summary>
        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_channels.TryGetValue(typeof(T), out IChannel channel))
            {
                ((Channel<T>)channel).Remove(handler);
            }
        }

        /// <summary>구독한 순서대로 호출한다. 구독자 안에서 다른 이벤트(같은 타입 포함)를 발행해도 된다.</summary>
        public void Publish<T>(in T evt) where T : struct
        {
            Action<Type, object> trace = Trace;
            if (trace != null)
            {
                trace(typeof(T), evt);
            }

            if (_channels.TryGetValue(typeof(T), out IChannel channel))
            {
                ((Channel<T>)channel).Publish(in evt);
            }
        }

        /// <summary>T 타입 이벤트의 구독자 수. 테스트와 디버그용.</summary>
        public int SubscriberCount<T>() where T : struct
        {
            return _channels.TryGetValue(typeof(T), out IChannel channel) ? ((Channel<T>)channel).Count : 0;
        }

        private Channel<T> GetOrCreateChannel<T>() where T : struct
        {
            if (_channels.TryGetValue(typeof(T), out IChannel existing))
            {
                return (Channel<T>)existing;
            }

            var created = new Channel<T>();
            _channels.Add(typeof(T), created);
            return created;
        }

        // 타입이 다른 채널을 한 딕셔너리에 담기 위한 표시용 인터페이스.
        private interface IChannel
        {
        }

        private sealed class Channel<T> : IChannel where T : struct
        {
            // 발행 중에는 목록을 줄이지 않는다. 해제된 자리는 null로 비워 두었다가(인덱스가 밀리지 않게)
            // 가장 바깥의 발행이 끝난 뒤에 한꺼번에 지운다.
            private readonly List<Action<T>> _handlers = new List<Action<T>>();
            private int _publishDepth;
            private bool _hasHoles;

            public int Count { get; private set; }

            public void Add(Action<T> handler)
            {
                if (_handlers.Contains(handler))
                {
                    return;
                }

                // 발행 중이어도 끝에 붙인다. 진행 중인 발행은 시작할 때의 개수까지만 돌기 때문에 이번에는 불리지 않는다.
                _handlers.Add(handler);
                Count++;
            }

            public void Remove(Action<T> handler)
            {
                int index = _handlers.IndexOf(handler);
                if (index < 0)
                {
                    return;
                }

                Count--;
                if (_publishDepth > 0)
                {
                    _handlers[index] = null;
                    _hasHoles = true;
                }
                else
                {
                    _handlers.RemoveAt(index);
                }
            }

            public void Publish(in T evt)
            {
                int countAtStart = _handlers.Count;
                _publishDepth++;
                try
                {
                    for (int i = 0; i < countAtStart; i++)
                    {
                        Action<T> handler = _handlers[i];
                        if (handler != null)
                        {
                            handler(evt);
                        }
                    }
                }
                finally
                {
                    // 구독자가 예외를 던져도 깊이를 되돌려야 다음 발행이 정상으로 돈다.
                    _publishDepth--;
                    if (_publishDepth == 0 && _hasHoles)
                    {
                        _handlers.RemoveAll(h => h == null);
                        _hasHoles = false;
                    }
                }
            }
        }
    }
}
