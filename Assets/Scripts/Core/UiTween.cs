using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Простые анимации интерфейса: пульсация, покачивание, наклон.
    /// Сделано кодом (без Animator), чтобы не тянуть анимационные клипы в проект.
    ///
    /// СОМНЕНИЕ: анимация масштаба конфликтует с подсветкой кнопок UIButton
    /// (она сама меняет масштаб при наведении). Поэтому Pulse/Bob лучше вешать
    /// на элементы, которые не являются UIButton (заголовок, иконки, картинки).
    /// </summary>
    public sealed class UiTween : MonoBehaviour
    {
        public enum Mode
        {
            Pulse,   // ровное увеличение-уменьшение
            Bob,     // покачивание вверх-вниз
            Swing,   // покачивание влево-вправо
            Flicker  // хаотичное «дрожание» масштаба — эффект старого хоррор-заголовка
        }

        public Mode mode = Mode.Pulse;
        public float amplitude = 0.06f;   // для Pulse — доля от базового масштаба
        public float speed = 2.2f;        // рад/сек
        public float phase;                // сдвиг фазы, чтобы элементы не пульсировали в унисон
        public bool randomPhase = true;

        private RectTransform _rect;
        private Transform _transform;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _basePosition;
        private float _time;

        private void Awake()
        {
            _rect = transform as RectTransform;
            _transform = transform;
            if (_rect != null) _baseScale = _rect.localScale;
            _basePosition = _transform.localPosition;

            if (randomPhase) phase = Random.Range(0f, 6.28f);
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime * speed;
            float wave = Mathf.Sin(_time + phase);

            switch (mode)
            {
                case Mode.Pulse:
                    if (_rect != null)
                        _rect.localScale = _baseScale * (1f + wave * amplitude);
                    break;

                case Mode.Bob:
                    _transform.localPosition = _basePosition + Vector3.up * (wave * amplitude);
                    break;

                case Mode.Swing:
                    _transform.localPosition = _basePosition + Vector3.right * (wave * amplitude);
                    break;
            }
        }
    }
}
