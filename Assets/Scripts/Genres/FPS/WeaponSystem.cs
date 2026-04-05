using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.Genres.FPS
{
    [CreateAssetMenu(menuName = "Forever Engine/FPS/Weapon")]
    public class WeaponData : ScriptableObject
    {
        public string WeaponName;
        public enum WeaponType { Hitscan, Projectile }
        public WeaponType Type;
        public float Damage = 25f;
        public float FireRate = 10f;
        public float Range = 100f;
        public float Spread = 0.02f;
        public int MagazineSize = 30;
        public float ReloadTime = 2f;
        public AnimationCurve RecoilPattern;
        public GameObject ProjectilePrefab;
        public AudioClip FireSound;
    }

    public class WeaponSystem : UnityEngine.MonoBehaviour
    {
        [SerializeField] private List<WeaponData> _weapons = new();
        private int _currentIndex;
        private int _ammoInMag;
        private float _nextFireTime;
        private bool _reloading;

        public WeaponData CurrentWeapon => _currentIndex >= 0 && _currentIndex < _weapons.Count ? _weapons[_currentIndex] : null;
        public int AmmoInMagazine => _ammoInMag;
        public bool IsReloading => _reloading;

        private void Start() { if (CurrentWeapon != null) _ammoInMag = CurrentWeapon.MagazineSize; }

        public bool Fire(Vector3 origin, Vector3 direction, out RaycastHit hit)
        {
            hit = default;
            var weapon = CurrentWeapon;
            if (weapon == null || _reloading || Time.time < _nextFireTime || _ammoInMag <= 0) return false;

            _nextFireTime = Time.time + 1f / weapon.FireRate;
            _ammoInMag--;

            Vector3 spread = Random.insideUnitSphere * weapon.Spread;
            Vector3 dir = (direction + spread).normalized;

            if (weapon.Type == WeaponData.WeaponType.Hitscan)
                return Physics.Raycast(origin, dir, out hit, weapon.Range);
            return false; // Projectile handled separately
        }

        public void Reload()
        {
            if (_reloading || CurrentWeapon == null) return;
            _reloading = true;
            Invoke(nameof(FinishReload), CurrentWeapon.ReloadTime);
        }

        private void FinishReload() { _ammoInMag = CurrentWeapon?.MagazineSize ?? 0; _reloading = false; }

        public void SwitchWeapon(int index)
        {
            if (index >= 0 && index < _weapons.Count) { _currentIndex = index; _ammoInMag = _weapons[index].MagazineSize; _reloading = false; }
        }
    }
}
