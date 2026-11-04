using System;
using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>Меню паузы: продолжить, настройки, заново, в меню.</summary>
    public class PauseMenuController : MonoBehaviour, IScreen
    {
        private static PauseMenuController _current;

        private RectTransform _root;

        public static PauseMenuController Create(Transform parent, Action onResume, Action onRestart, Action onMenu, Action onSettings)
        {
            Close();
            RectTransform rt = UIBuilder.Stretch(parent, "PauseMenu", 0f);
            var screen = rt.gameObject.AddComponent<PauseMenuController>();
            _current = screen;
            screen.Build(rt, onResume, onRestart, onMenu, onSettings);
            return screen;
        }

        public static void Close()
        {
            if (_current != null)
            {
                _current.Dispose();
                _current = null;
            }
        }

        private void Build(RectTransform root, Action onResume, Action onRestart, Action onMenu, Action onSettings)
        {
            _root = root;

            Image bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.05f, 0.06f, 0.78f);
            bg.raycastTarget = false;

            UIBuilder.Label(root, "ПАУЗА", 72, Theme.Text, TextAnchor.MiddleCenter, new Vector2(900f, 100f), new Vector2(0f, 320f), FontStyle.Bold);

            UIBuilder.Button(root, "Продолжить", new Vector2(520f, 84f), new Vector2(0f, 170f), () =>
            {
                AudioManager.Instance?.PlayUiBack();
                onResume?.Invoke();
            }, null, Theme.Accent);

            UIBuilder.Button(root, "Настройки", new Vector2(520f, 84f), new Vector2(0f, 60f), () =>
            {
                onSettings?.Invoke();
            });

            UIBuilder.Button(root, "Начать уровень заново", new Vector2(520f, 84f), new Vector2(0f, -50f), () =>
            {
                AudioManager.Instance?.PlayUiBack();
                onRestart?.Invoke();
            });

            UIBuilder.Button(root, "В главное меню", new Vector2(520f, 84f), new Vector2(0f, -160f), () =>
            {
                AudioManager.Instance?.PlayUiBack();
                onMenu?.Invoke();
            });

            UIBuilder.Label(root, GameConfig.FullInfo, 20, Theme.TextDim, TextAnchor.MiddleCenter,
                new Vector2(1000f, 40f), new Vector2(0f, -420f));
        }

        public void Dispose()
        {
            if (_root != null) Destroy(_root.gameObject);
        }
    }
}
