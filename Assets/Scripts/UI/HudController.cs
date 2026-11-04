using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>Игровой интерфейс: здоровье, патроны, прицел, задача, баннеры урона.</summary>
    public class HudController : MonoBehaviour
    {
        private GameController _game;
        private RectTransform _root;
        private Image _healthFill;
        private Text _healthText;
        private Text _ammoText;
        private Text _objectiveText;
        private Text _killsText;
        private Text _bannerTitle;
        private Text _bannerSub;
        private RectTransform _banner;
        private Image _damageFlash;
        private RectTransform _hitMarker;
        private Text _fpsText;
        private RectTransform[] _crosshairLines;
        private float _hitMarkerTime;
        private float _damageTime;
        private float _bannerTime;
        private float _ammoFlash;

        public static HudController Create(Transform parent, GameController game)
        {
            RectTransform rt = UIBuilder.Stretch(parent, "HUD", 0f);
            var hud = rt.gameObject.AddComponent<HudController>();
            hud._game = game;
            hud.Build(rt);
            return hud;
        }

        private void Build(RectTransform root)
        {
            _root = root;

            // ---- Прицел ----
            _crosshairLines = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 size = (i < 2) ? new Vector2(4f, 14f) : new Vector2(14f, 4f);
                Vector2 pos = (i == 0) ? new Vector2(0f, 16f) : (i == 1) ? new Vector2(0f, -16f)
                            : (i == 2) ? new Vector2(16f, 0f) : new Vector2(-16f, 0f);
                _crosshairLines[i] = UIBuilder.Rect(root, "Cross", size, pos);
                var img = _crosshairLines[i].gameObject.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.85f);
                img.raycastTarget = false;
            }

            // ---- Маркер попадания ----
            _hitMarker = UIBuilder.Rect(root, "HitMarker", new Vector2(34f, 34f), Vector2.zero);
            var markerImage = _hitMarker.gameObject.AddComponent<Image>();
            markerImage.sprite = IconFactory.Target();
            markerImage.color = new Color(1f, 0.85f, 0.3f, 0f);
            markerImage.raycastTarget = false;

            // ---- Здоровье ----
            RectTransform healthPanel = UIBuilder.Rect(root, "HealthPanel", new Vector2(420f, 84f), new Vector2(-660f, -450f));
            UIBuilder.Panel(healthPanel, "Bg", new Vector2(420f, 84f), Vector2.zero, new Color(0f, 0f, 0f, 0.45f));
            _healthFill = UIBuilder.Panel(healthPanel, "Fill", new Vector2(300f, 40f), new Vector2(-56f, 0f), Theme.Accent);
            _healthText = UIBuilder.Label(healthPanel, "100", 26, Theme.Text, TextAnchor.MiddleCenter, new Vector2(80f, 50f), new Vector2(146f, 0f), FontStyle.Bold);

            // ---- Патроны ----
            _ammoText = UIBuilder.Label(root, "14", 54, Theme.Text, TextAnchor.LowerRight, new Vector2(320f, 90f), new Vector2(720f, -430f), FontStyle.Bold);
            UIBuilder.Label(root, "/", 30, Theme.TextDim, TextAnchor.LowerRight, new Vector2(60f, 50f), new Vector2(600f, -466f));

            // ---- Задача ----
            RectTransform objectivePanel = UIBuilder.Rect(root, "ObjectivePanel", new Vector2(760f, 70f), new Vector2(0f, 460f));
            UIBuilder.Panel(objectivePanel, "Bg", new Vector2(760f, 70f), Vector2.zero, new Color(0f, 0f, 0f, 0.35f));
            _objectiveText = UIBuilder.Label(objectivePanel, "Уничтожьте киви-птиц", 26, Theme.Text, TextAnchor.MiddleCenter, new Vector2(720f, 50f), Vector2.zero, FontStyle.Bold);
            _killsText = UIBuilder.Label(objectivePanel, "0", 26, Theme.AccentLight, TextAnchor.MiddleLeft, new Vector2(120f, 50f), new Vector2(-300f, 0f), FontStyle.Bold);

            // ---- Баннер уровня ----
            _banner = UIBuilder.Rect(root, "Banner", new Vector2(1200f, 220f), new Vector2(0f, 140f));
            _bannerTitle = UIBuilder.Label(_banner, "", 74, Theme.Text, TextAnchor.MiddleCenter, new Vector2(1200f, 100f), new Vector2(0f, 30f), FontStyle.Bold);
            _bannerSub = UIBuilder.Label(_banner, "", 32, Theme.AccentLight, TextAnchor.MiddleCenter, new Vector2(1200f, 50f), new Vector2(0f, -50f));

            // ---- Урон (виньетка) ----
            _damageFlash = UIBuilder.Stretch(root, "DamageFlash", 0f).gameObject.AddComponent<Image>();
            _damageFlash.sprite = Theme.SoftCircle;
            _damageFlash.color = new Color(0.9f, 0.15f, 0.12f, 0f);
            _damageFlash.raycastTarget = false;
            _damageFlash.transform.SetAsFirstSibling();

            // ---- Кнопка паузы ----
            UIBuilder.IconButton(root, IconFactory.Gear(96), new Vector2(64f, 64f), new Vector2(880f, 470f), () =>
            {
                if (_game != null) _game.TogglePause();
            });

            // ---- FPS ----
            _fpsText = UIBuilder.Label(root, "", 22, Theme.TextDim, TextAnchor.UpperLeft, new Vector2(240f, 40f), new Vector2(-880f, 470f));
            _fpsText.gameObject.SetActive(SaveSystem.Data.showFps);
        }

        public void SetHealth(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            if (_healthFill != null)
            {
                float width = 300f * normalized;
                _healthFill.rectTransform.sizeDelta = new Vector2(width, 40f);
                _healthFill.rectTransform.anchoredPosition = new Vector2(-56f - (300f - width) * 0.5f, 0f);
                _healthFill.color = normalized > 0.55f ? Theme.Accent : (normalized > 0.25f ? Theme.Gold : Theme.Danger);
            }
            if (_healthText != null) _healthText.text = Mathf.RoundToInt(normalized * 100f).ToString();
        }

        public void SetAmmo(int current, int max)
        {
            if (_ammoText == null) return;
            _ammoText.text = current.ToString();
            _ammoText.color = current <= 0 ? Theme.Danger : Theme.Text;
        }

        public void SetObjective(int killed, int total)
        {
            if (_objectiveText != null) _objectiveText.text = "Уничтожьте киви-птиц";
            if (_killsText != null) _killsText.text = killed + " / " + total;
        }

        public void ShowBanner(string title, string subtitle)
        {
            if (_bannerTitle != null) _bannerTitle.text = title;
            if (_bannerSub != null) _bannerSub.text = subtitle;
            _bannerTime = 3.2f;
        }

        public void FlashDamage()
        {
            _damageTime = 0.45f;
        }

        public void ShowHitMarker()
        {
            _hitMarkerTime = 0.18f;
        }

        private void Update()
        {
            // Прицел
            if (_crosshairLines != null)
            {
                float spread = 14f;
                PlayerController player = _game != null ? _game.Player : null;
                if (player != null && player.weapon != null && player.weapon.Spec != null)
                {
                    spread = 10f + player.weapon.Spec.Spread * 3.2f;
                    if (InputHub.Key(KeyCode.LeftShift)) spread *= 1.5f;
                }
                _crosshairLines[0].anchoredPosition = new Vector2(0f, spread);
                _crosshairLines[1].anchoredPosition = new Vector2(0f, -spread);
                _crosshairLines[2].anchoredPosition = new Vector2(spread, 0f);
                _crosshairLines[3].anchoredPosition = new Vector2(-spread, 0f);
            }

            // Патроны
            PlayerController p = _game != null ? _game.Player : null;
            if (p != null && p.weapon != null)
            {
                SetAmmo(p.weapon.Ammo, p.weapon.Spec != null ? p.weapon.Spec.Magazine : 0);
            }

            // Счётчик убийств
            if (_game != null && _game.Director != null && _killsText != null)
            {
                _killsText.text = _game.Director.Killed + " / " + _game.Director.TotalToSpawn;
            }

            // Урон
            if (_damageFlash != null && _damageTime > 0f)
            {
                _damageTime -= Time.deltaTime;
                float a = Mathf.Clamp01(_damageTime / 0.45f) * 0.42f;
                _damageFlash.color = new Color(0.9f, 0.15f, 0.12f, a);
            }
            else if (_damageFlash != null)
            {
                _damageFlash.color = new Color(0.9f, 0.15f, 0.12f, 0f);
            }

            // Маркер попадания
            if (_hitMarker != null)
            {
                var markerImage = _hitMarker.GetComponent<Image>();
                if (markerImage != null && _hitMarkerTime > 0f)
                {
                    _hitMarkerTime -= Time.deltaTime;
                    markerImage.color = new Color(1f, 0.85f, 0.3f, Mathf.Clamp01(_hitMarkerTime / 0.18f));
                    _hitMarker.localScale = Vector3.one * (1.4f - Mathf.Clamp01(_hitMarkerTime / 0.18f) * 0.4f);
                }
                else if (markerImage.color.a > 0f)
                {
                    markerImage.color = new Color(1f, 0.85f, 0.3f, 0f);
                }
            }

            // Баннер
            if (_banner != null && _bannerTime > 0f)
            {
                _bannerTime -= Time.deltaTime;
                float a = Mathf.Clamp01(_bannerTime / 0.6f);
                CanvasGroup group = _banner.GetComponent<CanvasGroup>();
                if (group == null) group = _banner.gameObject.AddComponent<CanvasGroup>();
                group.alpha = Mathf.Clamp01(a * 1.4f);
                _banner.anchoredPosition = new Vector2(0f, 140f + (1f - a) * 30f);
            }
            else if (_banner != null)
            {
                CanvasGroup group = _banner.GetComponent<CanvasGroup>();
                if (group == null) group = _banner.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
            }

            // FPS
            if (_fpsText != null)
            {
                bool show = SaveSystem.Data.showFps;
                if (_fpsText.gameObject.activeSelf != show) _fpsText.gameObject.SetActive(show);
                if (show)
                    _fpsText.text = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)) + " FPS";
            }
        }
    }
}
