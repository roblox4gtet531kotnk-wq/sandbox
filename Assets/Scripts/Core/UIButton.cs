using System;
using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>
    /// Кнопка интерфейса с собственной обработкой мыши.
    /// Сделано так, чтобы UI работал и со старым Input Manager, и с новым Input System.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIButton : MonoBehaviour
    {
        public Image background;
        public Text label;
        public Image icon;
        public Action onClick;

        public bool interactable = true;
        public bool disabled;

        public Color normalColor = Theme.PanelLight;
        public Color hoverColor = new Color(0.20f, 0.28f, 0.31f, 1f);
        public Color pressedColor = Theme.AccentDark;
        public Color disabledColor = new Color(0.13f, 0.16f, 0.18f, 1f);

        private RectTransform _rect;
        private Vector3 _baseScale = Vector3.one;
        private bool _hover;
        private bool _pressed;
        private bool _pressOnButton;
        private float _scale = 1f;

        /// <summary>Все живые кнопки интерфейса — чтобы узнать, под курсором ли кнопка.</summary>
        private static readonly System.Collections.Generic.List<UIButton> ActiveButtons =
            new System.Collections.Generic.List<UIButton>();

        /// <summary>Указатель (мышь или палец) сейчас над какой-нибудь кнопкой?</summary>
        public static bool PointerOverUI
        {
            get
            {
                return IsOverUI(InputHub.PointerPosition);
            }
        }

        /// <summary>Находится ли указанная точка экрана над кнопкой интерфейса.</summary>
        public static bool IsOverUI(Vector2 screenPosition)
        {
            for (int i = ActiveButtons.Count - 1; i >= 0; i--)
            {
                UIButton button = ActiveButtons[i];
                if (button == null) { ActiveButtons.RemoveAt(i); continue; }
                if (!button.interactable || button.disabled) continue;
                RectTransform rt = button._rect;
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPosition, null))
                    return true;
            }
            return false;
        }

        public void Init()
        {
            _rect = GetComponent<RectTransform>();
            _baseScale = _rect.localScale;
            if (background != null && normalColor == Theme.PanelLight)
                normalColor = background.color;
        }

        private void OnEnable()
        {
            _rect = GetComponent<RectTransform>();
            if (_rect != null) _baseScale = _rect.localScale;
            if (!ActiveButtons.Contains(this)) ActiveButtons.Add(this);
        }

        private void OnDisable()
        {
            ActiveButtons.Remove(this);
        }

        private void Update()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();

            bool usable = interactable && !disabled;
            bool hover = false;
            bool down = false;
            bool released = false;

            int touches = InputHub.TouchCount;
            if (touches > 0)
            {
                // Телефон: палец. Ищем касание, попавшее в кнопку.
                int over = -1;
                for (int i = 0; i < touches; i++)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(_rect, InputHub.TouchPosition(i), null))
                    {
                        over = i;
                        break;
                    }
                }
                if (over >= 0)
                {
                    hover = true;
                    down = true;
                    released = InputHub.TouchEnded(over);
                }
            }
            else
            {
                // Компьютер: мышь.
                Vector2 mouse = InputHub.MousePosition;
                hover = RectTransformUtility.RectangleContainsScreenPoint(_rect, mouse, null);
                down = hover && InputHub.MouseHeld(0);
                released = InputHub.MouseReleased(0);
            }

            if (hover != _hover) _hover = hover;

            if (usable && down && !_pressed)
            {
                _pressed = true;
                // Нажатие началось ИМЕННО на этой кнопке.
                // Это важно на телефоне: палец отпускают — касаний уже нет,
                // указатель переключается на «мышь» (которая вне кнопки),
                // и проверка _hover дала бы false — кнопка не сработала бы.
                _pressOnButton = true;
            }
            else if (_pressed && (released || !down))
            {
                bool fire = usable && _pressOnButton && onClick != null;
                _pressed = false;
                _pressOnButton = false;
                if (fire)
                {
                    onClick();
                    AudioManager.Instance?.PlayUiClick();
                }
            }

            float targetScale = !usable ? 1f : (_pressed ? 0.965f : (_hover ? 1.035f : 1f));
            _scale = Mathf.Lerp(_scale, targetScale, Time.unscaledDeltaTime * 16f);
            if (_rect != null) _rect.localScale = _baseScale * _scale;

            ApplyColors();
        }

        private void ApplyColors()
        {
            Color target = !interactable || disabled ? disabledColor : (_pressed ? pressedColor : (_hover ? hoverColor : normalColor));
            if (background != null) background.color = target;

            if (label != null)
            {
                label.color = (!interactable || disabled)
                    ? new Color(Theme.TextDim.r, Theme.TextDim.g, Theme.TextDim.b, 0.55f)
                    : (_pressed ? Theme.TextOnAccent : Theme.Text);
            }
            if (icon != null)
            {
                icon.color = (!interactable || disabled)
                    ? new Color(Theme.TextDim.r, Theme.TextDim.g, Theme.TextDim.b, 0.5f)
                    : (_pressed ? Theme.TextOnAccent : Color.white);
            }
        }

        public void SetDisabled(bool value)
        {
            disabled = value;
            interactable = !value;
        }
    }
}
