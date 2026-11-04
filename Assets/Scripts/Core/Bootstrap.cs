using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Точка входа в сцену. Создаёт сервисы (звук, переходы) и запускает нужный экран.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        public enum BootMode
        {
            Menu,
            Game
        }

        [Tooltip("Что запускать при старте сцены")]
        public BootMode mode = BootMode.Menu;

        /// <summary>
        /// Активный экземпляр. Политика: ОДИН Bootstrap на приложение.
        ///
        /// Раньше выбор «кто лишний» шёл по InstanceID — это не работает:
        /// один Awake может выполниться раньше, чем появится второй объект,
        /// и тогда оба решат, что они первые.
        /// Теперь первый Awake занимает место, а все последующие — удаляются.
        /// При обычной смене сцены (а не additive) старый Bootstrap
        /// уничтожается вместе со сценой и место освобождается.
        /// </summary>
        private static Bootstrap _active;

        private void Awake()
        {
            if (_active != null && _active != this)
            {
                // Разбираемся, кто лишний.
                //   • Тот же самый экземпляр сцены — дубликат внутри одной
                //     сцены → удаляем себя.
                //   • Другая сцена — значит, старый Bootstrap ещё жив, но его
                //     сцена выгружается. Порядок Awake/OnDestroy при смене
                //     сцены Unity не гарантирует, поэтому место занимаем мы:
                //     иначе новая сцена осталась бы без интерфейса.
                //
                //     ВАЖНО: сравниваем именно ЭКЗЕМПЛЯР сцены, а не её имя.
                //     При перезапуске уровня грузится сцена с ТЕМ ЖЕ именем,
                //     но это другой экземпляр — и новый Bootstrap должен
                //     занять место, а не удалиться.
                bool sameScene = _active.gameObject.scene == gameObject.scene;

                if (sameScene)
                {
                    Debug.LogWarning("[KiviGun] В сцене уже есть Bootstrap — лишний удалён.");
                    Destroy(this);
                    return;
                }

                Debug.Log("[KiviGun] Смена сцены: новый Bootstrap занял место.");
            }
            _active = this;

            // Защита от запуска сразу в уровень: если сцена Game открылась без
            // запроса из меню (сборка началась с неё или открыта в редакторе),
            // отправляем игрока в главное меню.
            if (mode == BootMode.Game && !SceneFlow.LevelRequested)
            {
                Debug.Log("[KiviGun] Уровень запущен без запроса из меню — возврат в главное меню.");
                SceneFlow.GoToMenu();
                return;
            }

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = false;

            // Определяем класс устройства (ОЗУ, ядра, GPU, экран) — дальше по нему
            // подбираем качество и лимит кадров. Работает на любом Android 9–16.
            DeviceAdaptation.Ensure();

            SaveSystem.Load();
            DeviceAdaptation.AdaptiveFps = SaveSystem.Data.adaptiveFps;
            ApplyQuality(ResolveQualityLevel());

            // Достижения: сначала подписываемся на уведомления, потом проверяем
            // старые сохранения — иначе достижение, выданное прямо в Init(),
            // показалось бы вслепую (уведомление потерялось бы).
            if (GetComponent<AchievementNotifications>() == null)
                gameObject.AddComponent<AchievementNotifications>();

            // Escape / системная кнопка «Назад» на Android.
            if (GetComponent<BackHandler>() == null)
                gameObject.AddComponent<BackHandler>();

            // Сцена новая — старые обработчики «назад» больше не нужны.
            BackNavigator.Clear();

            Achievements.Init();

            AudioManager.GetOrCreate();
            SceneFlow.GetOrCreate();

            Canvas canvas = UIBuilder.CreateCanvas("UICanvas", 0);

            // Интерфейс вешаем в контейнер с учётом чёлки и системных панелей,
            // чтобы кнопки не уезжали под вырез камеры.
            // (ToastCanvas и FadeCanvas создаются отдельно и намеренно
            //  перекрывают весь экран — это всплывашки и затемнение.)
            RectTransform uiRoot = UIBuilder.SafeAreaRoot(canvas);
            Transform parent = uiRoot != null ? uiRoot : canvas.transform;

            if (mode == BootMode.Game)
            {
                GameController.Create(parent);
            }
            else
            {
                MainMenuScreen.Create(parent);
            }

            // Готовим музыку заранее: её генерация занимает доли секунды,
            // и без этого меню/уровень открывались бы с подвисанием.
            PrewarmMusic();

            LogStartupReport();
        }

        private void OnDestroy()
        {
            // Место освобождаем — следующая сцена сможет создать свой Bootstrap.
            if (_active == this) _active = null;
        }

        /// <summary>Пишет в консоль ключевое состояние — видно и в редакторе, и в logcat.</summary>
        private static void LogStartupReport()
        {
            Debug.Log("[KiviGun] Старт: " + GameConfig.FullInfo
                      + " | ввод: " + (InputHub.IsNewInputSystem ? "Input System (New)" : "Input Manager (Old)")
                      + " | качество: " + SaveSystem.Data.qualityLevel
                      + (SaveSystem.Data.qualityAuto ? " (авто)" : " (вручную)")
                      + " | устройство: " + DeviceAdaptation.TierLabel()
                      + " | fps: " + DeviceAdaptation.TargetFps
                      + " | дольки: " + SaveSystem.Slices
                      + " | уровень: " + (SceneFlow.NextLevelIndex + 1) + "/" + GameConfig.LevelCount);

            Debug.Log("[KiviGun] Железо: " + DeviceAdaptation.DeviceSummary);

            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
                Debug.LogWarning("[KiviGun] URP не активен — сцена может рисоваться неправильно. " +
                                 "Проверь: Kivi Gun ▸ Диагностика.");
        }

        /// <summary>Генерирует нужный музыкальный трек до того, как он зазвучит.</summary>
        private void PrewarmMusic()
        {
            ProceduralAudio.Style style = ProceduralAudio.Style.Menu;
            if (mode == BootMode.Game)
            {
                int index = Mathf.Clamp(SceneFlow.NextLevelIndex, 0, GameConfig.LevelCount - 1);
                style = ProceduralAudio.StyleFor(LevelLibrary.Get(index).Theme);
            }
            ProceduralAudio.GetMusic(style);
        }

        /// <summary>
        /// Какой уровень качества применить.
        /// Если игрок ничего не выбирал — берём уровень по классу устройства.
        /// </summary>
        public static int ResolveQualityLevel()
        {
            if (!SaveSystem.Data.qualityAuto)
                return Mathf.Clamp(SaveSystem.Data.qualityLevel, 0, 2);

            switch (DeviceAdaptation.DeviceTier)
            {
                case DeviceAdaptation.Tier.Low: return 0;
                case DeviceAdaptation.Tier.High: return 2;
                default: return 1;
            }
        }

        /// <summary>
        /// Применяет уровень качества (0 — низкое, 1 — среднее, 2 — высокое).
        ///
        /// Порядок ВСЕГДА один (иначе настройки будут затирать друг друга):
        ///   1) профиль игрока (или авто-подбор по устройству);
        ///   2) ограничения устройства и памяти (DeviceAdaptation);
        ///   3) лимит кадров с учётом частоты экрана.
        ///
        /// Это ЕДИНСТВЕННОЕ место, где меняются тени/сглаживание/текстуры.
        /// </summary>
        public static void ApplyQuality(int level)
        {
            level = Mathf.Clamp(level, 0, 2);
            int count = Mathf.Max(1, QualitySettings.names.Length);
            QualitySettings.SetQualityLevel(Mathf.Clamp(level, 0, count - 1), true);

            // --- 1. Профиль ---
            switch (level)
            {
                case 0:
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.shadowDistance = 25f;
                    QualitySettings.pixelLightCount = 2;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.softParticles = false;
                    QualitySettings.realtimeReflectionProbes = false;
                    break;
                case 1:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowDistance = 55f;
                    QualitySettings.pixelLightCount = 4;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.softParticles = false;
                    QualitySettings.realtimeReflectionProbes = false;
                    break;
                default:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 110f;
                    QualitySettings.pixelLightCount = 8;
                    QualitySettings.antiAliasing = 2;
                    QualitySettings.softParticles = true;
                    QualitySettings.realtimeReflectionProbes = true;
                    break;
            }

            // --- 2. Ограничения устройства и памяти ---
            DeviceAdaptation.ApplyDeviceConstraints();

            // --- 2b. Тени/MSAA/доп. источники живут в URP-ассете, а не в QualitySettings ---
            DeviceAdaptation.ApplyUrpProfile(level);

            // --- 3. Кадры (ступень под частоту экрана) ---
            DeviceAdaptation.ApplyFpsLimitForProfile(level);
        }
    }
}
