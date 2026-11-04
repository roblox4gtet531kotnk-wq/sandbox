using UnityEngine;

namespace KiviGun
{
    /// <summary>Единая тема оформления: цвета, шрифт, вспомогательные спрайты.</summary>
    public static class Theme
    {
        // ---- Палитра ----
        public static readonly Color Background = new Color32(10, 16, 20, 255);
        public static readonly Color BackgroundSoft = new Color32(18, 27, 31, 255);
        public static readonly Color Panel = new Color32(22, 33, 38, 245);
        public static readonly Color PanelLight = new Color32(32, 47, 53, 250);
        public static readonly Color PanelDark = new Color32(14, 21, 25, 250);
        public static readonly Color Outline = new Color32(58, 80, 88, 255);

        public static readonly Color Accent = new Color32(140, 198, 32, 255);      // киви-зелёный
        public static readonly Color AccentDark = new Color32(86, 128, 18, 255);
        public static readonly Color AccentLight = new Color32(196, 235, 92, 255);
        public static readonly Color Gold = new Color32(247, 191, 63, 255);
        public static readonly Color Orange = new Color32(240, 152, 60, 255);
        public static readonly Color Danger = new Color32(226, 86, 74, 255);
        public static readonly Color Blue = new Color32(92, 166, 232, 255);
        public static readonly Color Purple = new Color32(168, 120, 224, 255);

        public static readonly Color Text = new Color32(238, 245, 235, 255);
        public static readonly Color TextDim = new Color32(154, 172, 164, 255);
        public static readonly Color TextDark = new Color32(38, 50, 44, 255);
        public static readonly Color TextOnAccent = new Color32(20, 30, 12, 255);

        // ---- Шрифт ----
        private static Font _font;
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = LoadBuiltinFont();
                if (_font == null)
                {
                    string[] names = { "Arial", "Liberation Sans", "DejaVu Sans", "Helvetica" };
                    for (int i = 0; i < names.Length && _font == null; i++)
                    {
                        try { _font = Font.CreateDynamicFontFromOSFont(names[i], 32); }
                        catch { _font = null; }
                    }
                }
                return _font;
            }
        }

        private static Font LoadBuiltinFont()
        {
            // В Unity 2022+ встроенный шрифт называется LegacyRuntime.ttf, раньше — Arial.ttf.
            try
            {
                Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (f != null) return f;
            }
            catch { /* переходим к следующему варианту */ }
            try
            {
                Font f = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (f != null) return f;
            }
            catch { /* шрифт недоступен */ }
            return null;
        }

        // ---- Спрайты ----
        private static Sprite _rounded;
        private static Sprite _circle;
        private static Sprite _softCircle;

        /// <summary>Скруглённый прямоугольник (9-slice).</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int size = 48, radius = 14;
                var c = new PixelCanvas(size, size);
                c.FillRoundedRect(0, 0, size, size, radius, Color.white);
                _rounded = c.ToSprite(new Vector4(radius, radius, radius, radius));
                return _rounded;
            }
        }

        /// <summary>Круглая иконка.</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                const int size = 64;
                var c = new PixelCanvas(size, size);
                c.FillCircle(size * 0.5f, size * 0.5f, size * 0.5f - 1f, Color.white);
                _circle = c.ToSprite();
                return _circle;
            }
        }

        /// <summary>Мягкий круг (для свечения/эффектов).</summary>
        public static Sprite SoftCircle
        {
            get
            {
                if (_softCircle != null) return _softCircle;
                const int size = 96;
                var c = new PixelCanvas(size, size);
                c.FillRadialGlow(size * 0.5f, size * 0.5f, size * 0.5f - 1f, Color.white);
                _softCircle = c.ToSprite();
                return _softCircle;
            }
        }

        /// <summary>Вертикальный градиент.</summary>
        public static Sprite Gradient(Color top, Color bottom, int height = 256)
        {
            var c = new PixelCanvas(4, height);
            for (int y = 0; y < height; y++)
            {
                Color col = Color.Lerp(bottom, top, y / (float)(height - 1));
                for (int x = 0; x < 4; x++) c.Set(x, y, col);
            }
            return c.ToSprite();
        }
    }
}
