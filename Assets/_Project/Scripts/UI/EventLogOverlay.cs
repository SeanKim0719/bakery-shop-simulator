using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using BakeryShop.Core;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace BakeryShop.UI
{
    /// <summary>
    /// 개발용 이벤트 로그. F3으로 켜고 끈다. 에디터와 개발 빌드에서만 동작한다.
    /// 발행된 이벤트를 "시각  이름  내용"으로 최근 것부터 보여 준다.
    /// 버그를 볼 때 "무슨 순서로 무엇이 일어났는지"를 추측하지 않고 눈으로 확인하는 도구다.
    /// </summary>
    public sealed class EventLogOverlay : MonoBehaviour, IContextConsumer
    {
        private const int MaxLines = 20;

        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _text;

        private readonly Queue<string> _lines = new Queue<string>();
        private readonly Dictionary<Type, FieldInfo[]> _fieldsByType = new Dictionary<Type, FieldInfo[]>();
        private readonly StringBuilder _builder = new StringBuilder();

        private EventBus _bus;
        private TimeService _time;
        private Action<Type, object> _onTrace;
        private bool _isVisible;
        private bool _isDirty;

        public void Init(GameContext context)
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 화면이 꺼져 있어도 기록은 한다. 문제가 생긴 뒤에 F3을 눌러도 직전의 흐름을 볼 수 있어야 한다.
            // Trace를 연결하면 이벤트마다 박싱이 한 번 생기지만, 개발 빌드에서만이다.
            _bus = context.Bus;
            _time = context.Time;
            _onTrace = Record;
            _bus.Trace += _onTrace;
#endif
        }

        private void OnDestroy()
        {
            if (_bus != null && _onTrace != null)
            {
                _bus.Trace -= _onTrace;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                _isVisible = !_isVisible;
                if (_panel != null)
                {
                    _panel.SetActive(_isVisible);
                }

                _isDirty = true;
            }

            if (_isVisible && _isDirty)
            {
                Redraw();
            }
        }
#endif

        private void Record(Type eventType, object boxedEvent)
        {
            _lines.Enqueue(FormatLine(_time != null ? _time.SimTime : 0.0, eventType, boxedEvent));
            while (_lines.Count > MaxLines)
            {
                _lines.Dequeue();
            }

            _isDirty = true;
        }

        private void Redraw()
        {
            _isDirty = false;
            if (_text == null)
            {
                return;
            }

            _builder.Length = 0;
            _builder.AppendLine("이벤트 로그 (F3)");
            foreach (string line in _lines)
            {
                _builder.AppendLine(line);
            }

            _text.text = _builder.ToString();
        }

        // "0012.4  MoneyChanged  Balance=53600 Delta=3600 Reason=Sale"
        private string FormatLine(double simTime, Type eventType, object boxedEvent)
        {
            FieldInfo[] fields;
            if (!_fieldsByType.TryGetValue(eventType, out fields))
            {
                fields = eventType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                _fieldsByType.Add(eventType, fields);
            }

            var line = new StringBuilder();
            line.Append(simTime.ToString("0000.0", CultureInfo.InvariantCulture));
            line.Append("  ");
            line.Append(eventType.Name);
            line.Append(" ");
            foreach (FieldInfo field in fields)
            {
                line.Append(" ");
                line.Append(field.Name);
                line.Append("=");
                line.Append(Convert.ToString(field.GetValue(boxedEvent), CultureInfo.InvariantCulture));
            }

            return line.ToString();
        }
    }
}
