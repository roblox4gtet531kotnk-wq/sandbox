using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace KiviGun.EditorTools
{
    /// <summary>
    /// САМОПРОВЕРКА API проекта «Киви Ган».
    ///
    /// Зачем: Unity локально недоступна, поэтому часть вызовов API нельзя
    /// проверить компиляцией. Этот скрипт выполняет все сомнительные места
    /// прямо в твоём редакторе и печатает OK/FAIL по каждому.
    ///
    /// Меню: Kivi Gun ▸ Самопроверка API
    /// Потом: скопируй вывод консоли и пришли мне.
    ///
    /// Скрипт ничего не меняет в проекте (кроме создания временных объектов
    /// в памяти) и не трогает сохранение игрока.
    /// </summary>
    public static class KiviGunSelfCheck
    {
        private static readonly List<string> Results = new List<string>();
        private static int _ok;
        private static int _fail;

        [MenuItem("Kivi Gun/Самопроверка API")]
        public static void Run()
        {
            Results.Clear();
            _ok = 0;
            _fail = 0;

            CheckEditorApis();
            CheckUnity7Apis();
            CheckUrpReflection();
            CheckShadersAndAssets();
            CheckInputSystem();
            CheckGameCode();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("######## САМОПРОВЕРКА API «КИВИ ГАН» ########");
            for (int i = 0; i < Results.Count; i++) sb.AppendLine(Results[i]);
            sb.AppendLine();
            sb.AppendLine("ИТОГ: OK = " + _ok + ", FAIL = " + _fail);
            sb.AppendLine("######## КОНЕЦ ########");

            string text = sb.ToString();
            if (_fail == 0) Debug.Log(text);
            else Debug.LogWarning(text);

            try { EditorGUIUtility.systemCopyBuffer = text; }
            catch { /* буфер недоступен */ }
        }

        // ------------------------------------------------------------------
        //  1. Редакторные API (диагностика)
        // ------------------------------------------------------------------
        private static void CheckEditorApis()
        {
            Check("PlayerSettings.GetGraphicsAPIs(Android)", () =>
            {
                GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
                return apis == null ? "null" : string.Join(", ", apis);
            });

            Check("PlayerSettings.GetScriptingBackend(Android)", () =>
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android).ToString());

            Check("PlayerSettings.Android.minSdkVersion", () =>
                PlayerSettings.Android.minSdkVersion.ToString());

            Check("PlayerSettings.Android.targetSdkVersion", () =>
                PlayerSettings.Android.targetSdkVersion.ToString());

            Check("PlayerSettings.Android.targetArchitectures", () =>
                PlayerSettings.Android.targetArchitectures.ToString());

            Check("PlayerSettings.activeInputHandler (через рефлексию)", () =>
            {
                PropertyInfo p = typeof(PlayerSettings).GetProperty("activeInputHandler",
                    BindingFlags.Public | BindingFlags.Static);
                if (p == null) return "свойства нет (читаем из файла) — это не ошибка";
                return p.GetValue(null, null).ToString();
            });

            Check("GraphicsSettings.defaultRenderPipeline", () =>
            {
                RenderPipelineAsset a = GraphicsSettings.defaultRenderPipeline;
                return a != null ? a.name : "НЕ НАЗНАЧЕН";
            });

            Check("GraphicsSettings.currentRenderPipeline", () =>
            {
                RenderPipelineAsset a = GraphicsSettings.currentRenderPipeline;
                return a != null ? a.name : "встроенный (не URP)";
            });

            Check("QualitySettings.renderPipeline", () =>
            {
                RenderPipelineAsset a = QualitySettings.renderPipeline;
                return a != null ? a.name : "по умолчанию";
            });

            Check("QualitySettings.names", () => QualitySettings.names.Length + " уровней");

            Check("EditorBuildSettings.scenes", () =>
            {
                EditorBuildSettingsScene[] s = EditorBuildSettings.scenes;
                List<string> names = new List<string>();
                for (int i = 0; i < s.Length; i++) names.Add(s[i].path);
                return s.Length + " шт: " + string.Join(", ", names.ToArray());
            });

            Check("EditorUserBuildSettings.activeBuildTarget", () =>
                EditorUserBuildSettings.activeBuildTarget.ToString());

            Check("EditorGUIUtility.systemCopyBuffer (запись/чтение)", () =>
            {
                EditorGUIUtility.systemCopyBuffer = "KiviGunSelfCheck";
                return EditorGUIUtility.systemCopyBuffer == "KiviGunSelfCheck" ? "работает" : "НЕ РАБОТАЕТ";
            });

            Check("EditorPrefs: пути Android SDK/NDK/JDK", () =>
                "SDK=" + EditorPrefs.GetString("AndroidSdkRoot", "(нет)") +
                " | NDK=" + EditorPrefs.GetString("AndroidNdkRoot", "(нет)") +
                " | JDK=" + EditorPrefs.GetString("AndroidJdkRoot", "(нет)"));

            Check("Файлы проекта читаются (Assets/Scripts, ProjectSettings)", () =>
            {
                int scripts = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories).Length;
                string ps = File.Exists("ProjectSettings/ProjectSettings.asset") ? "ProjectSettings ✓" : "ProjectSettings ✗";
                return scripts + " скриптов, " + ps;
            });

            Check("Модуль Android Support в редакторе", () =>
            {
                bool installed = Directory.Exists(Path.Combine(
                    EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer"));
                bool supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
                return "модуль=" + (installed ? "установлен ✓" : "НЕ УСТАНОВЛЕН ✗") +
                       ", сборка под Android " + (supported ? "поддерживается ✓" : "не поддерживается ✗");
            });

            Check("packages-lock.json (разрешённые версии пакетов)", () =>
            {
                string path = "Packages/packages-lock.json";
                if (!File.Exists(path)) return "файла нет — версии определены приблизительно";
                string json = File.ReadAllText(path);
                string ugui = VersionOf(json, "com.unity.ugui");
                string input = VersionOf(json, "com.unity.inputsystem");
                string urp = VersionOf(json, "com.unity.render-pipelines.universal");
                return "ugui=" + ugui + ", inputsystem=" + input + ", urp=" + urp;
            });
        }

        // ------------------------------------------------------------------
        //  2. API Unity 7, которые мы переименовывали
        // ------------------------------------------------------------------
        private static void CheckUnity7Apis()
        {
            Check("KeyCode.Return (в Unity 7 нет KeyCode.Enter)", () =>
                KeyCode.Return.ToString());

            Check("MaterialGlobalIlluminationFlags.RealtimeIndirectEmission", () =>
                MaterialGlobalIlluminationFlags.RealtimeIndirectEmission.ToString());

            Check("Object.FindObjectsByType<T>() без параметров", () =>
            {
                UnityEngine.Object[] found = UnityEngine.Object.FindObjectsByType<Canvas>();
                return "нашлось Canvas: " + found.Length;
            });

            Check("UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()", () =>
            {
                IReadOnlyList<Assembly> list = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
                return "сборок загружено: " + list.Count;
            });

            Check("RectTransformUtility.RectangleContainsScreenPoint", () =>
            {
                bool inside = RectTransformUtility.RectangleContainsScreenPoint(
                    null, new Vector2(10f, 10f), null);
                return inside ? "ok" : "вызвана (false) — ошибки нет";
            });

            Check("Resources.GetBuiltinResource<Font>(\"LegacyRuntime.ttf\")", () =>
            {
                Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return f != null ? "шрифт найден: " + f.name : "НЕ НАЙДЕН (текст будет квадратами)";
            });

            Check("AudioClip.Create (процедурный звук)", () =>
            {
                AudioClip clip = AudioClip.Create("KiviSelfCheck", 44100, 2, 44100, false);
                if (clip == null) return "null";
                UnityEngine.Object.DestroyImmediate(clip);
                return "ок";
            });

            Check("Sprite.Create с 9-slice границей", () =>
            {
                Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(2, 2, 2, 2));
                UnityEngine.Object.DestroyImmediate(tex);
                return sprite != null ? "ок" : "null";
            });
        }

        // ------------------------------------------------------------------
        //  3. URP через рефлексию (самый рискованный кусок)
        // ------------------------------------------------------------------
        private static void CheckUrpReflection()
        {
            Check("Тип UniversalRenderPipelineAsset (рефлексия)", () =>
            {
                Type t = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
                return t != null ? "найден: " + t.Assembly.GetName().Name : "НЕ НАЙДЕН";
            });

            Check("Тип UniversalRendererData (рефлексия)", () =>
            {
                Type t = Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");
                return t != null ? "найден" : "НЕ НАЙДЕН";
            });

            Check("Тип PostProcessData (рефлексия)", () =>
            {
                Type t = Type.GetType("UnityEngine.Rendering.Universal.PostProcessData, Unity.RenderPipelines.Universal.Runtime");
                return t != null ? "найден" : "НЕ НАЙДЕН";
            });

            Check("Тип ResourceReloader из Core (рефлексия)", () =>
            {
                Type t = Type.GetType("UnityEngine.Rendering.ResourceReloader, Unity.RenderPipelines.Core.Runtime");
                return t != null ? "найден" : "НЕ НАЙДЕН";
            });

            Check("UniversalRenderPipelineAsset.Create(rendererData) (рефлексия)", () =>
            {
                Type t = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
                if (t == null) return "НЕ НАЙДЕН";
                MethodInfo m = t.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
                if (m == null) return "метода Create нет (используем запасной путь)";
                return "сигнатура: " + m + " | параметров: " + m.GetParameters().Length;
            });

            Check("Поле m_RendererDataList в URP-ассете (SerializedObject)", () =>
            {
                RenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/KiviGun_URP.asset");
                if (asset == null) return "URP-ассета нет (починитель ещё не создал) — это не ошибка";
                SerializedObject so = new SerializedObject(asset);
                SerializedProperty list = so.FindProperty("m_RendererDataList");
                if (list == null) return "СВОЙСТВО НЕ НАЙДЕНО — нужна правка!";
                SerializedProperty index = so.FindProperty("m_DefaultRendererIndex");
                int i = index != null ? index.intValue : 0;
                bool valid = i >= 0 && i < list.arraySize &&
                             list.GetArrayElementAtIndex(i).objectReferenceValue != null;
                return "элементов: " + list.arraySize + ", индекс по умолчанию: " + i +
                       ", " + (valid ? "рендерер есть ✓" : "рендерер НЕ НАЗНАЧЕН ✗");
            });

            Check("ResourceReloader.ReloadAllNullIn (рефлексия + вызов)", () =>
            {
                Type rr = Type.GetType("UnityEngine.Rendering.ResourceReloader, Unity.RenderPipelines.Core.Runtime");
                if (rr == null) return "ResourceReloader НЕ НАЙДЕН";
                MethodInfo m = rr.GetMethod("ReloadAllNullIn", BindingFlags.Public | BindingFlags.Static);
                if (m == null) return "метод ReloadAllNullIn не найден";
                return "сигнатура: " + m;
            });
        }

        // ------------------------------------------------------------------
        //  4. Шейдеры и ассеты
        // ------------------------------------------------------------------
        private static void CheckShadersAndAssets()
        {
            Check("Shader.Find(\"KiviGun/KiviLit\")", () => ShaderName("KiviGun/KiviLit"));
            Check("Shader.Find(\"KiviGun/KiviUnlit\")", () => ShaderName("KiviGun/KiviUnlit"));
            Check("Shader.Find(\"Universal Render Pipeline/Lit\")", () => ShaderName("Universal Render Pipeline/Lit"));
            Check("Shader.Find(\"Universal Render Pipeline/Unlit\")", () => ShaderName("Universal Render Pipeline/Unlit"));

            Check("Шаблон Assets/Resources/KiviLitTemplate.mat", () =>
                File.Exists("Assets/Resources/KiviLitTemplate.mat") ? "есть" : "НЕТ (починитель создаст)");

            Check("Сцены MainMenu / Game", () =>
                (File.Exists("Assets/Scenes/MainMenu.unity") ? "MainMenu ✓" : "MainMenu ✗") + " / " +
                (File.Exists("Assets/Scenes/Game.unity") ? "Game ✓" : "Game ✗"));
        }

        private static string ShaderName(string name)
        {
            Shader s = Shader.Find(name);
            return s != null ? "найден" : "НЕ НАЙДЕН";
        }

        // ------------------------------------------------------------------
        //  5. Input System
        // ------------------------------------------------------------------
        private static void CheckInputSystem()
        {
            Check("Тип UnityEngine.InputSystem.InputSystem (рефлексия)", () =>
            {
                Type t = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
                if (t == null) return "НЕ НАЙДЕН";
                PropertyInfo v = t.GetProperty("version", BindingFlags.Public | BindingFlags.Static);
                return v != null ? "версия пакета: " + v.GetValue(null, null) : "найден (версия неизвестна)";
            });

            Check("Тип Touchscreen (рефлексия)", () =>
            {
                Type t = Type.GetType("UnityEngine.InputSystem.Touchscreen, Unity.InputSystem");
                if (t == null) return "НЕ НАЙДЕН";
                PropertyInfo cur = t.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                object value = cur != null ? cur.GetValue(null, null) : null;
                return value != null ? "Touchscreen.current есть (устройство подключено)" : "Touchscreen.current = null (нет тач-устройства — это нормально на ПК)";
            });

            Check("Тип Mouse (рефлексия)", () =>
            {
                Type t = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
                if (t == null) return "НЕ НАЙДЕН";
                PropertyInfo cur = t.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                object value = cur != null ? cur.GetValue(null, null) : null;
                return value != null ? "Mouse.current есть" : "Mouse.current = null";
            });

            Check("enum TouchPhase: есть ли None", () =>
            {
                Type t = Type.GetType("UnityEngine.InputSystem.TouchPhase, Unity.InputSystem");
                if (t == null) return "НЕ НАЙДЕН";
                string[] names = Enum.GetNames(t);
                List<string> wanted = new List<string>();
                for (int i = 0; i < names.Length; i++) wanted.Add(names[i]);
                bool hasNone = wanted.Contains("None");
                return "состав: " + string.Join(", ", wanted.ToArray()) + (hasNone ? " → None есть ✓" : " → None НЕТ ✗");
            });

            Check("InputHub: безопасные вызовы (без устройств)", () =>
            {
                int touches = InputHub.TouchCount;
                Vector2 pos = InputHub.TouchPosition(0);
                Vector2 posBad = InputHub.TouchPosition(-1);
                Vector2 pointer = InputHub.PointerPosition;
                bool held = InputHub.PointerHeld;
                bool released = InputHub.PointerReleased;
                return "касаний=" + touches + ", TouchPosition(0)=" + pos + ", TouchPosition(-1)=" + posBad +
                       ", pointer=" + pointer + ", held=" + held + ", released=" + released;
            });
        }

        // ------------------------------------------------------------------
        //  6. Код игры
        // ------------------------------------------------------------------
        private static void CheckGameCode()
        {
            Check("LevelLibrary.All — 3 уровня", () => LevelLibrary.All.Length + " шт");

            Check("ShopCatalog: предметы", () =>
                ShopCatalog.Items.Count + " шт (пистолеты: " + ShopCatalog.Pistols.Count +
                ", шапки: " + ShopCatalog.Hats.Count + ")");

            Check("WeaponCatalog: пистолет", () =>
            {
                WeaponSpec spec = ShopCatalog.WeaponFor("pistol_base");
                if (spec == null) return "НЕ НАЙДЕН";
                return spec.Id + ": урон " + spec.Damage + ", магазин " + spec.Magazine +
                       ", темп " + spec.FireDelay + "с, перезарядка " + spec.ReloadTime + "с";
            });

            Check("MaterialFactory: шейдер + материал", () =>
            {
                Shader lit = MaterialFactory.LitShader;
                if (lit == null) return "ШЕЙДЕР НЕ НАЙДЕН";
                Material mat = MaterialFactory.Solid(new Color32(140, 198, 32, 255), true);
                MaterialFactory.SetupTransparent(mat);
                return "шейдер=" + lit.name + ", URP=" + MaterialFactory.UsesUrp +
                       ", прозрачность=" + (mat != null ? mat.renderQueue.ToString() : "null");
            });

            Check("IconFactory: КЭШ работает (две подряд — один спрайт)", () =>
            {
                Sprite a = IconFactory.KiwiSlice();
                Sprite b = IconFactory.KiwiSlice();
                if (a == null || b == null) return "спрайт null";
                return ReferenceEquals(a, b) ? "кэш работает ✓ (тот же объект)" : "КЭШ НЕ РАБОТАЕТ (создаются новые!)";
            });

            Check("IconFactory: все иконки создаются", () =>
            {
                List<string> bad = new List<string>();
                Sprite[] sprites =
                {
                    IconFactory.KiwiSlice(), IconFactory.Pistol(), IconFactory.Revolver(),
                    IconFactory.HatKiwi(), IconFactory.HatTop(), IconFactory.HatPropeller(),
                    IconFactory.HatCrown(), IconFactory.HatParty(), IconFactory.Star(),
                    IconFactory.Star(96, false), IconFactory.Lock(), IconFactory.Gear(),
                    IconFactory.ArrowBack(), IconFactory.Play(), IconFactory.Music(),
                    IconFactory.Trophy(), IconFactory.Heart(), IconFactory.Skull(),
                    IconFactory.Target(), IconFactory.Clock(), IconFactory.Duck()
                };
                for (int i = 0; i < sprites.Length; i++) if (sprites[i] == null) bad.Add("#" + i);
                return bad.Count == 0 ? "все 21 иконка созданы ✓" : "null-иконки: " + string.Join(",", bad.ToArray());
            });

            Check("Theme: спрайты (Rounded / Circle / SoftCircle)", () =>
            {
                Sprite r = Theme.Rounded, c = Theme.Circle, s = Theme.SoftCircle;
                Sprite r2 = Theme.Rounded;
                return "Rounded=" + (r != null) + ", Circle=" + (c != null) + ", SoftCircle=" + (s != null) +
                       ", кэш Rounded=" + ReferenceEquals(r, r2);
            });

            Check("Theme: шрифт", () =>
            {
                Font f = Theme.Font;
                return f != null ? "шрифт: " + f.name : "ШРИФТ НЕ НАЙДЕН (текст будет квадратами)";
            });

            Check("SaveSystem: чтение прогресса (без записи)", () =>
            {
                SaveSystem.Load();
                return "дольки=" + SaveSystem.Slices + ", уровень 1 открыт=" + SaveSystem.IsLevelUnlocked(0) +
                       ", уровень 2 открыт=" + SaveSystem.IsLevelUnlocked(1) +
                       ", уровень 3 открыт=" + SaveSystem.IsLevelUnlocked(2);
            });

            Check("GameConfig: название/версия/движок/платформа", () =>
                GameConfig.FullInfo);

            Check("Уровни: параметры", () =>
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                LevelDefinition[] all = LevelLibrary.All;
                for (int i = 0; i < all.Length; i++)
                {
                    LevelDefinition d = all[i];
                    sb.Append(d.Title + " (" + d.EnemyCount + " киви, " + d.RewardSlices + " дольек, " +
                              d.ParTimeSeconds + "с) ");
                }
                return sb.ToString().Trim();
            });
        }

        /// <summary>Ищет версию пакета в packages-lock.json (простой разбор по шаблону).</summary>
        private static string VersionOf(string json, string package)
        {
            if (string.IsNullOrEmpty(json)) return "?";

            string needle = "\"" + package + "\"";
            int index = json.IndexOf(needle, StringComparison.Ordinal);
            if (index < 0) return "нет";

            int versionAt = json.IndexOf("\"version\"", index, StringComparison.Ordinal);
            if (versionAt < 0 || versionAt > index + 400) return "?";

            int quote1 = json.IndexOf('"', json.IndexOf(':', versionAt) + 1);
            if (quote1 < 0) return "?";
            int quote2 = json.IndexOf('"', quote1 + 1);
            if (quote2 < 0) return "?";

            return json.Substring(quote1 + 1, quote2 - quote1 - 1);
        }

        // ------------------------------------------------------------------
        //  Обёртка проверки
        // ------------------------------------------------------------------
        private static void Check(string title, Func<string> test)
        {
            try
            {
                string result = test();
                if (result == null) result = "(null)";
                _ok++;
                Results.Add("OK    | " + title + " → " + result);
            }
            catch (Exception e)
            {
                _fail++;
                Results.Add("FAIL  | " + title + " → " + e.GetType().Name + ": " + e.Message);
            }
        }
    }
}
