using System;
using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>
    /// Экран достижений: карточки с иконкой, описанием и прогрессом.
    /// Открывается из главного меню.
    /// </summary>
    public class AchievementsScreen : MonoBehaviour, IScreen
    {
        private RectTransform _root;

        public static AchievementsScreen Create(Transform parent, Action onBack)
        {
            RectTransform rt = UIBuilder.Stretch(parent, "Achievements", 0f);
            var screen = rt.gameObject.AddComponent<AchievementsScreen>();
            screen.Build(rt, onBack);
            return screen;
        }

        private void Build(RectTransform root, Action onBack)
        {
            _root = root;

            Image bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.04f, 0.05f, 0.9f);
            bg.raycastTarget = false;

            UIBuilder.Label(root, "ДОСТИЖЕНИЯ", 60, Theme.Text, TextAnchor.MiddleCenter,
                new Vector2(1000f, 90f), new Vector2(0f, 410f), FontStyle.Bold);
            UIBuilder.Label(root, "Собрано: " + Achievements.UnlockedCount + " / " + Achievements.List.Length,
                26, Theme.AccentLight, TextAnchor.MiddleCenter, new Vector2(1000f, 40f), new Vector2(0f, 350f));

            Achievement[] all = Achievements.List;
            float cardW = 560f;
            float cardH = 230f;
            float gapX = 60f;
            float gapY = 40f;

            for (int i = 0; i < all.Length; i++)
            {
                int column = i % 2;
                int row = i / 2;

                float x = (column == 0 ? -1f : 1f) * (cardW * 0.5f + gapX * 0.5f);
                float y = 130f - row * (cardH + gapY);

                BuildCard(all[i], new Vector2(x, y), cardW, cardH);
            }

            UIBuilder.Button(root, "НАЗАД", new Vector2(360f, 76f), new Vector2(0f, -420f), () =>
            {
                AudioManager.Instance?.PlayUiBack();
                onBack?.Invoke();
                Dispose();
            }, IconFactory.ArrowBack(), Theme.Accent);
        }

        private void BuildCard(Achievement a, Vector2 position, float width, float height)
        {
            bool unlocked = Achievements.IsUnlocked(a.Id);
            int progress = Mathf.Min(Achievements.Progress(a), a.Target);

            RectTransform card = UIBuilder.Rect(_root, "Ach_" + a.Id, new Vector2(width, height), position);
            UIBuilder.Panel(card, "Bg", new Vector2(width, height), Vector2.zero,
                unlocked ? new Color(0.10f, 0.13f, 0.09f, 0.96f) : new Color(0.06f, 0.09f, 0.11f, 0.94f));

            // Рамка: у полученных — золотая
            UIBuilder.Panel(card, "Frame", new Vector2(width - 16f, height - 16f), Vector2.zero,
                unlocked ? new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.9f) : new Color(0.14f, 0.19f, 0.22f, 1f));

            // Иконка
            Sprite icon = a.Icon != null ? a.Icon() : IconFactory.Trophy();
            UIBuilder.Picture(card, icon, new Vector2(96f, 96f), new Vector2(-width * 0.5f + 90f, 20f),
                unlocked ? Color.white : new Color(0.45f, 0.5f, 0.52f, 0.7f));

            UIBuilder.Label(card, a.Title, 28, unlocked ? Theme.Gold : Theme.Text, TextAnchor.MiddleLeft,
                new Vector2(width - 240f, 40f), new Vector2(70f, height * 0.5f - 52f), FontStyle.Bold);

            UIBuilder.Label(card, a.Description, 22, Theme.TextDim, TextAnchor.MiddleLeft,
                new Vector2(width - 240f, 36f), new Vector2(70f, height * 0.5f - 96f));

            // Прогресс
            string progressText = unlocked ? "ПОЛУЧЕНО" : progress + " / " + a.Target;
            UIBuilder.Label(card, progressText, 24, unlocked ? Theme.AccentLight : Theme.TextDim,
                TextAnchor.MiddleLeft, new Vector2(240f, 34f), new Vector2(70f, -height * 0.5f + 48f), FontStyle.Bold);

            // Полоска прогресса
            float barW = width - 240f;
            RectTransform bar = UIBuilder.Rect(card, "Bar", new Vector2(barW, 12f), new Vector2(70f, -height * 0.5f + 84f));
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = new Color(0.12f, 0.16f, 0.18f, 1f);
            barImage.raycastTarget = false;

            float fill = a.Target > 0 ? Mathf.Clamp01((float)progress / a.Target) : 0f;
            if (fill > 0.01f)
            {
                RectTransform fillRect = UIBuilder.Rect(bar, "Fill", new Vector2(barW * fill, 12f),
                    new Vector2(-barW * 0.5f + barW * fill * 0.5f, 0f));
                var fillImage = fillRect.gameObject.AddComponent<Image>();
                fillImage.color = unlocked ? Theme.Gold : Theme.Accent;
                fillImage.raycastTarget = false;
            }
        }

        public void Dispose()
        {
            if (_root != null) Destroy(_root.gameObject);
        }
    }
}
