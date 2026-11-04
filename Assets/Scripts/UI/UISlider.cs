using System;
using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>Простой ползунок (без EventSystem — работает с любым бэкендом ввода).</summary>
    public class UISlider : MonoBehaviour
    {
        public RectTransform fill;
        public RectTransform knob;
        public Text valueLabel;
        public float value = 0.5f;
        public float min = 0f;
        public float max = 1f;
        public Action<float> onChange;
        /// <summary>Вызывается, когда пользователь отпустил ползунок (не каждый кадр).</summary>
        public Action onRelease;
        public string suffix = "";

        private RectTransform _bar;
        private bool _dragging;

        public void Setup(RectTransform bar, RectTransform fillRect, RectTransform knobRect, Text label, float initial)
        {
            _bar = bar;
            fill = fillRect;
            knob = knobRect;
            valueLabel = label;
            value = Mathf.Clamp(initial, min, max);
            Refresh();
        }

        public void SetValue(float newValue, bool notify = true)
        {
            value = Mathf.Clamp(newValue, min, max);
            Refresh();
            if (notify) onChange?.Invoke(value);
        }

        private void Refresh()
        {
            float t = Mathf.InverseLerp(min, max, value);
            if (fill != null)
            {
                fill.sizeDelta = new Vector2(320f * t, fill.sizeDelta.y);
                fill.anchoredPosition = new Vector2(-160f + 160f * t, fill.anchoredPosition.y);
            }
            if (knob != null)
            {
                knob.anchoredPosition = new Vector2(-160f + 320f * t, knob.anchoredPosition.y);
            }
            if (valueLabel != null)
            {
                valueLabel.text = suffix.Length > 0
                    ? suffix + " " + Mathf.RoundToInt(value * 100f) + "%"
                    : Mathf.RoundToInt(value * 100f) + "%";
            }
        }

        private void Update()
        {
            if (_bar == null) return;

            // Работает и мышью, и пальцем — на телефоне ползунок иначе не двигается.
            int touches = InputHub.TouchCount;
            Vector2 pointer;
            bool pressed;
            bool held;

            if (touches > 0)
            {
                pointer = InputHub.TouchPosition(0);
                pressed = InputHub.TouchBegan(0);
                held = true;
            }
            else
            {
                pointer = InputHub.MousePosition;
                pressed = InputHub.MousePressed(0);
                held = InputHub.MouseHeld(0);
            }

            bool inside = RectTransformUtility.RectangleContainsScreenPoint(_bar, pointer, null);

            bool wasDragging = _dragging;
            if (inside && pressed) _dragging = true;
            if (!held) _dragging = false;

            if (_dragging)
            {
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_bar, pointer, null, out local);
                float t = Mathf.Clamp01((local.x + _bar.rect.width * 0.5f) / Mathf.Max(1f, _bar.rect.width));
                SetValue(Mathf.Lerp(min, max, t));
            }
            else if (wasDragging)
            {
                // Ползунок отпустили — самое время сохранить настройки.
                // (Во время перетаскивания сохранение вызывать нельзя:
                //  это десятки записей на диск в секунду.)
                onRelease?.Invoke();
            }
        }
    }
}
