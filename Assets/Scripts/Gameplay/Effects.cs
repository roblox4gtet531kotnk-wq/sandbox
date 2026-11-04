using System.Collections.Generic;
using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Пул эффектов: трассеры, попадания, вспышки выстрела, облачка из перьев.
    /// Всё переиспользуется — на Android это критично.
    /// </summary>
    public class Effects : MonoBehaviour
    {
        public static Effects Instance { get; private set; }

        private enum FxMode { Fade, Expand, Tracer }

        /// <summary>
        /// Пул материалов эффекта. Раньше на каждый выстрел создавался новый Material —
        /// на телефоне это десятки новых материалов в секунду (память + draw call'ы).
        /// Теперь материалы переиспользуются.
        /// </summary>
        private class MatPool
        {
            private readonly Stack<Material> _materials = new Stack<Material>();
            private readonly Color _color;
            private readonly bool _transparent;

            public MatPool(Color color, bool transparent)
            {
                _color = color;
                _transparent = transparent;
            }

            public Material Get()
            {
                Material mat;
                if (_materials.Count > 0)
                {
                    mat = _materials.Pop();
                }
                else
                {
                    mat = MaterialFactory.Solid(_color, _transparent);
                    if (_transparent) MaterialFactory.SetupTransparent(mat);
                }

                // Возвращаем базовый цвет — вдруг эффект погас и оставил alpha = 0.
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", _color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", _color);
                return mat;
            }

            public void Release(Material mat)
            {
                if (mat != null) _materials.Push(mat);
            }
        }

        private class FxEntry
        {
            public GameObject Go;
            public Material Mat;
            public MatPool Pool;
            public FxMode Mode;
            public float Time;
            public float Duration;
            public Vector3 ScaleStart;
            public Vector3 ScaleEnd;
            public Color ColorStart;
        }

        private readonly List<FxEntry> _active = new List<FxEntry>();
        private readonly Stack<GameObject> _pool = new Stack<GameObject>();
        private MatPool _tracerPool;
        private MatPool _impactPool;
        private MatPool _greenPool;
        private MatPool _muzzlePool;
        private MatPool _poofPool;
        private float _shakeAmount;
        private float _shakeTime;
        private Transform _tracerRoot;

        public static Effects GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Effects");
            return go.AddComponent<Effects>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            var root = new GameObject("FxRoot");
            root.transform.SetParent(transform, false);
            _tracerRoot = root.transform;

            _tracerPool = new MatPool(new Color32(255, 236, 160, 255), true);
            _impactPool = new MatPool(new Color32(240, 220, 170, 255), true);
            _greenPool = new MatPool(new Color32(140, 198, 32, 255), true);
            _muzzlePool = new MatPool(new Color32(255, 226, 140, 255), true);
            _poofPool = new MatPool(new Color32(168, 208, 84, 255), true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                FxEntry fx = _active[i];
                fx.Time += dt;
                float t = Mathf.Clamp01(fx.Time / fx.Duration);

                if (fx.Go != null)
                {
                    Vector3 scale = Vector3.Lerp(fx.ScaleStart, fx.ScaleEnd, t);
                    fx.Go.transform.localScale = scale;
                    if (fx.Mat != null)
                    {
                        Color c = fx.ColorStart;
                        c.a = Mathf.Lerp(fx.ColorStart.a, 0f, t);
                        if (fx.Mat.HasProperty("_BaseColor")) fx.Mat.SetColor("_BaseColor", c);
                        if (fx.Mat.HasProperty("_Color")) fx.Mat.SetColor("_Color", c);
                    }
                }

                if (t >= 1f)
                {
                    if (fx.Go != null)
                    {
                        fx.Go.SetActive(false);
                        _pool.Push(fx.Go);
                    }
                    if (fx.Pool != null) fx.Pool.Release(fx.Mat);
                    _active.RemoveAt(i);
                }
            }

            // Тряска камеры
            if (_shakeTime > 0f)
            {
                _shakeTime -= dt;
                Camera cam = Camera.main;
                if (cam != null)
                {
                    float k = Mathf.Clamp01(_shakeTime / 0.25f) * _shakeAmount;
                    cam.transform.localRotation = Quaternion.Euler(
                        UnityEngine.Random.Range(-k, k),
                        UnityEngine.Random.Range(-k, k),
                        UnityEngine.Random.Range(-k, k));
                }
                else if (cam != null)
                {
                    // Иначе после тряски камера осталась бы наклонённой.
                    cam.transform.localRotation = Quaternion.identity;
                }
            }
        }

        private GameObject Take(string name)
        {
            GameObject go;
            if (_pool.Count > 0)
            {
                go = _pool.Pop();
                go.name = name;
                go.SetActive(true);
                return go;
            }
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) col.enabled = false;   // отключаем сразу, чтобы не мешал выстрелам
            go.transform.SetParent(_tracerRoot, false);
            return go;
        }

        /// <summary>Линия-трассер от выстрела.</summary>
        public void Tracer(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.2f) return;

            Material mat = _tracerPool.Get();

            GameObject go = Take("Tracer");
            go.transform.position = (from + to) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(dir.normalized);
            go.transform.localScale = new Vector3(0.035f, 0.035f, length);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            _active.Add(new FxEntry
            {
                Go = go,
                Mat = mat,
                Pool = _tracerPool,
                Mode = FxMode.Tracer,
                Time = 0f,
                Duration = 0.07f,
                ScaleStart = go.transform.localScale,
                ScaleEnd = new Vector3(0.01f, 0.01f, length),
                ColorStart = new Color(1f, 0.92f, 0.6f, 0.95f)
            });
        }

        /// <summary>Искры при попадании в стену.</summary>
        public void Impact(Vector3 position, Vector3 direction)
        {
            Material mat = _impactPool.Get();

            GameObject go = Take("Impact");
            go.transform.position = position + direction * 0.05f;
            go.transform.localScale = Vector3.one * 0.12f;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            _active.Add(new FxEntry
            {
                Go = go,
                Mat = mat,
                Pool = _impactPool,
                Mode = FxMode.Expand,
                Time = 0f,
                Duration = 0.18f,
                ScaleStart = new Vector3(0.12f, 0.12f, 0.12f),
                ScaleEnd = new Vector3(0.5f, 0.5f, 0.5f),
                ColorStart = new Color(1f, 0.9f, 0.7f, 0.85f)
            });
        }

        /// <summary>Зелёное облачко перьев при попадании в киви.</summary>
        public void ImpactGreen(Vector3 position, Vector3 direction)
        {
            Material mat = _greenPool.Get();

            for (int i = 0; i < 3; i++)
            {
                GameObject go = Take("KiwiHit");
                go.transform.position = position + direction * 0.08f + UnityEngine.Random.insideUnitSphere * 0.12f;
                go.transform.localScale = Vector3.one * 0.1f;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;

                _active.Add(new FxEntry
                {
                    Go = go,
                    Mat = mat,
                    Pool = _greenPool,
                    Mode = FxMode.Expand,
                    Time = -i * 0.05f,
                    Duration = 0.3f,
                    ScaleStart = new Vector3(0.1f, 0.1f, 0.1f),
                    ScaleEnd = new Vector3(0.34f, 0.34f, 0.34f),
                    ColorStart = new Color(0.55f, 0.8f, 0.2f, 0.9f)
                });
            }
        }

        /// <summary>Вспышка у дула.</summary>
        public void MuzzleFlash(Vector3 position, Vector3 direction)
        {
            Material mat = _muzzlePool.Get();

            GameObject go = Take("Muzzle");
            go.transform.position = position + direction * 0.12f;
            go.transform.localScale = Vector3.one * 0.18f;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            _active.Add(new FxEntry
            {
                Go = go,
                Mat = mat,
                Pool = _muzzlePool,
                Mode = FxMode.Expand,
                Time = 0f,
                Duration = 0.09f,
                ScaleStart = new Vector3(0.16f, 0.16f, 0.16f),
                ScaleEnd = new Vector3(0.45f, 0.45f, 0.45f),
                ColorStart = new Color(1f, 0.85f, 0.45f, 0.95f)
            });
        }

        /// <summary>Пух от убитой птицы.</summary>
        public void KiwiPoof(Vector3 position)
        {
            Material mat = _poofPool.Get();

            for (int i = 0; i < 5; i++)
            {
                GameObject go = Take("Poof");
                go.transform.position = position + UnityEngine.Random.insideUnitSphere * 0.3f;
                go.transform.localScale = Vector3.one * 0.12f;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;

                _active.Add(new FxEntry
                {
                    Go = go,
                    Mat = mat,
                    Pool = _poofPool,
                    Mode = FxMode.Expand,
                    Time = -i * 0.04f,
                    Duration = 0.45f,
                    ScaleStart = new Vector3(0.12f, 0.12f, 0.12f),
                    ScaleEnd = new Vector3(0.5f, 0.5f, 0.5f),
                    ColorStart = new Color(0.6f, 0.8f, 0.3f, 0.8f)
                });
            }
        }

        /// <summary>Тряска камеры.</summary>
        public void Shake(float amount)
        {
            _shakeAmount = Mathf.Max(_shakeAmount, amount);
            _shakeTime = 0.25f;
        }
    }
}
