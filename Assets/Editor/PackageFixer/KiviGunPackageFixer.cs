using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Unity 7 требует явно указывать, что делать со статическими полями при перезагрузке домена.
// Нам они не нужны — это просто кэши, поэтому гасим предупреждение UAL0003/UAL0010.
#pragma warning disable UAL0003, UAL0010

namespace KiviGun.Tools
{
    /// <summary>
    /// Автоматический «починитель» зависимостей проекта «Киви Ган».
    ///
    /// Собран в отдельной Assembly Definition и не ссылается на игровой код,
    /// поэтому компилируется и запускается даже тогда, когда проект не собирается
    /// (например, отсутствует uGUI и в коде не находятся Image/Text).
    ///
    /// Главный принцип: версии берутся НЕ из интернета и не «на глаз», а из
    /// комплекта самого редактора — Editor/Data/Resources/PackageManager/BuiltInPackages.
    /// Так починитель точно знает, какая версия uGUI и Input System положилена
    /// в твой Unity (для 7000.0.0a12 это uGUI 7.0.0 и Input System 7.0.0).
    ///
    /// Что делает:
    ///  • убирает из manifest.json модули, которых нет в твоём редакторе
    ///    (одна неразрешимая запись ломает resolve всего графа пакетов);
    ///  • прописывает com.unity.ugui (нужен для UnityEngine.UI),
    ///    com.unity.inputsystem (новый ввод) и com.unity.render-pipelines.universal
    ///    (в Unity 7 это единственный пайплайн) — в версиях комплекта редактора;
    ///  • переключает Active Input Handling на «Input System Package (New)»,
    ///    но только когда пакет Input System действительно собран; иначе оставляет
    ///    старый Input Manager, чтобы проект продолжал компилироваться.
    /// </summary>
    [InitializeOnLoad]
    public static class KiviGunPackageFixer
    {
        private const string UguiPackage = "com.unity.ugui";
        private const string InputPackage = "com.unity.inputsystem";
        private const string UrpPackage = "com.unity.render-pipelines.universal";

        private const string ManifestPath = "Packages/manifest.json";
        private const string ScriptAssembliesPath = "Library/ScriptAssemblies/";
        private const double RetryDelaySeconds = 5d;

        /// <summary>Пакеты, о которых уже просили (время последней попытки).</summary>
        private static readonly System.Collections.Generic.Dictionary<string, double> Requested =
            new System.Collections.Generic.Dictionary<string, double>();

        private static double _nextRun;
        private static int _tick;

        static KiviGunPackageFixer()
        {
            // Проверяем не только один раз: починитель должен доделать работу,
            // когда пакеты ещё не успели встать (после перезагрузки домена).
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRun) return;
            _nextRun = now + RetryDelaySeconds;

            _tick++;
            if (_tick % 6 == 1) FixEverything();   // раз в полминуты — полная проверка
            else FixEverythingLight();             // чаще — только пакеты
        }

        [MenuItem("Kivi Gun/Починить зависимости")]
        public static void FixEverything()
        {
            EnsureManifestExists();
            RemoveUnknownModules();
            EnsurePackage(UguiPackage);
            EnsurePackage(InputPackage);
            EnsurePackage(UrpPackage);
            EnsureInputHandling();
            VerifyInstalled();
            EnsureUrp();
        }

        /// <summary>
        /// Создаёт (или чинит) URP-ассет вместе с Universal Renderer Data.
        /// Без рендерера Unity 7 пишет «Default Renderer is missing» каждый кадр
        /// и сцена не рисуется, поэтому делаем это автоматически.
        /// </summary>
        private static void EnsureUrp()
        {
            if (!File.Exists(ScriptAssembliesPath + "Unity.RenderPipelines.Universal.Runtime.dll")) return;
            KiviGunUrpSetup.Ensure();
        }

        /// <summary>Лёгкий проход: проверка пакетов без трогания ProjectSettings.</summary>
        private static void FixEverythingLight()
        {
            EnsurePackage(UguiPackage);
            EnsurePackage(InputPackage);
            EnsurePackage(UrpPackage);
        }

        // ------------------------------------------------------------------
        //  Комплект редактора: какие версии пакетов доступны «из коробки»
        // ------------------------------------------------------------------

