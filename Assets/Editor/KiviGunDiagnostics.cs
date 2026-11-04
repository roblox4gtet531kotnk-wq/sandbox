using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace KiviGun.EditorTools
{
    /// <summary>
    /// Диагностика проекта «Киви Ган».
    ///
    /// Меню Unity:
    ///   Kivi Gun ▸ Диагностика              — печатает отчёт в консоль;
    ///   Kivi Gun ▸ Диагностика + в буфер   — копирует отчёт в буфер обмена
    ///   и сохраняет KiviGunReport.txt в корне проекта.
    ///
    /// Отчёт собирает: версии, пакеты, режим ввода, URP-ассет и рендерер,
    /// шейдеры, сцены, настройки Android — и вырезает ошибки из журнала редактора.
    ///
    /// ВАЖНО:
    ///  • файл должен лежать в Editor-сборке (папка Assets/Editor) — он использует UnityEditor;
    ///  • доступность отдельных API зависит от версии Unity, поэтому проверки
    ///    изолированы друг от друга (сбой одной не роняет весь отчёт);
    ///  • отчёт содержит локальные пути и выдержки из журнала — перед отправкой
    ///    убедись, что там нет ничего лишнего.
    /// </summary>
    public static class KiviGunDiagnostics
    {
        private const string UrpAssetPath = "Assets/Settings/KiviGun_URP.asset";
        private const string RendererAssetPath = "Assets/Settings/KiviGun_Renderer.asset";
        private const string LitTemplatePath = "Assets/Resources/KiviLitTemplate.mat";
        private const string UnlitTemplatePath = "Assets/Resources/KiviUnlitTemplate.mat";
        private const string ReportFileName = "KiviGunReport.txt";
        private const string ReportStart = "######## ОТЧЁТ ДИАГНОСТИКИ";
        private const string ReportEnd = "######## КОНЕЦ ОТЧЁТА";

        private static readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();
        private static readonly List<string> Problems = new List<string>();
        private static readonly List<string> Notes = new List<string>();
        private static string _lastReport = string.Empty;

        // ------------------------------------------------------------------
        //  Точки входа
        // ------------------------------------------------------------------

        [MenuItem("Kivi Gun/Диагностика")]
        public static void Run()
        {
            _lastReport = BuildReport();

            if (Problems.Count == 0)
                Debug.Log("[KiviGun] Диагностика: проблем не найдено ✓\n\n" + _lastReport);
            else
                Debug.LogWarning("[KiviGun] Диагностика: проблем — " + Problems.Count + "\n\n" + _lastReport);
        }

        /// <summary>Отчёт + копия в буфер обмена + файл в корне проекта.</summary>
        [MenuItem("Kivi Gun/Диагностика + в буфер")]
        public static void RunAndCopy()
        {
            Run();

            bool copied = false;
            try
            {
                EditorGUIUtility.systemCopyBuffer = _lastReport;
                copied = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KiviGun] Не удалось скопировать отчёт в буфер: " + e.Message);
            }

            bool saved = false;
            string savedPath = null;
            try
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(projectRoot)) projectRoot = Directory.GetCurrentDirectory();

                savedPath = Path.Combine(projectRoot, ReportFileName);
                File.WriteAllText(savedPath, _lastReport);
                saved = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KiviGun] Не удалось сохранить отчёт: " + e.Message);
            }

            if (copied)
                Debug.Log("[KiviGun] Отчёт скопирован в буфер обмена — вставь его в чат (Ctrl+V)." +
                          (saved ? "\n[KiviGun] Файл отчёта: " + savedPath : ""));
            else
                Debug.LogWarning("[KiviGun] Отчёт НЕ удалось скопировать в буфер обмена. " +
                                 (saved ? "Но он сохранён в файле: " + savedPath + " — открой и пришли текстом."
                                        : "И файл сохранить не вышло — скопируй отчёт из консоли вручную."));
        }

        // ------------------------------------------------------------------
        //  Сбор отчёта
        // ------------------------------------------------------------------
        private static string BuildReport()
        {
            sb.Clear();
            Problems.Clear();
            Notes.Clear();

            sb.AppendLine(ReportStart + " «КИВИ ГАН»");
            sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Внимание: отчёт содержит локальные пути и выдержки из журнала редактора.");

            Section("Редактор", CheckEditor);
            Section("Пакеты", CheckPackages);
            Section("Ввод", CheckInput);
            Section("Рендер-пайплайн", CheckRenderPipeline);
            Section("Шейдеры и материалы", CheckShaders);
            Section("Сцены", CheckScenes);
            Section("Android", CheckAndroid);
            Section("Содержимое", CheckContent);
            Section("Журнал редактора", AppendEditorLog);

            if (Notes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== ЗАМЕТКИ ===");
                for (int i = 0; i < Notes.Count; i++) sb.AppendLine("• " + Notes[i]);
            }

            if (Problems.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== ИТОГ: ПРОБЛЕМ НЕ НАЙДЕНО ✓ ===");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("=== ПРОБЛЕМЫ (" + Problems.Count + ") ===");
                for (int i = 0; i < Problems.Count; i++) sb.AppendLine("✗ " + Problems[i]);
                sb.AppendLine();
                sb.AppendLine("Что делать: Kivi Gun ▸ Починить зависимости → затем снова Диагностика.");
            }

            sb.AppendLine(ReportEnd);
            return sb.ToString();
        }

        /// <summary>Каждая секция изолирована: сбой одной не роняет весь отчёт.</summary>
        private static void Section(string title, Action check)
        {
            sb.AppendLine();
            sb.AppendLine("=== " + title.ToUpperInvariant() + " ===");
            try
            {
                check();
            }
            catch (Exception e)
            {
                sb.AppendLine("(секция «" + title + "» не отработала: " + e.GetType().Name + ": " + e.Message + ")");
                Notes.Add("Секция «" + title + "» завершилась ошибкой: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        //  1. Редактор
        // ------------------------------------------------------------------
        private static void CheckEditor()
        {
            sb.AppendLine("Unity:      " + Application.unityVersion);
            sb.AppendLine("Платформа:  " + EditorUserBuildSettings.activeBuildTarget);
            sb.AppendLine("Проект:     " + Application.dataPath);
            sb.AppendLine("Режим:      " + (Application.isPlaying ? "идёт игра" : "редактор"));
            sb.AppendLine("Журнал:    " + GetConsoleLogPath());
        }

        // ------------------------------------------------------------------
        //  2. Пакеты — по packages-lock.json (это разрешённые версии), а не по тексту manifest
        // ------------------------------------------------------------------
        private static void CheckPackages()
        {
            string manifest = ReadTextSafe("Packages/manifest.json");
            string lockJson = ReadTextSafe("Packages/packages-lock.json");

            Report("uGUI", "com.unity.ugui", "UnityEngine.UI.dll", manifest, lockJson);
            Report("Input System", "com.unity.inputsystem", "Unity.InputSystem.dll", manifest, lockJson);
            Report("URP", "com.unity.render-pipelines.universal", "Unity.RenderPipelines.Universal.Runtime.dll", manifest, lockJson);

            if (string.IsNullOrEmpty(lockJson))
                Notes.Add("Нет Packages/packages-lock.json — версии пакетов определены приблизительно (по manifest).");
        }

        private static void Report(string title, string package, string assembly, string manifest, string lockJson)
        {
            bool inManifest = !string.IsNullOrEmpty(manifest) && manifest.Contains(package);
            string lockedVersion = FindLockedVersion(lockJson, package);
            bool built = File.Exists("Library/ScriptAssemblies/" + assembly);

            sb.AppendLine(title + ":");
            sb.AppendLine("   в manifest.json: " + (inManifest ? "да" : "нет"));
            sb.AppendLine("   версия (lock):   " + (lockedVersion ?? "не определена"));
            sb.AppendLine("   сборка готова:   " + (built ? "да" : "нет") + "  (" + assembly + ")");

            if (!inManifest && lockedVersion == null)
            {
                Problems.Add(title + ": пакет " + package + " не прописан в manifest.json");
            }
            else if (!built)
            {
                Problems.Add(title + ": пакет есть, но его сборка не готова (" + assembly + " отсутствует) — подожди пересборку");
            }
            else if (lockedVersion != null)
            {
                Notes.Add(title + ": установлена версия " + lockedVersion);
            }
        }

        /// <summary>Ищет версию пакета в packages-lock.json (обычный JSON, разбираем по шаблону).</summary>
        private static string FindLockedVersion(string lockJson, string package)
        {
            if (string.IsNullOrEmpty(lockJson)) return null;

            string needle = "\"" + package + "\"";
            int index = lockJson.IndexOf(needle, StringComparison.Ordinal);
            if (index < 0) return null;

            int versionAt = lockJson.IndexOf("\"version\"", index, StringComparison.Ordinal);
            if (versionAt < 0 || versionAt > index + 400) return null;

            int quote1 = lockJson.IndexOf('"', lockJson.IndexOf(':', versionAt) + 1);
            if (quote1 < 0) return null;
            int quote2 = lockJson.IndexOf('"', quote1 + 1);
            if (quote2 < 0) return null;

            return lockJson.Substring(quote1 + 1, quote2 - quote1 - 1);
        }

        // ------------------------------------------------------------------
        //  3. Ввод
        // ------------------------------------------------------------------
        private static void CheckInput()
        {
            string mode = ReadActiveInputHandler();
            sb.AppendLine("Active Input Handling: " + mode);

            bool hasNew = File.Exists("Library/ScriptAssemblies/Unity.InputSystem.dll");
            sb.AppendLine("Пакет Input System собран: " + (hasNew ? "да" : "нет"));
            sb.AppendLine("Догадка (не проверка): в коде ветка нового ввода активна, если " +
                          "проект собран с ENABLE_INPUT_SYSTEM — это зависит от режима выше.");

            if (!hasNew) Problems.Add("Input System не собрана — новый ввод не работает");
            else if (mode.StartsWith("0"))
                Notes.Add("Включён старый Input Manager. Починитель переключит на новый, когда пакет встанет.");
        }

        /// <summary>Читаем режим ввода из ProjectSettings (в YAML строка может быть с отступом).</summary>
        private static string ReadActiveInputHandler()
        {
            try
            {
                string[] lines = File.ReadAllLines("ProjectSettings/ProjectSettings.asset");
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (!line.StartsWith("activeInputHandler:")) continue;

                    string value = line.Substring("activeInputHandler:".Length).Trim();
                    if (value == "0") return "0 — старый Input Manager";
                    if (value == "1") return "1 — новый Input System (New)";
                    if (value == "2") return "2 — оба (Both)";
                    return value;
                }
            }
            catch (Exception e)
            {
                return "не прочитано (" + e.Message + ")";
            }
            return "не найдено";
        }

        // ------------------------------------------------------------------
        //  4. Рендер-пайплайн
        // ------------------------------------------------------------------
        private static void CheckRenderPipeline()
        {
            RenderPipelineAsset expected = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath);
            if (expected == null)
            {
                sb.AppendLine("URP-ассет: НЕТ ✗");
                Problems.Add("Нет URP-ассета " + UrpAssetPath + " — сцена не будет рисоваться");
            }
            else
            {
                sb.AppendLine("URP-ассет: " + expected.name);

                bool hasRenderer = HasDefaultRenderer(expected);
                sb.AppendLine("Renderer Data по умолчанию: " + (hasRenderer ? "есть ✓" : "НЕТ ✗"));
                if (!hasRenderer)
                    Problems.Add("В URP-ассете нет рендерера по умолчанию — будет «Default Renderer is missing»");

                if (!File.Exists(RendererAssetPath) && hasRenderer)
                    Notes.Add("Renderer Data встроен в ассет, отдельного файла " + RendererAssetPath + " нет");
            }

            RenderPipelineAsset global = GraphicsSettings.defaultRenderPipeline;
            sb.AppendLine("Graphics Settings: " + (global != null ? global.name : "НЕ НАЗНАЧЕН ✗"));
            if (global == null) Problems.Add("URP не назначен в Graphics Settings");
            else if (expected != null && global != expected)
                Problems.Add("В Graphics Settings назначен другой пайплайн (" + global.name + "), а не " + expected.name);

            // Уровни качества: override важнее глобальной настройки.
            int current = QualitySettings.GetQualityLevel();
            int levels = QualitySettings.names.Length;
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                RenderPipelineAsset q = QualitySettings.renderPipeline;
                string name = i == current ? " (текущий)" : "";
                sb.AppendLine("Quality «" + i + "»: " + (q != null ? q.name : "по умолчанию") + name);
                if (q != null && expected != null && q != expected)
                    Problems.Add("На уровне качества " + i + " назначен другой пайплайн: " + q.name);
            }
            if (current >= 0 && current < levels) QualitySettings.SetQualityLevel(current, false);

            sb.AppendLine("Активный пайплайн: " +
                (GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.name
                    : "встроенный (не URP)"));
        }

        /// <summary>Проверяет именно тот рендерер, который помечен как рендерер по умолчанию.</summary>
        private static bool HasDefaultRenderer(RenderPipelineAsset asset)
        {
            try
            {
                SerializedObject so = new SerializedObject(asset);
                SerializedProperty list = so.FindProperty("m_RendererDataList");
                if (list == null || !list.isArray || list.arraySize == 0) return false;

                SerializedProperty index = so.FindProperty("m_DefaultRendererIndex");
                int i = index != null ? index.intValue : 0;
                if (i < 0 || i >= list.arraySize) return false;

                return list.GetArrayElementAtIndex(i).objectReferenceValue != null;
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------------
        //  5. Шейдеры
        // ------------------------------------------------------------------
        private static void CheckShaders()
        {
            CheckShader("KiviGun/KiviLit", "свой освещаемый", false);
            CheckShader("KiviGun/KiviUnlit", "свой неосвещаемый", false);
            CheckShader("Universal Render Pipeline/Lit", "URP Lit", false);
            CheckShader("Universal Render Pipeline/Unlit", "URP Unlit", false);

            sb.AppendLine("Шаблон KiviLitTemplate:   " + (File.Exists(LitTemplatePath) ? "есть ✓" : "НЕТ ✗"));
            sb.AppendLine("Шаблон KiviUnlitTemplate: " + (File.Exists(UnlitTemplatePath) ? "есть ✓" : "НЕТ ✗"));

            if (!File.Exists(LitTemplatePath))
                Problems.Add("Нет " + LitTemplatePath + " — материалы в сборке могут остаться без шейдера");

            Notes.Add("Shader.Find в редакторе не гарантирует, что шейдер попадёт в сборку — " +
                      "для своих шейдеров проверь Always Included Shaders.");
        }

        private static void CheckShader(string shaderName, string title, bool required)
        {
            Shader shader = Shader.Find(shaderName);
            sb.AppendLine(shaderName + " (" + title + "): " + (shader != null ? "найден ✓" : "НЕ НАЙДЕН ✗"));
            if (shader == null && required) Problems.Add("Не найден обязательный шейдер " + shaderName);
        }

        // ------------------------------------------------------------------
        //  6. Сцены
        // ------------------------------------------------------------------
        private static void CheckScenes()
        {
            string[] scenes = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Game.unity" };
            for (int i = 0; i < scenes.Length; i++)
            {
                bool exists = File.Exists(scenes[i]);
                sb.AppendLine(scenes[i] + ": " + (exists ? "есть ✓" : "НЕТ ✗"));
                if (!exists) Problems.Add("Нет сцены " + scenes[i]);
            }

            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            int enabled = 0;
            bool menu = false, game = false;

            for (int i = 0; i < buildScenes.Length; i++)
            {
                if (buildScenes[i].enabled) enabled++;
                if (!buildScenes[i].enabled) continue;

                if (buildScenes[i].path == scenes[0]) menu = true;
                if (buildScenes[i].path == scenes[1]) game = true;
            }

            sb.AppendLine("В Build Settings: " + buildScenes.Length + " записей, включено " + enabled +
                          ", меню=" + (menu ? "да" : "нет") + ", игра=" + (game ? "да" : "нет"));
            if (!menu || !game) Problems.Add("Не все сцены добавлены в Build Settings (или выключены)");
        }

        // ------------------------------------------------------------------
        //  7. Android
        // ------------------------------------------------------------------
        private static void CheckAndroid()
        {
            sb.AppendLine("minSdk:      " + PlayerSettings.Android.minSdkVersion);
            sb.AppendLine("targetSdk:   " + PlayerSettings.Android.targetSdkVersion);
            sb.AppendLine("Архитектуры: " + PlayerSettings.Android.targetArchitectures);
            sb.AppendLine("Бэкенд:      " + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android));

            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (apis != null && apis.Length > 0)
                sb.AppendLine("Графика:     " + string.Join(", ", apis));

            // Модуль Android Support: путь внутри установленного редактора.
            bool hasModule = Directory.Exists(Path.Combine(
                EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer"));
            bool supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

            sb.AppendLine("Модуль Android Support: " + (hasModule ? "установлен ✓" : "НЕ УСТАНОВЛЕН ✗"));
            sb.AppendLine("Сборка под Android поддерживается редактором: " + (supported ? "да ✓" : "нет ✗"));

            if (!hasModule || !supported)
                Problems.Add("Нет модуля Android Build Support (Unity Hub ▸ Installs ▸ Add ▸ " +
                             "Android Build Support) — APK не собрать");

            sb.AppendLine("SDK:  " + EditorPrefs.GetString("AndroidSdkRoot", "(не задан)"));
            sb.AppendLine("NDK:  " + EditorPrefs.GetString("AndroidNdkRoot", "(не задан)"));
            sb.AppendLine("JDK:  " + EditorPrefs.GetString("AndroidJdkRoot", "(не задан)"));
        }

        // ------------------------------------------------------------------
        //  8. Содержимое
        // ------------------------------------------------------------------
        private static void CheckContent()
        {
            sb.AppendLine("Скриптов: " + CountFiles("Assets/Scripts", "*.cs"));
            sb.AppendLine("Шейдеров: " + CountFiles("Assets/Shaders", "*.shader"));
            sb.AppendLine("Сцен:     " + CountFiles("Assets/Scenes", "*.unity"));
        }

        private static int CountFiles(string folder, string pattern)
        {
            try
            {
                if (!Directory.Exists(folder)) return 0;
                return Directory.GetFiles(folder, pattern, SearchOption.AllDirectories).Length;
            }
            catch
            {
                return 0;
            }
        }

        // ------------------------------------------------------------------
        //  9. Журнал редактора (только хвост, без нашего прошлого отчёта)
        // ------------------------------------------------------------------
        private static void AppendEditorLog()
        {
            string path = GetConsoleLogPath();
            if (!File.Exists(path))
            {
                sb.AppendLine("(журнал не найден: " + path + ")");
                return;
            }

            string[] lines = ReadTailLines(path, 4000, 2 * 1024 * 1024);
            int found = 0;
            bool insideReport = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;

                // Наш собственный прошлый отчёт вырезаем, чтобы не путать его с ошибками.
                if (line.StartsWith(ReportStart) || line.StartsWith(ReportEnd)) { insideReport = !insideReport; continue; }
                if (insideReport) continue;

                bool interesting =
                    line.Contains("error CS") ||
                    line.Contains("Shader error") ||
                    line.Contains("Exception") ||
                    line.Contains("[KiviGun]") ||
                    line.Contains("Default Renderer") ||
                    line.Contains("warning CS") ||
                    line.Contains("Compilation failed") ||
                    line.Contains("Scripts have compiler errors");

                if (!interesting) continue;

                sb.AppendLine(line);
                found++;
                if (found >= 150) { sb.AppendLine("… (обрезано)"); break; }
            }

            if (found == 0)
                sb.AppendLine("По заданным шаблонам (error CS, Shader error, Exception, [KiviGun]) " +
                              "в хвосте журнала ничего не найдено. Это не значит, что ошибок нет — " +
                              "фильтр ловит только перечисленные шаблоны.");
        }

        private static string GetConsoleLogPath()
        {
            try
            {
                if (!string.IsNullOrEmpty(Application.consoleLogPath) && File.Exists(Application.consoleLogPath))
                    return Application.consoleLogPath;
            }
            catch { /* используем запасной путь */ }

            try
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Unity", "Editor", "Editor.log");
            }
            catch
            {
                return "(путь неизвестен)";
            }
        }

        /// <summary>Читает только хвост файла — журнал редактора бывает сотни мегабайт.</summary>
        private static string[] ReadTailLines(string path, int maxLines, int maxBytes)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long start = Math.Max(0, fs.Length - maxBytes);
                    fs.Seek(start, SeekOrigin.Begin);

                    int length = (int)(fs.Length - start);
                    byte[] buffer = new byte[length];
                    int read = fs.Read(buffer, 0, length);

                    string text = Encoding.UTF8.GetString(buffer, 0, Math.Max(0, read));
                    string[] all = text.Split('\n');

                    int from = Math.Max(0, all.Length - maxLines);
                    List<string> result = new List<string>();
                    for (int i = from; i < all.Length; i++) result.Add(all[i]);
                    return result.ToArray();
                }
            }
            catch (Exception e)
            {
                return new string[] { "(не удалось прочитать журнал: " + e.Message + ")" };
            }
        }

        private static string ReadTextSafe(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
