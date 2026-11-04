using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Киви-птица. Преследует игрока, атакует клювом, умирает с эффектами.
    ///
    /// УМНОЕ ПОВЕДЕНИЕ (новое): птица не тупо бежит на игрока, а:
    ///   • кружит вокруг него на расстоянии (не даёт easy-аим);
    ///   • заходит сбоку или сзади (фланк);
    ///   • после попадания прячется за укрытие;
    ///   • при низком здоровье убегает;
    ///   • поднимает тревогу — соседние киви тоже становятся умными.
    ///
    /// СОМНЕНИЕ: «ум» киви считается по дистанции и наличию укрытий, а не
    /// по реальной модели ИИ. Если хочешь сложнее — можно добавить проверку
    /// линии огня (Raycast до игрока) перед атакой.
    /// </summary>
    public class KiwiEnemy : MonoBehaviour
    {
        /// <summary>Что птица делает сейчас.</summary>
        public enum KiwiBrain
        {
            Patrol,  // патрулирует у своего места (не видит игрока)
            Chase,   // базовое: бежит к игроку
            Circle,  // умное: кружит вокруг игрока
            Flank,   // умное: заходит сбоку/сзади
            Cover,   // умное: прячется за укрытием
            Flee     // умное: убегает, когда почти убит
        }

        public float maxHealth = 55f;
        public float health = 55f;
        public float speed = 2.2f;
        public float damage = 8f;
        public float attackRange = 1.35f;
        public float attackCooldown = 1.1f;
        public float hopHeight = 0.55f;
        public int sliceDrop = 1;

        public Transform target;

        /// <summary>Точки укрытий на карте (задаёт EnemyDirector).</summary>
        public Vector3[] coverPoints;

        /// <summary>Птица «в курсе» (0 = спокойно, 1 = в бою).</summary>
        public float alertness;

        // --- Восприятие (патруль → заметил → погоня → потерял) ---

        /// <summary>На каком расстоянии птица замечает игрока.</summary>
        public float sightRange = 12f;

        /// <summary>Сектор обзора в градусах (180 = видит и спереди, и сзади).</summary>
        public float viewAngle = 170f;

        /// <summary>Сколько секунд птица помнит, где видела игрока.</summary>
        public float memorySeconds = 4f;

        /// <summary>Радиус патрулирования вокруг места, где птица появилась.</summary>
        public float patrolRadius = 6f;

        /// <summary>Сработало, когда птицу задели — директор поднимает стаю.</summary>
        public System.Action<KiwiEnemy> OnHurt;

        public System.Action<KiwiEnemy> OnDied;

        private Transform _model;
        private Transform _wingL;
        private Transform _wingR;
        private GameObject _alertMark;
        private Collider _collider;
        private float _nextAttackTime;
        private float _hopTimer;
        private float _flapTimer;
        private float _deathTimer;
        private bool _dying;
        private bool _dead;
        private Vector3 _velocity;
        private float _hitFlash;
        private Renderer[] _renderers;
        private Color[] _baseColors;
        private MaterialPropertyBlock _propertyBlock;

        private KiwiBrain _brain = KiwiBrain.Patrol;
        private float _brainTimer;
        private float _circleSign = 1f;
        private Vector3 _coverTarget;
        private bool _hasCover;
        private Vector3 _moveDirection = Vector3.forward;

        // --- Восприятие ---
        private Vector3 _homePosition;
        private Vector3 _lastSeenPosition;
        private float _lastSeenTime = float.NegativeInfinity;
        private bool _seesPlayer;

        // --- Патруль ---
        private Vector3 _patrolTarget;
        private bool _hasPatrolTarget;
        private float _nextPatrolTime;

        /// <summary>Сторона обхода препятствия (запоминается, чтобы не дёргаться).</summary>
        private float _avoidSide = 1f;

        /// <summary>Сколько секунд птица упирается в препятствие.</summary>
        private float _blockedTime;

        /// <summary>Сколько секунд птица уже убегает (ограничиваем бегство).</summary>
        private float _fleeTime;

        /// <summary>Бегство исчерпано — птица больше не убегает (иначе спрячется навсегда).</summary>
        private bool _fleeExhausted;

        public bool IsDead => _dead;
        public KiwiBrain Brain => _brain;

        private void Awake()
        {
            _model = CharacterFactory.CreateKiwi(transform, 1f).transform;

            // Один коллайдер вместо десятка — важно для мобильного FPS
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                if (c.gameObject != gameObject) Destroy(c);
            }
            _collider = gameObject.GetComponent<Collider>();
            CapsuleCollider capsule = _collider as CapsuleCollider;
            if (capsule == null)
            {
                // Коллайдер должен быть капсулой — именно у неё есть center/radius/height.
                if (_collider != null) Destroy(_collider);
                capsule = gameObject.AddComponent<CapsuleCollider>();
                _collider = capsule;
            }
            capsule.center = new Vector3(0f, 0.55f, 0f);
            capsule.radius = 0.45f;
            capsule.height = 1.15f;

            _wingL = _model.Find("WingL");
            _wingR = _model.Find("WingR");

            // Значок тревоги над головой: игрок сразу видит, что птица «включила ум».
            _alertMark = MaterialFactory.Sphere("AlertMark", new Vector3(0f, 1.15f, 0f), 0.26f,
                new Color32(255, 208, 64, 255), _model, false, true);
            Material alertMat = MaterialFactory.Emissive(new Color32(255, 208, 64, 255),
                new Color32(255, 208, 64, 255), 2.2f);
            var alertRenderer = _alertMark != null ? _alertMark.GetComponent<MeshRenderer>() : null;
            if (alertRenderer != null) alertRenderer.sharedMaterial = alertMat;
            if (_alertMark != null) _alertMark.SetActive(false);

            // Вспышка при попадании — через MaterialPropertyBlock, а НЕ через r.material.
            // r.material создаёт КОПИЮ материала для каждого рендерера: на 8 птицах
            // это десятки лишних материалов в памяти (на телефоне это важно).
            // MaterialPropertyBlock меняет цвет только у этого рендерера.
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseColors = new Color[_renderers.Length];
            _propertyBlock = new MaterialPropertyBlock();

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                Material shared = r != null ? r.sharedMaterial : null;
                if (shared != null && shared.HasProperty("_BaseColor")) _baseColors[i] = shared.GetColor("_BaseColor");
                else if (shared != null && shared.HasProperty("_Color")) _baseColors[i] = shared.GetColor("_Color");
                else _baseColors[i] = Color.white;
            }

            _circleSign = UnityEngine.Random.value > 0.5f ? 1f : -1f;
            _homePosition = transform.position;
        }

        public void Configure(float enemySpeed, float enemyDamage)
        {
            speed = enemySpeed;
            damage = enemyDamage;
        }

        /// <summary>
        /// Точка «дома» птицы (центр патрулирования). Ставится спавнером —
        /// на случай, если птицу создали раньше, чем поставили на место.
        /// </summary>
        public void SetHome(Vector3 position)
        {
            _homePosition = position;
        }

        // ------------------------------------------------------------------
        //  Поведение
        // ------------------------------------------------------------------

        /// <summary>Тревога: птица заметила игрока (или товарища) и становится умнее.</summary>
        public void Alert(Vector3 noisePosition)
        {
            alertness = 1f;
            _lastSeenPosition = noisePosition;
            _lastSeenTime = Time.time;

            if (_brain == KiwiBrain.Chase) SetBrain(KiwiBrain.Circle, 2.4f);
            else if (_brain == KiwiBrain.Patrol) SetBrain(KiwiBrain.Chase, 2.5f);
        }

        private void SetBrain(KiwiBrain brain, float duration)
        {
            _brain = brain;
            _brainTimer = duration;

            if (brain != KiwiBrain.Flee) _fleeTime = 0f;

            if (brain == KiwiBrain.Cover) PickCover();
            if (brain == KiwiBrain.Circle && UnityEngine.Random.value > 0.5f) _circleSign = -_circleSign;
        }

        private void Update()
        {
            if (_dying)
            {
                UpdateDeath();
                return;
            }
            if (_dead) return;

            float dt = Time.deltaTime;
            _flapTimer += dt * (4f + speed);
            UpdateHop(dt);

            if (target == null) return;

            // Реальное расстояние до игрока — только для атаки.
            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;
            float playerDistance = toPlayer.magnitude;

            // Восприятие: птица «видит» игрока только в секторе обзора
            // и если между ними нет стены. Память держит последнюю точку.
            UpdatePerception();

            // Атака — по реальной дистанции И прямой видимости
            // (сквозь стену бить нельзя).
            if (playerDistance <= attackRange) TryAttack();

            // Куда идти и какое расстояние считать «до цели»:
            //   • видим игрока — цель это игрок;
            //   • не видим — цель это последняя известная точка.
            // Раньше расстояние всегда считалось до текущей позиции игрока,
            // и птица «знала», где он, даже когда не видела его.
            Vector3 aim;
            if (_seesPlayer) aim = target.position;
            else if (HasMemory) aim = _lastSeenPosition;
            else aim = _homePosition;      // ни разу не видели — идём к своему месту

            Vector3 toAim = aim - transform.position;
            toAim.y = 0f;
            float aimDistance = toAim.magnitude;

            Think(dt, aimDistance, toAim);

            // Поворачиваемся туда, куда идём (иначе птица смотрит в пол)
            if (_moveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(_moveDirection.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 8f * dt);
            }

            // Махи крыльев
            float flap = Mathf.Sin(_flapTimer) * 22f;
            if (_wingL != null) _wingL.localRotation = Quaternion.Euler(0f, 0f, 8f + flap);
            if (_wingR != null) _wingR.localRotation = Quaternion.Euler(0f, 0f, -8f - flap);

            // Значок тревоги: горит, пока птица в «умном» режиме
            if (_alertMark != null)
            {
                bool show = alertness > 0.35f;
                if (_alertMark.activeSelf != show) _alertMark.SetActive(show);
                if (show)
                {
                    float s = 1f + Mathf.Sin(Time.time * 7f) * 0.22f;
                    _alertMark.transform.localScale = Vector3.one * s;
                    _alertMark.transform.localPosition = new Vector3(0f, 1.15f + Mathf.Sin(Time.time * 4f) * 0.05f, 0f);
                }
            }

            // Приседание при прыжке
            if (_model != null)
            {
                Vector3 p = _model.localPosition;
                p.y = Mathf.Lerp(p.y, 0f, 10f * dt);
                _model.localPosition = p;
            }

            if (_hitFlash > 0f)
            {
                _hitFlash -= dt;
                if (_hitFlash <= 0f) SetTint(Color.white);
            }
        }

        private void Think(float dt, float distance, Vector3 toTarget)
        {
            _brainTimer -= dt;
            alertness = Mathf.Max(0f, alertness - dt * 0.35f);

            // Птица заметила игрока — бросаем патруль и переходим в погоню.
            if (_seesPlayer && _brain == KiwiBrain.Patrol)
                SetBrain(KiwiBrain.Chase, 3f);

            // Игрока не видно и память о нём истекла — теряем из виду,
            // возвращаемся к патрулю (раненые в укрытии остаются в укрытии).
            if (!_seesPlayer && !HasMemory &&
                _brain != KiwiBrain.Patrol && _brain != KiwiBrain.Flee && _brain != KiwiBrain.Cover)
                SetBrain(KiwiBrain.Patrol, 2f);

            // Мало здоровья — убегаем (умное поведение).
            // Если бегство уже исчерпано — птица больше не убегает
            // (иначе она «спряталась» бы за краем карты навсегда).
            if (health <= maxHealth * 0.3f && !_fleeExhausted &&
                _brain != KiwiBrain.Flee && _brain != KiwiBrain.Cover)
                SetBrain(KiwiBrain.Flee, 2.2f);

            switch (_brain)
            {
                case KiwiBrain.Patrol:
                    if (_brainTimer <= 0f) SetBrain(_seesPlayer ? KiwiBrain.Chase : KiwiBrain.Patrol, 2.5f);
                    break;
                case KiwiBrain.Circle:
                    // Кружим, потом переходим во фланк и обратно.
                    if (_brainTimer <= 0f)
                        SetBrain(alertness > 0.4f ? KiwiBrain.Flank : KiwiBrain.Chase, 2.6f);
                    break;
                case KiwiBrain.Flank:
                    if (_brainTimer <= 0f)
                        SetBrain(alertness > 0.4f ? KiwiBrain.Circle : KiwiBrain.Chase, 2.2f);
                    break;
                case KiwiBrain.Cover:
                    if (_brainTimer <= 0f) SetBrain(KiwiBrain.Flank, 2.4f);
                    break;
                case KiwiBrain.Flee:
                    // НАМЕРЕННО: птица с низким здоровьем убегает и убегает
                    // дальше (не лезет в бой). Но не бесконечно — иначе она
                    // «спрячется» за краем карты и уровень не пройти.
                    if (_brainTimer <= 0f)
                    {
                        _fleeTime += 2.2f;
                        if (_fleeTime >= 12f)
                        {
                            // Бегство исчерпано: птица больше НЕ берёт режим
                            // «убегать» (иначе правило «мало HP → бежать»
                            // снова отправляло бы её в Flee на следующем кадре).
                            _fleeExhausted = true;
                            _fleeTime = 0f;
                            SetBrain(KiwiBrain.Chase, 1f);
                        }
                        else
                        {
                            SetBrain(KiwiBrain.Flee, 2.2f);
                        }
                    }
                    break;
            }

            switch (_brain)
            {
                case KiwiBrain.Patrol: MovePatrol(dt); break;
                case KiwiBrain.Circle: MoveCircle(dt, distance, toTarget); break;
                case KiwiBrain.Flank: MoveFlank(dt, distance, toTarget); break;
                case KiwiBrain.Cover: MoveCover(dt, distance); break;
                case KiwiBrain.Flee: MoveFlee(dt, distance, toTarget); break;
                default: MoveChase(dt, distance, toTarget); break;
            }
        }

        /// <summary>
        /// Восприятие: птица видит игрока в секторе обзора, если между ними
        /// нет препятствия. Логика зрения — общая с компонентом SmartKiwi
        /// (Assets/Scripts/Enemies/SmartKiwi.cs), чтобы не было двух разных
        /// «глаз» у птицы.
        /// </summary>
        private void UpdatePerception()
        {
            if (target == null)
            {
                _seesPlayer = false;
                return;
            }

            _seesPlayer = SmartKiwi.CanSee(transform, target, sightRange, viewAngle, Physics.AllLayers);

            if (_seesPlayer)
            {
                _lastSeenPosition = target.position;
                _lastSeenTime = Time.time;
                alertness = 1f;
            }
        }

        /// <summary>Помнит ли птица, где видела игрока.</summary>
        public bool HasMemory => Time.time - _lastSeenTime <= memorySeconds;

        /// <summary>Видит ли птица игрока прямо сейчас.</summary>
        public bool SeesPlayer => _seesPlayer;

        /// <summary>
        /// Патрулирование: птица бродит вокруг места, где появилась, пока
        /// не заметит игрока.
        /// </summary>
        private void MovePatrol(float dt)
        {
            if (!_hasPatrolTarget)
            {
                if (Time.time < _nextPatrolTime)
                {
                    _moveDirection = Vector3.zero;
                    return;
                }

                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                float r = UnityEngine.Random.Range(1.5f, Mathf.Max(2f, patrolRadius));
                _patrolTarget = _homePosition + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                _hasPatrolTarget = true;
                _nextPatrolTime = Time.time + 2.2f;
                return;
            }

            Vector3 toTarget = _patrolTarget - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 1.2f)
            {
                // Дошли — постояли и выбрали новую точку.
                _hasPatrolTarget = false;
                _moveDirection = Vector3.zero;
                return;
            }

            Vector3 move = AvoidObstacles(toTarget.normalized);
            _moveDirection = move;
            transform.position += move * speed * 0.55f * dt;   // патруль — неспешно
        }

        /// <summary>Базовое поведение: прямо к цели (игроку или последней известной точке).</summary>
        private void MoveChase(float dt, float distance, Vector3 toTarget)
        {
            if (toTarget.sqrMagnitude < 0.01f) return;

            Vector3 dir = AvoidObstacles(toTarget.normalized);
            _moveDirection = dir;
            transform.position += dir * speed * dt;
        }

        /// <summary>Кружит вокруг игрока на средней дистанции — целиться сложнее.</summary>
        private void MoveCircle(float dt, float distance, Vector3 toTarget)
        {
            Vector3 dir = toTarget.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * _circleSign;
            const float radius = 4.2f;

            Vector3 move;
            if (distance > radius + 1.3f) move = dir;                 // далеко — сближаемся
            else if (distance < radius - 1.3f) move = -dir * 0.8f;     // близко — отходим
            else move = side;                                          // кружим

            move = AvoidObstacles(move.normalized);
            _moveDirection = move;
            transform.position += move * speed * 1.05f * dt;
        }

        /// <summary>Заходит сбоку или сзади — к игроку в спину.</summary>
        private void MoveFlank(float dt, float distance, Vector3 toTarget)
        {
            Vector3 forward;
            if (_seesPlayer && target != null)
            {
                // Видим игрока — знаем, куда он смотрит: заходим за спину.
                forward = target.forward;
                forward.y = 0f;
            }
            else
            {
                // Не видим — идём к последней известной точке (не «знаем» позицию).
                forward = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : Vector3.forward;
            }

            if (forward.sqrMagnitude < 0.01f) forward = toTarget.normalized;

            // Обходим сзади: точка за спиной у игрока (или за последней известной точкой).
            Vector3 aimPoint = (_seesPlayer && target != null) ? target.position : _lastSeenPosition;
            Vector3 behind = aimPoint - forward * 3.4f;
            behind.y = transform.position.y;

            Vector3 toFlank = behind - transform.position;
            toFlank.y = 0f;

            Vector3 move = toFlank.sqrMagnitude > 0.05f ? toFlank.normalized : toTarget.normalized;
            move = AvoidObstacles(move);
            _moveDirection = move;
            transform.position += move * speed * 1.15f * dt;
        }

        /// <summary>Прячется за ближайшим укрытием (если укрытий нет — идёт фланком).</summary>
        private void MoveCover(float dt, float distance)
        {
            if (!_hasCover)
            {
                SetBrain(KiwiBrain.Flank, 2f);
                return;
            }

            Vector3 toCover = _coverTarget - transform.position;
            toCover.y = 0f;

            if (toCover.sqrMagnitude < 1.4f)
            {
                // Мы в укрытии: ждём и ловим момент для атаки.
                _moveDirection = Vector3.zero;
                return;
            }

            Vector3 move = AvoidObstacles(toCover.normalized);
            _moveDirection = move;
            transform.position += move * speed * 1.2f * dt;
        }

        /// <summary>Убегает от игрока.</summary>
        private void MoveFlee(float dt, float distance, Vector3 toTarget)
        {
            Vector3 away = -toTarget.normalized;
            Vector3 move = AvoidObstacles(away);
            _moveDirection = move;
            transform.position += move * speed * 1.3f * dt;
        }

        /// <summary>Выбирает ближайшее укрытие (но не слишком близко к игроку).</summary>
        private void PickCover()
        {
            _hasCover = false;
            if (coverPoints == null || coverPoints.Length == 0) return;

            float best = float.MaxValue;
            Vector3 bestPoint = Vector3.zero;

            for (int i = 0; i < coverPoints.Length; i++)
            {
                Vector3 p = coverPoints[i];
                float d = (p - transform.position).sqrMagnitude;
                if (d >= best) continue;

                // Укрытие должно быть не вплотную к игроку, иначе прятаться бессмысленно.
                // Если игрока не видно — сравниваем с последней известной точкой,
                // а не с его настоящей позицией (птица не должна «знать» её).
                Vector3 playerSpot = _seesPlayer && target != null ? target.position : _lastSeenPosition;
                if ((p - playerSpot).sqrMagnitude < 4f) continue;

                best = d;
                bestPoint = p;
            }

            if (best < float.MaxValue)
            {
                _coverTarget = bestPoint;
                _hasCover = true;
            }
        }

        private void TryAttack()
        {
            if (Time.time < _nextAttackTime) return;
            if (!CanReachPlayer()) return;      // не бьём сквозь стену и не «по памяти»

            _nextAttackTime = Time.time + attackCooldown;
            Attack();
        }

        /// <summary>
        /// Достаёт ли птица до игрока прямо сейчас: он рядом И его видно
        /// (между ними нет стены).
        /// </summary>
        private bool CanReachPlayer()
        {
            if (target == null) return false;

            // Дальность атаки — РОВНО attackRange (без «+30%»).
            // Расстояние считаем так же, как в CanSee — от «глаз» к «глазам»,
            // иначе горизонтальная и объёмная дистанции расходятся.
            Vector3 eye = transform.position + Vector3.up * 0.8f;
            Vector3 aim = target.position + Vector3.up * 0.8f;
            Vector3 to = aim - eye;
            if (to.magnitude > attackRange) return false;

            return SmartKiwi.CanSee(transform, target, attackRange, 360f, Physics.AllLayers);
        }

        /// <summary>
        /// Обход препятствий. Сторона обхода запоминается: раньше она выбиралась
        /// случайно каждый кадр, и у стены птица дёргалась туда-сюда.
        /// </summary>
        private Vector3 AvoidObstacles(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return dir;

            RaycastHit hit;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, dir, out hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                _blockedTime += Time.deltaTime;

                // Меняем сторону обхода не чаще раза в 0.5 с: иначе у стены
                // птица металась бы между двумя направлениями.
                if (_blockedTime > 0.5f)
                {
                    _avoidSide = -_avoidSide;
                    _blockedTime = 0f;
                }

                Vector3 side = Vector3.Cross(Vector3.up, dir) * _avoidSide;
                return (dir * 0.4f + side).normalized;
            }

            _blockedTime = 0f;
            return dir;
        }

        private void UpdateHop(float dt)
        {
            _hopTimer -= dt;
            if (_hopTimer <= 0f)
            {
                _hopTimer = UnityEngine.Random.Range(0.45f, 0.8f);
                _velocity.y = UnityEngine.Random.Range(2.4f, 3.4f);
                if (_model != null)
                    _model.localPosition = new Vector3(0f, -0.18f, 0f);
            }

            _velocity.y += -16f * dt;
            transform.position += Vector3.up * _velocity.y * dt;

            // Приземление
            RaycastHit ground;
            if (Physics.Raycast(transform.position + Vector3.up * 0.6f, Vector3.down, out ground, 1.4f, ~0, QueryTriggerInteraction.Ignore))
            {
                float floor = ground.point.y;
                if (transform.position.y <= floor + 0.02f)
                {
                    transform.position = new Vector3(transform.position.x, floor, transform.position.z);
                    _velocity.y = 0f;
                }
            }

            // Страховка: выпрыгнули за край уровня / луч не достал до пола —
            // не падаем в бесконечность (выглядело как «киви сами исчезают»),
            // а возвращаемся на своё место спавна.
            if (transform.position.y < -8f)
            {
                transform.position = new Vector3(_homePosition.x, _homePosition.y + 1f, _homePosition.z);
                _velocity = Vector3.zero;
            }
        }

        private void Attack()
        {
            // Повторная проверка расстояния: к моменту удара игрок мог отбежать.
            if (target == null) return;
            Vector3 eye = transform.position + Vector3.up * 0.8f;
            Vector3 aim = target.position + Vector3.up * 0.8f;
            Vector3 to = aim - eye;
            if (to.magnitude > attackRange) return;

            AudioManager.Instance?.PlaySfx(ProceduralAudio.Sfx.KiwiHit, 0.45f, UnityEngine.Random.Range(1.1f, 1.3f));

            if (target != null)
            {
                PlayerController player = target.GetComponent<PlayerController>();
                if (player != null)
                {
                    player.TakeDamage(damage, transform.position);
                }
            }
        }

        public void TakeDamage(float amount, Vector3 direction, GameObject source)
        {
            if (_dead || _dying) return;
            health -= amount;
            _hitFlash = 0.12f;
            SetTint(new Color(1f, 0.45f, 0.45f));

            Vector3 push = new Vector3(direction.x, 0f, direction.z).normalized;
            transform.position += push * 0.12f;

            // Умная реакция: отскок в сторону + попытка спрятаться.
            Vector3 dodge = Vector3.Cross(Vector3.up, push) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);
            transform.position += dodge * 0.35f;

            alertness = 1f;
            if (_brain != KiwiBrain.Flee) SetBrain(KiwiBrain.Cover, 2.2f);

            if (OnHurt != null) OnHurt(this);

            if (health <= 0f) Die();
        }

        /// <summary>
        /// Вспышка цвета при попадании (и восстановление цвета после неё).
        /// Через MaterialPropertyBlock: общий материал птицы не трогаем,
        /// копий материалов не создаём.
        /// </summary>
        private void SetTint(Color color)
        {
            if (_renderers == null || _baseColors == null) return;

            // factor = 1 при color = white → цвет возвращается к исходному.
            Color factor = color * 0.35f + new Color(0.65f, 0.65f, 0.65f, 1f);

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;

                Color tinted = _baseColors[i] * factor;

                _propertyBlock.Clear();
                r.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", tinted);   // URP Lit
                _propertyBlock.SetColor("_Color", tinted);       // встроенный шейдер
                r.SetPropertyBlock(_propertyBlock);
            }
        }

        private void Die()
        {
            _dying = true;
            _deathTimer = 0f;
            if (_collider != null) _collider.enabled = false;
            AudioManager.Instance?.PlaySfx(ProceduralAudio.Sfx.KiwiDie, 0.7f, UnityEngine.Random.Range(0.9f, 1.1f));

            Effects.Instance?.KiwiPoof(transform.position + Vector3.up * 0.6f);

            if (sliceDrop > 0)
            {
                Pickup spawned = Pickup.Spawn(new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z), sliceDrop);
                if (spawned != null) spawned.target = target;
            }

            OnDied?.Invoke(this);
        }

        private void UpdateDeath()
        {
            _deathTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_deathTimer / 0.6f);
            transform.Rotate(0f, 0f, 220f * Time.deltaTime);
            transform.position += Vector3.down * 0.9f * Time.deltaTime;
            if (_model != null) _model.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, t);
            if (t >= 1f)
            {
                _dead = true;
                Destroy(gameObject);
            }
        }
    }
}
