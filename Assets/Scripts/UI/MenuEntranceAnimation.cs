using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>
    /// Лёгкая анимация появления меню: плавный fade-in всей панели
    /// и лёгкое «дыхание» заголовка (вверх-вниз).
    ///
    /// В «Киви Ган» главное меню создаётся кодом, поэтому компонент ставится
    /// на корень экрана и заголовок передаётся методом <see cref="SetTitle"/>.
    ///
    /// СОМНЕНИЕ: в проекте уже есть своя анимация появления (кнопки выезжают
    /// по очереди, заголовок проявляется). Этот компонент добавляет общий
    /// fade-in панели и «дыхание» заголовка — они не конфликтуют: один
    /// управляет прозрачностью CanvasGroup, второй — позицией заголовка.
    /// Если покачивание заголовка покажется лишним — просто убери компонент.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class MenuEntranceAnimation : MonoBehaviour
    {
        [SerializeField] private RectTransform title;
        [SerializeField] private float fadeDuration = 0.6f;
        [SerializeField] private float floatDistance = 6f;
        [SerializeField] private float floatSpeed = 1.5f;

        private CanvasGroup _group;
        private Vector2 _titleStart;
        private float _elapsed;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group != null) _group.alpha = 0f;

            if (title != null) _titleStart = title.anchoredPosition;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;

            float duration = Mathf.Max(0.01f, fadeDuration);
            if (_group != null) _group.alpha = Mathf.Clamp01(_elapsed / duration);

            if (title != null)
            {
                float offset = Mathf.Sin(_elapsed * floatSpeed) * floatDistance;
                title.anchoredPosition = _titleStart + Vector2.up * offset;
            }
        }

        /// <summary>Заголовок задаётся кодом (меню создаётся программно).</summary>
        public void SetTitle(RectTransform titleRect)
        {
            title = titleRect;
            if (title != null) _titleStart = title.anchoredPosition;
        }
    }
}
