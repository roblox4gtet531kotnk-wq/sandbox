using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace KiviGun.EditorTools
{
    /// <summary>
    /// НАСТРОЙКА ANDROID: Android 9 (API 28) … Android 16 (API 36).
    ///
    /// Что ставим:
    ///  • minSdk = API 28 (Android 9);
    ///  • targetSdk = максимальный API, который знает твой редактор, но не выше 36;
    ///  • ARM64 + IL2CPP (Google Play требует 64-бит);
    ///  • Vulkan первым, OpenGLES3 вторым;
    ///  • линковка/стриппинг, frame pacing.
    ///
    /// ЧЕСТНО ПРО РЕШЕНИЯ, КОТОРЫЕ НЕЛЬЗЯ ПРОВЕРИТЬ ЗДЕСЬ:
    ///  • minSdk 28 НЕ гарантирует наличие Vulkan на устройстве — часть телефонов
    ///    на Android 9 без него пойдёт по ветке OpenGLES3 (так и задумано);
    ///  • «Android 9 — это почти все устройства» — цифры живут в Google Play
    ///    Console твоего проекта, я их не знаю и не выдумываю;
    ///  • известный редактору targetSdk не значит, что установлен такой же
    ///    Android SDK — проверяем папку platforms/android-<api> отдельно;
    ///  • ARM64-only исключает 32-битные устройства (на Android 9+ их почти нет,
    ///    но решение сознательное — см. AddArmv7IfNeeded()).
    ///
    /// РЕШЕНИЯ БЕЗ ТЕСТА НА УСТРОЙСТВЕ (R8, Vulkan первым, blitType Never)
    /// собраны под флагом UseUntestedOptions — если что-то пойдёт не так,
    /// выключи его и напиши разработчику.
    /// </summary>
    public static class KiviGunAndroidSetup
    {
        /// <summary>Android 9 — минимальная поддерживаемая версия.</summary>
        public const int MinApi = 28;

        /// <summary>Android 16 — верхняя граница, под которую готовимся.</summary>
        public const int MaxApi = 36;

        /// <summary>
        /// Включать решения, которые не проверены на реальном устройстве:
        /// R8-минификация в релизной сборке, Vulkan первым в списке, blitType Never.
        /// </summary>
        public static bool UseUntestedOptions = true;

        private static readonly System.Text.StringBuilder Log = new System.Text.StringBuilder();

        // ------------------------------------------------------------------
        //  Точка входа
        // ------------------------------------------------------------------
        [MenuItem("Kivi Gun/Настроить Android (API 28–36)")]
        public static void ApplyWithReport()
        {
            bool ok = Apply(true);
            if (!ok) Debug.LogWarning("[KiviGun] Настройка Android завершилась с замечаниями — смотри отчёт выше.");
        }

        /// <summary>
        /// Применяет настройки Android.
        /// verbose = true — печатаем подробный отчёт в консоль.
        /// Возвращает false, если что-то применить НЕ удалось.
        /// </summary>
        public static bool Apply(bool verbose)
        {
            Log.Clear();
            Line("=== НАСТРОЙКА ANDROID: API " + MinApi + "–" + MaxApi + " ===");

            bool ok = true;

            // ---- 1. Уровни API ----
            AndroidSdkVersions minSdk;
            AndroidSdkVersions targetSdk;

            bool minFound = TryFindApiMember(MinApi, out minSdk);
            if (!minFound)
            {
                // В редакторе нет члена для API 28 — берём ближайший доступный сверху.
                int found;
                minFound = TryFindNearestApi(MinApi, true, out found, out minSdk);
                if (minFound)
                {
                    Line("  ⚠ minSdk: члена AndroidApiLevel" + MinApi + " нет в этом редакторе, " +
                         "берём ближайший доступный: API " + found);
                }
                else
                {
                    Line("  ✗ minSdk: не удалось подобрать уровень API — настройка пропущена");
                    ok = false;
                }
            }
            else
            {
                Line("  · minSdk: API " + MinApi + " (Android 9) ✓");
            }

            int targetFound;
            bool targetOk = TryFindMaxApi(MaxApi, out targetFound, out targetSdk);
            if (!targetOk)
            {
                Line("  ✗ targetSdk: не удалось определить — оставляю как есть");
                ok = false;
            }
            else
            {
                Line("  · targetSdk: API " + targetFound +
                     (targetFound >= MaxApi
                         ? " (Android 16)"
                         : " — выше этого значения этот редактор не знает"));
            }

            if (minFound)
            {
                try
                {
                    PlayerSettings.Android.minSdkVersion = minSdk;
                }
                catch (Exception e)
                {
                    Line("  ✗ minSdk: не поставилось — " + e.Message);
                    ok = false;
                }
            }

            if (targetOk)
            {
                try
                {
                    PlayerSettings.Android.targetSdkVersion = targetSdk;
                }
                catch (Exception e)
                {
                    Line("  ✗ targetSdk: не поставилось — " + e.Message);
                    ok = false;
                }
            }

            // Проверяем, что под такой API реально есть Android SDK.
            if (targetOk) ok &= CheckAndroidSdkForApi(targetFound);

            // ---- 2. Архитектура и бэкенд ----
            try
            {
                // Только ARM64: Google Play требует 64-бит. 32-битные устройства
                // (ARMv7) при этом исключаются — сознательный размен (см. шапку).
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
                PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, true);
                Line("  · ARM64 + IL2CPP (Release) + стриппинг Low + многопоточная графика ✓");
            }
            catch (Exception e)
            {
                Line("  ✗ архитектура/бэкенд: " + e.Message);
                ok = false;
            }

            // ---- 3. Графика ----
            try
            {
                // Порядок в массиве = приоритет. На части Android 9 Vulkan нет —
                // тогда Unity возьмёт следующий в списке (OpenGLES3).
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                    new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
                Line("  · Графика: Vulkan → OpenGLES3 ✓");
            }
            catch (Exception e)
            {
                Line("  ✗ Графика: " + e.Message);
                ok = false;
            }

            // ---- 4. Прочее (через рефлексию: набор свойств зависит от версии Unity) ----
            TrySetAndroidFlag("optimizedFramePacing", true);   // ровный FPS на Android
            TrySetAndroidFlag("resizeableActivity", true);      // Android 16 требует для планшетов/складных
            TrySetAndroidFlag("renderOutsideSafeArea", false);  // не заезжаем под чёлку

            if (UseUntestedOptions)
            {
                // ВНИМАНИЕ: решения без теста на устройстве.
                TrySetAndroidFlag("minifyRelease", true);       // R8 — меньше APK; если релиз вылетит — выключи
                Line("  ⚠ minifyRelease (R8) включён без теста на устройстве — выключается флагом UseUntestedOptions");
            }

            try
            {
                // Ориентация: UIOrientation — НЕ набор флагов, | комбинировать нельзя.
                // Поэтому AutoRotation + явный список разрешённых поворотов.
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

                PlayerSettings.Android.forceSDCardPermission = false;
                PlayerSettings.Android.bundleVersionCode = BumpVersionCode();   // только вверх!
                PlayerSettings.colorSpace = ColorSpace.Linear;
                PlayerSettings.Android.minifyDebug = false;

                if (UseUntestedOptions)
                    PlayerSettings.Android.blitType = AndroidBlitType.Never;

                Line("  · Ориентация: AutoRotation (только ландшафт), Linear, без записи на SD ✓");
                Line("  · bundleVersionCode = " + PlayerSettings.Android.bundleVersionCode +
                     " (понижать нельзя — Google Play не примет сборку с меньшим кодом)");
            }
            catch (Exception e)
            {
                Line("  ✗ прочие настройки: " + e.Message);
                ok = false;
            }

            // ---- 5. Проверка модуля Android ----
            bool module = Directory.Exists(Path.Combine(
                EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer"));
            bool supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
            Line("  · Модуль Android Build Support: " + (module ? "установлен ✓" : "НЕ УСТАНОВЛЕН ✗ (Unity Hub ▸ Installs)"));
            Line("  · Сборка под Android: " + (supported ? "поддерживается ✓" : "не поддерживается ✗"));
            if (!module || !supported) ok = false;

            Line(ok ? "=== ГОТОВО ✓ ===" : "=== ЕСТЬ ЗАМЕЧАНИЯ — см. строки выше ===");

            if (verbose) Debug.Log("[KiviGun] Настройка Android:\n" + Log.ToString());
            return ok;
        }

        /// <summary>
        /// Поднимает version code, но НИКОГДА не понижает: Google Play не примет
        /// обновление с кодом меньше предыдущего.
        /// </summary>
        private static int BumpVersionCode()
        {
            const int wanted = 10;   // соответствует версии 0.0.10
            int current = 0;
            try
            {
                current = PlayerSettings.Android.bundleVersionCode;
            }
            catch
            {
                current = 0;
            }

            return current >= wanted ? current : wanted;
        }

        /// <summary>
        /// Проверяет, установлен ли Android SDK нужной версии.
        /// Известный редактору targetSdk не значит, что такой SDK есть на диске.
        /// </summary>
        private static bool CheckAndroidSdkForApi(int api)
        {
            string sdkRoot = EditorPrefs.GetString("AndroidSdkRoot", "");
            if (string.IsNullOrEmpty(sdkRoot))
            {
                Line("  ⚠ Android SDK: путь не задан (EditorPrefs: AndroidSdkRoot) — проверить не могу");
                return true;   // это не ошибка настройки, а недостаток информации
            }

            string platform = Path.Combine(sdkRoot, "platforms", "android-" + api);
            if (Directory.Exists(platform))
            {
                Line("  · Android SDK: platforms/android-" + api + " найден ✓");
                return true;
            }

            Line("  ⚠ Android SDK: platforms/android-" + api + " не найден в " + sdkRoot +
                 " — сборка под этот API может не собраться");
            return true;   // предупреждение, а не поломка настройки
        }

        // ------------------------------------------------------------------
        //  Вспомогательные функции
        // ------------------------------------------------------------------
        private static void Line(string text)
        {
            Log.AppendLine(text);
        }

        /// <summary>Ищет точный член перечисления вида AndroidApiLevelNN.</summary>
        private static bool TryFindApiMember(int api, out AndroidSdkVersions value)
        {
            value = default(AndroidSdkVersions);
            string[] names = Enum.GetNames(typeof(AndroidSdkVersions));
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] != "AndroidApiLevel" + api) continue;
                value = (AndroidSdkVersions)Enum.Parse(typeof(AndroidSdkVersions), names[i]);
                return true;
            }
            return false;
        }

        /// <summary>Ближайший доступный уровень API: atLeast=true — не ниже, false — не выше.</summary>
        private static bool TryFindNearestApi(int api, bool atLeast, out int found, out AndroidSdkVersions value)
        {
            found = 0;
            value = default(AndroidSdkVersions);
            bool ok = false;

            string[] names = Enum.GetNames(typeof(AndroidSdkVersions));
            for (int i = 0; i < names.Length; i++)
            {
                if (!names[i].StartsWith("AndroidApiLevel")) continue;

                int n;
                if (!int.TryParse(names[i].Substring("AndroidApiLevel".Length), out n)) continue;
                if (atLeast ? (n < api) : (n > api)) continue;

                if (!ok || (atLeast ? (n < found) : (n > found)))
                {
                    found = n;
                    ok = true;
                }
            }

            if (ok) value = (AndroidSdkVersions)Enum.Parse(typeof(AndroidSdkVersions), "AndroidApiLevel" + found);
            return ok;
        }

        /// <summary>Наибольший уровень API, не выше maxApi.</summary>
        private static bool TryFindMaxApi(int maxApi, out int found, out AndroidSdkVersions value)
        {
            found = 0;
            value = default(AndroidSdkVersions);
            bool ok = false;

            string[] names = Enum.GetNames(typeof(AndroidSdkVersions));
            for (int i = 0; i < names.Length; i++)
            {
                if (!names[i].StartsWith("AndroidApiLevel")) continue;

                int n;
                if (!int.TryParse(names[i].Substring("AndroidApiLevel".Length), out n)) continue;
                if (n > maxApi) continue;

                if (!ok || n > found)
                {
                    found = n;
                    ok = true;
                }
            }

            if (ok) value = (AndroidSdkVersions)Enum.Parse(typeof(AndroidSdkVersions), "AndroidApiLevel" + found);
            return ok;
        }

        /// <summary>
        /// Ставит булево свойство в настройках Android через рефлексию.
        /// Набор свойств у PlayerSettings.Android отличается от версии к версии
        /// Unity, а ломать компиляцию из-за одного флага нельзя.
        /// </summary>
        private static bool TrySetAndroidFlag(string propertyName, bool value)
        {
            try
            {
                Type type = FindAndroidSettingsType();
                if (type == null)
                {
                    Line("  ⚠ " + propertyName + ": тип настроек Android не найден — пропускаю");
                    return false;
                }

                PropertyInfo property = type.GetProperty(propertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                if (property == null)
                {
                    Line("  · " + propertyName + ": в этой версии Unity такого свойства нет — пропускаю (не ошибка)");
                    return false;
                }

                MethodInfo setter = property.GetSetMethod(true);
                if (setter == null)
                {
                    Line("  · " + propertyName + ": свойство только для чтения — пропускаю");
                    return false;
                }

                object instance = setter.IsStatic ? null : GetAndroidInstance();
                setter.Invoke(instance, new object[] { value });
                Line("  · " + propertyName + " = " + value + " ✓");
                return true;
            }
            catch (Exception e)
            {
                Line("  ⚠ " + propertyName + ": " + e.Message);
                return false;
            }
        }

        /// <summary>Тип настроек Android (в разных Unity это вложенный класс или свойство).</summary>
        private static Type FindAndroidSettingsType()
        {
            PropertyInfo property = typeof(PlayerSettings).GetProperty("Android", BindingFlags.Public | BindingFlags.Static);
            if (property != null && property.PropertyType != null) return property.PropertyType;

            string[] names = { "Android", "AndroidSettings" };
            for (int i = 0; i < names.Length; i++)
            {
                Type nested = typeof(PlayerSettings).GetNestedType(names[i], BindingFlags.Public | BindingFlags.NonPublic);
                if (nested != null) return nested;
            }

            return null;
        }

        private static object GetAndroidInstance()
        {
            PropertyInfo property = typeof(PlayerSettings).GetProperty("Android", BindingFlags.Public | BindingFlags.Static);
            return property != null ? property.GetValue(null, null) : null;
        }
    }
}
