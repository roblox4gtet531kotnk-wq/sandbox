using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Управление уткой: движение, прыжок, прицеливание, стрельба, здоровье.
    /// Поддерживает мышь + клавиатуру и тач-контролы для Android.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Движение")]
        public float moveSpeed = 6.2f;
        public float sprintSpeed = 9.4f;
        public float acceleration = 14f;
        public float jumpHeight = 1.5f;
        public float gravity = -22f;
        public float rotationSpeed = 14f;

        [Header("Бой")]
        public float maxHealth = 100f;
        public float health = 100f;
        public float regenDelay = 4.5f;
        public float regenRate = 12f;

        [Header("Камера")]
        public Camera cam;
        public Transform cameraPivot;
        public float thirdPersonDistance = 3.6f;
        public float cameraHeight = 1.55f;
        public float shoulderOffset = 0.55f;

        public WeaponHolder weapon;
        public bool InputEnabled = true;
        public bool Alive { get; private set; } = true;

        public System.Action<float> OnHealthChanged;
        public System.Action OnDied;
        public System.Action OnHitTaken;

        private CharacterController _cc;
        private float _verticalVelocity;
        private float _yaw;
        private float _pitch;
        private float _lastDamageTime;
        private float _recoil;
        private Vector3 _moveInput;
        private bool _touchLook;
        // СЛОТЫ пальцев (не порядковые номера): при отпускании одного пальца
        // порядковые номера остальных сдвигаются, и джойстик с обзором
        // начали бы следить за разными пальцами.
        private int _touchLookSlot = -1;
        private Vector2 _touchLookLast;
        private int _touchMoveSlot = -1;
        private Vector2 _touchMoveOrigin;
        private Vector2 _touchMove;

        public void Setup(Camera camera, Transform pivot)
        {
            cam = camera;
            cameraPivot = pivot;
            _cc = GetComponent<CharacterController>();
            _yaw = transform.eulerAngles.y;
            Alive = true;
            health = maxHealth;
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cam == null) cam = Camera.main;
            health = maxHealth;
        }

        private void Update()
        {
            if (!Alive)
            {
                ApplyGravityOnly();
                return;
            }

            float dt = Time.deltaTime;
            HandleLook(dt);
            HandleMovement(dt);
            HandleTouch();
            HandleShooting();
            HandleHealth(dt);
            UpdateCamera(dt);
        }

        // ---------------- Взгляд ----------------

        private void HandleLook(float dt)
        {
            if (!InputEnabled) return;
            float sens = SaveSystem.Data.sensitivity;

            // Мышь: крутим камеру всегда, пока курсор не над интерфейсом —
            // иначе нельзя было бы целиться, удерживая ЛКМ для стрельбы.
            if (!UIButton.PointerOverUI)
            {
                Vector2 delta = InputHub.MouseDelta;
                ApplyLook(delta.x * 0.12f * sens, delta.y * 0.12f * sens);
            }

            // Тач: перетаскивание по правой половине экрана
            if (_touchLook && _touchLookSlot >= 0)
            {
                Vector2 pos = InputHub.TouchPositionBySlot(_touchLookSlot);
                Vector2 delta = pos - _touchLookLast;
                _touchLookLast = pos;
                ApplyLook(delta.x * 0.16f * sens, delta.y * 0.16f * sens);
            }
        }

        private void ApplyLook(float yawDelta, float pitchDelta)
        {
            _yaw += yawDelta;
            // Инверсия вертикали — переключатель в настройках.
            float pitch = SaveSystem.Data.invertY ? pitchDelta : -pitchDelta;
            _pitch = Mathf.Clamp(_pitch + pitch, -70f, 70f);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitch + _recoil, 0f, 0f);
        }

        // ---------------- Движение ----------------

        private void HandleMovement(float dt)
        {
            float ax = InputHub.AxisX;
            float ay = InputHub.AxisY;

            if (_touchMoveSlot >= 0)
            {
                Vector2 stick = _touchMove;
                ax += stick.x;
                ay += stick.y;
            }

            Vector3 input = new Vector3(ax, 0f, ay);
            if (input.sqrMagnitude > 1f) input.Normalize();
            _moveInput = Vector3.Lerp(_moveInput, input, acceleration * dt * 0.1f);

            bool sprint = InputHub.Key(KeyCode.LeftShift) || InputHub.Key(KeyCode.RightShift);
            float speed = sprint ? sprintSpeed : moveSpeed;

            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 move = (forward * _moveInput.z + right * _moveInput.x) * speed;

            if (_cc.isGrounded)
            {
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
                if (InputHub.KeyDown(KeyCode.Space)) _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            _verticalVelocity += gravity * dt;

            Vector3 velocity = move + Vector3.up * _verticalVelocity;
            _cc.Move(velocity * dt);

            // Плавный поворот к направлению движения
            if (move.sqrMagnitude > 0.4f)
            {
                Quaternion look = Quaternion.LookRotation(new Vector3(move.x, 0f, move.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationSpeed * dt);
                _yaw = transform.eulerAngles.y;
            }
        }

        private void ApplyGravityOnly()
        {
            _verticalVelocity += gravity * Time.deltaTime;
            _cc.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
        }

        // ---------------- Касания ----------------

        private void HandleTouch()
        {
            // Сенсорное управление можно отключить в настройках — тогда играем мышью/гироскопом.
            if (!SaveSystem.Data.touchControls)
            {
                _touchMoveSlot = -1;
                _touchLookSlot = -1;
                _touchLook = false;
                _touchMove = Vector2.zero;
                return;
            }

            int touches = InputHub.TouchCount;
            if (touches == 0)
            {
                _touchMoveSlot = -1;
                _touchLookSlot = -1;
                _touchLook = false;
                _touchMove = Vector2.zero;
                return;
            }

            // Палец, который отпустили, больше не активен — освобождаем слот.
            if (_touchMoveSlot >= 0 && !InputHub.TouchActive(_touchMoveSlot))
            {
                _touchMoveSlot = -1;
                _touchMove = Vector2.zero;
            }
            if (_touchLookSlot >= 0 && !InputHub.TouchActive(_touchLookSlot))
            {
                _touchLookSlot = -1;
                _touchLook = false;
            }

            for (int i = 0; i < touches; i++)
            {
                Vector2 pos = InputHub.TouchPosition(i);
                bool left = pos.x < Screen.width * 0.5f;

                // Касание началось — запоминаем СЛОТ (он стабилен до отпускания).
                if (InputHub.TouchBegan(i))
                {
                    int slot = InputHub.TouchSlotOf(i);
                    if (slot < 0) continue;

                    // Палец на интерфейсе — это не джойстик и не обзор.
                    if (UIButton.IsOverUI(pos)) continue;

                    if (left && _touchMoveSlot < 0)
                    {
                        _touchMoveSlot = slot;
                        _touchMoveOrigin = pos;
                    }
                    else if (!left && _touchLookSlot < 0)
                    {
                        _touchLookSlot = slot;
                        _touchLookLast = pos;
                        _touchLook = true;
                    }
                }
            }

            if (_touchMoveSlot >= 0)
            {
                Vector2 current = InputHub.TouchPositionBySlot(_touchMoveSlot);
                Vector2 delta = current - _touchMoveOrigin;
                _touchMove = Vector2.ClampMagnitude(delta / 70f, 1f);
            }

            if (_touchLookSlot >= 0 && InputHub.TouchEndedBySlot(_touchLookSlot))
            {
                _touchLook = false;
                _touchLookSlot = -1;
            }
        }

        // ---------------- Стрельба ----------------

        private void HandleShooting()
        {
            if (weapon == null || !InputEnabled) return;

            bool wantFire = InputHub.MouseHeld(0);
            if (_touchLookSlot >= 0 && SaveSystem.Data.touchControls) wantFire = true;    // палец на правой половине = стрельба
            if (InputHub.Key(KeyCode.Space)) wantFire = false;   // пробел — прыжок, не стрельба

            if (wantFire && weapon.CanFire)
            {
                if (weapon.TryFire()) DoShot();
            }

            if (InputHub.KeyDown(KeyCode.R)) weapon.StartReload();
        }

        private void DoShot()
        {
            WeaponSpec spec = weapon.Spec;
            if (spec == null) return;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 aimPoint;
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 300f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.root == transform.root)
                {
                    // Попадание в самого себя — ищем следующую цель
                    aimPoint = ray.origin + ray.direction * 300f;
                    if (!FindEnemyHit(ray, out hit)) { /* цель не найдена */ }
                    else aimPoint = hit.point;
                }
                else
                {
                    aimPoint = hit.point;
                }
            }
            else
            {
                aimPoint = ray.origin + ray.direction * 300f;
            }

            // Разброс
            Vector3 dir = (aimPoint - transform.position).normalized;
            dir += Random.insideUnitSphere * (spec.Spread * 0.01f);
            dir.Normalize();

            Vector3 muzzlePos = weapon.Muzzle != null ? weapon.Muzzle.position : transform.position + Vector3.up * 1.3f + transform.forward * 0.6f;

            RaycastHit damageHit;
            Vector3 endPoint = muzzlePos + dir * 300f;
            if (Physics.Raycast(muzzlePos, dir, out damageHit, 300f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (damageHit.collider.transform.root != transform.root)
                {
                    endPoint = damageHit.point;
                    KiwiEnemy enemy = damageHit.collider.GetComponentInParent<KiwiEnemy>();
                    if (enemy != null)
                    {
                        enemy.TakeDamage(spec.Damage, dir, gameObject);
                        Effects.Instance?.ImpactGreen(endPoint, dir);
                        AudioManager.Instance?.PlaySfx(ProceduralAudio.Sfx.KiwiHit, 0.55f, UnityEngine.Random.Range(0.9f, 1.1f));
                    }
                    else
                    {
                        Effects.Instance?.Impact(endPoint, dir);
                        AudioManager.Instance?.PlaySfx(ProceduralAudio.Sfx.Impact, 0.4f);
                    }
                }
            }

            Effects.Instance?.Tracer(muzzlePos, endPoint);
            Effects.Instance?.MuzzleFlash(muzzlePos, dir);
            AudioManager.Instance?.PlaySfx(ProceduralAudio.Sfx.Shoot, 0.75f, UnityEngine.Random.Range(0.94f, 1.06f));

            _recoil += spec.Recoil;
            if (SaveSystem.Data.screenShake)
                Effects.Instance?.Shake(spec.Recoil * 0.12f);
        }

        private bool FindEnemyHit(Ray ray, out RaycastHit hit)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            hit = default(RaycastHit);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.transform.root == transform.root) continue;
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    hit = hits[i];
                    found = true;
                }
            }
            return found;
        }

        // ---------------- Здоровье ----------------

        private void HandleHealth(float dt)
        {
            if (Time.time - _lastDamageTime > regenDelay && health < maxHealth)
            {
                health = Mathf.Min(maxHealth, health + regenRate * dt);
                OnHealthChanged?.Invoke(health);
            }
        }

        public void TakeDamage(float amount, Vector3 sourcePosition)
        {
            if (!Alive) return;
            health = Mathf.Max(0f, health - amount);
            _lastDamageTime = Time.time;
            OnHealthChanged?.Invoke(health);
            OnHitTaken?.Invoke();
            AudioManager.Instance?.PlaySfx(ProceduralAudio.Sfx.Hurt, 0.8f);

            // Вибрация телефона (в настройках можно отключить).
            if (SaveSystem.Data.vibration && Application.isMobilePlatform)
                Handheld.Vibrate();

            if (SaveSystem.Data.screenShake)
                Effects.Instance?.Shake(0.35f);

            if (health <= 0f)
            {
                Alive = false;
                OnDied?.Invoke();
            }
        }

        // ---------------- Камера ----------------

        private void UpdateCamera(float dt)
        {
            if (cam == null || cameraPivot == null) return;

            _recoil = Mathf.Lerp(_recoil, 0f, dt * 9f);

            bool first = SaveSystem.Data.firstPerson;
            Vector3 localPos = first
                ? new Vector3(0f, 0.02f, 0.25f)
                : new Vector3(shoulderOffset, 0.22f, -thirdPersonDistance);
            cam.transform.localPosition = Vector3.Lerp(cam.transform.localPosition, localPos, 12f * dt);

            // Не даём камере уйти в стены
            if (!first)
            {
                Vector3 origin = cameraPivot.position;
                Vector3 target = cam.transform.position;
                Vector3 dir = target - origin;
                float dist = dir.magnitude;
                RaycastHit hit;
                if (dist > 0.05f && Physics.Raycast(origin, dir.normalized, out hit, dist, ~0, QueryTriggerInteraction.Ignore))
                {
                    cam.transform.position = origin + dir.normalized * Mathf.Max(0.4f, hit.distance - 0.25f);
                }
            }
        }
    }
}
