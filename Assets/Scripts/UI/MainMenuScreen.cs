using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>
    /// Главное меню: 3D-диорама с уткой и киви, заголовок и пункты:
    /// одиночная игра, мультиплеер (в разработке), магазин, достижения, разработчики.
    ///
    /// Анимации: заголовок пульсирует и появляется с fade-in, кнопки выезжают
    /// по очереди, диорама медленно крутится, уточка и киви покачиваются.
    /// </summary>
    public class MainMenuScreen : MonoBehaviour, IScreen
    {
        private RectTransform _root;
        private GameObject _diorama;
        private IScreen _current;
        private Text _currencyText;
        private Text _titleText;
        private Text _subtitleText;
        private readonly System.Collections.Generic.List<RectTransform> _menuButtons =
            new System.Collections.Generic.List<RectTransform>();

        /// <summary>Кнопки главного меню (плюс шестерёнка настроек).</summary>
        private readonly System.Collections.Generic.List<RectTransform> _interactive =
            new System.Collections.Generic.List<RectTransform>();

        public static MainMenuScreen Create(Transform uiParent)
        {
            RectTransform rt = UIBuilder.Stretch(uiParent, "MainMenu", 0f);
            var screen = rt.gameObject.AddComponent<MainMenuScreen>();
            screen.Build(rt);
            // Кнопка «назад» в меню: закрывает подменю, а из главного меню
            // выходит из приложения (обычное поведение на Android).
            BackNavigator.Push(screen.HandleBack);
            return screen;
        }

        private void Build(RectTransform root)
        {
            _root = root;

            BuildDiorama();

            // Фоновая подложка (градиент)
            Image bg = root.gameObject.AddComponent<Image>();
            bg.sprite = Theme.Gradient(new Color32(16, 26, 30, 120), new Color32(6, 10, 13, 220));
            bg.color = new Color(1f, 1f, 1f, 0.55f);
            bg.raycastTarget = false;

            // Заголовок
            Text title = UIBuilder.Label(root, GameConfig.GameTitle, 96, Theme.Text, TextAnchor.MiddleCenter,
                new Vector2(1200f, 140f), new Vector2(0f, 400f), FontStyle.Bold);
            title.supportRichText = true;
            title.text = "<color=#A8E63C>КИВИ</color> <color=#FFC93F>ГАН</color>";
            _titleText = title;

            // Мягкое появление всей панели меню + лёгкое «дыхание» заголовка.
            var entrance = root.gameObject.AddComponent<MenuEntranceAnimation>();
            entrance.SetTitle(title.transform as RectTransform);

            // Лёгкая ровная пульсация заголовка — чистый «КИВИ ГАН» без мерцания.
            var titleTween = title.transform.gameObject.AddComponent<UiTween>();
            titleTween.mode = UiTween.Mode.Pulse;
            titleTween.amplitude = 0.035f;
            titleTween.speed = 1.6f;

            _subtitleText = UIBuilder.Label(root, "ВЫДЕРЖИ НОЧЬ • " + GameConfig.LevelCount +
                                               " УРОВНЯ • УТКА ПРОТИВ КИВИ-ПТИЦ", 24, Theme.TextDim,
                TextAnchor.MiddleCenter, new Vector2(1200f, 40f), new Vector2(0f, 310f));

            // --- Кнопки --- колонка СЛЕВА, как в меню FNAF: вертикальный список
            // у левого края экрана, а справа — диорама с персонажами.
            const float menuX = -430f;
            float startY = 180f;
            AddMenuButton(root, "ОДИНОЧНАЯ ИГРА", new Vector2(560f, 92f), new Vector2(menuX, startY), OpenLevelSelect,
                IconFactory.Play(), Theme.Accent);
            AddMenuButton(root, "МУЛЬТИПЛЕЕР (В РАЗРАБОТКЕ)", new Vector2(560f, 82f), new Vector2(menuX, startY - 106f),
                () => ToastManager.GetOrCreate().Show("Мультиплеер уже в разработке!"), null, Theme.PanelLight);
            AddMenuButton(root, "МАГАЗИН", new Vector2(560f, 92f), new Vector2(menuX, startY - 212f), OpenShop,
                IconFactory.KiwiSlice(), Theme.PanelLight);
            AddMenuButton(root, "ДОСТИЖЕНИЯ", new Vector2(560f, 82f), new Vector2(menuX, startY - 318f), OpenAchievements,
                IconFactory.Trophy(), Theme.PanelLight);
            AddMenuButton(root, "РАЗРАБОТЧИКИ", new Vector2(560f, 82f), new Vector2(menuX, startY - 424f), OpenDevelopers,
                IconFactory.Duck(), Theme.PanelLight);

            // Валюта
            RectTransform currency = UIBuilder.Rect(root, "Currency", new Vector2(320f, 64f), new Vector2(780f, 480f));
            UIBuilder.Panel(currency, "Bg", new Vector2(320f, 64f), Vector2.zero, new Color(0f, 0f, 0f, 0.45f));
            UIBuilder.Picture(currency, IconFactory.KiwiSlice(), new Vector2(46f, 46f), new Vector2(-120f, 0f), Color.white);
            _currencyText = UIBuilder.Label(currency, "0", 30, Theme.AccentLight, TextAnchor.MiddleCenter,
                new Vector2(180f, 50f), new Vector2(50f, 0f), FontStyle.Bold);
            RefreshCurrency();

            // Настройки
            UIButton gear = UIBuilder.IconButton(root, IconFactory.Gear(), new Vector2(64f, 64f),
                new Vector2(880f, 480f), OpenSettings);
            if (gear != null) _interactive.Add(gear.transform as RectTransform);

            // Подпись версии
            UIBuilder.Label(root, GameConfig.FullInfo, 20, Theme.TextDim, TextAnchor.MiddleCenter,
                new Vector2(1200f, 40f), new Vector2(0f, -500f));

            AudioManager.Instance?.PlayMusic(ProceduralAudio.Style.Menu);

            // Появление меню
            StartCoroutine(IntroAnimation());

            // Иногда в меню раздаётся тихий «смех» — отсылка к хоррор-fangame.
            StartCoroutine(AmbientLaugh());
        }

        /// <summary>Случайный тихий смех каждые 8–18 секунд.</summary>
        private IEnumerator AmbientLaugh()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(8f, 18f));
                AudioManager.Instance?.PlaySfx(ProceduralAudio.Sfx.Laugh, 0.25f, UnityEngine.Random.Range(0.85f, 1.15f));
            }
        }

        private void AddMenuButton(RectTransform parent, string text, Vector2 size, Vector2 position,
                                    System.Action onClick, Sprite icon, Color color)
        {
            UIButton button = UIBuilder.Button(parent, text, size, position, onClick, icon, color, 26);
            if (button == null) return;
            RectTransform rt = button.transform as RectTransform;
            _menuButtons.Add(rt);
            _interactive.Add(rt);
        }

        /// <summary>
        /// Включает/выключает кликабельность главного меню.
        ///
        /// Нужно, потому что наши UIButton проверяют попадание курсора САМИ
        /// (без EventSystem), и фон подменю их не перекрывает: без этого
        /// можно было бы нажать «МАГАЗИН» под открытым магазином.
        /// </summary>
        private void SetMenuInteractive(bool value)
        {
            for (int i = 0; i < _interactive.Count; i++)
            {
                if (_interactive[i] == null) continue;
                _interactive[i].gameObject.SetActive(value);
            }
        }

        /// <summary>Заголовок и кнопки появляются по очереди — «живое» меню.</summary>
        private IEnumerator IntroAnimation()
        {
            // Пока меню «выезжает», нажимать нечего: кнопки включаем в конце.
            SetMenuInteractive(false);

            if (_titleText != null)
            {
                Color c = _titleText.color;
                _titleText.color = new Color(c.r, c.g, c.b, 0f);
            }
            if (_subtitleText != null)
            {
                Color c = _subtitleText.color;
                _subtitleText.color = new Color(c.r, c.g, c.b, 0f);
            }

            // Кнопки стартуют сдвинутыми влево
            var targets = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < _menuButtons.Count; i++)
            {
                targets.Add(_menuButtons[i].anchoredPosition);
                _menuButtons[i].anchoredPosition = targets[i] + new Vector2(-70f, 0f);
            }

            const float titleTime = 0.45f;
            float t = 0f;
            while (t < titleTime)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / titleTime);

                if (_titleText != null)
                {
                    Color c = _titleText.color;
                    _titleText.color = new Color(c.r, c.g, c.b, k);
                }
                if (_subtitleText != null)
                {
                    Color c = _subtitleText.color;
                    _subtitleText.color = new Color(c.r, c.g, c.b, Mathf.Clamp01((k - 0.4f) / 0.6f));
                }
                yield return null;
            }

            // Кнопки выезжают по очереди
            for (int i = 0; i < _menuButtons.Count; i++)
            {
                Vector2 start = _menuButtons[i].anchoredPosition;
                float time = 0.28f;
                float tt = 0f;
                while (tt < time)
                {
                    tt += Time.unscaledDeltaTime;
                    float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(tt / time), 3f);
                    _menuButtons[i].anchoredPosition = Vector2.LerpUnclamped(start, targets[i], k);
                    yield return null;
                }
                _menuButtons[i].anchoredPosition = targets[i];
                yield return new WaitForSecondsRealtime(0.05f);
            }

            SetMenuInteractive(true);
        }

        private void RefreshCurrency()
        {
            if (_currencyText != null) _currencyText.text = SaveSystem.Slices.ToString();
        }

        // ---------------- Диорама ----------------

        private void BuildDiorama()
        {
            _diorama = new GameObject("MenuDiorama");

            // Свет
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(_diorama.transform, false);
            sunGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color32(255, 240, 210, 255);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(90, 120, 140, 255);
            RenderSettings.ambientEquatorColor = new Color32(60, 76, 80, 255);
            RenderSettings.ambientGroundColor = new Color32(36, 44, 40, 255);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color32(40, 62, 72, 255);

            // Основание
            MaterialFactory.Cylinder("Island", new Vector3(0f, -1.2f, 0f), 26f, 2.4f, new Color32(62, 104, 74, 255), _diorama.transform);
            MaterialFactory.Cylinder("IslandTop", new Vector3(0f, -0.05f, 0f), 25f, 0.4f, new Color32(92, 146, 84, 255), _diorama.transform, false);

            // Домики на фоне
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                float r = 16f + (i % 3) * 3.2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float h = 3f + (i % 4) * 1.6f;
                MaterialFactory.Box("House", p + new Vector3(0f, h * 0.5f, 0f), new Vector3(4f, h, 4f),
                    new Color32(214, 208, 196, 255), _diorama.transform);
                MaterialFactory.Box("Roof", p + new Vector3(0f, h + 0.4f, 0f), new Vector3(4.6f, 0.6f, 4.6f),
                    new Color32(180, 86, 66, 255), _diorama.transform);
                Material lit = MaterialFactory.Emissive(new Color32(255, 226, 150, 255), new Color32(255, 226, 150, 255), 2f);
                GameObject window = MaterialFactory.Box("Window", p + new Vector3(0f, h * 0.55f, -2.05f),
                    new Vector3(1.2f, 1.2f, 0.12f), Color.white, _diorama.transform, false);
                window.GetComponent<MeshRenderer>().sharedMaterial = lit;
            }

            // Памятник утке
            MaterialFactory.Cylinder("Pedestal", new Vector3(0f, 0.6f, 0f), 5.4f, 1.2f, new Color32(196, 200, 196, 255), _diorama.transform);
            GameObject statue = CharacterFactory.CreateDuck(_diorama.transform, SaveSystem.EquippedHat);
            statue.transform.position = new Vector3(0f, 1.2f, 0f);
            statue.transform.localScale = Vector3.one * 2.6f;
            var spin = statue.AddComponent<Spin>();
            spin.axis = Vector3.up;
            spin.speed = 12f;

            // Уточка ещё и покачивается — диорама «живая»
            var bob = statue.AddComponent<FloatBob>();
            bob.amplitude = 0.25f;
            bob.speed = 1.1f;

            // Киви-птицы вокруг
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2f + 0.6f;
                GameObject kiwi = CharacterFactory.CreateKiwi(_diorama.transform, 1.4f);
                kiwi.transform.position = new Vector3(Mathf.Cos(a) * 9f, 0.2f, Mathf.Sin(a) * 9f);
                var kspin = kiwi.AddComponent<Spin>();
                kspin.axis = Vector3.up;
                kspin.speed = 14f;

                var kbob = kiwi.AddComponent<FloatBob>();
                kbob.amplitude = 0.35f;
                kbob.speed = 1.6f + i * 0.25f;
            }

            // Летающие дольки
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                var slice = MaterialFactory.Cylinder("FloatSlice", new Vector3(Mathf.Cos(a) * 11f, 3f + (i % 3) * 1.2f, Mathf.Sin(a) * 11f),
                    0.9f, 0.18f, new Color32(140, 198, 32, 255), _diorama.transform, false);
                slice.transform.localRotation = Quaternion.Euler(24f, a * Mathf.Rad2Deg, 18f);
                var fspin = slice.AddComponent<Spin>();
                fspin.axis = Vector3.up;
                fspin.speed = 22f;
            }

            // Камера с медленным облётом
            var pivot = new GameObject("MenuCameraPivot");
            pivot.transform.SetParent(_diorama.transform, false);
            pivot.transform.position = new Vector3(0f, 7f, 0f);

            var camGo = new GameObject("MenuCamera");
            camGo.transform.SetParent(pivot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -19f);
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 52f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(38, 60, 70, 255);
            camGo.AddComponent<AudioListener>();

            var spinCam = pivot.AddComponent<Spin>();
            spinCam.axis = Vector3.up;
            spinCam.speed = 3.2f;
        }

        // ---------------- Переходы между экранами ----------------

        private void OpenLevelSelect()
        {
            CloseCurrent();
            SetMenuInteractive(false);
            _current = LevelSelectScreen.Create(_root, OpenMain);
        }

        private void OpenShop()
        {
            CloseCurrent();
            SetMenuInteractive(false);
            _current = ShopScreen.Create(_root, OpenMain);
        }

        private void OpenDevelopers()
        {
            CloseCurrent();
            SetMenuInteractive(false);
            _current = DevelopersScreen.Create(_root, OpenMain);
        }

        private void OpenAchievements()
        {
            CloseCurrent();
            SetMenuInteractive(false);
            _current = AchievementsScreen.Create(_root, OpenMain);
        }

        private void OpenSettings()
        {
            CloseCurrent();
            SetMenuInteractive(false);
            _current = SettingsScreen.Create(_root, OpenMain);
        }

        private void CloseCurrent()
        {
            if (_current != null)
            {
                _current.Dispose();
                _current = null;
            }
        }

        /// <summary>Кнопка «Назад» в меню: закрываем открытое подменю.</summary>
        private void HandleBack()
        {
            if (_current != null)
            {
                OpenMain();
                return;
            }

            // В главном меню подменю нет — выходим из приложения
            // (на Android это ожидаемое поведение системной кнопки).
            BackNavigator.Pop();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void OpenMain()
        {
            CloseCurrent();
            SetMenuInteractive(true);
            RefreshCurrency();
            RefreshDioramaHat();
        }

        /// <summary>
        /// Обновляет шапку на утке в диораме.
        /// Раньше диорама строилась один раз — купленная в магазине шапка
        /// появлялась только после перезапуска игры.
        /// </summary>
        private void RefreshDioramaHat()
        {
            if (_diorama == null) return;
            Transform duck = _diorama.transform.Find("Duck");
            if (duck == null) return;
            Transform anchor = duck.Find("HatAnchor");
            if (anchor == null) return;
            CharacterFactory.ApplyHat(anchor, SaveSystem.EquippedHat);
        }

        public void Dispose()
        {
            CloseCurrent();
            if (_diorama != null) Destroy(_diorama);
            if (_root != null) Destroy(_root.gameObject);
        }

        private void OnDestroy()
        {
            // Диорама — корневой объект сцены: если экран меню уничтожили
            // раньше сцены, она осталась бы «сиротой» и продолжала бы
            // крутиться. Убираем её в любом случае.
            if (_diorama != null)
            {
                Destroy(_diorama);
                _diorama = null;
            }

            // Обработчик «назад» больше не нужен (сцена выгружается).
            BackNavigator.Pop();
        }
    }
}
