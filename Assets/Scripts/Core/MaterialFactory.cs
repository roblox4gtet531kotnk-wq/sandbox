using UnityEngine;
using UnityEngine.Rendering;

namespace KiviGun
{
    /// <summary>
    /// Создаёт материалы и простые 3D-объекты в рантайме.
    /// Поддерживает URP (Unity 7), встроенный пайплайн и запасной собственный шейдер.
    /// </summary>
    public static class MaterialFactory
    {
        private static Shader _litShader;
        private static Shader _unlitShader;

        // ---------------- Шейдеры ----------------

        /// <summary>Основной освещаемый шейдер.</summary>
        public static Shader LitShader
        {
            get
            {
                if (_litShader != null) return _litShader;

                // 1) Шаблон из Resources создаётся скриптом настройки проекта — он всегда попадает в сборку.
                Material template = Resources.Load<Material>("KiviLitTemplate");
                if (template != null && template.shader != null)
                {
                    _litShader = template.shader;
                    return _litShader;
                }

                // 2) URP (Unity 7 / Unity 6 с установленным URP)
                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    Shader s = Shader.Find("Universal Render Pipeline/Lit");
                    if (s != null)
                    {
                        _litShader = s;
                        return _litShader;
                    }
                }

                // 3) Собственный шейдер проекта (URP-совместимый, работает всегда)
                Shader own = Shader.Find("KiviGun/KiviLit");
                if (own != null)
                {
                    _litShader = own;
                    return _litShader;
                }

                // 4) Встроенный пайплайн (в Unity 7 его уже нет — только на старых версиях)
                Shader std = Shader.Find("Standard");
                if (std != null)
                {
                    _litShader = std;
                    return _litShader;
                }

                _litShader = Shader.Find("Unlit/Color");
                return _litShader;
            }
        }

