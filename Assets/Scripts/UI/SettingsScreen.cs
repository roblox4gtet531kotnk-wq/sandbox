using System;
using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>
    /// Экран настроек: звук, графика, управление, сброс прогресса.
    ///
    /// Вёрстка: две колонки, чтобы поместить все переключатели на экране телефона.
    ///   • Слева  — подпись в x=-300, кнопка в x=-100;
    ///   • Справа — подпись в x=130,  кнопка в x=340.
    /// Ползунки (музыка/звуки/чувствительность/FOV) занимают всю ширину панели.
    ///
    /// СОМНЕНИЕ: панель сделана выше (840 px), чем раньше (760 px) — иначе
    /// нижние переключатели уезжали за экран телефона. Если на маленьких
    /// экранах (4:3) панель начнёт обрезаться — скажи, верну компактный вид.
    /// </summary>
    public class SettingsScreen : MonoBehaviour, IScreen
    {
        private RectTransform _root;
        private static SettingsScreen _current;

        public static SettingsScreen Create(Transform parent, Action onClose)
        {
            RectTransform rt = UIBuilder.Stretch(parent, "SettingsScreen", 0f);
            var screen = rt.gameObject.AddComponent<SettingsScreen>();
            _current = screen;
            screen.Build(rt, onClose);
            return screen;
        }

        /// <summary>Закрыть экран настроек (если он открыт).</summary>
        public static void Close()
        {
            if (_current != null)
            {
                _current.Dispose();
                _current = null;
            }
        }

        private void Build(RectTransform root, Action onClose)
        {
            _root = root;

            Image bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.04f, 0.05f, 0.9f);
            bg.raycastTarget = false;

            UIBuilder.Label(root, "НАСТРОЙКИ", 56, Theme.Text, TextAnchor.MiddleCenter, new Vector2(900f, 80f), new Vector2(0f, 480f), FontStyle.Bold);

            RectTransform panel = UIBuilder.Rect(root, "Panel", new Vector2(880f, 840f), new Vector3(0f, -10f, 0f));
            UIBuilder.Panel(panel, "Bg", new Vector2(880f, 840f), Vector2.zero, new Color(0.06f, 0.09f, 0.11f, 0.95f));

            float y = 330f;

            // --- Музыка ---
            AddSlider(panel, "Громкость музыки", SaveSystem.Data.musicVolume, 0f, 1f, y, v =>
            {
                SaveSystem.Data.musicVolume = v;
                AudioManager.Instance?.ApplyVolumes();
            });
            y -= 80f;

            // --- Звуки ---
            AddSlider(panel, "Громкость звуков", SaveSystem.Data.sfxVolume, 0f, 1f, y, v =>
            {
                SaveSystem.Data.sfxVolume = v;
                AudioManager.Instance?.ApplyVolumes();
            });
            y -= 80f;

            // --- Чувствительность ---
            AddSlider(panel, "Чувствительность мыши", SaveSystem.Data.sensitivity, 0.3f, 4f, y, v =>
            {
                SaveSystem.Data.sensitivity = v;
            }, "x");
            y -= 80f;

            // --- FOV ---
            AddSlider(panel, "Поле зрения", SaveSystem.Data.fov, 55f, 105f, y, v =>
            {
                SaveSystem.Data.fov = v;
                Camera[] cams = Camera.allCameras;
                for (int i = 0; i < cams.Length; i++) cams[i].fieldOfView = v;
            });
            y -= 90f;

            // --- Качество ---
            // Сомнение по вёрстке: подпись «Качество графики» раньше перекрывалась
            // кнопками (все координаты считаются от центра панели). Убрал подпись —
            // кнопки «НИЗКОЕ / СРЕДНЕЕ / ВЫСОКОЕ / АВТО» говорят сами за себя.
            //
            // Подсветку выбранной кнопки обновляем сами: UIButton каждый кадр
            // перекрашивает фон в normalColor, поэтому менять надо именно его.
            string[] qualityNames = { "НИЗКОЕ", "СРЕДНЕЕ", "ВЫСОКОЕ", "АВТО" };
            UIButton[] qualityButtons = new UIButton[qualityNames.Length];

            void RefreshQualityButtons()
            {
                for (int i = 0; i < qualityButtons.Length; i++)
                {
                    if (qualityButtons[i] == null) continue;

                    Color color = IsQualityButtonActive(i) ? Theme.Accent : Theme.PanelLight;
                    qualityButtons[i].normalColor = color;
                    if (qualityButtons[i].background != null) qualityButtons[i].background.color = color;
                }
            }

            for (int i = 0; i < qualityNames.Length; i++)
            {
                int index = i;
                qualityButtons[i] = UIBuilder.Button(panel, qualityNames[i], new Vector2(160f, 56f),
                    new Vector2(-330f + i * 180f, y), () =>
                {
                    if (index == 3)
                    {
                        // АВТО — качество само выбирается под устройство.
                        SaveSystem.Data.qualityAuto = true;
                        SaveSystem.Save();
                        Bootstrap.ApplyQuality(Bootstrap.ResolveQualityLevel());
                        RefreshQualityButtons();
                        ToastManager.GetOrCreate().Show("Авто качество: " + DeviceAdaptation.TierLabel());
                        return;
                    }

                    SaveSystem.Data.qualityLevel = index;
                    SaveSystem.Data.qualityAuto = false;   // игрок выбрал вручную — больше не авто
                    SaveSystem.Save();
                    Bootstrap.ApplyQuality(index);
                    RefreshQualityButtons();
                    ToastManager.GetOrCreate().Show("Качество: " + qualityNames[index]);
                }, null, Theme.PanelLight, 22);
            }

            RefreshQualityButtons();
            y -= 90f;

            // --- Две колонки переключателей ---
            // Левая колонка
            AddToggle(panel, "Авто качество", -300f, -100f, y,
                () => SaveSystem.Data.qualityAuto,
                v =>
                {
                    SaveSystem.Data.qualityAuto = v;
                    SaveSystem.Save();
                    if (v) Bootstrap.ApplyQuality(Bootstrap.ResolveQualityLevel());
                    RefreshQualityButtons();
                });

            // Правая колонка
            AddToggle(panel, "Вибрация", 130f, 340f, y,
                () => SaveSystem.Data.vibration,
                v =>
                {
                    SaveSystem.Data.vibration = v;
                    SaveSystem.Save();
                    if (v && Application.isMobilePlatform) Handheld.Vibrate();
                });
            y -= 80f;

            AddToggle(panel, "Счётчик FPS", -300f, -100f, y,
                () => SaveSystem.Data.showFps,
                v =>
                {
                    SaveSystem.Data.showFps = v;
                    SaveSystem.Save();
                });

            AddToggle(panel, "Авто FPS", 130f, 340f, y,
                () => SaveSystem.Data.adaptiveFps,
                v =>
                {
                    SaveSystem.Data.adaptiveFps = v;
                    SaveSystem.Save();
                    DeviceAdaptation.AdaptiveFps = v;
                });
            y -= 80f;

            AddToggle(panel, "От 1-го лица", -300f, -100f, y,
                () => SaveSystem.Data.firstPerson,
                v =>
                {
                    SaveSystem.Data.firstPerson = v;
                    SaveSystem.Save();
                });

            AddToggle(panel, "Инверсия Y", 130f, 340f, y,
                () => SaveSystem.Data.invertY,
                v =>
                {
                    SaveSystem.Data.invertY = v;
                    SaveSystem.Save();
                });
            y -= 80f;

            AddToggle(panel, "Тряска камеры", -300f, -100f, y,
                () => SaveSystem.Data.screenShake,
                v =>
                {
                    SaveSystem.Data.screenShake = v;
                    SaveSystem.Save();
                });

            AddToggle(panel, "Сенсорный ввод", 130f, 340f, y,
                () => SaveSystem.Data.touchControls,
                v =>
                {
                    SaveSystem.Data.touchControls = v;
                    SaveSystem.Save();
                });

            // --- Кнопки внизу ---
            // Раньше «Назад» стоял на y=-440, а «Сбросить прогресс» на y=-520 —
            // при высоте экрана 1080 последняя кнопка уезжала за нижний край.
            // Теперь обе стоят рядом на y=-490.
            UIBuilder.Button(root, "Назад", new Vector2(340f, 76f), new Vector2(-190f, -490f), () =>
            {
                AudioManager.Instance?.PlayUiBack();
                onClose?.Invoke();
                Dispose();
            }, IconFactory.ArrowBack(), Theme.Accent);

            UIBuilder.Button(root, "Сбросить прогресс", new Vector2(340f, 60f), new Vector2(190f, -490f), () =>
            {
                SaveSystem.ResetProgress();
                ToastManager.GetOrCreate().Show("Прогресс сброшен");
            }, null, new Color(0.35f, 0.16f, 0.16f, 1f), 24);
        }

        /// <summary>Подсветка кнопок качества: ручной выбор или режим «АВТО».</summary>
        private static bool IsQualityButtonActive(int index)
        {
            if (index == 3) return SaveSystem.Data.qualityAuto;
            return !SaveSystem.Data.qualityAuto && SaveSystem.Data.qualityLevel == index;
        }

        /// <summary>Переключатель «ВКЛ/ВЫКЛ» с подписью слева.</summary>
        private void AddToggle(RectTransform parent, string caption, float labelX, float buttonX, float y,
                               Func<bool> getter, Action<bool> setter)
        {
            UIBuilder.Label(parent, caption, 24, Theme.TextDim, TextAnchor.MiddleLeft, new Vector2(220f, 40f), new Vector2(labelX, y));

            UIButton button = UIBuilder.Button(parent, "", new Vector2(180f, 56f), new Vector2(buttonX, y), null, null, Theme.PanelLight, 24);
            Text label = button.label;

            void Refresh()
            {
                bool value = getter();
                label.text = value ? "ВКЛ" : "ВЫКЛ";
                Color color = value ? Theme.Accent : Theme.PanelLight;
                button.normalColor = color;
                if (button.background != null) button.background.color = color;
            }

            Refresh();
            button.onClick = () =>
            {
                setter(!getter());
                SaveSystem.Save();
                Refresh();
            };
        }

        private void AddSlider(RectTransform parent, string label, float value, float min, float max, float y,
                               Action<float> onChange, string suffix = "")
        {
            UIBuilder.Label(parent, label, 26, Theme.TextDim, TextAnchor.MiddleLeft, new Vector2(420f, 40f), new Vector2(-200f, y));

            RectTransform bar = UIBuilder.Rect(parent, "SliderBar", new Vector2(320f, 16f), new Vector2(200f, y));
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = new Color(0.16f, 0.2f, 0.23f, 1f);
            barImage.raycastTarget = false;

            RectTransform fill = UIBuilder.Rect(bar, "Fill", new Vector2(0f, 16f), new Vector2(-160f, 0f));
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = Theme.Accent;
            fillImage.raycastTarget = false;

            RectTransform knob = UIBuilder.Rect(parent, "Knob", new Vector2(26f, 26f), new Vector2(200f, y));
            var knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = Theme.Circle;
            knobImage.color = Theme.Text;
            knobImage.raycastTarget = false;

            Text valueText = UIBuilder.Label(parent, "", 24, Theme.Text, TextAnchor.MiddleLeft, new Vector2(120f, 40f), new Vector2(380f, y), FontStyle.Bold);

            var slider = parent.gameObject.AddComponent<UISlider>();
            slider.Setup(bar, fill, knob, valueText, value);
            slider.min = min;
            slider.max = max;
            slider.suffix = suffix;
            slider.onChange = onChange;
            // Сохранение — один раз, когда ползунок отпустили (а не на каждое движение).
            slider.onRelease = () => SaveSystem.Save();
            slider.SetValue(value, false);
        }

        public void Dispose()
        {
            if (_root != null) Destroy(_root.gameObject);
            if (_current == this) _current = null;
        }
    }
}
