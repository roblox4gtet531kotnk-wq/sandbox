using System.Collections.Generic;
using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Отвечает за появление киви-птиц на карте: волнами, с задержкой,
    /// и следит, чтобы уровень считался пройденным, когда врагов не осталось.
    /// </summary>
    public class EnemyDirector : MonoBehaviour
    {
        public List<Vector3> SpawnPoints = new List<Vector3>();
        public int TotalToSpawn;
        public float SpawnInterval = 1.1f;
        public int MaxAlive = 8;
        public float EnemySpeed = 2.2f;
        public float EnemyDamage = 8f;
        public Transform PlayerTarget;
        public LevelDefinition Definition;

        public int Spawned { get; private set; }
        public int Alive { get; private set; }
        public int Killed { get; private set; }
        public bool Finished => Spawned >= TotalToSpawn && Alive <= 0;

        public System.Action OnAllCleared;
        public System.Action<int> OnProgress;

        /// <summary>Точки укрытий на карте — киви используют их, чтобы прятаться.</summary>
        public Vector3[] CoverPoints;

        private float _timer;
        private bool _done;
        private readonly List<KiwiEnemy> _enemies = new List<KiwiEnemy>();

        public void Begin(LevelDefinition def, List<Vector3> points, Transform player)
        {
            Definition = def;
            SpawnPoints = points ?? new List<Vector3>();
            TotalToSpawn = def != null ? def.EnemyCount : 0;
            EnemySpeed = def != null ? def.EnemySpeed : 2.2f;
            EnemyDamage = def != null ? def.EnemyDamage : 8f;
            PlayerTarget = player;
            Spawned = 0;
            Alive = 0;
            Killed = 0;
            _done = false;
            _timer = 0.8f;
        }

        /// <summary>Задать укрытия (вызывается из GameController после постройки карты).</summary>
        public void SetCoverPoints(Transform[] coverTransforms)
        {
            if (coverTransforms == null || coverTransforms.Length == 0)
            {
                CoverPoints = new Vector3[0];
                return;
            }

            // null-пропускаем: раньше на их месте оказывался ноль мировых
            // координат, и птицы считали начало координат укрытием.
            var points = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < coverTransforms.Length; i++)
            {
                if (coverTransforms[i] == null) continue;
                points.Add(coverTransforms[i].position);
            }

            CoverPoints = points.ToArray();
        }

        /// <summary>
        /// Тревога: когда одну киви задели, соседние тоже становятся умнее
        /// (кружат и фланкируют вместо тупого преследования).
        /// </summary>
        public void AlertSquad(Vector3 noisePosition, float radius)
        {
            float radiusSq = radius * radius;
            for (int i = 0; i < _enemies.Count; i++)
            {
                KiwiEnemy enemy = _enemies[i];
                if (enemy == null) continue;
                if ((enemy.transform.position - noisePosition).sqrMagnitude > radiusSq) continue;

                enemy.Alert(noisePosition);
            }
        }

        private void Update()
        {
            if (_done) return;

            // Уровень сломан (нет точек появления) — ни победы, ни новых птиц.
            if (_broken) return;

            if (Spawned < TotalToSpawn)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f && Alive < MaxAlive)
                {
                    _timer = SpawnInterval;
                    SpawnOne();
                }
            }
            else if (Alive <= 0)
            {
                _done = true;
                OnAllCleared?.Invoke();
            }
        }

        /// <summary>Уровень собран неверно (например, нет точек появления киви).</summary>
        public event System.Action OnLevelFailed;

        /// <summary>Директор остановился из-за ошибки уровня — победа больше не наступит.</summary>
        private bool _broken;

        private void SpawnOne()
        {
            if (SpawnPoints.Count == 0)
            {
                // Ошибка постройки уровня: нет ни одной точки появления.
                // Раньше здесь просто увеличивался Spawned — и директор
                // объявлял победу, хотя ни одна птица не появилась.
                Debug.LogError("[KiviGun] На уровне нет точек появления киви — уровень собран неверно.");
                _broken = true;
                Spawned = TotalToSpawn;
                ToastManager.GetOrCreate().Show("Ошибка уровня: нет точек появления киви");
                OnLevelFailed?.Invoke();
                return;
            }

            Vector3 point = SpawnPoints[UnityEngine.Random.Range(0, SpawnPoints.Count)];
            // Небольшой разброс, чтобы птицы не стояли друг в друге
            point += new Vector3(UnityEngine.Random.Range(-1.2f, 1.2f), 0f, UnityEngine.Random.Range(-1.2f, 1.2f));

            // Модель птицы создаёт сам KiwiEnemy (в Awake).
            // Если создать её ещё и здесь — киви будет рендериться дважды (z-fighting).
            var go = new GameObject("Kiwi");
            // Птица — ребёнок директора: её жизненный цикл привязан к уровню,
            // и при пересоздании уровня или смене сцены она не останется
            // «сиротой» в сцене.
            go.transform.SetParent(transform, false);
            go.transform.position = point;

            KiwiEnemy enemy = go.AddComponent<KiwiEnemy>();
            enemy.Configure(EnemySpeed, EnemyDamage);
            enemy.SetHome(point);          // центр патрулирования — точка появления
            enemy.target = PlayerTarget;
            enemy.coverPoints = CoverPoints;
            enemy.OnDied += HandleDeath;
            enemy.OnHurt += HandleHurt;

            _enemies.Add(enemy);
            Spawned++;
            Alive++;
            OnProgress?.Invoke(Spawned);
        }

        private void HandleDeath(KiwiEnemy enemy)
        {
            Alive = Mathf.Max(0, Alive - 1);
            Killed++;
            _enemies.Remove(enemy);
        }

        /// <summary>Одну киви задели — поднимаем тревогу (умное поведение стаи).</summary>
        private void HandleHurt(KiwiEnemy enemy)
        {
            if (enemy == null) return;
            AlertSquad(enemy.transform.position, 12f);
        }

        /// <summary>Убирает всех живых врагов (например, при поражении игрока).</summary>
        public void ClearAll()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i] != null) Destroy(_enemies[i].gameObject);
            }
            _enemies.Clear();
            Alive = 0;
        }
    }
}
