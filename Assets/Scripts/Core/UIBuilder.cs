using System;
using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>Экран — объект сцены, который можно показать и закрыть.</summary>
    public interface IScreen
    {
        void Dispose();
    }

    /// <summary>Фабрика uGUI-элементов: весь интерфейс игры собирается кодом.</summary>
    public static class UIBuilder
    {
        public static Canvas CreateCanvas(string name, int sortOrder = 0, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null) go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>
        /// Контейнер интерфейса с учётом безопасной зоны экрана (чёлка, панели).
        /// Всё, что рисуется в игре, вешаем в него, а не прямо в канвас.
        /// </summary>
        public static RectTransform SafeAreaRoot(Canvas canvas)
        {
            if (canvas == null) return null;

            Transform found = canvas.transform.Find("SafeArea");
            if (found != null) return found as RectTransform;

            var go = new GameObject("SafeArea", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.AddComponent<SafeAreaFitter>();
            return rt;
        }

        /// <summary>Создаёт RectTransform с заданным размером и позицией (якорь по центру).</summary>
        public static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        /// <summary>Растягивает элемент на весь родитель (с отступами).</summary>
        public static RectTransform Stretch(Transform parent, string name, float padding = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
            return rt;
        }

        /// <summary>Растягивает элемент по одной из сторон.</summary>
        public static RectTransform Anchor(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                           Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static Image Panel(Transform parent, string name, Vector2 size, Vector2 position, Color color,
                                  Sprite sprite = null, bool sliced = true)
        {
            RectTransform rt = Rect(parent, name, size, position);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : Theme.Rounded;
            img.type = sliced && img.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image Picture(Transform parent, Sprite sprite, Vector2 size, Vector2 position, Color color)
        {
            RectTransform rt = Rect(parent, "Icon", size, position);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        public static Text Label(Transform parent, string text, int fontSize, Color color, TextAnchor anchor,
                                 Vector2 size, Vector2 position, FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = Rect(parent, "Label", size, position);
            var label = rt.gameObject.AddComponent<Text>();
            // Шрифт может не найтись (например, на устройстве без системных
            // шрифтов) — тогда оставляем встроенный шрифт Text, иначе текст
            // просто не рисуется.
            Font themeFont = Theme.Font;
            if (themeFont != null) label.font = themeFont;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = anchor;
            label.fontStyle = style;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = true;
            return label;
        }

        /// <summary>Создаёт кнопку. Ввод обрабатывается собственным компонентом UIButton.</summary>
        public static UIButton Button(Transform parent, string text, Vector2 size, Vector2 position,
                                      Action onClick, Sprite icon = null, Color? accent = null, int fontSize = 30)
        {
            RectTransform rt = Rect(parent, "Button", size, position);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Theme.Rounded;
            img.type = Image.Type.Sliced;
            img.color = accent ?? Theme.PanelLight;
            img.raycastTarget = false;

            var button = rt.gameObject.AddComponent<UIButton>();
            button.background = img;
            button.onClick = onClick;

            float iconSize = Mathf.Min(size.y * 0.55f, 64f);
            if (icon != null)
            {
                float labelWidth = size.x - iconSize * 2f;
                button.icon = Picture(rt, icon, new Vector2(iconSize, iconSize),
                    new Vector2(-size.x * 0.5f + iconSize * 0.75f, 0f), Color.white);
                button.label = Label(rt, text, fontSize, Theme.Text, TextAnchor.MiddleCenter,
                    new Vector2(labelWidth, size.y * 0.8f), new Vector2(iconSize * 0.5f, 0f), FontStyle.Bold);
            }
            else
            {
                button.label = Label(rt, text, fontSize, Theme.Text, TextAnchor.MiddleCenter,
                    new Vector2(size.x * 0.92f, size.y * 0.8f), Vector2.zero, FontStyle.Bold);
            }

            button.Init();
            return button;
        }

        /// <summary>Кнопка-иконка (например, шестерёнка настроек).</summary>
        public static UIButton IconButton(Transform parent, Sprite icon, Vector2 size, Vector2 position, Action onClick)
        {
            RectTransform rt = Rect(parent, "IconButton", size, position);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Theme.Rounded;
            img.type = Image.Type.Sliced;
            img.color = new Color(Theme.PanelLight.r, Theme.PanelLight.g, Theme.PanelLight.b, 220f);
            img.raycastTarget = false;

            var button = rt.gameObject.AddComponent<UIButton>();
            button.background = img;
            button.onClick = onClick;
            button.icon = Picture(rt, icon, size * 0.62f, Vector2.zero, Theme.Text);
            button.Init();
            return button;
        }

        /// <summary>Простой разделитель-линия.</summary>
        public static Image Divider(Transform parent, Vector2 size, Vector2 position, Color? color = null)
        {
            RectTransform rt = Rect(parent, "Divider", size, position);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color ?? Theme.Outline;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Горизонтальный ряд центрированных элементов.</summary>
        public static Vector2 RowPosition(int index, int count, float spacing, float totalWidth)
        {
            float start = -totalWidth * 0.5f + (totalWidth - spacing * (count - 1)) * 0.5f;
            return new Vector2(start + index * spacing, 0f);
        }
    }
}
