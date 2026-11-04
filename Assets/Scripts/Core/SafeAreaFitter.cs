using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Подгоняет контейнер интерфейса под «безопасную зону» экрана:
    /// вырез под камеру (чёлка), системные панели, «островки» на Android 9–16.
    ///
    /// Родитель контейнера (канвас) занимает весь экран, поэтому достаточно
    /// пересчитать anchors по safeArea.
    ///
    /// СОМНЕНИЕ: на Android 15/16 при targetSdk 35+ система включает edge-to-edge,
    /// и Screen.safeArea может возвращать весь экран (без вычета панели навигации).
    /// Тогда интерфейс уедет под жестовую полосу. Это надо проверить на реальных
    /// устройствах с разными режимами навигации (3 кнопки / жесты / fullscreen).
    /// </summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _applied = new Rect(-1f, -1f, -1f, -1f);
        private Vector2Int _screenSize;

        private void Awake()
        {
            _rect = transform as RectTransform;
            Apply(true);
        }

        private void Update()
        {
            // Реагируем и на изменение safeArea, и на изменение размера экрана
            // (поворот, смена разрешения) — даже если числа safeArea совпали.
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != _applied || size != _screenSize)
                Apply(true);
        }

        /// <summary>Принудительно применить safe area (после смены ориентации и т.п.).</summary>
        public void ApplyNow()
        {
            Apply(true);
        }

        private void Apply(bool force)
        {
            if (_rect == null) _rect = transform as RectTransform;
            if (_rect == null) return;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = Screen.safeArea;
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);

            if (!force && safe == _applied && size == _screenSize) return;

            // Считаем anchors каждый раз — так контейнер корректно возвращается
            // к полному экрану, если safe area снова стал равным экрану.
            _rect.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
            _rect.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;

            _applied = safe;
            _screenSize = size;
        }
    }
}
