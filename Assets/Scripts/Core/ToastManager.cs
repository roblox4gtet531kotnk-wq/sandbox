using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>Всплывающие уведомления: «Куплено!», «Не хватает дольек» и т.п.</summary>
    public class ToastManager : MonoBehaviour
    {
        private static ToastManager _instance;
        private readonly List<string> _queue = new List<string>();
        private RectTransform _root;
        private bool _showing;

        public static ToastManager GetOrCreate()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("Toasts");
            _instance = go.AddComponent<ToastManager>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            Canvas canvas = UIBuilder.CreateCanvas("ToastCanvas", 1500);
            canvas.transform.SetParent(transform, false);
            _root = UIBuilder.Anchor(canvas.transform, "Stack", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-420f, 90f), new Vector2(420f, 90f));
            _root.pivot = new Vector2(0.5f, 0f);
        }

        public void Show(string message)
        {
            _queue.Add(message);
            if (!_showing) StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            _showing = true;
            while (_queue.Count > 0)
            {
                string message = _queue[0];
                _queue.RemoveAt(0);
                yield return ShowOne(message);
            }
            _showing = false;
        }

        private IEnumerator ShowOne(string message)
        {
            RectTransform rt = UIBuilder.Rect(_root, "Toast", new Vector2(0f, 74f), new Vector2(0f, 0f));
            rt.pivot = new Vector2(0.5f, 0f);

            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = Theme.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(Theme.PanelDark.r, Theme.PanelDark.g, Theme.PanelDark.b, 0.95f);
            bg.raycastTarget = false;

            Text label = UIBuilder.Label(rt, message, 26, Theme.Text, TextAnchor.MiddleCenter,
                new Vector2(780f, 60f), Vector2.zero, FontStyle.Bold);

            const float duration = 2.2f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float inT = Mathf.Clamp01(t / 0.22f);
                float outT = Mathf.Clamp01((duration - t) / 0.35f);
                float ease = 1f - Mathf.Pow(1f - inT, 3f);
                rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(-24f, 0f, ease) + (1f - outT) * -10f);
                bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, outT);
                label.color = new Color(Theme.Text.r, Theme.Text.g, Theme.Text.b, outT);
                yield return null;
            }

            Destroy(rt.gameObject);
        }
    }
}
