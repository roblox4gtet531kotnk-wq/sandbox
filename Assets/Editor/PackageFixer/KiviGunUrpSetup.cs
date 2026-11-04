using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Unity 7 требует явно указывать, что делать со статическими полями при перезагрузке домена.
#pragma warning disable UAL0003, UAL0010

namespace KiviGun.Tools
{
    /// <summary>
    /// Настройка Universal Render Pipeline: URP-ассет + Universal Renderer Data.
    ///
    /// Зачем: URP-ассет, созданный «вручную» (CreateInstance), остаётся без
    /// Renderer Data. Тогда каждый кадр в консоль падает
    /// «Default Renderer is missing, make sure there is a Renderer assigned as the
    /// default on the current Universal RP asset: …», а сцена не рисуется.
    ///
    /// Встроенный редакторский путь делает так:
    ///     rendererData = CreateInstance&lt;UniversalRendererData&gt;();
    ///     rendererData.postProcessData = PostProcessData.GetDefaultPostProcessData();
    ///     AssetDatabase.CreateAsset(rendererData, path);
    ///     ResourceReloader.ReloadAllNullIn(rendererData, packagePath);
    ///     asset = UniversalRenderPipelineAsset.Create(rendererData);
    /// Здесь ровно этот путь, но через рефлексию — чтобы сборка починителя
    /// не зависела от наличия пакета URP и собиралась даже до его установки.
    ///
    /// ВАЖНО: файл лежит в Editor-сборке (Assets/Editor/… + asmdef c
    /// includePlatforms: ["Editor"]), потому что использует UnityEditor.
    /// Версия пакета в разных редакторах разная (URP 17.x в Unity 6/7),
    /// поэтому всё, что зависит от версии, идёт через рефлексию.
    /// </summary>
    public static class KiviGunUrpSetup
    {
        public const string UrpAssetPath = "Assets/Settings/KiviGun_URP.asset";
        public const string RendererAssetPath = "Assets/Settings/KiviGun_Renderer.asset";

        private const string UrpPackagePath = "Packages/com.unity.render-pipelines.universal";
        private const string UrpRuntimeAssembly = "Unity.RenderPipelines.Universal.Runtime";
        private const string CoreRuntimeAssembly = "Unity.RenderPipelines.Core.Runtime";
        private const string UrpAssetTypeName = "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset";
        private const string RendererDataTypeName = "UnityEngine.Rendering.Universal.UniversalRendererData";

        /// <summary>Когда последний раз проверяли все уровни качества (не чаще раза в минуту).</summary>
        private static double _lastQualityCheck;

        private static bool _announced;

        /// <summary>
        /// Создаёт (или чинит) URP-ассет и рендерер, назначает пайплайн проекту.
        /// Возвращает true, только если всё действительно настроено.
        /// </summary>
        public static bool Ensure()
        {
            try
            {
                return EnsureInternal();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KiviGun] Настройка URP не удалась: " + e.Message);
                return false;
            }
        }

        private static bool EnsureInternal()
        {
            Type urpType = Type.GetType(UrpAssetTypeName + ", " + UrpRuntimeAssembly);
            if (urpType == null) return false;   // URP ещё не установлен — починитель дойдёт позже.

            Type dataType = Type.GetType(RendererDataTypeName + ", " + UrpRuntimeAssembly);
            if (dataType == null)
            {
                Debug.LogWarning("[KiviGun] Не найден тип UniversalRendererData — URP настроить нельзя.");
                return false;
            }

            if (!Directory.Exists("Assets/Settings")) Directory.CreateDirectory("Assets/Settings");

            // ---- 1. Renderer Data ----
            ScriptableObject rendererData = EnsureRendererData(dataType);
            if (rendererData == null) return false;

            // ---- 2. URP-ассет ----
            RenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath);
            if (asset != null && !urpType.IsInstanceOfType(asset))
            {
                Debug.LogError("[KiviGun] По пути " + UrpAssetPath + " лежит не URP-ассет (" +
                               asset.GetType().Name + "). Переименуй файл или поменяй путь — " +
                               "перезаписывать чужой ассет нельзя.");
                return false;
            }

            bool created = false;
            if (asset == null)
            {
                asset = CreateUrpAsset(urpType, rendererData);
                if (asset == null)
                {
                    Debug.LogWarning("[KiviGun] Не удалось создать URP-ассет.");
                    return false;
                }
                AssetDatabase.CreateAsset(asset, UrpAssetPath);
                created = true;
            }

            // ---- 3. Рендерер в списке ассета ----
            if (!HasDefaultRenderer(asset))
            {
                if (!AssignRendererData(asset, rendererData, urpType))
                {
                    Debug.LogWarning("[KiviGun] Не удалось назначить Renderer Data в URP-ассет.");
                    return false;
                }
                Announce("починен");
            }
            else if (created)
            {
                Announce("создан");
            }

            // ---- 4. Назначение проекту ----
            ApplyToProject(asset, created);

