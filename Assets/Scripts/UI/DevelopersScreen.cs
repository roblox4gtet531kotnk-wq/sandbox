using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>Экран «Разработчики» — визитная студии «Kivi Games».</summary>
    public class DevelopersScreen : MonoBehaviour, IScreen
    {
        private RectTransform _root;

        public static DevelopersScreen Create(Transform parent, System.Action onBack)
        {
            RectTransform rt = UIBuilder.Stretch(parent, "Developers", 0f);
            var screen = rt.gameObject.AddComponent<DevelopersScreen>();
            screen.Build(rt, onBack);
            return screen;
        }

        private void Build(RectTransform root, System.Action onBack)
        {
            _root = root;

            Image bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.04f, 0.05f, 0.9f);
            bg.raycastTarget = false;

            UIBuilder.Label(root, "РАЗРАБОТЧИКИ", 60, Theme.Text, TextAnchor.MiddleCenter,
                new Vector2(1000f, 90f), new Vector2(0f, 400f), FontStyle.Bold);
            UIBuilder.Label(root, GameConfig.Company + " • команда из трёх человек и одного алгоритма",
                24, Theme.TextDim, TextAnchor.MiddleCenter, new Vector2(1200f, 40f), new Vector2(0f, 340f));

            AddCard(root, new Vector2(0f, 80f), "Данил", "ГЛАВНЫЙ РАЗРАБОТЧИК",
                "1 000 000 000 строк кода", "Все баги разбежались. К счастью.",
                new Color32(140, 198, 32, 255), IconFactory.Duck());

            AddCard(root, new Vector2(-640f, 80f), "Коля", "ДИЗАЙНЕР",
                "0 строк кода, 100% вкуса", "Рисует киви-птиц по 40 часов в сутки.",
                new Color32(92, 166, 232, 255), IconFactory.KiwiSlice());

            AddCard(root, new Vector2(640f, 80f), "ИИ", "ОСНОВАТЕЛЬ",
                "∞ строк кода", "Основал студию. Сам же и написал игру. И вставил баги.",
                new Color32(168, 120, 224, 255), IconFactory.Gear());

            // Конфигурация игры
            RectTransform cfg = UIBuilder.Rect(root, "Config", new Vector2(1400f, 260f), new Vector2(0f, -230f));
            UIBuilder.Panel(cfg, "Bg", new Vector2(1400f, 260f), Vector2.zero, new Color(0f, 0f, 0f, 0.4f));
            UIBuilder.Label(cfg, "КОНФИГУРАЦИЯ ИГРЫ", 24, Theme.AccentLight, TextAnchor.MiddleLeft,
                new Vector2(600f, 40f), new Vector2(-600f, 100f), FontStyle.Bold);

            string[,] config =
            {
                { "Название", GameConfig.GameTitle + " (" + GameConfig.GameTitleLatin + ")" },
                { "Движок", GameConfig.Engine },
                { "Платформа", GameConfig.Platform },
                { "Версия", GameConfig.Version },
                { "Валюта", GameConfig.Currency },
                { "Режимы", "Одиночный • Мультиплеер (в разработке)" },
            };

            for (int i = 0; i < config.GetLength(0); i++)
            {
                float x = -300f + (i % 2) * 600f;
                float y = 50f - (i / 2) * 52f;
                UIBuilder.Label(cfg, config[i, 0], 22, Theme.TextDim, TextAnchor.MiddleLeft, new Vector2(220f, 36f), new Vector2(x, y));
                UIBuilder.Label(cfg, config[i, 1], 24, Theme.Text, TextAnchor.MiddleLeft, new Vector2(400f, 36f), new Vector2(x + 130f, y), FontStyle.Bold);
            }

            UIBuilder.Button(root, "НАЗАД", new Vector2(320f, 74f), new Vector2(0f, -400f), () =>
            {
                AudioManager.Instance?.PlayUiBack();
                onBack?.Invoke();
            }, IconFactory.ArrowBack());
        }

        private static void AddCard(RectTransform root, Vector2 position, string name, string role,
                                    string lines, string fact, Color color, Sprite icon)
        {
            RectTransform card = UIBuilder.Rect(root, "Card_" + name, new Vector2(560f, 440f), position);
            UIBuilder.Panel(card, "Bg", new Vector2(560f, 440f), Vector2.zero, new Color(0.07f, 0.1f, 0.12f, 0.95f));
            UIBuilder.Panel(card, "Stripe", new Vector2(560f, 8f), new Vector2(0f, 216f), color);

            if (icon != null)
                UIBuilder.Picture(card, icon, new Vector2(120f, 120f), new Vector2(0f, 110f), color);

            UIBuilder.Label(card, name, 40, Theme.Text, TextAnchor.MiddleCenter,
                new Vector2(520f, 56f), new Vector2(0f, -30f), FontStyle.Bold);
            UIBuilder.Label(card, role, 22, color, TextAnchor.MiddleCenter,
                new Vector2(520f, 36f), new Vector2(0f, -85f), FontStyle.Bold);
            UIBuilder.Label(card, lines, 26, Theme.Gold, TextAnchor.MiddleCenter,
                new Vector2(520f, 40f), new Vector2(0f, -135f), FontStyle.Bold);
            UIBuilder.Label(card, fact, 20, Theme.TextDim, TextAnchor.UpperCenter,
                new Vector2(500f, 70f), new Vector2(0f, -190f));
        }

        public void Dispose()
        {
            if (_root != null) Destroy(_root.gameObject);
        }
    }
}
