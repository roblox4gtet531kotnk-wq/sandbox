using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Лёгкое покачивание объекта вверх-вниз (для диорамы главного меню).
    /// Дешевле и проще, чем отдельный аниматор.
    /// </summary>
    public sealed class FloatBob : MonoBehaviour
    {
        public float amplitude = 0.18f;   // на сколько поднимается
        public float speed = 1.3f;        // рад/сек
        public float phase;               // чтобы объекты не двигались в унисон

        private Vector3 _basePosition;
        private bool _ready;

        private void Awake()
        {
            _basePosition = transform.localPosition;
            _ready = true;
        }

        private void Update()
        {
            if (!_ready) return;
            transform.localPosition = _basePosition
                                      + Vector3.up * (Mathf.Sin(Time.time * speed + phase) * amplitude);
        }
    }
}