            AssetDatabase.SaveAssets();
            return true;
        }

        // ------------------------------------------------------------------
        //  Renderer Data
        // ------------------------------------------------------------------

        private static ScriptableObject EnsureRendererData(Type dataType)
        {
            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(RendererAssetPath);
            if (existing != null)
            {
                if (!dataType.IsInstanceOfType(existing))
                {
                    Debug.LogError("[KiviGun] По пути " + RendererAssetPath + " лежит не Renderer Data (" +
                                   existing.GetType().Name + "). Поменяй путь — перезаписывать чужой ассет нельзя.");
                    return null;
                }

                ScriptableObject data = (ScriptableObject)existing;
                // Если postProcessData потерялся (ассет создан до установки пакета) — восстановим.
                if (!HasPostProcessData(data, dataType))
                {
                    if (AssignPostProcessData(data, dataType))
                    {
                        EditorUtility.SetDirty(data);
                        Debug.Log("[KiviGun] postProcessData восстановлен в " + RendererAssetPath);
                    }
                    else
                    {
                        Debug.LogWarning("[KiviGun] Не удалось восстановить postProcessData в " + RendererAssetPath);
                    }
                }
                // Шейдеры URP могли не подтянуться (например, ассет создан до установки пакета).
                ReloadUrpResources(data);
                return data;
            }

            ScriptableObject created = ScriptableObject.CreateInstance(dataType);
            if (!AssignPostProcessData(created, dataType))
                Debug.LogWarning("[KiviGun] postProcessData не назначен — пост-обработка может не работать.");

            AssetDatabase.CreateAsset(created, RendererAssetPath);

            // Подтягиваем шейдеры и материалы URP из пакета.
            ReloadUrpResources(created);
            AssetDatabase.SaveAssets();
            return created;
        }

        /// <summary>
        /// Проверяет, назначен ли postProcessData (ищем и как поле, и как свойство).
        /// Если способ найти не удалось — считаем, что он есть, и ничего не трогаем.
        /// </summary>
        private static bool HasPostProcessData(ScriptableObject data, Type dataType)
        {
            try
            {
                FieldInfo field = dataType.GetField("postProcessData",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return field.GetValue(data) != null;

                PropertyInfo prop = dataType.GetProperty("postProcessData",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null) return prop.GetValue(data, null) != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KiviGun] Проверка postProcessData: " + e.Message);
            }

            return true;
        }

        /// <summary>
        /// Проставляет postProcessData. В разных версиях URP это либо поле,
        /// либо свойство — пробуем оба. Возвращает false, если назначить не вышло.
        /// </summary>
        private static bool AssignPostProcessData(ScriptableObject data, Type dataType)
        {
            try
            {
                Type ppType = Type.GetType("UnityEngine.Rendering.Universal.PostProcessData, " + UrpRuntimeAssembly);
                if (ppType == null) return false;

                MethodInfo getter = FindStaticMethod(ppType, "GetDefaultPostProcessData", 0);
                if (getter == null) return false;

                object pp = getter.Invoke(null, null);
                if (pp == null) return false;

                FieldInfo field = dataType.GetField("postProcessData",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(data, pp);
                    return true;
                }

                PropertyInfo prop = dataType.GetProperty("postProcessData",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.CanWrite)
                {
                    MethodInfo setter = prop.GetSetMethod(true);
                    if (setter != null)
                    {
                        setter.Invoke(data, new object[] { pp });
                        return true;
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KiviGun] postProcessData: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// ResourceReloader.ReloadAllNullIn — заполняет пустые ссылки на шейдеры из пакета URP.
        /// Внимание: метод возвращает «что-то изменилось», а не «успех»!
        /// Поэтому false — это норма (нечего было подтягивать), а вот исключение — проблема.
        /// </summary>
        private static void ReloadUrpResources(object container)
        {
            try
            {
                Type reloader = Type.GetType("UnityEngine.Rendering.ResourceReloader, " + CoreRuntimeAssembly);
                if (reloader == null)
                {
                    Debug.LogWarning("[KiviGun] ResourceReloader не найден — шейдеры URP могут не подтянуться.");
                    return;
                }

                MethodInfo reload = FindStaticMethod(reloader, "ReloadAllNullIn", 2);
                if (reload == null)
                {
                    Debug.LogWarning("[KiviGun] Метод ResourceReloader.ReloadAllNullIn не найден.");
                    return;
                }

                reload.Invoke(null, new object[] { container, UrpPackagePath });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KiviGun] ResourceReloader: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        //  URP-ассет
        // ------------------------------------------------------------------

        private static RenderPipelineAsset CreateUrpAsset(Type urpType, ScriptableObject rendererData)
        {
            try
            {
                // GetMethod("Create") может дать AmbiguousMatchException, если перегрузок несколько.
                // Поэтому перебираем все методы и выбираем по имени и числу параметров.
                MethodInfo[] methods = urpType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo m = methods[i];
                    if (m.Name != "Create") continue;

                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length != 1) continue;
                    if (!ps[0].ParameterType.IsInstanceOfType(rendererData)) continue;

                    return m.Invoke(null, new object[] { rendererData }) as RenderPipelineAsset;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KiviGun] UniversalRenderPipelineAsset.Create: " + e.Message);
            }

            return ScriptableObject.CreateInstance(urpType) as RenderPipelineAsset;
        }

        /// <summary>
        /// Кладёт рендерер в список URP-ассета (поле m_RendererDataList).
        /// Существующий список не обрезаем: заполняем только нужный элемент,
        /// чтобы не удалить чужие настройки проекта.
        /// </summary>
        private static bool AssignRendererData(RenderPipelineAsset asset, ScriptableObject rendererData, Type urpType)
        {
            string[] candidates = { "m_RendererDataList", "rendererDataList", "m_RendererData" };
            for (int i = 0; i < candidates.Length; i++)
            {
                SerializedObject so = new SerializedObject(asset);
                SerializedProperty list = so.FindProperty(candidates[i]);
                if (list == null || !list.isArray) continue;

                SerializedProperty defIndex = so.FindProperty("m_DefaultRendererIndex");
                int index = defIndex != null ? defIndex.intValue : 0;
                if (index < 0 || index >= list.arraySize) index = 0;

                if (list.arraySize <= index) list.arraySize = index + 1;
                list.GetArrayElementAtIndex(index).objectReferenceValue = rendererData;
                if (defIndex != null) defIndex.intValue = index;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                return true;
            }

            // Запасной путь: ищем поле-массив нужного типа напрямую.
            FieldInfo[] fields = urpType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                if (!fields[i].FieldType.IsArray) continue;
                Type element = fields[i].FieldType.GetElementType();
                if (element == null || !element.IsInstanceOfType(rendererData)) continue;

                Array array = fields[i].GetValue(asset) as Array;
                if (array == null || array.Length == 0)
                {
                    array = Array.CreateInstance(element, 1);
                    array.SetValue(rendererData, 0);
                }
                else
                {
                    array.SetValue(rendererData, 0);
                }
                fields[i].SetValue(asset, array);

                // В запасном пути тоже нужно сбросить индекс рендерера по умолчанию.
                SetDefaultRendererIndex(urpType, asset, 0);

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                return true;
            }

            return false;
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

        private static void SetDefaultRendererIndex(Type urpType, RenderPipelineAsset asset, int value)
        {
            try
            {
                FieldInfo field = urpType.GetField("m_DefaultRendererIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(int))
                {
                    field.SetValue(asset, value);
                    return;
                }

                SerializedObject so = new SerializedObject(asset);
                SerializedProperty prop = so.FindProperty("m_DefaultRendererIndex");
                if (prop != null)
                {
                    prop.intValue = value;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KiviGun] m_DefaultRendererIndex: " + e.Message);
            }
        }

        /// <summary>
        /// Назначает пайплайн проекту. Проверяем и Graphics Settings, и уровни качества:
        /// override на уровне качества важнее глобальной настройки и может указывать
        /// на другой ассет — тогда сцена будет рисоваться не тем пайплайном.
        /// </summary>
        private static void ApplyToProject(RenderPipelineAsset asset, bool force)
        {
            bool need = force
                        || GraphicsSettings.defaultRenderPipeline != asset
                        || QualitySettings.renderPipeline != asset;

            // Полная проверка всех уровней качества — не чаще раза в минуту.
            double now = EditorApplication.timeSinceStartup;
            if (!need && now - _lastQualityCheck > 60d)
            {
                _lastQualityCheck = now;
                need = AnyQualityLevelDiffers(asset);
            }

            if (!need) return;

            GraphicsSettings.defaultRenderPipeline = asset;

            int current = QualitySettings.GetQualityLevel();
            int levels = QualitySettings.names.Length;
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            if (current >= 0 && current < levels) QualitySettings.SetQualityLevel(current, false);
        }

        /// <summary>Есть ли уровень качества, у которого стоит другой пайплайн.</summary>
        private static bool AnyQualityLevelDiffers(RenderPipelineAsset asset)
        {
            int current = QualitySettings.GetQualityLevel();
            int levels = QualitySettings.names.Length;
            bool differs = false;

            for (int i = 0; i < levels && !differs; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != asset) differs = true;
            }

            if (current >= 0 && current < levels) QualitySettings.SetQualityLevel(current, false);
            return differs;
        }

        /// <summary>Ищет статический метод по имени и числу параметров (без AmbiguousMatchException).</summary>
        private static MethodInfo FindStaticMethod(Type type, string name, int parameterCount)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != name) continue;
                if (methods[i].GetParameters().Length != parameterCount) continue;
                return methods[i];
            }
            return null;
        }

        private static void Announce(string what)
        {
            if (_announced) return;
            _announced = true;
            Debug.Log("[KiviGun] URP-ассет " + what + " (" + UrpAssetPath + " + " + RendererAssetPath + ") ✓");
        }
    }
}

#pragma warning restore UAL0003, UAL0010
