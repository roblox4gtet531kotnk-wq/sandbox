using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>
    /// Выбор уровня: карточки, звёзды, рекорды, блокировки.
    ///
    /// Сетка в один ряд — но карточки стали уже (430 px вместо 440 + промежуток
    /// 30 px), потому что уровней теперь четыре: 4×440 + 3×60 = 1940 px, а
    /// ширина экрана 1920 px — крайние карточки уезжали за край.
    /// Высота карточки увеличена до 600 px, чтобы кнопка «ИГРАТЬ» не свисала
    /// за нижний край карточки (раньше вылезала на 43 px).
    /// </summary>
    public class LevelSelectScreen : MonoBehaviour, IScreen
    {
        private RectTransform _root;

        public static LevelSelectScreen Create(Transform parent, System.Action onBack)
        {
            RectTransform rt = UIBuilder.Stretch(parent, "LevelSelect", 0f);
            var screen = rt.gameObject.AddComponent<LevelSelectScreen>();
            screen.Build(rt, onBack);
            return screen;
        }

        private void Build(RectTransform root, System.Action onBack)
        {
            _root = root;

            Image bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.04f, 0.05f, 0.88f);
            bg.raycastTarget = false;

            UIBuilder.Label(root, "ОДИНОЧНАЯ ИГРА", 56, Theme.Text, TextAnchor.MiddleCenter,
                new Vector2(1200f, 80f), new Vector2(0f, 430f), FontStyle.Bold);
            UIBuilder.Label(root, "Пройди уровень, чтобы открыть следующий", 24, Theme.TextDim,
                TextAnchor.MiddleCenter, new Vector2(1200f, 40f), new Vector2(0f, 375f));

            LevelDefinition[] levels = LevelLibrary.All;
            const float cardWidth = 430f;
            const float cardHeight = 600f;
            const float spacing = 30f;
            float total = levels.Length * cardWidth + (levels.Length - 1) * spacing;

            for (int i = 0; i < levels.Length; i++)
            {
                LevelDefinition def = levels[i];
                bool unlocked = SaveSystem.IsLevelUnlocked(i);
                bool completed = SaveSystem.IsLevelCompleted(i);

                Vector2 pos = new Vector2(-total * 0.5f + cardWidth * 0.5f + i * (cardWidth + spacing), 20f);
                RectTransform card = UIBuilder.Rect(root, "Card" + i, new Vector2(cardWidth, cardHeight), pos);

                Color bgColor = unlocked ? new Color(0.07f, 0.1f, 0.12f, 0.95f) : new Color(0.05f, 0.06f, 0.07f, 0.9f);
                UIBuilder.Panel(card, "Bg", new Vector2(cardWidth, cardHeight), Vector2.zero, bgColor);

                // Цветовая полоса темы
                UIBuilder.Panel(card, "Stripe", new Vector2(cardWidth, 10f), new Vector2(0f, cardHeight * 0.5f - 8f),
                    unlocked ? def.ThemeColor : new Color(0.25f, 0.28f, 0.3f, 1f));

                UIBuilder.Label(card, def.Subtitle.ToUpperInvariant(), 20, Theme.TextDim, TextAnchor.MiddleCenter,
                    new Vector2(cardWidth, 32f), new Vector2(0f, 245f));
                UIBuilder.Label(card, def.Title, 30, unlocked ? Theme.Text : Theme.TextDim, TextAnchor.MiddleCenter,
                    new Vector2(cardWidth - 30f, 50f), new Vector2(0f, 190f), FontStyle.Bold);

                // Иконка темы
                Sprite icon = ThemeIcon(def.Theme);
                UIBuilder.Picture(card, icon, new Vector2(110f, 110f), new Vector2(0f, 95f),
                    unlocked ? def.ThemeColor : new Color(0.3f, 0.33f, 0.35f, 0.6f));

                // Описание
                UIBuilder.Label(card, def.Description, 19, Theme.TextDim, TextAnchor.UpperCenter,
                    new Vector2(cardWidth - 40f, 110f), new Vector2(0f, -25f));

                // Звёзды
                int stars = SaveSystem.Stars(i);
                for (int s = 0; s < 3; s++)
                {
                    UIBuilder.Picture(card, IconFactory.Star(), new Vector2(36f, 36f), new Vector2((s - 1) * 44f, -115f),
                        s < stars ? Theme.Gold : new Color(0.22f, 0.25f, 0.27f, 0.8f));
                }

                // Рекорд
                int best = SaveSystem.Data.bestTimes[i];
                string bestText = best > 0 ? "Рекорд: " + (best / 60).ToString("00") + ":" + (best % 60).ToString("00") : "Рекорд: —";
                UIBuilder.Label(card, bestText, 22, Theme.TextDim, TextAnchor.MiddleCenter,
                    new Vector2(cardWidth - 40f, 34f), new Vector2(0f, -165f));

                // Награда
                UIBuilder.Picture(card, IconFactory.KiwiSlice(), new Vector2(32f, 32f), new Vector2(-40f, -205f), Color.white);
                UIBuilder.Label(card, "+" + def.RewardSlices, 24, Theme.AccentLight, TextAnchor.MiddleCenter,
                    new Vector2(120f, 34f), new Vector2(20f, -205f), FontStyle.Bold);

                if (!unlocked)
                {
                    // Замок
                    UIBuilder.Picture(card, IconFactory.Lock(), new Vector2(76f, 76f), new Vector2(0f, 85f), Color.white);
                    UIBuilder.Label(card, "ЗАКРЫТО", 26, Theme.Danger, TextAnchor.MiddleCenter,
                        new Vector2(cardWidth - 30f, 40f), new Vector2(0f, -255f), FontStyle.Bold);
                }
                else
                {
                    string label = completed ? "ПРОЙТИ ЕЩЁ" : "ИГРАТЬ";
                    UIBuilder.Button(card, label, new Vector2(cardWidth - 70f, 76f), new Vector2(0f, -255f), () =>
                    {
                        AudioManager.Instance?.PlayUiClick();
                        SceneFlow.GoToLevel(i);
                    }, null, completed ? Theme.PanelLight : Theme.Accent, 26);
                }
            }

            UIBuilder.Button(root, "НАЗАД", new Vector2(320f, 74f), new Vector2(0f, -400f), () =>
            {
                AudioManager.Instance?.PlayUiBack();
                onBack?.Invoke();
            }, IconFactory.ArrowBack());
        }

        /// <summary>Иконка темы уровня для карточки.</summary>
        private static Sprite ThemeIcon(LevelTheme theme)
        {
            switch (theme)
            {
                case LevelTheme.City: return IconFactory.Target();
                case LevelTheme.Settlement: return IconFactory.Star();
                case LevelTheme.Wheatfield: return IconFactory.Wheat();
                default: return IconFactory.Trophy();
            }
        }

        public void Dispose()
        {
            if (_root != null) Destroy(_root.gameObject);
        }
    }
}
