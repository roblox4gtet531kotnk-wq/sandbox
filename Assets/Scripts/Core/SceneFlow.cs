using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>Переход между сценами с затемнением.</summary>
    public class SceneFlow : MonoBehaviour
    {
        private static SceneFlow _instance;

        /// <summary>Индекс уровня, который нужно запустить следующим.</summary>
        public static int NextLevelIndex = 0;

        public const string MenuScene = "MainMenu";
        public const string GameScene = "Game";

        private Image _fade;
        private bool _busy;

        public static SceneFlow GetOrCreate()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("SceneFlow");
            _instance = go.AddComponent<SceneFlow>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            Canvas canvas = UIBuilder.CreateCanvas("FadeCanvas", 2000);
            canvas.transform.SetParent(transform, false);
            _fade = UIBuilder.Stretch(canvas.transform, "Fade", 0f).gameObject.AddComponent<Image>();
            _fade.color = new Color(0f, 0f, 0f, 1f);
            _fade.raycastTarget = false;
            _fade.gameObject.SetActive(false);
        }

        public static void GoToMenu()
        {
            LevelRequested = false;
            GetOrCreate().LoadScene(MenuScene);
        }

        public static void GoToLevel(int levelIndex)
        {
            NextLevelIndex = Mathf.Clamp(levelIndex, 0, GameConfig.LevelCount - 1);
            LevelRequested = true;
            GetOrCreate().LoadScene(GameScene);
        }

        public void LoadScene(string sceneName)
        {
            if (_busy) return;
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            _busy = true;
            _fade.gameObject.SetActive(true);
            _fade.color = new Color(0f, 0f, 0f, 0f);

            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                _fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / 0.35f));
                yield return null;
            }

            // Сброс накопленных одноразовых объектов
            Time.timeScale = 1f;

            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                yield return SceneManagerLoad(sceneName);
            }
            else
            {
                Debug.LogWarning("[KiviGun] Сцена '" + sceneName + "' не найдена. Создайте её через меню Kivi Gun ▸ Настроить проект.");
                yield return null;
            }

            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                _fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(1f - t / 0.35f));
                yield return null;
            }

            _fade.gameObject.SetActive(false);
            _busy = false;
        }

        private static IEnumerator SceneManagerLoad(string sceneName)
        {
            UnityEngine.AsyncOperation op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone) yield return null;
        }
    }
}
