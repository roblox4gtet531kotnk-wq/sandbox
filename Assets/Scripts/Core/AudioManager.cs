using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Менеджер аудио: музыка с плавным переходом, звуковые эффекты, громкость из настроек.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;

        public static AudioManager Instance
        {
            get { return _instance; }
        }

        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource _sfx;
        private AudioSource _activeMusic;
        private ProceduralAudio.Style _currentStyle;
        private bool _hasStyle;
        private float _musicScale = 1f;      // множитель громкости музыки (тихая музыка на 4-м уровне)
        private AudioClip _externalClip;     // внешний трек из Resources (если задан) — только для диагностики

        public static AudioManager GetOrCreate()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("AudioManager");
            _instance = go.AddComponent<AudioManager>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _musicA = CreateSource("MusicA", true, 0f);
            _musicB = CreateSource("MusicB", true, 0f);
            _sfx = CreateSource("Sfx", false, 1f);
            _activeMusic = _musicA;

            ApplyVolumes();
        }

        private AudioSource CreateSource(string name, bool loop, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = loop;
            src.volume = volume;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.dopplerLevel = 0f;
            return src;
        }

        private void Update()
        {
            // Плавное переключение громкости музыки
            float targetA = _activeMusic == _musicA ? 1f : 0f;
            float targetB = _activeMusic == _musicB ? 1f : 0f;
            _musicA.volume = Mathf.MoveTowards(_musicA.volume, targetA * MusicVolume * _musicScale, Time.unscaledDeltaTime * 1.2f);
            _musicB.volume = Mathf.MoveTowards(_musicB.volume, targetB * MusicVolume * _musicScale, Time.unscaledDeltaTime * 1.2f);

            // Затухший источник останавливаем: иначе старый трек продолжает
            // играть с нулевой громкостью (и занимает AudioSource).
            // Проверка именно isPlaying: раньше условие было обратным
            // (Stop вызывался только для источника, который и так не играл).
            if (_musicA != _activeMusic && _musicA.isPlaying && _musicA.volume <= 0.001f) _musicA.Stop();
            if (_musicB != _activeMusic && _musicB.isPlaying && _musicB.volume <= 0.001f) _musicB.Stop();
        }

        public float MusicVolume => SaveSystem.Data.musicVolume * SaveSystem.Data.masterVolume;
        public float SfxVolume => SaveSystem.Data.sfxVolume * SaveSystem.Data.masterVolume;

        public void ApplyVolumes()
        {
            if (_sfx != null) _sfx.volume = SfxVolume;
        }

        /// <summary>Переключает музыкальный трек с плавным кроссфейдом.</summary>
        public void PlayMusic(ProceduralAudio.Style style)
        {
            PlayMusic(style, 1f);
        }

        /// <summary>
        /// Переключает музыку с множителем громкости.
        /// volumeScale &lt; 1 — «тихая» музыка (например, на 4-м уровне).
        /// </summary>
        public void PlayMusic(ProceduralAudio.Style style, float volumeScale)
        {
            if (_hasStyle && _currentStyle == style && _activeMusic != null && _activeMusic.isPlaying)
            {
                // Тот же трек — просто меняем тихоту.
                _musicScale = Mathf.Clamp(volumeScale, 0.05f, 1f);
                return;
            }

            AudioClip clip = ProceduralAudio.GetMusic(style);
            if (clip == null) return;

            _musicScale = Mathf.Clamp(volumeScale, 0.05f, 1f);
            _externalClip = null;            // играет процедурный трек, не внешний
            SwitchTo(clip, true);
            _currentStyle = style;
            _hasStyle = true;
        }

        /// <summary>
        /// Внешний трек из Resources (свой файл вместо процедурного).
        /// Если файла нет — просто ничего не делаем, останется текущая музыка.
        /// </summary>
        public bool PlayExternalMusic(string resourcePath, float volumeScale)
        {
            AudioClip clip = ProceduralAudio.GetExternalMusic(resourcePath);
            if (clip == null) return false;

            return PlayMusicClip(clip, volumeScale);
        }

        /// <summary>Множитель громкости музыки (например, «тихая музыка уровня»).</summary>
        public void SetMusicScale(float scale)
        {
            _musicScale = Mathf.Clamp(scale, 0.05f, 1f);
        }

        /// <summary>
        /// Включает произвольный клип как фоновую музыку (через AudioManager,
        /// чтобы работал общий регулятор громкости из настроек).
        /// Используется компонентом LevelMusic.
        /// </summary>
        public bool PlayMusicClip(AudioClip clip, float volumeScale)
        {
            if (clip == null) return false;

            // Проверяем именно то, что играет сейчас, а не «последний внешний клип»:
            // раньше _externalClip оставался заданным после переключения на
            // процедурную музыку, и проверка вводила в заблуждение.
            if (_activeMusic != null && _activeMusic.isPlaying && _activeMusic.clip == clip)
            {
                _musicScale = Mathf.Clamp(volumeScale, 0.05f, 1f);
                return true;
            }

            _musicScale = Mathf.Clamp(volumeScale, 0.05f, 1f);
            _externalClip = clip;
            SwitchTo(clip, false);
            return true;
        }

        /// <summary>
        /// Переключает музыку на другой клип (с кроссфейдом по двум источникам).
        /// procedural = true → это стиль ProceduralAudio (сохраняем _currentStyle),
        /// иначе — внешний файл (стиль сбрасывается).
        /// </summary>
        private void SwitchTo(AudioClip clip, bool procedural)
        {
            if (clip == null) return;

            AudioSource next = (_activeMusic == _musicA) ? _musicB : _musicA;
            next.clip = clip;
            next.volume = 0f;
            next.loop = true;
            next.Play();

            _activeMusic = next;
            _hasStyle = procedural;     // теперь это не процедурный стиль
            if (!procedural) _currentStyle = ProceduralAudio.Style.Menu;
        }

        public void StopMusic()
        {
            if (_musicA != null) _musicA.Stop();
            if (_musicB != null) _musicB.Stop();
        }

        public void PlaySfx(ProceduralAudio.Sfx sfx, float volume = 1f, float pitch = 1f)
        {
            if (_sfx == null) return;
            AudioClip clip = ProceduralAudio.GetSfx(sfx);
            if (clip == null) return;
            _sfx.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            // Громкость источника уже равна SfxVolume (см. ApplyVolumes),
            // поэтому здесь только личный множитель звука — иначе громкость
            // возводится в квадрат и звуки слишком тихие.
            _sfx.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public void PlayUiClick() => PlaySfx(ProceduralAudio.Sfx.UiClick, 0.7f, UnityEngine.Random.Range(0.97f, 1.05f));
        public void PlayUiBack() => PlaySfx(ProceduralAudio.Sfx.UiBack, 0.7f);
    }
}
