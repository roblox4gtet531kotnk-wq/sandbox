using System;
using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>Экран итогов уровня: статистика, звёзды, награда и кнопки.</summary>
    public class ResultsScreen : MonoBehaviour, IScreen
    {
        private RectTransform _root;

        public static ResultsScreen Create(Transform parent, bool victory, LevelDefinition definition, float time,
                                          int kills, int collected, int stars, Action<string> onAction)
        {
            RectTransform rt = UIBuilder.Stretch(parent, "Results", 0f);
            var screen = rt.gameObject.AddComponent<ResultsScreen>();
            screen.Build(rt, victory, definition, time, kills, collected, stars, onAction);
            return screen;
        }

        private void Build(RectTransform root, bool victory, LevelDefinition definition, float time,
                           int kills, int collected, int stars, Action<string> onAction)
        {
            _root = root;

            Image bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.05f, 0.06f, 0.82f);
            bg.raycastTarget = false;

            Color accent = victory ? Theme.Accent : Theme.Danger;
            UIBuilder.Label(root, victory ? "УРОВЕНЬ ПРОЙДЕН!" : "ПОПЫТКА ПРОВАЛЕНА", 64, accent,
                TextAnchor.MiddleCenter, new Vector2(1400f, 100f), new Vector2(0f, 340f), FontStyle.Bold);
            UIBuilder.Label(root, definition != null ? definition.Title : "", 30, Theme.TextDim,
                TextAnchor.MiddleCenter, new Vector2(1000f, 50f), new Vector2(0f, 280f));

            // Звёзды
            for (int i = 0; i < 3; i++)
            {
                UIBuilder.Picture(root, IconFactory.Star(), new Vector2(84f, 84f), new Vector2((i - 1) * 110f, 180f),
                    i < stars ? Theme.Gold : new Color(0.2f, 0.24f, 0.26f, 0.8f));
            }

            // Статистика
            RectTransform stats = UIBuilder.Rect(root, "Stats", new Vector2(720f, 300f), new Vector2(0f, -20f));
            UIBuilder.Panel(stats, "Bg", new Vector2(720f, 300f), Vector2.zero, new Color(0f, 0f, 0f, 0.4f));

            string minutes = Mathf.FloorToInt(time / 60f).ToString("00");
            string seconds = Mathf.FloorToInt(time % 60f).ToString("00");

            AddStat(stats, "Время", minutes + ":" + seconds, new Vector2(0f, 100f));
            AddStat(stats, "Убито киви", kills.ToString(), new Vector2(0f, 40f));
            AddStat(stats, "Собрано дольек", collected.ToString(), new Vector2(0f, -20f));
            AddStat(stats, "Награда за уровень", "+" + (definition != null ? definition.RewardSlices : 0), new Vector2(0f, -80f));

            // Кнопки
            bool hasNext = definition != null && definition.Index + 1 < GameConfig.LevelCount && victory;

            if (hasNext)
            {
                UIBuilder.Button(root, "Следующий уровень", new Vector2(560f, 88f), new Vector2(0f, -270f), () =>
                {
                    onAction?.Invoke("next");
                }, null, Theme.Accent);
            }

            UIBuilder.Button(root, victory ? "Пройти ещё раз" : "Попробовать снова", new Vector2(560f, 88f),
                new Vector2(0f, hasNext ? -370f : -270f), () =>
                {
                    onAction?.Invoke("retry");
                });

            UIBuilder.Button(root, "В главное меню", new Vector2(560f, 88f),
                new Vector2(0f, hasNext ? -470f : -370f), () =>
                {
                    onAction?.Invoke("menu");
                });

            UIBuilder.Label(root, "Киви-дольки: " + SaveSystem.Slices, 26, Theme.AccentLight,
                TextAnchor.MiddleCenter, new Vector2(600f, 40f), new Vector2(0f, -540f), FontStyle.Bold);
        }

        private static void AddStat(RectTransform parent, string label, string value, Vector2 position)
        {
            UIBuilder.Label(parent, label, 26, Theme.TextDim, TextAnchor.MiddleLeft, new Vector2(320f, 44f), position + new Vector2(-150f, 0f));
            UIBuilder.Label(parent, value, 30, Theme.Text, TextAnchor.MiddleRight, new Vector2(240f, 44f), position + new Vector2(190f, 0f), FontStyle.Bold);
        }

        public void Dispose()
        {
            if (_root != null) Destroy(_root.gameObject);
        }
    }
}