        /// <summary>Папка со встроенными пакетами редактора (ugui, inputsystem, модули).</summary>
        private static string BuiltInPackagesRoot
        {
            get
            {
                try
                {
                    string contents = EditorApplication.applicationContentsPath;
                    if (string.IsNullOrEmpty(contents)) return null;
                    return Path.Combine(contents, Path.Combine("Resources", Path.Combine("PackageManager", "BuiltInPackages")));
                }
                catch (System.Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>Есть ли такой встроенный пакет (папка) в комплекте редактора.</summary>
        private static bool EditorHasBuiltInPackage(string packageName)
        {
            try
            {
                string root = BuiltInPackagesRoot;
                if (string.IsNullOrEmpty(root)) return false;
                return Directory.Exists(Path.Combine(root, packageName));
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>Версия встроенного пакета из его package.json (null — пакета в комплекте нет).</summary>
        private static string GetBuiltInPackageVersion(string packageName)
        {
            try
            {
                string root = BuiltInPackagesRoot;
                if (string.IsNullOrEmpty(root)) return null;

                string packageJson = Path.Combine(Path.Combine(root, packageName), "package.json");
                if (!File.Exists(packageJson)) return null;

                Match match = Regex.Match(File.ReadAllText(packageJson), "\"version\"\\s*:\\s*\"([^\"]+)\"");
                if (!match.Success) return null;
                return match.Groups[1].Value;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>Знает ли редактор такой пакет (без обращения к VersionsInfo — только name/version).</summary>
        private static bool IsRegisteredPackage(string packageName)
        {
            try
            {
                UnityEditor.PackageManager.PackageInfo[] all =
                    UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
                if (all == null) return false;

                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name == packageName) return true;
                }
                return false;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        // ------------------------------------------------------------------
        //  Манифест
        // ------------------------------------------------------------------

        /// <summary>Внутренний текст блока "dependencies" (без фигурных скобок).</summary>
        private static bool TryGetDependenciesBlock(string json, out int start, out int length)
        {
            start = 0;
            length = 0;
            if (string.IsNullOrEmpty(json)) return false;

            Match open = Regex.Match(json, "\"dependencies\"\\s*:\\s*\\{");
            if (!open.Success) return false;

            int brace = open.Index + open.Length - 1;
            int depth = 0;
            for (int i = brace; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        start = brace + 1;
                        length = i - brace - 1;
                        return true;
                    }
                }
            }
            return false;
        }

        private static string ReadManifest()
        {
            try
            {
                if (!File.Exists(ManifestPath)) return null;
                return File.ReadAllText(ManifestPath);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>Создаёт минимальный манифест, если его нет.</summary>
        private static void EnsureManifestExists()
        {
            if (File.Exists(ManifestPath)) return;
            try
            {
                string dir = Path.GetDirectoryName(ManifestPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(ManifestPath, "{\n  \"dependencies\": {\n  }\n}\n");
                Debug.Log("[KiviGun] Создан новый Packages/manifest.json.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KiviGun] Не удалось создать manifest.json: " + e.Message);
            }
        }

        /// <summary>Версия пакета, прописанная в manifest.json (null — пакета там нет).</summary>
        private static string GetDependencyVersion(string packageName)
        {
            string json = ReadManifest();
            if (json == null) return null;

            int start, length;
            if (!TryGetDependenciesBlock(json, out start, out length)) return null;

            string block = json.Substring(start, length);
            Match match = Regex.Match(block, "\"" + Regex.Escape(packageName) + "\"\\s*:\\s*\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>Все пакеты, прописанные в manifest.json.</summary>
        private static string[] GetDependencyNames()
        {
            string json = ReadManifest();
            if (json == null) return new string[0];

            int start, length;
            if (!TryGetDependenciesBlock(json, out start, out length)) return new string[0];

            string block = json.Substring(start, length);
            MatchCollection matches = Regex.Matches(block, "\"(com\\.[^\"]+)\"\\s*:");
            if (matches.Count == 0) return new string[0];

            string[] names = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++) names[i] = matches[i].Groups[1].Value;
            return names;
        }

        /// <summary>Прописывает (или меняет версию) пакета в manifest.json.</summary>
        private static bool SetDependency(string packageName, string version)
        {
            string json = ReadManifest();
            if (json == null) return false;

            int start, length;
            if (!TryGetDependenciesBlock(json, out start, out length)) return false;

            string block = json.Substring(start, length);
            Match existing = Regex.Match(block, "\"" + Regex.Escape(packageName) + "\"\\s*:\\s*\"[^\"]+\"");
            string newBlock;

            if (existing.Success)
            {
                newBlock = block.Remove(existing.Index, existing.Length)
                               .Insert(existing.Index, "\"" + packageName + "\": \"" + version + "\"");
            }
            else
            {
                string trimmed = block.TrimEnd();
                string entry = "\"" + packageName + "\": \"" + version + "\"";
                if (trimmed.Trim().Length == 0) newBlock = "\n    " + entry + "\n  ";
                else newBlock = trimmed + ",\n    " + entry + "\n  ";
            }

            string updated = json.Substring(0, start) + newBlock + json.Substring(start + length);
            if (updated == json) return false;

            try
            {
                File.WriteAllText(ManifestPath, updated);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KiviGun] Не удалось записать manifest.json: " + e.Message);
                return false;
            }
        }

        /// <summary>Убирает пакет из manifest.json.</summary>
        private static bool RemoveDependency(string packageName)
        {
            string json = ReadManifest();
            if (json == null) return false;

            int start, length;
            if (!TryGetDependenciesBlock(json, out start, out length)) return false;

            string block = json.Substring(start, length);
            Match existing = Regex.Match(block, "\"" + Regex.Escape(packageName) + "\"\\s*:\\s*\"[^\"]+\"");
            if (!existing.Success) return false;

            string newBlock = block.Remove(existing.Index, existing.Length);
            newBlock = Regex.Replace(newBlock, ",\\s*,", ",");
            newBlock = Regex.Replace(newBlock, ",\\s*$", "");
            newBlock = Regex.Replace(newBlock, "^\\s*,\\s*", "");
            newBlock = Regex.Replace(newBlock, "\\n[ \\t]*\\n", "\n");

            string updated = json.Substring(0, start) + newBlock + json.Substring(start + length);
            if (updated == json) return false;

            try
            {
                File.WriteAllText(ManifestPath, updated);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KiviGun] Не удалось записать manifest.json: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Убирает из манифеста модули, которых нет в этом редакторе.
        /// Такая запись не резолвится и ломает resolve ВСЕГО графа пакетов —
        /// из-за чего не ставились даже корректные uGUI/Input System.
        /// </summary>
        private static void RemoveUnknownModules()
        {
            string root = BuiltInPackagesRoot;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;

            string[] names = GetDependencyNames();
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (!name.StartsWith("com.unity.modules.")) continue;
                if (EditorHasBuiltInPackage(name)) continue;
                if (IsRegisteredPackage(name)) continue;

                Debug.LogWarning("[KiviGun] Модуль " + name + " отсутствует в этом редакторе — убираю его из manifest.json.");
                RemoveDependency(name);
            }
        }

        // ------------------------------------------------------------------
        //  Установка пакетов
        // ------------------------------------------------------------------

        /// <summary>
        /// Гарантирует, что пакет подключён к проекту.
        /// Если он лежит в комплекте редактора — пишем в манифест его родную версию.
        /// Если нет — ставим через Package Manager (он сам подберёт совместимую).
        /// </summary>
        private static void EnsurePackage(string packageName)
        {
            string builtInVersion = GetBuiltInPackageVersion(packageName);
            string current = GetDependencyVersion(packageName);

            if (builtInVersion != null)
            {
                if (current == builtInVersion) return;

                if (SetDependency(packageName, builtInVersion))
                {
                    Debug.Log("[KiviGun] " + packageName + " → " + builtInVersion +
                              " (версия из комплекта редактора " + Application.unityVersion + ")");
                }
                return;
            }

            // Встроенного пакета нет — версия должна быть подобрана Package Manager'ом.
            if (current != null) return;

            RequestAdd(packageName);
        }

        /// <summary>Просит Package Manager поставить пакет (не чаще раза в минуту на пакет).</summary>
        private static void RequestAdd(string packageName)
        {
            double now = EditorApplication.timeSinceStartup;
            double last;
            if (Requested.TryGetValue(packageName, out last) && now - last < 60d) return;
            Requested[packageName] = now;

            Debug.Log("[KiviGun] Устанавливаю пакет " + packageName + "...");
            UnityEditor.PackageManager.Client.Add(packageName);
        }

        // ------------------------------------------------------------------
        //  Ввод
        // ------------------------------------------------------------------

        /// <summary>
        /// Собран ли пакет Input System (то есть можно ли включать новый ввод).
        /// Проверяем именно наличие собранной сборки: пока DLL нет, переключать ввод
        /// нельзя — иначе код начнёт ссылаться на ещё не существующий Unity.InputSystem.
        /// </summary>
        private static bool IsInputSystemReady()
        {
            if (GetDependencyVersion(InputPackage) == null) return false;
            return File.Exists(ScriptAssembliesPath + "Unity.InputSystem.dll");
        }

        /// <summary>
        /// Рефлексией проверяем, что API, который использует InputHub, реально есть
        /// в установленной версии Input System. В Unity 7 пакет переехал на версию 7.x,
        /// и если что-то переименовали — мы остаёмся на старом Input Manager,
        /// чтобы проект продолжал компилироваться (InputHub умеет оба режима).
        /// </summary>
        private static bool IsInputSystemApiUsable()
        {
            try
            {
                System.Reflection.Assembly asm = null;
                System.Collections.Generic.IReadOnlyList<System.Reflection.Assembly> loaded =
                    UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
                for (int i = 0; i < loaded.Count; i++)
                {
                    if (loaded[i] == null) continue;
                    if (loaded[i].GetName().Name == "Unity.InputSystem") { asm = loaded[i]; break; }
                }
                if (asm == null) return false;

                const System.Reflection.BindingFlags Flags =
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Instance;

                System.Type keyboard = asm.GetType("UnityEngine.InputSystem.Keyboard");
                System.Type mouse = asm.GetType("UnityEngine.InputSystem.Mouse");
                System.Type touchscreen = asm.GetType("UnityEngine.InputSystem.Touchscreen");
                if (keyboard == null || mouse == null || touchscreen == null) return false;

                if (keyboard.GetProperty("current", Flags) == null) return false;
                if (keyboard.GetProperty("anyKey", Flags) == null) return false;
                if (keyboard.GetProperty("wKey", Flags) == null) return false;
                if (keyboard.GetProperty("spaceKey", Flags) == null) return false;
                if (keyboard.GetProperty("leftShiftKey", Flags) == null) return false;
                if (keyboard.GetProperty("escapeKey", Flags) == null) return false;

                if (mouse.GetProperty("current", Flags) == null) return false;
                if (mouse.GetProperty("leftButton", Flags) == null) return false;
                if (mouse.GetProperty("rightButton", Flags) == null) return false;

                if (touchscreen.GetProperty("current", Flags) == null) return false;
                if (touchscreen.GetProperty("touches", Flags) == null) return false;

                if (asm.GetType("UnityEngine.InputSystem.TouchPhase") == null) return false;

                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Active Input Handling = «Input System Package (New)», но только если пакет
        /// действительно установлен. Иначе оставляем старый Input Manager — так проект
        /// компилируется в любом случае и не сыплет ошибками про Unity.InputSystem.
        /// </summary>
        private static void EnsureInputHandling()
        {
            bool inputReady = IsInputSystemReady() && IsInputSystemApiUsable();
            int target = inputReady ? 1 : 0;   // 0 = Input Manager (Old), 1 = Input System (New)

            try
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets == null || assets.Length == 0) return;

                SerializedObject serialized = new SerializedObject(assets[0]);
                SerializedProperty prop = serialized.FindProperty("activeInputHandler");
                if (prop == null) return;
                if (prop.intValue == target) return;

                prop.intValue = target;
                serialized.ApplyModifiedProperties();

                if (inputReady)
                {
                    Debug.Log("[KiviGun] Active Input Handling → Input System Package (New).");
                }
                else if (!IsInputSystemReady())
                {
                    Debug.Log("[KiviGun] Active Input Handling → Input Manager (Old): пакет Input System ещё не собран.");
                }
                else
                {
                    Debug.LogWarning("[KiviGun] API Input System в этой версии не совпадает с тем, что использует игра — " +
                                     "оставляю Input Manager (Old). Игра работает с обоими режимами ввода.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KiviGun] Не удалось переключить Active Input Handling: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        //  Ручное включение нового ввода (меню)
        // ------------------------------------------------------------------

        /// <summary>
        /// Меню «Kivi Gun ▸ Включить новый Input System».
        ///
        /// Игрок просил работать именно на новом Input System Package (New),
        /// а не на старом Input Manager. Здесь мы:
        ///   1. проверяем, что пакет прописан в манифесте и реально собран;
        ///   2. проверяем рефлексией, что API, который использует игра, есть в пакете;
        ///   3. только после этого переключаем ProjectSettings.activeInputHandler = 1.
        ///
        /// Если что-то не сходится — честно говорим, что именно мешает, и НЕ ломаем
        /// проект: игра (InputHub) умеет работать и со старым вводом.
        /// </summary>
        [MenuItem("Kivi Gun/Включить новый Input System (New)")]
        public static void EnableNewInputSystem()
        {
            EnsureManifestExists();
            EnsurePackage(InputPackage);

            if (GetDependencyVersion(InputPackage) == null)
            {
                Debug.LogError("[KiviGun] Пакет com.unity.inputsystem не прописан в manifest.json — " +
                               "добавь его через Window ▸ Package Manager и повтори.");
                return;
            }

            if (!File.Exists(ScriptAssembliesPath + "Unity.InputSystem.dll"))
            {
                Debug.LogWarning("[KiviGun] Пакет Input System ещё не собран (нет Library/ScriptAssemblies/Unity.InputSystem.dll). " +
                                 "Дождись пересборки пакетов и повтори — новый ввод включается только после сборки DLL.");
                return;
            }

            if (!IsInputSystemApiUsable())
            {
                Debug.LogError("[KiviGun] API установленной версии Input System не совпадает с тем, что использует игра. " +
                               "Новый ввод НЕ включаю, чтобы проект не перестал компилироваться. " +
                               "Список нужных API: Keyboard.current/anyKey/wKey/spaceKey/leftShiftKey/escapeKey, " +
                               "Mouse.current/leftButton/rightButton, Touchscreen.current/touches, TouchPhase.");
                return;
            }

            try
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets == null || assets.Length == 0)
                {
                    Debug.LogError("[KiviGun] Не найден ProjectSettings/ProjectSettings.asset.");
                    return;
                }

                SerializedObject serialized = new SerializedObject(assets[0]);
                SerializedProperty prop = serialized.FindProperty("activeInputHandler");
                if (prop == null)
                {
                    Debug.LogError("[KiviGun] В ProjectSettings нет поля activeInputHandler.");
                    return;
                }

                prop.intValue = 1;   // 1 = Input System Package (New)
                serialized.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();

                Debug.Log("[KiviGun] Active Input Handling = Input System Package (New) ✓. " +
                          "Unity перезапустит домен — это нормально.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[KiviGun] Не удалось включить новый ввод: " + e.Message);
            }
        }

        /// <summary>Меню «Kivi Gun ▸ Состояние Input System» — просто показывает, что сейчас включено.</summary>
        [MenuItem("Kivi Gun/Состояние Input System")]
        public static void ReportInputSystemState()
        {
            int current = -1;
            try
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets != null && assets.Length > 0)
                {
                    SerializedObject serialized = new SerializedObject(assets[0]);
                    SerializedProperty prop = serialized.FindProperty("activeInputHandler");
                    if (prop != null) current = prop.intValue;
                }
            }
            catch (System.Exception)
            {
                current = -1;
            }

            string mode = current == 1 ? "Input System Package (New)"
                        : current == 0 ? "Input Manager (Old)"
                        : "неизвестно";
            string dll = File.Exists(ScriptAssembliesPath + "Unity.InputSystem.dll") ? "собран" : "НЕ собран";
            string api = IsInputSystemApiUsable() ? "совпадает" : "НЕ совпадает";

            Debug.Log("[KiviGun] Active Input Handling: " + mode +
                      " | DLL пакета: " + dll +
                      " | API: " + api +
                      " | версия пакета в манифесте: " + (GetDependencyVersion(InputPackage) ?? "нет") +
                      " | редактор: " + Application.unityVersion);
        }

        // ------------------------------------------------------------------
        //  Проверка
        // ------------------------------------------------------------------

        /// <summary>Понятный отчёт: что готово, что ещё нет и что делать руками.</summary>
        private static void VerifyInstalled()
        {
            bool uguiReady = File.Exists(ScriptAssembliesPath + "UnityEngine.UI.dll");
            bool inputReady = File.Exists(ScriptAssembliesPath + "Unity.InputSystem.dll");
            bool urpReady = File.Exists(ScriptAssembliesPath + "Unity.RenderPipelines.Universal.Runtime.dll");

            if (uguiReady && inputReady && urpReady)
            {
                Debug.Log("[KiviGun] Все зависимости на месте ✓ (uGUI, Input System и URP собраны).");
                return;
            }

            if (!uguiReady)
            {
                Debug.LogWarning("[KiviGun] uGUI ещё не подключился (нет UnityEngine.UI.dll).\n" +
                                 "Подожди пересборку пакетов. Если не помогло — Window ▸ Package Manager ▸ + ▸ " +
                                 "Install package by name ▸ com.unity.ugui ▸ Add.");
            }

            if (!inputReady)
            {
                Debug.LogWarning("[KiviGun] Input System ещё не собрана (нет Unity.InputSystem.dll).\n" +
                                 "Подожди пересборку пакетов. Если не помогло — Window ▸ Package Manager ▸ + ▸ " +
                                 "Install package by name ▸ com.unity.inputsystem ▸ Add.");
            }

            if (!urpReady)
            {
                Debug.LogWarning("[KiviGun] URP ещё не установлен (в Unity 7 это единственный пайплайн).\n" +
                                 "Подожди пересборку пакетов. Если не помогло — Window ▸ Package Manager ▸ + ▸ " +
                                 "Install package by name ▸ com.unity.render-pipelines.universal ▸ Add.");
            }
        }
    }
}

#pragma warning restore UAL0003, UAL0010