        /// <summary>Шейдер без освещения (свечения, трассеры, эффекты).</summary>
        public static Shader UnlitShader
        {
            get
            {
                if (_unlitShader != null) return _unlitShader;

                Material template = Resources.Load<Material>("KiviUnlitTemplate");
                if (template != null && template.shader != null)
                {
                    _unlitShader = template.shader;
                    return _unlitShader;
                }

                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    Shader s = Shader.Find("Universal Render Pipeline/Unlit");
                    if (s != null)
                    {
                        _unlitShader = s;
                        return _unlitShader;
                    }
                }

                // Собственный шейдер проекта (URP-совместимый)
                Shader own = Shader.Find("KiviGun/KiviUnlit");
                if (own != null)
                {
                    _unlitShader = own;
                    return _unlitShader;
                }

                Shader std = Shader.Find("Unlit/Color");
                if (std != null)
                {
                    _unlitShader = std;
                    return _unlitShader;
                }

                return null;
            }
        }

        public static bool UsesUrp => GraphicsSettings.currentRenderPipeline != null;

        // ---------------- Кэш материалов ----------------
        //
        // Сомнение, которое стоило проверить: раньше каждый кубик и сфера создавали
        // СВОЙ материал. На уровне с сотнями объектов это сотни материалов в памяти
        // и лишние переключения батчей. Теперь одинаковые «цвет + параметры»
        // переиспользуют один материал.

        private static readonly System.Collections.Generic.Dictionary<string, Material> MaterialCache =
            new System.Collections.Generic.Dictionary<string, Material>();

        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> TextureCache =
            new System.Collections.Generic.Dictionary<string, Texture2D>();

        /// <summary>Сбрасывает кэш (например, после смены шейдера/пайплайна).</summary>
        public static void ClearCache()
        {
            MaterialCache.Clear();
            TextureCache.Clear();
            TextureIds.Clear();
        }

        // ---------------- Ключ для текстуры ----------------
        //
        // Раньше здесь был texture.GetInstanceID(). В Unity 7 этот метод
        // помечен как устаревший с ошибкой (CS0619: «Use GetEntityId instead»)
        // — проект из-за этого не собирался. Свой счётчик работает в любой
        // версии Unity и не может дать совпадений.
        private static readonly System.Collections.Generic.Dictionary<Texture, int> TextureIds =
            new System.Collections.Generic.Dictionary<Texture, int>();
        private static int _nextTextureId = 1;

        private static int TextureKey(Texture texture)
        {
            if (texture == null) return 0;
            int id;
            if (!TextureIds.TryGetValue(texture, out id))
            {
                id = _nextTextureId++;
                TextureIds[texture] = id;
            }
            return id;
        }

        // ---------------- Материалы ----------------

        public static Material Solid(Color color, bool unlit = false, float smoothness = 0.12f, float metallic = 0f)
        {
            Shader shader = unlit ? UnlitShader : LitShader;
            if (shader == null)
            {
                Debug.LogError("[KiviGun] Не найден шейдер — проверь, установлен ли URP.");
                return null;
            }

            string key = (unlit ? "U|" : "L|") + ColorUtility.ToHtmlStringRGBA(color) +
                         "|" + smoothness.ToString("F2") + "|" + metallic.ToString("F2");

            Material cached;
            if (MaterialCache.TryGetValue(key, out cached) && cached != null) return cached;

            var mat = new Material(shader) { name = "KiviMat_" + ColorUtility.ToHtmlStringRGB(color) };
            ApplyColor(mat, color);
            if (!unlit)
            {
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            }

            MaterialCache[key] = mat;
            return mat;
        }

        /// <summary>
        /// Материал с процедурной текстурой — так плоские цвета выглядят «живее».
        /// Текстура генерируется кодом (никаких файлов в проекте) и кэшируется.
        /// </summary>
        public static Material Textured(Color color, Texture2D texture, Vector2 tiling,
                                        float smoothness = 0.06f, float metallic = 0f)
        {
            if (texture == null) return Solid(color, false, smoothness, metallic);

            Shader shader = LitShader;
            if (shader == null) return Solid(color, false, smoothness, metallic);

            string key = "T|" + ColorUtility.ToHtmlStringRGBA(color) + "|" + TextureKey(texture) +
                         "|" + tiling.x.ToString("F2") + "|" + tiling.y.ToString("F2") +
                         "|" + smoothness.ToString("F2") + "|" + metallic.ToString("F2");

            Material cached;
            if (MaterialCache.TryGetValue(key, out cached) && cached != null) return cached;

            var mat = new Material(shader) { name = "KiviMatT_" + ColorUtility.ToHtmlStringRGB(color) };
            ApplyColor(mat, color);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
            if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", tiling);
            else if (mat.HasProperty("_MainTex")) mat.SetTextureScale("_MainTex", tiling);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            MaterialCache[key] = mat;
            return mat;
        }

        // ---------------- Процедурные текстуры ----------------

        /// <summary>Мягкий шум (земля, бетон, трава). Тайлится по краям.</summary>
        public static Texture2D NoiseTexture(int size, Color a, Color b, float frequency, int seed)
        {
            string key = "N|" + size + "|" + ColorUtility.ToHtmlStringRGBA(a) + "|" +
                         ColorUtility.ToHtmlStringRGBA(b) + "|" + frequency.ToString("F2") + "|" + seed;
            Texture2D cached;
            if (TextureCache.TryGetValue(key, out cached) && cached != null) return cached;

            var tex = new Texture2D(Mathf.Max(4, size), Mathf.Max(4, size), TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 2;

            var rnd = new System.Random(seed);
            var pixels = new Color[tex.width * tex.height];
            int cells = Mathf.Max(2, Mathf.RoundToInt(tex.width * frequency));

            // Считаем сетку значений и сглаживаем — получается мягкая «каша», а не шум телевизора.
            var grid = new float[cells * cells];
            for (int i = 0; i < grid.Length; i++) grid[i] = (float)rnd.NextDouble();

            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    float fx = (float)x / tex.width * cells;
                    float fy = (float)y / tex.height * cells;
                    int x0 = Mathf.FloorToInt(fx) % cells;
                    int y0 = Mathf.FloorToInt(fy) % cells;
                    int x1 = (x0 + 1) % cells;
                    int y1 = (y0 + 1) % cells;
                    float tx = fx - Mathf.Floor(fx);
                    float ty = fy - Mathf.Floor(fy);
                    tx = tx * tx * (3f - 2f * tx);
                    ty = ty * ty * (3f - 2f * ty);

                    float v00 = grid[y0 * cells + x0];
                    float v10 = grid[y0 * cells + x1];
                    float v01 = grid[y1 * cells + x0];
                    float v11 = grid[y1 * cells + x1];
                    float top = Mathf.Lerp(v00, v10, tx);
                    float bottom = Mathf.Lerp(v01, v11, tx);
                    float v = Mathf.Lerp(top, bottom, ty);

                    pixels[y * tex.width + x] = Color.Lerp(a, b, v);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true);
            TextureCache[key] = tex;
            return tex;
        }

        /// <summary>Полосы (доски сарая, заборы).</summary>
        public static Texture2D StripeTexture(int size, Color a, Color b, int stripes)
        {
            string key = "S|" + size + "|" + ColorUtility.ToHtmlStringRGBA(a) + "|" +
                         ColorUtility.ToHtmlStringRGBA(b) + "|" + stripes;
            Texture2D cached;
            if (TextureCache.TryGetValue(key, out cached) && cached != null) return cached;

            int w = Mathf.Max(4, size);
            var tex = new Texture2D(w, w, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            var pixels = new Color[w * w];
            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float t = (float)x / w * stripes;
                    int stripe = Mathf.FloorToInt(t);
                    float local = t - stripe;
                    // Лёгкая неровность внутри полосы — не выглядит как идеальный принт.
                    float shade = 0.86f + 0.14f * Mathf.Sin(local * Mathf.PI);
                    Color c = (stripe % 2 == 0) ? a : b;
                    pixels[y * w + x] = c * shade;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true);
            TextureCache[key] = tex;
            return tex;
        }

        /// <summary>Клетчатый пол (плитка, окна зданий).</summary>
        public static Texture2D CheckerTexture(int size, Color a, Color b, int cells)
        {
            string key = "C|" + size + "|" + ColorUtility.ToHtmlStringRGBA(a) + "|" +
                         ColorUtility.ToHtmlStringRGBA(b) + "|" + cells;
            Texture2D cached;
            if (TextureCache.TryGetValue(key, out cached) && cached != null) return cached;

            int w = Mathf.Max(4, size);
            var tex = new Texture2D(w, w, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            var pixels = new Color[w * w];
            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int cx = Mathf.FloorToInt((float)x / w * cells);
                    int cy = Mathf.FloorToInt((float)y / w * cells);
                    pixels[y * w + x] = ((cx + cy) % 2 == 0) ? a : b;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true);
            TextureCache[key] = tex;
            return tex;
        }

        public static void ApplyColor(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }

        /// <summary>Включает свечение (окна, неон, трассеры).</summary>
        public static Material Emissive(Color color, Color emission, float intensity = 1.6f)
        {
            // Свой экземпляр: базовый Solid() отдаёт кэшированный материал,
            // а свечение/прозрачность — это разные настройки того же материала.
            Material mat = Clone(Solid(color));
            if (mat == null) return null;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission * intensity);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeIndirectEmission;
            }
            return mat;
        }

        /// <summary>Полупрозрачный материал (стекло, окна).</summary>
        public static Material Transparent(Color color, float alpha = 0.35f)
        {
            Material mat = Clone(Solid(color));
            if (mat == null) return null;
            Color c = color; c.a = alpha;
            ApplyColor(mat, c);
            SetupTransparent(mat);
            return mat;
        }

        /// <summary>Отдельная копия материала (чтобы не портить общий кэш).</summary>
        private static Material Clone(Material source)
        {
            if (source == null) return null;
            var copy = new Material(source);
            copy.name = source.name + "_Inst";
            return copy;
        }

        public static void SetupTransparent(Material mat)
        {
            if (mat == null) return;
            // URP
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
        }

        // ---------------- Примитивы ----------------

        public static GameObject Box(string name, Vector3 position, Vector3 size, Color color,
                                     Transform parent = null, bool collider = true, bool unlit = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Prepare(go, name, position, size, color, parent, collider, unlit);
            return go;
        }

        /// <summary>Вариант с готовым материалом (когда цветом не обойтись).</summary>
        public static GameObject Box(string name, Vector3 position, Vector3 size, Material material,
                                     Transform parent = null, bool collider = true)
        {
            GameObject go = Box(name, position, size, Color.white, parent, collider, false);
            if (go != null && material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Вариант с готовым материалом.</summary>
        public static GameObject Sphere(string name, Vector3 position, float diameter, Material material,
                                        Transform parent = null, bool collider = true)
        {
            GameObject go = Sphere(name, position, diameter, Color.white, parent, collider, false);
            if (go != null && material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Вариант с готовым материалом.</summary>
        public static GameObject Cylinder(string name, Vector3 position, float diameter, float height,
                                          Material material, Transform parent = null, bool collider = true)
        {
            GameObject go = Cylinder(name, position, diameter, height, Color.white, parent, collider, false);
            if (go != null && material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        public static GameObject Sphere(string name, Vector3 position, float diameter, Color color,
                                        Transform parent = null, bool collider = true, bool unlit = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Prepare(go, name, position, Vector3.one * diameter, color, parent, collider, unlit);
            return go;
        }

        public static GameObject Cylinder(string name, Vector3 position, float diameter, float height, Color color,
                                          Transform parent = null, bool collider = true, bool unlit = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Prepare(go, name, position, new Vector3(diameter, height * 0.5f, diameter), color, parent, collider, unlit);
            return go;
        }

        public static GameObject Capsule(string name, Vector3 position, float diameter, float height, Color color,
                                         Transform parent = null, bool collider = true, bool unlit = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Prepare(go, name, position, new Vector3(diameter, height * 0.5f, diameter), color, parent, collider, unlit);
            return go;
        }

        public static GameObject Plane(string name, Vector3 position, Vector3 size, Color color,
                                       Transform parent = null, bool collider = true, bool unlit = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Prepare(go, name, position, new Vector3(size.x, size.y, size.z), color, parent, collider, unlit);
            return go;
        }

        /// <summary>Конус (клюв утки, крыша и т.п.) — меш генерируется вручную.</summary>
        public static GameObject Cone(string name, Vector3 position, float diameter, float height, Color color,
                                       Transform parent = null, bool collider = false, bool unlit = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildConeMesh(16, 0.5f, 0.5f);
            go.AddComponent<MeshRenderer>().sharedMaterial = Solid(color, unlit);
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
            return go;
        }

        private static void Prepare(GameObject go, string name, Vector3 position, Vector3 size, Color color,
                                    Transform parent, bool collider, bool unlit)
        {
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.transform.localRotation = Quaternion.identity;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = Solid(color, unlit);

            var col = go.GetComponent<Collider>();
            if (col != null && !collider) Object.Destroy(col);
        }

        /// <summary>Простой конический меш (используется как клюв, крыша, шляпа).</summary>
        public static Mesh BuildConeMesh(int segments, float radius, float height)
        {
            segments = Mathf.Max(3, segments);
            var mesh = new Mesh { name = "KiviCone" };
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];

            verts[0] = new Vector3(0f, height * 0.5f, 0f);
            verts[1] = new Vector3(0f, -height * 0.5f, 0f);

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts[i + 2] = new Vector3(Mathf.Cos(a) * radius, -height * 0.5f, Mathf.Sin(a) * radius);
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i + 2;
                int b = (i + 1) % segments + 2;
                tris[i * 3] = 0; tris[i * 3 + 1] = a; tris[i * 3 + 2] = b;
            }

            var sideTris = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                int a = i + 2;
                int b = (i + 1) % segments + 2;
                sideTris[i * 3] = 1; sideTris[i * 3 + 1] = b; sideTris[i * 3 + 2] = a;
            }

            mesh.vertices = verts;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(tris, 0);
            mesh.SetTriangles(sideTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Простой цилиндр-меш (для столбов и труб без примитива).</summary>
        public static GameObject Pipe(string name, Vector3 position, float radius, float height, Color color, Transform parent = null)
        {
            GameObject go = Cylinder(name, position, radius * 2f, height, color, parent);
            return go;
        }
    }
}
