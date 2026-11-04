using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Управляет игровым процессом уровня: строит карту, создаёт игрока и врагов,
    /// считает статистику, выдаёт награду и показывает результаты.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public enum State { Playing, Paused, Won, Lost }

        public State CurrentState = State.Playing;
        public LevelDefinition Definition { get; private set; }
        public PlayerController Player { get; private set; }
        public EnemyDirector Director { get; private set; }
        public HudController Hud { get; private set; }

        private LevelBuilder.LevelData _level;
        private Camera _camera;
        private float _levelTime;
        private int _slicesAtStart;
        private int _kills;
        private bool _finished;

        public static GameController Create(Transform uiParent)
        {
            var go = new GameObject("GameController");
            GameController controller = go.AddComponent<GameController>();
            controller.Build(uiParent);
            return controller;
        }

        private void Build(Transform uiParent)
        {
            // Контейнер интерфейса сохраняем сразу: искать канвас через
            // FindObjectsByType — лотерея (их может быть несколько), а нам
            // нужен именно тот, который передали.
            _uiParent = uiParent;

            int index = Mathf.Clamp(SceneFlow.NextLevelIndex, 0, GameConfig.LevelCount - 1);
            Definition = LevelLibrary.Get(index);
            _slicesAtStart = SaveSystem.Slices;

            // --- Карта ---
            _level = LevelBuilder.Build(Definition, transform);

            // --- Игрок ---
            GameObject playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            playerGo.transform.position = _level.PlayerSpawn;

            var controller = playerGo.AddComponent<CharacterController>();
            controller.height = 1.75f;
            controller.radius = 0.42f;
            controller.center = new Vector3(0f, 0.88f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.4f;

            GameObject duck = CharacterFactory.CreateDuck(playerGo.transform, SaveSystem.EquippedHat);
            duck.transform.localPosition = Vector3.zero;

            // --- Камера ---
            var pivotGo = new GameObject("CameraPivot");
            pivotGo.transform.SetParent(playerGo.transform, false);
            pivotGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            var camGo = new GameObject("MainCamera");
            camGo.transform.SetParent(pivotGo.transform, false);
            camGo.transform.localPosition = new Vector3(0.55f, 0.22f, -3.6f);
            camGo.tag = "MainCamera";
            _camera = camGo.AddComponent<Camera>();
            _camera.fieldOfView = SaveSystem.Data.fov;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();

            Player = playerGo.AddComponent<PlayerController>();
            Player.Setup(_camera, pivotGo.transform);
            Player.OnHealthChanged += OnHealthChanged;
            Player.OnDied += OnPlayerDied;
            Player.OnHitTaken += OnPlayerHit;

            // --- Оружие ---
            var hand = new GameObject("HandAnchor");
            hand.transform.SetParent(duck.transform, false);
            hand.transform.localPosition = new Vector3(0.24f, 0.95f, 0.42f);
            hand.transform.localRotation = Quaternion.Euler(-4f, 0f, 0f);

            var weaponHolder = playerGo.AddComponent<WeaponHolder>();
            weaponHolder.Setup(ShopCatalog.WeaponFor(SaveSystem.EquippedWeapon), hand.transform);
            Player.weapon = weaponHolder;   // без этого игрок не может стрелять

            // --- Враги ---
            var directorGo = new GameObject("EnemyDirector");
            directorGo.transform.SetParent(transform, false);
            Director = directorGo.AddComponent<EnemyDirector>();
            Director.OnAllCleared += OnAllEnemiesCleared;
            Director.OnLevelFailed += OnLevelBroken;
            Director.Begin(Definition, _level.EnemySpawns, playerGo.transform);

            // Кнопка «назад» на уровне открывает паузу.
            BackNavigator.Push(HandleBack);

            // Укрытия на карте: умные киви используют их, чтобы прятаться.
            Director.SetCoverPoints(_level.CoverPoints);

            // --- Эффекты и HUD ---
            Effects.GetOrCreate();
            Hud = HudController.Create(uiParent, this);

            // --- Музыка ---
            // На 4-м уровне — тихая «хлебная» баллада. Если игрок положит свой файл
            // в Assets/Resources/Music/Wheatfield, он заменит процедурный трек.
            AudioManager music = AudioManager.Instance;
            if (music != null)
            {
                if (Definition.Theme == LevelTheme.Wheatfield)
                {
                    bool external = music.PlayExternalMusic("Music/Wheatfield", 0.55f);
                    if (!external) music.PlayMusic(ProceduralAudio.Style.Wheatfield, 0.55f);
                }
                else
                {
                    music.PlayMusic(ProceduralAudio.StyleFor(Definition.Theme));
                }
            }

            // Музыкой уровня управляет AudioManager (один путь, без дублей):
            // компонент LevelMusic на этот объект НЕ вешаем — иначе один и тот же
            // клип запускался бы дважды.

            Hud.ShowBanner(Definition.Title, Definition.Subtitle);

            // Диагностика уровня: видно в консоли редактора и в logcat на телефоне.
            Debug.Log("[KiviGun] Уровень " + (Definition.Index + 1) + " «" + Definition.Title + "»: киви " +
                      Definition.EnemyCount + ", точек спавна " + _level.EnemySpawns.Count +
                      ", оружие " + weaponHolder.Spec.Id + " (" + weaponHolder.Spec.Magazine + " патронов, урон " +
                      weaponHolder.Spec.Damage + "), спавн игрока " + _level.PlayerSpawn +
                      ", убийств в статистике " + SaveSystem.Data.totalKills);
        }

        private void Update()
        {
            if (CurrentState == State.Playing)
            {
                _levelTime += Time.deltaTime;

                SyncKills();

                Hud.SetObjective(_kills, Director != null ? Director.TotalToSpawn : 0);
            }
        }

        /// <summary>
        /// Синхронизирует счётчик убийств со статистикой и достижениями.
        ///
        /// Вызывается не только в Update, но и ПЕРЕД сменой состояния
        /// (победа/поражение): иначе последнее убийство могло не попасть
        /// в статистику — директор вызывает OnAllCleared сразу после смерти
        /// последней птицы, и состояние менялось до следующего Update.
        ///
        /// Считаем разницей (пачкой), а не по одному — иначе счётчик прыгал бы
        /// на +1 каждый кадр. Единственное место, где растёт totalKills.
        /// </summary>
        private void SyncKills()
        {
            if (Director == null) return;

            int killed = Director.Killed;
            int delta = killed - _kills;
            if (delta <= 0) return;

            AchievementSystem.RegisterKiwiKills(delta);
            _kills = killed;
        }

        private void OnHealthChanged(float health)
        {
            if (Hud != null) Hud.SetHealth(health / Mathf.Max(1f, Player.maxHealth));
        }

        private void OnPlayerHit()
        {
            if (Hud != null) Hud.FlashDamage();
        }

        private void OnPlayerDied()
        {
            if (_finished) return;
            _finished = true;
            BackNavigator.Pop();

            // Последние убийства (если игрок умер сразу после выстрела) — тоже в счёт.
            SyncKills();

            CurrentState = State.Lost;
            SaveSystem.Data.totalDeaths++;
            SaveSystem.Save();

            if (Director != null) Director.ClearAll();
            AudioManager.Instance?.PlaySfx(ProceduralAudio.Sfx.Lose, 0.9f);
            ShowResults(false);
        }

        private void OnAllEnemiesCleared()
        {
            if (_finished) return;
            _finished = true;
            BackNavigator.Pop();

            // Последнее убийство могло не попасть в Update — считаем здесь.
            SyncKills();

            CurrentState = State.Won;

            int stars = 1;
            if (_levelTime <= Definition.ParTimeSeconds) stars = 3;
            else if (_levelTime <= Definition.ParTimeSeconds * 1.5f) stars = 2;

            SaveSystem.CompleteLevel(Definition.Index, _levelTime, stars);
            SaveSystem.AddSlices(Definition.RewardSlices);
            AudioManager.Instance?.PlaySfx(ProceduralAudio.Sfx.Win, 0.9f);

            ShowResults(true);
        }

        /// <summary>
        /// Уровень собран неверно (например, нет точек появления киви).
        /// Победы не будет: показываем поражение, чтобы игрок мог переиграть,
        /// а не «выиграл» уровень, на котором никого не было.
        /// </summary>
        private void OnLevelBroken()
        {
            if (_finished) return;
            _finished = true;
            BackNavigator.Pop();

            CurrentState = State.Lost;
            ShowResults(false);
        }

        private void ShowResults(bool victory)
        {
            int collected = Mathf.Max(0, SaveSystem.Slices - _slicesAtStart);
            int stars = victory ? SaveSystem.Stars(Definition.Index) : 0;
            int kills = Director != null ? Director.Killed : _kills;

            ResultsScreen.Create(GetUIRoot(), victory, Definition, _levelTime, kills, collected, stars, OnResultAction);
        }

        private Transform _uiRoot;
        private Transform _uiParent;

        /// <summary>
        /// Контейнер, в который вешаем игровой интерфейс (результаты, пауза).
        /// Раньше канвас искался через FindObjectsByType — это лотерея (канвасов
        /// в сцене может быть несколько, и выбирался не тот). Теперь мы помним
        /// переданный uiParent и берём из него контейнер «SafeArea»
        /// (иначе элементы уедут под чёлку и системные панели).
        /// </summary>
        private Transform GetUIRoot()
        {
            if (_uiRoot != null) return _uiRoot;

            Transform parent = _uiParent;
            if (parent == null)
            {
                // Запасной путь: канвас всё-таки придётся найти.
                // В Unity 7 перегрузка с FindObjectsSortMode устарела (CS0618) —
                // используем версию без параметра сортировки.
                Canvas[] canvases = FindObjectsByType<Canvas>();
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i].sortingOrder != 0) continue;
                    parent = canvases[i].transform;
                    break;
                }
            }

            if (parent == null) return transform;

            Transform safe = parent.Find("SafeArea");
            _uiRoot = safe != null ? safe : parent;
            return _uiRoot;
        }

        private void OnResultAction(string action)
        {
            switch (action)
            {
                case "retry":
                    SceneFlow.NextLevelIndex = Definition.Index;
                    SceneFlow.GetOrCreate().LoadScene(SceneFlow.GameScene);
                    break;
                case "next":
                    if (Definition.Index + 1 < GameConfig.LevelCount)
                    {
                        SceneFlow.NextLevelIndex = Definition.Index + 1;
                        SceneFlow.GetOrCreate().LoadScene(SceneFlow.GameScene);
                    }
                    else
                    {
                        SceneFlow.GetOrCreate().LoadScene(SceneFlow.MenuScene);
                    }
                    break;
                default:
                    SceneFlow.GetOrCreate().LoadScene(SceneFlow.MenuScene);
                    break;
            }
        }

        // ---------------- Пауза ----------------

        /// <summary>
        /// Кнопка «назад» (Escape / системная на Android) открывает паузу,
        /// а в паузе — снимает её.
        /// </summary>
        private void HandleBack()
        {
            if (CurrentState == State.Won || CurrentState == State.Lost) return;
            TogglePause();
        }

        public void TogglePause()
        {
            if (CurrentState == State.Won || CurrentState == State.Lost) return;

            if (CurrentState == State.Paused)
            {
                Resume();
            }
            else
            {
                CurrentState = State.Paused;
                Time.timeScale = 0f;
                if (Player != null) Player.InputEnabled = false;
                PauseMenuController.Create(GetUIRoot(), Resume, Restart, ToMenu, OpenSettings);
            }
        }

        public void Resume()
        {
            CurrentState = State.Playing;
            Time.timeScale = 1f;
            if (Player != null) Player.InputEnabled = true;
            PauseMenuController.Close();
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            SceneFlow.NextLevelIndex = Definition.Index;
            SceneFlow.GetOrCreate().LoadScene(SceneFlow.GameScene);
        }

        private void ToMenu()
        {
            Time.timeScale = 1f;
            SceneFlow.GetOrCreate().LoadScene(SceneFlow.MenuScene);
        }

        private void OpenSettings()
        {
            // Раньше onClose был null — кнопка «Назад» ничего не делала,
            // и игрок оставался застрявшим в настройках без пути назад.
            SettingsScreen.Create(GetUIRoot(), () =>
            {
                SettingsScreen.Close();
                // Возвращаем меню паузы (игра остаётся на паузе).
                PauseMenuController.Create(GetUIRoot(), Resume, Restart, ToMenu, OpenSettings);
            });
        }
    }
}
