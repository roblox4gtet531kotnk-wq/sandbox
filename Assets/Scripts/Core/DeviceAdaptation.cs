using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace KiviGun
{
    /// <summary>
    /// АВТОПОДСТРОЙКА ПОД УСТРОЙСТВО (Android 9–16, от «слабых» до флагманов).
    ///
    /// ВАЖНО про владение настройками:
    ///  • качество (тени, сглаживание, текстуры) применяет ОДИН владелец —
    ///    Bootstrap.ApplyQuality(). Здесь мы только сообщаем ограничения устройства;
    ///  • лимитом FPS и масштабом рендера владеет этот класс;
    ///  • порядок всегда один: профиль игрока → ограничения устройства/памяти →
    ///    адаптивные правки по реальному времени кадра.
    ///
    /// СОМНЕНИЯ (исправлены по ревью):
    ///  • SystemInfo.processorFrequency и graphicsMemorySize на части Android
    ///    возвращают 0 или мусор → в оценке класса устройства они НЕ участвуют,
    ///    только в логе;
    ///  • Android округляет Application.targetFrameRate до делителя частоты
    ///    экрана, поэтому произвольные 55 или 25 не работают. Отсюда список
    ///    «допустимых ступеней» FpsSteps;
    ///  • ScalableBufferManager работает не везде (нужен URP с Dynamic Resolution),
    ///    поэтому фактический масштаб читаем из ScalableBufferManager.widthScaleFactor,
    ///    а не считаем, что он применился;
    ///  • QualitySettings.masterTextureLimit в новых Unity может устареть —
    ///    обращаемся к нему через рефлексию, чтобы не ломать компиляцию.
    /// </summary>
    public sealed class DeviceAdaptation : MonoBehaviour
    {
        public enum Tier
        {
            Low = 0,
            Medium = 1,
            High = 2
        }

        /// <summary>Класс устройства, определённый при запуске.</summary>
        public static Tier DeviceTier { get; private set; }

        /// <summary>Реальная частота экрана (Гц).</summary>
        public static int DisplayRefreshRate { get; private set; }

        /// <summary>Текущий измеренный FPS (усреднение за окно ~1 с).</summary>
        public static int CurrentFps { get; private set; }

        /// <summary>Текущий лимит кадров, который выставлен в Application.targetFrameRate.</summary>
        public static int TargetFps { get; private set; }

        /// <summary>Была ли сигнализация о нехватке памяти (после неё качество не повышаем).</summary>
        public static bool MemoryPressure { get; private set; }

        /// <summary>Запрошенный масштаб рендера, % (то, что мы хотели). Стартуем со 100%.</summary>
        public static int RequestedRenderScale { get; private set; } = 100;

        /// <summary>Фактический масштаб рендера (может не совпадать с запрошенным).</summary>
        public static float ActualRenderScale { get; private set; } = 1f;

        /// <summary>Включена ли автоподстройка (FPS + масштаб). В настройках можно отключить.</summary>
        public static bool AdaptiveFps { get; set; } = true;

        /// <summary>Краткая сводка по устройству — печатается в консоль при старте.</summary>
        public static string DeviceSummary { get; private set; } = "";

        private const int MinRenderScale = 60;
        private const int MaxRenderScale = 100;

        private static DeviceAdaptation _instance;
        private static readonly List<int> FpsSteps = new List<int>();
        private static PropertyInfo _textureLimitProperty;

        private static int _stepIndex;
        private int _frames;
        private float _frameTimeSum;
        private float _windowStart = -1f;
        private float _lastChange;
        private int _goodWindows;

        /// <summary>Создаёт (или возвращает) адаптер. Вызывается из Bootstrap.</summary>
        public static DeviceAdaptation Ensure()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("KiviGun.DeviceAdaptation");
            // HideFlags.HideAndDontSave НЕЛЬЗЯ: он ставит DontSaveInBuild,
            // и в собранной игре объект просто не появится.
            go.hideFlags = HideFlags.HideInHierarchy;
            _instance = go.AddComponent<DeviceAdaptation>();
            return _instance;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            DetectDevice();
            BuildFpsSteps(DisplayRefreshRate);

            Application.lowMemory += OnLowMemory;
        }

        private void OnDestroy()
        {
            Application.lowMemory -= OnLowMemory;
            if (_instance == this) _instance = null;
        }

        private void OnApplicationPause(bool paused)
        {
            // Во время паузы кадры не идут — окно измерения надо сбросить,
            // иначе после возврата получим «ложную» просадку FPS.
            ResetWindow();
        }

        private void ResetWindow()
        {
            _frames = 0;
            _frameTimeSum = 0f;
            _windowStart = -1f;
        }

        // ------------------------------------------------------------------
        //  Определение класса устройства
        // ------------------------------------------------------------------
        private void DetectDevice()
        {
            int ram = SystemInfo.systemMemorySize;      // МБ. 0 = неизвестно
            int cores = SystemInfo.processorCount;      // ядра
            int gpuMem = SystemInfo.graphicsMemorySize;  // МБ. 0 = неизвестно (на мобильных — часто мусор)
            int pixels = Mathf.Max(1, Screen.width * Screen.height);

            // Оценка строится только на ОЗУ и ядрах — это единственные показатели,
            // которым можно доверять на Android. Видеопамять и разрешение — только в лог.
            int score = 1; // по умолчанию — средний

            if (ram <= 0) score = 0;                   // ОЗУ неизвестно — считаем слабым (безопасно)
            else if (ram <= 2048) score = 0;           // 1–2 ГБ
            else if (ram <= 4096) score = 1;           // 3–4 ГБ
            else score = 2;                            // 6+ ГБ

            if (cores <= 2) score = 0;
            else if (cores >= 8 && ram >= 6000) score = 2;

            // Старый графический API — признак не самого мощного телефона,
            // но это лишь ограничение «не выше среднего», а не приговор.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
                score = Mathf.Min(score, 1);

            DeviceTier = (Tier)Mathf.Clamp(score, 0, 2);
            DisplayRefreshRate = DetectRefreshRate();

            DeviceSummary = "модель: " + SystemInfo.deviceModel
                            + " | ОС: " + SystemInfo.operatingSystem
                            + " | GPU: " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")"
                            + " | ОЗУ: " + ram + " МБ"
                            + " | ядра: " + cores
                            + " | GPU-память: " + (gpuMem > 0 ? gpuMem + " МБ (не влияет на оценку)" : "неизвестно")
                            + " | экран: " + Screen.width + "x" + Screen.height + " (" + pixels + " px)"
                            + " | частота: " + DisplayRefreshRate + " Гц"
                            + " | класс: " + DeviceTier;
        }

        private static int DetectRefreshRate()
        {
            try
            {
                RefreshRate rate = Screen.currentResolution.refreshRateRatio;
                // rate.value — double, а Mathf.RoundToInt ждёт float:
                // без явного приведения была ошибка CS1503.
                int hz = Mathf.RoundToInt((float)rate.value);
                if (hz >= 24 && hz <= 240) return hz;
            }
            catch (System.Exception)
            {
                // На старых редакторах refreshRateRatio нет — оставляем 60.
            }
            return 60;
        }

        // ------------------------------------------------------------------
        //  Допустимые ступени FPS
        // ------------------------------------------------------------------
        /// <summary>
        /// Android округляет targetFrameRate до делителя частоты панели:
        /// на 60 Гц нельзя попросить 55 или 25 — будет 30 или 20.
        /// Поэтому работаем только со «ступенями» — делителями частоты экрана.
        /// </summary>
        private static void BuildFpsSteps(int refresh)
        {
            FpsSteps.Clear();

            // Берём ВСЕ делители частоты экрана от 20 — именно их Unity умеет
            // выдавать на Android. Например: 60 Гц → 60/30/20; 90 Гц → 90/45/30;
            // 120 Гц → 120/60/40/30/24/20.
            for (int d = 20; d <= refresh; d++)
            {
                if (refresh % d == 0) FpsSteps.Add(d);
            }

            // 30 делит 60/90/120 — наш «аварийный» минимум для слабых устройств.
            if (!FpsSteps.Contains(30)) FpsSteps.Add(30);

            FpsSteps.Sort((a, b) => b.CompareTo(a));   // от большего к меньшему
        }

        /// <summary>Ближайшая допустимая ступень FPS, не выше запрошенной.</summary>
        private static int NearestStep(int wanted)
        {
            if (FpsSteps.Count == 0) return Mathf.Clamp(wanted, 24, 120);

            for (int i = 0; i < FpsSteps.Count; i++)
                if (FpsSteps[i] <= wanted) return FpsSteps[i];

            return FpsSteps[FpsSteps.Count - 1];
        }

        private static void ApplyStep(int index)
        {
            if (FpsSteps.Count == 0) return;

            _stepIndex = Mathf.Clamp(index, 0, FpsSteps.Count - 1);
            TargetFps = FpsSteps[_stepIndex];
            Application.targetFrameRate = TargetFps;
            QualitySettings.vSyncCount = 0;   // на Android vsync не нужен, мешает контролю FPS
        }

        /// <summary>Индекс ступени в списке (для синхронизации адаптера и профиля).</summary>
        private static int IndexOfStep(int value)
        {
            for (int i = 0; i < FpsSteps.Count; i++)
                if (FpsSteps[i] == value) return i;
            return 0;
        }

        // ------------------------------------------------------------------
        //  Ограничения, которые накладывает устройство (вызывает Bootstrap)
        // ------------------------------------------------------------------
        /// <summary>
        /// Ограничения от устройства и памяти. Применяются ПОСЛЕ профиля игрока,
        /// поэтому «высокое» качество на слабом телефоне всё равно будет урезано.
        /// </summary>
        public static void ApplyDeviceConstraints()
        {
            if (DeviceTier == Tier.Low)
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.pixelLightCount = Mathf.Min(QualitySettings.pixelLightCount, 2);
                QualitySettings.antiAliasing = 0;
                SetTextureLimit(2);
            }

            if (MemoryPressure)
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.softParticles = false;
                QualitySettings.realtimeReflectionProbes = false;
                SetTextureLimit(Mathf.Min(GetTextureLimit() + 1, 4));
            }
        }

        /// <summary>
        /// Настройки самого URP-ассета: в URP QualitySettings.shadows и подобное
        /// почти не работают — тени, доп. источники и MSAA задаются в URP Asset.
        /// Поэтому профиль применяем и туда (через рефлексию, чтобы не зависеть
        /// от версии URP).
        ///
        /// СОМНЕНИЕ: имена полей в URP 17 провереть не удалось — если поле не
        /// найдено, оно просто пропускается (без ошибок). Значения по умолчанию
        /// для «среднего» и «высокого» задаются здесь же, поэтому переключение
        /// профиля назад тоже корректно.
        /// </summary>
        public static void ApplyUrpProfile(int profileLevel)
        {
            RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null) asset = QualitySettings.renderPipeline;
            if (asset == null) return;

            bool constrained = DeviceTier == Tier.Low || MemoryPressure;
            int level = constrained ? 0 : Mathf.Clamp(profileLevel, 0, 2);

            switch (level)
            {
                case 0:
                    TrySetMember(asset, new[] { "mainLightShadowsSupported", "m_MainLightShadowsSupported" }, false);
                    TrySetMember(asset, new[] { "additionalLightShadowsSupported", "m_AdditionalLightShadowsSupported" }, false);
                    TrySetMember(asset, new[] { "shadowDistance", "m_ShadowDistance" }, 25f);
                    TrySetMember(asset, new[] { "additionalLightsPerObjectLimit", "m_AdditionalLightsPerObjectLimit" }, 0);
                    TrySetMember(asset, new[] { "msaaSampleCount", "m_MSAASampleCount" }, 1);
                    TrySetMember(asset, new[] { "supportsHDR", "m_SupportsHDR" }, false);
                    break;
                case 1:
                    TrySetMember(asset, new[] { "mainLightShadowsSupported", "m_MainLightShadowsSupported" }, true);
                    TrySetMember(asset, new[] { "additionalLightShadowsSupported", "m_AdditionalLightShadowsSupported" }, false);
                    TrySetMember(asset, new[] { "shadowDistance", "m_ShadowDistance" }, 55f);
                    TrySetMember(asset, new[] { "additionalLightsPerObjectLimit", "m_AdditionalLightsPerObjectLimit" }, 4);
                    TrySetMember(asset, new[] { "msaaSampleCount", "m_MSAASampleCount" }, 1);
                    TrySetMember(asset, new[] { "supportsHDR", "m_SupportsHDR" }, false);
                    break;
                default:
                    TrySetMember(asset, new[] { "mainLightShadowsSupported", "m_MainLightShadowsSupported" }, true);
                    TrySetMember(asset, new[] { "additionalLightShadowsSupported", "m_AdditionalLightShadowsSupported" }, true);
                    TrySetMember(asset, new[] { "shadowDistance", "m_ShadowDistance" }, 110f);
                    TrySetMember(asset, new[] { "additionalLightsPerObjectLimit", "m_AdditionalLightsPerObjectLimit" }, 8);
                    TrySetMember(asset, new[] { "msaaSampleCount", "m_MSAASampleCount" }, 2);
                    TrySetMember(asset, new[] { "supportsHDR", "m_SupportsHDR" }, false);
                    break;
            }
        }

        /// <summary>Ставит поле или свойство объекта по первому подходящему имени.</summary>
        private static bool TrySetMember(object target, string[] names, object value)
        {
            if (target == null) return false;

            Type type = target.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                FieldInfo field = type.GetField(names[i],
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && !field.IsInitOnly)
                {
                    try
                    {
                        field.SetValue(target, value);
                        return true;
                    }
                    catch (System.Exception)
                    {
                        // Несовпадение типа — пробуем следующее имя.
                    }
                }

                PropertyInfo property = type.GetProperty(names[i],
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanWrite)
                {
                    try
                    {
                        property.SetValue(target, value, null);
                        return true;
                    }
                    catch (System.Exception)
                    {
                        // Несовпадение типа — пробуем следующее имя.
                    }
                }
            }

            return false;
        }

        /// <summary>Лимит FPS для выбранного профиля (с учётом частоты экрана и памяти).</summary>
        public static void ApplyFpsLimitForProfile(int profileLevel)
        {
            int wanted;
            if (profileLevel <= 0) wanted = 30;
            else if (profileLevel == 1) wanted = 60;
            else wanted = Mathf.Min(DisplayRefreshRate, 90);   // «высокое»: нативная частота, но не выше 90

            if (MemoryPressure) wanted = Mathf.Min(wanted, 30);

            SetFpsLimit(wanted);
        }

        /// <summary>Задать лимит кадров снаружи (значение округляется до допустимой ступени).</summary>
        public static void SetFpsLimit(int fps)
        {
            int step = NearestStep(fps);
            _stepIndex = IndexOfStep(step);
            TargetFps = step;
            Application.targetFrameRate = step;
            QualitySettings.vSyncCount = 0;
        }

        // ------------------------------------------------------------------
        //  Слежение за временем кадра
        // ------------------------------------------------------------------
        private void Update()
        {
            float now = Time.unscaledTime;
            if (_windowStart < 0f) _windowStart = now;

            _frames++;
            _frameTimeSum += Time.unscaledDeltaTime;

            float elapsed = now - _windowStart;
            if (elapsed < 1f) return;

            // FPS = кадры / ФАКТИЧЕСКАЯ длина окна (окно может длиться больше секунды).
            CurrentFps = Mathf.RoundToInt(_frames / Mathf.Max(0.0001f, elapsed));
            float avgFrameMs = _frameTimeSum / Mathf.Max(1, _frames) * 1000f;

            _frames = 0;
            _frameTimeSum = 0f;
            _windowStart = now;

            ActualRenderScale = ScalableBufferManager.widthScaleFactor;

            if (!AdaptiveFps) return;
            if (now - _lastChange < 2f) return;   // не дёргаем настройки чаще раза в 2 секунды

            float budgetMs = 1000f / Mathf.Max(1f, TargetFps);

            if (avgFrameMs > budgetMs * 1.25f)
            {
                // Кадр не укладывается в бюджет.
                _goodWindows = 0;

                // Сначала уменьшаем масштаб рендера: снижение лимита FPS само по себе
                // НЕ ускоряет тяжёлый кадр (просто реже рисуем).
                if (RequestedRenderScale > MinRenderScale)
                {
                    SetRenderScale(RequestedRenderScale - 10);
                    _lastChange = now;
                }
                else if (!MemoryPressure && _stepIndex < FpsSteps.Count - 1)
                {
                    // Масштаб уже минимальный — опускаем ступень FPS (экономит батарею).
                    ApplyStep(_stepIndex + 1);
                    _lastChange = now;
                }
                return;
            }

            if (avgFrameMs < budgetMs * 0.75f)
            {
                _goodWindows++;
                if (_goodWindows < 3) return;   // не поднимаем на одном удачном окне

                _goodWindows = 0;

                // Возвращаем качество: сначала масштаб рендера, потом ступень FPS.
                if (RequestedRenderScale < MaxRenderScale)
                {
                    SetRenderScale(RequestedRenderScale + 10);
                    _lastChange = now;
                }
                else if (!MemoryPressure && _stepIndex > 0)
                {
                    ApplyStep(_stepIndex - 1);
                    _lastChange = now;
                }
            }
            else
            {
                _goodWindows = 0;
            }
        }

        /// <summary>
        /// Масштаб рендера. Работает только при включённом в URP «Dynamic Resolution»;
        /// иначе фактический масштаб останется 1.0 — это не ошибка.
        /// </summary>
        private static void SetRenderScale(int percent)
        {
            int clamped = Mathf.Clamp(percent, MinRenderScale, MaxRenderScale);
            RequestedRenderScale = clamped;

            float s = clamped / 100f;
            try
            {
                ScalableBufferManager.ResizeBuffers(s, s);
                ActualRenderScale = ScalableBufferManager.widthScaleFactor;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KiviGun] Масштаб рендера недоступен: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        //  Нехватка памяти
        // ------------------------------------------------------------------
        private void OnLowMemory()
        {
            MemoryPressure = true;

            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
            SetTextureLimit(Mathf.Min(GetTextureLimit() + 1, 4));

            // Не затираем уже сделанное уменьшение: берём минимум из текущего и 80.
            SetRenderScale(Mathf.Min(RequestedRenderScale, 80));
            SetFpsLimit(30);

            GC.Collect();
            Resources.UnloadUnusedAssets();

            // Переприменяем ограничения (профиль мог поднять качество обратно).
            if (_instance != null) Bootstrap.ApplyQuality(Bootstrap.ResolveQualityLevel());

            Debug.LogWarning("[KiviGun] Мало памяти — качество снижено автоматически.");
        }

        // ------------------------------------------------------------------
        //  masterTextureLimit — через рефлексию (в новых Unity может устареть)
        // ------------------------------------------------------------------
        private static PropertyInfo TextureLimitProperty
        {
            get
            {
                if (_textureLimitProperty == null)
                {
                    _textureLimitProperty = typeof(QualitySettings).GetProperty("masterTextureLimit",
                        BindingFlags.Public | BindingFlags.Static);
                }
                return _textureLimitProperty;
            }
        }

        /// <summary>
        /// Ограничение разрешения текстур. Значение 2 = каждая сторона вдвое ниже
        /// (то есть вчетверо меньше пикселей), 1 = без ограничений.
        /// </summary>
        private static void SetTextureLimit(int limit)
        {
            PropertyInfo property = TextureLimitProperty;
            if (property == null)
            {
                Debug.LogWarning("[KiviGun] QualitySettings.masterTextureLimit недоступен — пропускаю.");
                return;
            }

            try
            {
                property.SetValue(null, Mathf.Clamp(limit, 1, 4), null);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KiviGun] masterTextureLimit: " + e.Message);
            }
        }

        private static int GetTextureLimit()
        {
            PropertyInfo property = TextureLimitProperty;
            if (property == null) return 1;

            try
            {
                object value = property.GetValue(null, null);
                return value != null ? Mathf.Clamp(System.Convert.ToInt32(value), 1, 4) : 1;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>Сводка для окна настроек: «среднее (4 ГБ, 8 ядер, Vulkan)».</summary>
        public static string TierLabel()
        {
            string name;
            switch (DeviceTier)
            {
                case Tier.Low: name = "слабое"; break;
                case Tier.High: name = "мощное"; break;
                default: name = "среднее"; break;
            }

            return name + " (" + SystemInfo.systemMemorySize + " МБ ОЗУ, " +
                   SystemInfo.processorCount + " ядер, " +
                   SystemInfo.graphicsDeviceType + ")";
        }
    }
}
