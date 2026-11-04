using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace KiviGun.EditorTools
{
    /// <summary>
    /// Автоматическая настройка проекта «Киви Ган»:
    /// параметры Android, сцены, URP, ресурсы шейдеров и качество графики.
    /// Запускается один раз при первом открытии проекта, либо вручную через меню Kivi Gun.
    /// </summary>
    public static class KiviGunSetup
    {
        public const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        public const string GameScenePath = "Assets/Scenes/Game.unity";
        public const string UrpAssetPath = "Assets/Settings/KiviGun_URP.asset";
        public const string LitTemplatePath = "Assets/Resources/KiviLitTemplate.mat";
        public const string UnlitTemplatePath = "Assets/Resources/KiviUnlitTemplate.mat";

        private const string StageKey = "KiviGun.SetupStage";
        private const string UrpPackage = "com.unity.render-pipelines.universal";
        private const string InputPackage = "com.unity.inputsystem";

        /// <summary>Версия Input System, которую подбирает Package Manager под твой редактор.</summary>
        public const string InputSystemVersion = "подбирается автоматически";

        [InitializeOnLoadMethod]
        private static void ScheduleAutoSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        private static void AutoSetup()
        {
            try
            {
                EnsureProject();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KiviGun] Автонастройка не завершилась: " + e.Message);
            }
        }

        [MenuItem("Kivi Gun/Настроить проект")]
        public static void SetupProject()
        {
            EnsureProject();
            EditorUtility.DisplayDialog("Киви Ган", "Проект настроен:\n• Android-параметры\n• Сцены и сборка\n• URP и шейдеры\n• Параметры игрока", "Ок");
        }

        [MenuItem("Kivi Gun/Открыть главное меню")]
        public static void OpenMenuScene()
        {
            EnsureScenes();
            if (File.Exists(MenuScenePath)) EditorSceneManager.OpenScene(MenuScenePath);
        }

        [MenuItem("Kivi Gun/Собрать APK (Android)")]
        public static void BuildAndroid()
        {
            EnsureProject();
            string path = EditorUtility.SaveFilePanel("Сохранить APK", "", "KiviGun", "apk");
            if (string.IsNullOrEmpty(path)) return;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, GameScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log("[KiviGun] APK собран: " + path);
            else
                Debug.LogError("[KiviGun] Сборка не удалась: " + report.summary.result);
        }

        // ------------------------------------------------------------------

        private static void EnsureProject()
        {
            int stage = EditorPrefs.GetInt(StageKey, 0);

            // Шаг 1: убедиться, что URP установлен (в Unity 7 это обязательный пайплайн).
            if (!IsPackageInstalled(UrpPackage))
            {
                EditorPrefs.SetInt(StageKey, 1);
                if (TryRequestPackage(UrpPackage, "KiviGun.UrpRequested", "Universal Render Pipeline"))
                {
                    Debug.Log("[KiviGun] Устанавливаем Universal Render Pipeline...");
                    UnityEditor.PackageManager.Client.Add(UrpPackage);
                    return;
                }
                // Пакет уже запрашивали, но он не встал — ждём следующей попытки.
                return;
            }

            // Шаг 1b: убедиться, что Input System на месте (в коде весь ввод идёт через InputHub).
            if (!IsPackageInstalled(InputPackage))
            {
                if (TryRequestPackage(InputPackage, "KiviGun.InputRequested", "Input System"))
                {
                    Debug.Log("[KiviGun] Устанавливаем Input System " + InputSystemVersion + "...");
                    UnityEditor.PackageManager.Client.Add(InputPackage);
                    return;
                }
                return;
            }

            ApplyPlayerSettings();
            EnsureInputHandling();
            EnsureScenes();
            EnsureRenderPipeline();
            EnsureResourceTemplates();
            EnsureAlwaysIncludedShaders();
            EnsureReadme();

            if (stage < 2) EditorPrefs.SetInt(StageKey, 2);
            AssetDatabase.SaveAssets();
            Debug.Log("[KiviGun] Проект готов. Откройте сцену MainMenu и нажмите Play.");
        }

        /// <summary>
        /// Переключателем «Active Input Handling» занимается починитель
        /// (Assets/Editor/PackageFixer) — он проверяет API пакета рефлексией и
        /// переключает ввод только когда это точно безопасно. Здесь — только лог.
        /// </summary>
        private static void EnsureInputHandling()
        {
            bool inputReady = File.Exists("Library/ScriptAssemblies/Unity.InputSystem.dll");
            Debug.Log("[KiviGun] Режим ввода: " + (inputReady
                ? "пакет Input System собран (переключатель обновит починитель)."
                : "Input Manager (Old) — пакет Input System ещё не собран."));
        }

        /// <summary>
        /// Разрешает повторный запрос пакета не чаще раза в 2 минуты.
        /// Время хранится по часам (а не по времени сессии редактора), поэтому
        /// защита работает и после перезапуска Unity.
        /// </summary>
        private static bool TryRequestPackage(string packageName, string prefsKey, string humanName)
        {
            long ticks = 0;
            string stored = EditorPrefs.GetString(prefsKey, string.Empty);
            if (!string.IsNullOrEmpty(stored)) long.TryParse(stored, out ticks);

            System.DateTime last = ticks > 0
                ? new System.DateTime(ticks, System.DateTimeKind.Utc)
                : System.DateTime.MinValue;

            if ((System.DateTime.UtcNow - last).TotalSeconds < 120d) return false;

            EditorPrefs.SetString(prefsKey, System.DateTime.UtcNow.Ticks.ToString());
            return true;
        }

        /// <summary>
        /// Пакет считается установленным, если он прописан в manifest.json проекта
        /// (одного «известства» редактору недостаточно: встроенные пакеты всегда в списке).
        /// </summary>
        private static bool IsPackageInstalled(string packageName)
        {
            bool inManifest = false;
            try
            {
                if (File.Exists("Packages/manifest.json"))
                    inManifest = File.ReadAllText("Packages/manifest.json").Contains("\"" + packageName + "\"");
            }
            catch (System.Exception)
            {
                inManifest = false;
            }
            if (!inManifest) return false;

            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            if (packages == null) return false;
            for (int i = 0; i < packages.Length; i++)
                if (packages[i].name == packageName) return true;
            return false;
        }

        // ------------------------------------------------------------------
        //  Параметры игрока (Android)
        // ------------------------------------------------------------------
        private static void ApplyPlayerSettings()
        {
            try
            {
                PlayerSettings.companyName = GameConfig.Company;
                PlayerSettings.productName = GameConfig.GameTitleLatin;
                PlayerSettings.bundleVersion = GameConfig.Version;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, GameConfig.BundleId);
                PlayerSettings.colorSpace = ColorSpace.Linear;
                PlayerSettings.runInBackground = false;
                PlayerSettings.stripEngineCode = true;
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            }
            catch (System.Exception e) { Debug.LogWarning("[KiviGun] PlayerSettings: " + e.Message); }

            try
            {
                // UIOrientation — НЕ набор флагов: «LandscapeLeft | LandscapeRight»
                // даёт неверное значение. Правильно — AutoRotation + список
                // разрешённых поворотов.
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            }
            catch (System.Exception e) { Debug.LogWarning("[KiviGun] Ориентация: " + e.Message); }

            // Android-параметры (API 28–36, ARM64, IL2CPP, Vulkan+GLES3, frame pacing)
            // вынесены в отдельный файл: там же подбор targetSdk по возможностям
            // редактора и отчёт в консоль.
            if (!KiviGunAndroidSetup.Apply(false))
                Debug.LogWarning("[KiviGun] Часть Android-параметров применить не удалось — " +
                                 "открой Kivi Gun ▸ Настроить Android (API 28–36) и посмотри отчёт.");
        }

        // ------------------------------------------------------------------
        //  Сцены
        // ------------------------------------------------------------------
        private static void EnsureScenes()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");

            if (!File.Exists(MenuScenePath))
            {
                UnityEngine.SceneManagement.Scene scene =
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("Bootstrap");
                go.AddComponent<Bootstrap>().mode = Bootstrap.BootMode.Menu;
                EditorSceneManager.SaveScene(scene, MenuScenePath);
                Debug.Log("[KiviGun] Создана сцена " + MenuScenePath);
            }

            if (!File.Exists(GameScenePath))
            {
                UnityEngine.SceneManagement.Scene scene =
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("Bootstrap");
                go.AddComponent<Bootstrap>().mode = Bootstrap.BootMode.Game;
                EditorSceneManager.SaveScene(scene, GameScenePath);
                Debug.Log("[KiviGun] Создана сцена " + GameScenePath);
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
        }

        // ------------------------------------------------------------------
        //  Render Pipeline
        // ------------------------------------------------------------------
        private static void EnsureRenderPipeline()
        {
            // URP-ассет и Universal Renderer Data создаёт модуль починителя:
            // без рендерера Unity 7 не рисует сцену («Default Renderer is missing»).
            if (KiviGun.Tools.KiviGunUrpSetup.Ensure()) return;

            Debug.Log("[KiviGun] URP не найден — используется встроенный шейдер KiviGun/KiviLit.");
        }

        // ------------------------------------------------------------------
        //  Шаблоны материалов (гарантируют попадание шейдеров в сборку)
        // ------------------------------------------------------------------
        private static void EnsureResourceTemplates()
        {
            if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            if (lit == null) lit = Shader.Find("KiviGun/KiviLit");

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            if (unlit == null) unlit = Shader.Find("KiviGun/KiviUnlit");

            if (lit != null) CreateTemplate(LitTemplatePath, lit);
            if (unlit != null) CreateTemplate(UnlitTemplatePath, unlit);
        }

        private static void CreateTemplate(string path, Shader shader)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (existing.shader != shader) existing.shader = shader;
                return;
            }
            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        private static void EnsureAlwaysIncludedShaders()
        {
            string[] needed =
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit",
                "Sprites/Default",
                "UI/Default",
                "KiviGun/KiviLit",
                "KiviGun/KiviUnlit"
            };

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;

            var serialized = new SerializedObject(assets[0]);
            SerializedProperty list = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;

            for (int i = 0; i < needed.Length; i++)
            {
                Shader shader = Shader.Find(needed[i]);
                if (shader == null) continue;

                bool found = false;
                for (int j = 0; j < list.arraySize; j++)
                {
                    if (list.GetArrayElementAtIndex(j).objectReferenceValue == shader) { found = true; break; }
                }
                if (found) continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            serialized.ApplyModifiedProperties();
        }

        private static void EnsureReadme()
        {
            string path = "Assets/КИВИ ГАН — README.txt";
            if (File.Exists(path)) return;
            File.WriteAllText(path,
                "КИВИ ГАН v" + GameConfig.Version + "\n" +
                "Движок: " + GameConfig.Engine + "\n" +
                "Платформа: " + GameConfig.Platform + "\n\n" +
                "Сцены: MainMenu и Game. Нажмите Play в сцене MainMenu.\n" +
                "Все модели, текстуры, иконки и музыка генерируются кодом.\n");
            AssetDatabase.ImportAsset(path);
        }
    }
}
