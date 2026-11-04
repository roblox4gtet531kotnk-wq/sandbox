// ВАРИАНТ С НАВМЕШЕМ — по умолчанию НЕ КОМПИЛИРУЕТСЯ.
//
// ПОЧЕМУ: тип NavMeshAgent живёт в модуле UnityEngine.AIModule
// (пакет com.unity.modules.ai). Если модуля нет в проекте — весь проект
// падает с ошибкой:
//
//   CS1069: The type name 'NavMeshAgent' could not be found in the
//           namespace 'UnityEngine.AI'
//
// Модуль AI уже прописан в Packages/manifest.json, но чтобы проект
// собирался ВСЕГДА (даже если модуль выключат), код ниже закрыт
// условием компиляции KIVI_NAVMESH.
//
// КАК ВКЛЮЧИТЬ (если реально понадобится навмеш):
//   1) Player Settings ▸ Other Settings ▸ Scripting Define Symbols
//      → добавить: KIVI_NAVMESH   (для Android);
//   2) убедиться, что в Packages/manifest.json есть
//      "com.unity.modules.ai": "1.0.0";
//   3) на объекте птицы: компонент SmartKiwiNavMesh (сам добавит NavMeshAgent);
//   4) запечь навмеш (Window ▸ AI ▸ Navigation) для карты.
//
// В игре «Киви Ган» навмеша нет: карты строятся кодом в рантайме,
// птицу ведёт KiwiEnemy (свой обход препятствий и честная память).
// Поэтому этот файл по умолчанию пустой для компилятора.

#if KIVI_NAVMESH
using UnityEngine;
using UnityEngine.AI;

namespace KiviGun
{
    /// <summary>
    /// «Умная» птица на NavMeshAgent: патрулирует → замечает игрока → преследует →
    /// теряет из виду (память на несколько секунд) → снова патрулирует.
    ///
    /// Логика зрения — общая с KiwiEnemy (SmartKiwi.CanSee), чтобы у птиц
    /// не было двух разных «глаз».
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SmartKiwiNavMesh : MonoBehaviour
    {
        [Header("Цель")]
        [SerializeField] private Transform player;

        [Header("Обнаружение")]
        [SerializeField] private float sightRange = 12f;
        [SerializeField, Range(1f, 360f)] private float viewAngle = 140f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Патруль")]
        [SerializeField] private float patrolRadius = 7f;
        [SerializeField] private float patrolWait = 2f;

        [Header("Преследование")]
        [SerializeField] private float memorySeconds = 4f;
        [SerializeField] private float chaseUpdateInterval = 0.25f;

        private NavMeshAgent _agent;
        private Vector3 _home;
        private Vector3 _lastSeenPosition;
        private float _lastSeenTime = float.NegativeInfinity;
        private float _nextChaseUpdate;
        private float _nextPatrolTime;
        private bool _warnedNoNavMesh;

        /// <summary>Видит ли птица игрока прямо сейчас.</summary>
        public bool SeesPlayer { get; private set; }

        /// <summary>Помнит ли птица, где был игрок (память на memorySeconds).</summary>
        public bool RemembersPlayer => Time.time - _lastSeenTime <= memorySeconds;

        /// <summary>Где птица видела игрока в последний раз.</summary>
        public Vector3 LastSeenPosition => _lastSeenPosition;

        /// <summary>Есть ли на уровне навмеш (иначе компонент только «смотрит»).</summary>
        public bool HasNavMesh => _agent != null && _agent.isOnNavMesh;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _home = transform.position;
        }

        private void Update()
        {
            if (_agent == null) return;

            if (!_agent.isOnNavMesh)
            {
                // Навмеша нет — двигаться нечем. Компонент работает только
                // как «глаза и память», движение берёт на себя KiwiEnemy.
                if (!_warnedNoNavMesh)
                {
                    _warnedNoNavMesh = true;
                    Debug.Log("[KiviGun] SmartKiwiNavMesh: на уровне нет NavMesh — птицу ведёт KiwiEnemy.");
                }
                UpdatePerception();
                return;
            }

            UpdatePerception();

            if (RemembersPlayer)
            {
                if (Time.time >= _nextChaseUpdate)
                {
                    _agent.SetDestination(_lastSeenPosition);
                    _nextChaseUpdate = Time.time + chaseUpdateInterval;
                }
                return;
            }

            Patrol();
        }

        private void UpdatePerception()
        {
            SeesPlayer = SmartKiwi.CanSee(transform, player, sightRange, viewAngle, obstacleMask.value);
            if (SeesPlayer)
            {
                _lastSeenPosition = player.position;
                _lastSeenTime = Time.time;
            }
        }

        /// <summary>Позволяет назначить игрока после его создания.</summary>
        public void SetPlayer(Transform target)
        {
            player = target;
        }

        /// <summary>Слой препятствий (стены, ящики). Игрок и сама птица исключаются всегда.</summary>
        public void SetObstacleMask(int mask)
        {
            obstacleMask = mask;
        }

        /// <summary>Точка, вокруг которой птица патрулирует (обычно точка спавна).</summary>
        public void SetPatrolHome(Vector3 position)
        {
            _home = position;
        }

        private void Patrol()
        {
            if (_agent.pathPending) return;

            if (_agent.hasPath &&
                _agent.remainingDistance > _agent.stoppingDistance + 0.2f)
            {
                return;
            }

            if (Time.time < _nextPatrolTime) return;

            Vector2 random = Random.insideUnitCircle * patrolRadius;
            Vector3 candidate = _home + new Vector3(random.x, 0f, random.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);

            _nextPatrolTime = Time.time + patrolWait;
        }
    }
}
#endif
