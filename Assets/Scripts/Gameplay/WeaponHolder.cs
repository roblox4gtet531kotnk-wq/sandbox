using System;
using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Держит текущее оружие: модель, патроны, перезарядку и темп стрельбы.
    /// </summary>
    public class WeaponHolder : MonoBehaviour
    {
        public WeaponSpec Spec;
        public int Ammo;
        public bool Reloading { get; private set; }
        public float ReloadProgress { get; private set; }

        public Action<int, int> OnAmmoChanged;   // текущие, максимум
        public Action OnShot;
        public Action OnReloadStart;
        public Action OnReloadEnd;

        private float _nextFireTime;
        private float _reloadEndTime;
        private Transform _gunAnchor;
        private GameObject _gunModel;

        public Transform Muzzle
        {
            get
            {
                if (_gunModel != null)
                {
                    Transform m = _gunModel.transform.Find("Muzzle");
                    if (m != null) return m;
                }
                return transform;
            }
        }

        public void Setup(WeaponSpec spec, Transform gunAnchor)
        {
            Spec = spec ?? ShopCatalog.WeaponFor("pistol_base");
            _gunAnchor = gunAnchor;
            Ammo = Spec.Magazine;
            Reloading = false;
            ReloadProgress = 0f;

            if (_gunModel != null) Destroy(_gunModel);
            if (_gunAnchor != null)
            {
                _gunModel = WeaponFactory.Build(Spec, _gunAnchor);
                _gunModel.transform.localPosition = Vector3.zero;
                _gunModel.transform.localRotation = Quaternion.identity;
            }
            OnAmmoChanged?.Invoke(Ammo, Spec.Magazine);

            // Связываем оружие с игроком: без этого он не сможет стрелять.
            PlayerController player = GetComponent<PlayerController>();
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (player != null) player.weapon = this;
        }

        public bool CanFire => !Reloading && Ammo > 0 && Time.time >= _nextFireTime;

        public bool TryFire()
        {
            if (Spec == null) return false;
            if (Reloading) return false;
            if (Time.time < _nextFireTime) return false;

            if (Ammo <= 0)
            {
                StartReload();
                return false;
            }

            Ammo--;
            _nextFireTime = Time.time + Spec.FireDelay;
            OnShot?.Invoke();
            OnAmmoChanged?.Invoke(Ammo, Spec.Magazine);
            if (Ammo <= 0) StartReload();
            return true;
        }

        public void StartReload()
        {
            if (Spec == null || Reloading) return;
            if (Ammo >= Spec.Magazine) return;
            Reloading = true;
            ReloadProgress = 0f;
            _reloadEndTime = Time.time + Spec.ReloadTime;
            OnReloadStart?.Invoke();
        }

        private void Update()
        {
            if (!Reloading) return;
            ReloadProgress = Mathf.Clamp01(1f - (_reloadEndTime - Time.time) / Mathf.Max(0.01f, Spec.ReloadTime));
            if (Time.time >= _reloadEndTime)
            {
                Reloading = false;
                ReloadProgress = 0f;
                Ammo = Spec.Magazine;
                OnAmmoChanged?.Invoke(Ammo, Spec.Magazine);
                OnReloadEnd?.Invoke();
            }
        }

        public void AddAmmo(int amount)
        {
            if (Spec == null) return;
            Ammo = Mathf.Clamp(Ammo + amount, 0, Spec.Magazine);
            OnAmmoChanged?.Invoke(Ammo, Spec.Magazine);
        }
    }
}
