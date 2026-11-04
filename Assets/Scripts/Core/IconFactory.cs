using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Генератор иконок: всё рисуется пиксель за пикселем в рантайме.
    /// Никаких PNG-файлов — игра самодостаточна.
    /// </summary>
    public static class IconFactory
    {
        // ---------------- Кэш спрайтов ----------------
        //
        // Иконки рисуются попиксельно в рантайме. Если создавать их каждый раз,
        // магазин при каждом клике «съедает» десятки текстур 96×96 —
        // на телефоне это десятки мегабайт за несколько минут.
        // Поэтому спрайты кэшируются и переиспользуются.

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> Cache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        private static Sprite Cached(string key, System.Func<Sprite> build)
        {
            Sprite sprite;
            if (Cache.TryGetValue(key, out sprite) && sprite != null) return sprite;
            sprite = build();
            Cache[key] = sprite;
            return sprite;
        }

        private const int S = 96;

        // ---------------- Валюта ----------------

        /// <summary>Долька киви: коричневая кожура, зелёная мякоть, белое сердцевина, чёрные семечки.</summary>
        public static Sprite KiwiSlice(int size = S)
        {
            return Cached("KiwiSlice" + size, () => Build_KiwiSlice(size));
        }

        private static Sprite Build_KiwiSlice(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float cx = size * 0.5f, cy = size * 0.5f;
            float r = size * 0.46f;

            c.FillEllipse(cx, cy, r, r * 0.92f, new Color32(122, 82, 46, 255));      // кожура
            c.FillEllipse(cx, cy, r * 0.88f, r * 0.80f, new Color32(122, 196, 40, 255)); // мякоть
            c.FillEllipse(cx, cy, r * 0.42f, r * 0.38f, new Color32(238, 243, 214, 255)); // сердцевина

            // семечки по кругу
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                float rad = r * 0.62f;
                float px = cx + Mathf.Cos(a) * rad;
                float py = cy + Mathf.Sin(a) * rad * 0.9f;
                c.FillEllipse(px, py, size * 0.022f, size * 0.032f, new Color32(28, 26, 24, 255));
            }
            return c.ToSprite();
        }

        // ---------------- Оружие ----------------

        /// <summary>Силуэт пистолета.</summary>
        public static Sprite Pistol(int size = S)
        {
            return Cached("Pistol" + size, () => Build_Pistol(size));
        }

        private static Sprite Build_Pistol(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            Color body = new Color32(58, 66, 74, 255);
            Color dark = new Color32(34, 40, 46, 255);
            Color accent = new Color32(140, 198, 32, 255);

            // ствол и рамка
            c.FillRoundedRect(w * 0.16f, h * 0.55f, w * 0.62f, h * 0.16f, h * 0.05f, body);
            c.FillRoundedRect(w * 0.16f, h * 0.44f, w * 0.20f, h * 0.16f, h * 0.04f, dark);
            // рукоять
            c.FillRoundedRect(w * 0.24f, h * 0.18f, w * 0.16f, h * 0.30f, h * 0.05f, dark);
            // дуло
            c.FillRect(w * 0.72f, h * 0.56f, w * 0.10f, h * 0.10f, dark);
            // козырёк
            c.FillRect(w * 0.22f, h * 0.42f, w * 0.05f, h * 0.16f, accent);
            return c.ToSprite();
        }

        /// <summary>Силуэт револьвера (с барабаном).</summary>
        public static Sprite Revolver(int size = S)
        {
            return Cached("Revolver" + size, () => Build_Revolver(size));
        }

        private static Sprite Build_Revolver(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            Color body = new Color32(72, 78, 88, 255);
            Color dark = new Color32(40, 46, 54, 255);
            Color accent = new Color32(247, 191, 63, 255);

            c.FillRoundedRect(w * 0.18f, h * 0.58f, w * 0.58f, h * 0.14f, h * 0.05f, body);
            c.FillCircle(w * 0.44f, h * 0.52f, h * 0.13f, dark);
            c.FillCircle(w * 0.44f, h * 0.52f, h * 0.05f, accent);
            c.FillRoundedRect(w * 0.26f, h * 0.20f, w * 0.15f, h * 0.28f, h * 0.05f, dark);
            c.FillRect(w * 0.70f, h * 0.59f, w * 0.10f, h * 0.10f, dark);
            return c.ToSprite();
        }

        // ---------------- Шапки ----------------

        /// <summary>Шляпа-киви: зелёная кепка с долькой киви сверху.</summary>
        public static Sprite HatKiwi(int size = S)
        {
            return Cached("HatKiwi" + size, () => Build_HatKiwi(size));
        }

        private static Sprite Build_HatKiwi(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            Color cap = new Color32(112, 168, 40, 255);
            Color capDark = new Color32(74, 116, 26, 255);
            c.FillRoundedRect(w * 0.20f, h * 0.28f, w * 0.60f, h * 0.30f, h * 0.14f, cap);
            c.FillRoundedRect(w * 0.14f, h * 0.20f, w * 0.72f, h * 0.12f, h * 0.06f, capDark);
            c.FillEllipse(w * 0.50f, h * 0.62f, w * 0.16f, h * 0.16f, new Color32(122, 196, 40, 255));
            c.FillEllipse(w * 0.50f, h * 0.62f, w * 0.07f, h * 0.07f, new Color32(238, 243, 214, 255));
            return c.ToSprite();
        }

        /// <summary>Цилиндр.</summary>
        public static Sprite HatTop(int size = S)
        {
            return Cached("HatTop" + size, () => Build_HatTop(size));
        }

        private static Sprite Build_HatTop(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            c.FillRoundedRect(w * 0.14f, h * 0.18f, w * 0.72f, h * 0.10f, h * 0.04f, new Color32(30, 34, 40, 255));
            c.FillRect(w * 0.26f, h * 0.28f, w * 0.48f, h * 0.44f, new Color32(42, 48, 56, 255));
            c.FillRect(w * 0.26f, h * 0.30f, w * 0.48f, h * 0.09f, new Color32(226, 86, 74, 255));
            return c.ToSprite();
        }

        /// <summary>Кепка с пропеллером.</summary>
        public static Sprite HatPropeller(int size = S)
        {
            return Cached("HatPropeller" + size, () => Build_HatPropeller(size));
        }

        private static Sprite Build_HatPropeller(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            c.FillRoundedRect(w * 0.22f, h * 0.24f, w * 0.56f, h * 0.26f, h * 0.12f, new Color32(226, 86, 74, 255));
            c.FillRect(w * 0.12f, h * 0.24f, w * 0.24f, h * 0.07f, new Color32(176, 60, 52, 255));
            c.FillRect(w * 0.48f, h * 0.50f, w * 0.04f, h * 0.18f, new Color32(58, 66, 74, 255));
            c.FillEllipse(w * 0.42f, h * 0.68f, w * 0.13f, h * 0.045f, new Color32(92, 166, 232, 255));
            c.FillEllipse(w * 0.60f, h * 0.68f, w * 0.13f, h * 0.045f, new Color32(92, 166, 232, 255));
            return c.ToSprite();
        }

        /// <summary>Корона.</summary>
        public static Sprite HatCrown(int size = S)
        {
            return Cached("HatCrown" + size, () => Build_HatCrown(size));
        }

        private static Sprite Build_HatCrown(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            Color gold = new Color32(247, 191, 63, 255);
            Color goldDark = new Color32(196, 140, 26, 255);
            var pts = new[]
            {
                new Vector2(w * 0.18f, h * 0.30f),
                new Vector2(w * 0.18f, h * 0.66f),
                new Vector2(w * 0.32f, h * 0.50f),
                new Vector2(w * 0.46f, h * 0.72f),
                new Vector2(w * 0.60f, h * 0.50f),
                new Vector2(w * 0.74f, h * 0.66f),
                new Vector2(w * 0.74f, h * 0.30f),
            };
            c.FillPolygon(pts, gold);
            c.FillRect(w * 0.18f, h * 0.22f, w * 0.56f, h * 0.10f, goldDark);
            c.FillCircle(w * 0.46f, h * 0.40f, h * 0.045f, new Color32(226, 86, 74, 255));
            return c.ToSprite();
        }

        /// <summary>Праздничный колпак.</summary>
        public static Sprite HatParty(int size = S)
        {
            return Cached("HatParty" + size, () => Build_HatParty(size));
        }

        private static Sprite Build_HatParty(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            var pts = new[]
            {
                new Vector2(w * 0.18f, h * 0.26f),
                new Vector2(w * 0.82f, h * 0.26f),
                new Vector2(w * 0.58f, h * 0.76f),
            };
            c.FillPolygon(pts, new Color32(168, 120, 224, 255));
            c.FillCircle(w * 0.58f, h * 0.80f, h * 0.08f, new Color32(247, 191, 63, 255));
            c.FillCircle(w * 0.36f, h * 0.44f, h * 0.045f, new Color32(247, 191, 63, 255));
            c.FillCircle(w * 0.58f, h * 0.40f, h * 0.045f, new Color32(92, 166, 232, 255));
            return c.ToSprite();
        }

        // ---------------- Служебные иконки ----------------

        public static Sprite Star(int size = S, bool filled = true)
        {
            return Cached("Star" + size + "_" + filled, () => Build_Star(size, filled));
        }

        private static Sprite Build_Star(int size = S, bool filled = true)
        {
            var c = new PixelCanvas(size, size);
            float cx = size * 0.5f, cy = size * 0.5f, r = size * 0.44f;
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float rad = (i % 2 == 0) ? r : r * 0.45f;
                pts[i] = new Vector2(cx + Mathf.Cos(ang) * rad, cy + Mathf.Sin(ang) * rad);
            }
            c.FillPolygon(pts, filled ? new Color32(247, 191, 63, 255) : new Color32(90, 104, 100, 255));
            return c.ToSprite();
        }

        public static Sprite Lock(int size = S)
        {
            return Cached("Lock" + size, () => Build_Lock(size));
        }

        private static Sprite Build_Lock(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            c.StrokeCircle(w * 0.5f, h * 0.62f, w * 0.20f, w * 0.055f, new Color32(210, 220, 214, 255));
            c.FillRoundedRect(w * 0.30f, h * 0.22f, w * 0.40f, h * 0.32f, h * 0.06f, new Color32(247, 191, 63, 255));
            c.FillCircle(w * 0.5f, h * 0.36f, w * 0.045f, new Color32(60, 50, 20, 255));
            return c.ToSprite();
        }

        public static Sprite Gear(int size = S)
        {
            return Cached("Gear" + size, () => Build_Gear(size));
        }

        private static Sprite Build_Gear(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            Color col = new Color32(210, 220, 214, 255);
            c.FillCircle(w * 0.5f, h * 0.5f, w * 0.30f, col);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                float px = w * 0.5f + Mathf.Cos(a) * w * 0.36f;
                float py = h * 0.5f + Mathf.Sin(a) * h * 0.36f;
                c.FillCircle(px, py, w * 0.075f, col);
            }
            c.FillCircle(w * 0.5f, h * 0.5f, w * 0.13f, new Color32(20, 27, 31, 255));
            return c.ToSprite();
        }

        public static Sprite ArrowBack(int size = S)
        {
            return Cached("ArrowBack" + size, () => Build_ArrowBack(size));
        }

        private static Sprite Build_ArrowBack(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            Color col = new Color32(230, 240, 232, 255);
            var pts = new[]
            {
                new Vector2(w * 0.24f, h * 0.50f),
                new Vector2(w * 0.52f, h * 0.78f),
                new Vector2(w * 0.52f, h * 0.62f),
                new Vector2(w * 0.78f, h * 0.62f),
                new Vector2(w * 0.78f, h * 0.38f),
                new Vector2(w * 0.52f, h * 0.38f),
                new Vector2(w * 0.52f, h * 0.22f),
            };
            c.FillPolygon(pts, col);
            return c.ToSprite();
        }

        public static Sprite Play(int size = S)
        {
            return Cached("Play" + size, () => Build_Play(size));
        }

        private static Sprite Build_Play(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            var pts = new[]
            {
                new Vector2(w * 0.32f, h * 0.22f),
                new Vector2(w * 0.80f, h * 0.50f),
                new Vector2(w * 0.32f, h * 0.78f),
            };
            c.FillPolygon(pts, new Color32(140, 198, 32, 255));
            return c.ToSprite();
        }

        public static Sprite Music(int size = S)
        {
            return Cached("Music" + size, () => Build_Music(size));
        }

        private static Sprite Build_Music(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            Color col = new Color32(140, 198, 32, 255);
            c.FillRect(w * 0.38f, h * 0.22f, w * 0.06f, h * 0.48f, col);
            c.FillRect(w * 0.38f, h * 0.64f, w * 0.34f, h * 0.06f, col);
            c.FillRect(w * 0.66f, h * 0.28f, w * 0.06f, h * 0.42f, col);
            c.FillCircle(w * 0.32f, h * 0.24f, w * 0.09f, col);
            c.FillCircle(w * 0.60f, h * 0.30f, w * 0.09f, col);
            return c.ToSprite();
        }

        public static Sprite Trophy(int size = S)
        {
            return Cached("Trophy" + size, () => Build_Trophy(size));
        }

        private static Sprite Build_Trophy(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            Color gold = new Color32(247, 191, 63, 255);
            c.FillRoundedRect(w * 0.30f, h * 0.38f, w * 0.40f, h * 0.30f, h * 0.06f, gold);
            c.StrokeCircle(w * 0.20f, h * 0.56f, w * 0.11f, w * 0.04f, gold);
            c.StrokeCircle(w * 0.80f, h * 0.56f, w * 0.11f, w * 0.04f, gold);
            c.FillRect(w * 0.45f, h * 0.24f, w * 0.10f, h * 0.16f, gold);
            c.FillRoundedRect(w * 0.28f, h * 0.14f, w * 0.44f, h * 0.11f, h * 0.04f, gold);
            return c.ToSprite();
        }

        public static Sprite Heart(int size = S)
        {
            return Cached("Heart" + size, () => Build_Heart(size));
        }

        private static Sprite Build_Heart(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            Color col = new Color32(226, 86, 74, 255);
            c.FillCircle(w * 0.36f, h * 0.60f, w * 0.19f, col);
            c.FillCircle(w * 0.64f, h * 0.60f, w * 0.19f, col);
            var pts = new[]
            {
                new Vector2(w * 0.18f, h * 0.58f),
                new Vector2(w * 0.82f, h * 0.58f),
                new Vector2(w * 0.50f, h * 0.20f),
            };
            c.FillPolygon(pts, col);
            return c.ToSprite();
        }

        public static Sprite Skull(int size = S)
        {
            return Cached("Skull" + size, () => Build_Skull(size));
        }

        private static Sprite Build_Skull(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            Color bone = new Color32(232, 238, 230, 255);
            c.FillCircle(w * 0.5f, h * 0.58f, w * 0.30f, bone);
            c.FillRoundedRect(w * 0.34f, h * 0.22f, w * 0.32f, h * 0.22f, h * 0.05f, bone);
            c.FillCircle(w * 0.40f, h * 0.62f, w * 0.085f, new Color32(24, 30, 34, 255));
            c.FillCircle(w * 0.60f, h * 0.62f, w * 0.085f, new Color32(24, 30, 34, 255));
            c.FillRect(w * 0.46f, h * 0.30f, w * 0.04f, h * 0.14f, new Color32(24, 30, 34, 255));
            return c.ToSprite();
        }

        public static Sprite Target(int size = S)
        {
            return Cached("Target" + size, () => Build_Target(size));
        }

        private static Sprite Build_Target(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            c.FillCircle(w * 0.5f, h * 0.5f, w * 0.42f, new Color32(226, 86, 74, 255));
            c.FillCircle(w * 0.5f, h * 0.5f, w * 0.28f, new Color32(238, 244, 236, 255));
            c.FillCircle(w * 0.5f, h * 0.5f, w * 0.13f, new Color32(226, 86, 74, 255));
            return c.ToSprite();
        }

        public static Sprite Clock(int size = S)
        {
            return Cached("Clock" + size, () => Build_Clock(size));
        }

        private static Sprite Build_Clock(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            c.FillCircle(w * 0.5f, h * 0.5f, w * 0.40f, new Color32(92, 166, 232, 255));
            c.FillCircle(w * 0.5f, h * 0.5f, w * 0.34f, new Color32(240, 245, 238, 255));
            c.FillRect(w * 0.48f, h * 0.52f, w * 0.04f, h * 0.20f, new Color32(30, 40, 44, 255));
            c.FillRect(w * 0.50f, h * 0.48f, w * 0.16f, h * 0.04f, new Color32(30, 40, 44, 255));
            return c.ToSprite();
        }

        /// <summary>Логотип-уточка (для меню).</summary>
        public static Sprite Duck(int size = S)
        {
            return Cached("Duck" + size, () => Build_Duck(size));
        }

        private static Sprite Build_Duck(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            Color body = new Color32(246, 244, 232, 255);
            Color dark = new Color32(210, 206, 190, 255);
            Color beak = new Color32(242, 160, 54, 255);
            c.FillEllipse(w * 0.48f, h * 0.42f, w * 0.30f, h * 0.26f, body);
            c.FillCircle(w * 0.68f, h * 0.64f, w * 0.16f, body);
            var pts = new[] { new Vector2(w * 0.78f, h * 0.66f), new Vector2(w * 0.98f, h * 0.61f), new Vector2(w * 0.78f, h * 0.56f) };
            c.FillPolygon(pts, beak);
            c.FillCircle(w * 0.72f, h * 0.68f, w * 0.032f, new Color32(28, 32, 34, 255));
            c.FillEllipse(w * 0.36f, h * 0.30f, w * 0.16f, h * 0.09f, dark);
            c.FillRect(w * 0.30f, h * 0.14f, w * 0.05f, h * 0.18f, beak);
            c.FillRect(w * 0.52f, h * 0.14f, w * 0.05f, h * 0.18f, beak);
            return c.ToSprite();
        }

        /// <summary>Колос пшеницы — иконка темы «Пшеничное поле».</summary>
        public static Sprite Wheat(int size = S)
        {
            return Cached("Wheat" + size, () => Build_Wheat(size));
        }

        private static Sprite Build_Wheat(int size = S)
        {
            var c = new PixelCanvas(size, size);
            float w = size, h = size;
            Color stalk = new Color32(206, 168, 74, 255);
            Color grain = new Color32(238, 202, 112, 255);
            Color grainDark = new Color32(214, 172, 82, 255);
            Color leaf = new Color32(128, 168, 76, 255);

            // Стебель
            c.FillRect(w * 0.47f, h * 0.12f, w * 0.06f, h * 0.62f, stalk);

            // Листья по бокам стебля
            c.FillEllipse(w * 0.33f, h * 0.32f, w * 0.15f, w * 0.055f, leaf);
            c.FillEllipse(w * 0.67f, h * 0.40f, w * 0.15f, w * 0.055f, leaf);

            // Колосья — пары зёрен, сужающиеся кверху
            for (int i = 0; i < 5; i++)
            {
                float t = i / 5f;
                float y = h * (0.62f + t * 0.30f);
                float rx = w * (0.085f - t * 0.012f);
                float ry = w * 0.048f;
                Color col = (i % 2 == 0) ? grain : grainDark;
                c.FillEllipse(w * 0.50f - rx * 0.85f, y, rx, ry, col);
                c.FillEllipse(w * 0.50f + rx * 0.85f, y, rx, ry, col);
            }

            // Макушка
            c.FillEllipse(w * 0.50f, h * 0.95f, w * 0.045f, w * 0.075f, grain);
            return c.ToSprite();
        }
    }
}
