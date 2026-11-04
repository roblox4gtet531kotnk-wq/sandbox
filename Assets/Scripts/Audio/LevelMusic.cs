using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Тихая фоновая дорожка уровня.
    ///
    /// ВАЖНО (чтобы не было двух музык сразу):
    ///   если в игре есть AudioManager — дорожка играет ЧЕРЕЗ него (тогда
    ///   работает общий регулятор громкости из настроек, и музыка меню
    ///   корректно сменяется музыкой уровня). Свой AudioSource используется
    ///   только как запасной вариант (если AudioManager в сцене нет).
    ///
    /// Как назначить трек: повесь компонент на объект уровня и впиши клип
    /// в поле Music (Inspector). Без клипа ничего не играет и пишется
    /// предупреждение в консоль.
    ///
    /// В «Киви Ган» 4-й уровень («Пшеничное поле») играет тихо (громкость 0.55)
    /// процедурной балладой; если положить свой файл в
    /// Assets/Resources/Music/Wheatfield — он подхватится автоматически.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class LevelMusic : MonoBehaviour
    {
        [SerializeField] private AudioClip music;
        [SerializeField, Range(0f, 1f)] private float volume = 0.15f;

        private AudioSource _source;
        private bool _viaManager;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.volume = volume;
        }

        private void Start()
        {
            if (music == null)
            {
                Debug.LogWarning("[KiviGun] Музыка уровня не назначена.");
                return;
            }

            // Сначала пробуем общий AudioManager — так громкость из настроек
            // работает для всех дорожек сразу.
            AudioManager manager = AudioManager.Instance;
            if (manager != null)
            {
                _viaManager = manager.PlayMusicClip(music, volume);
            }

            if (!_viaManager)
            {
                _source.clip = music;
                _source.volume = volume;
                _source.Play();
            }
        }

        private void OnDestroy()
        {
            // Свою дорожку глушим — иначе она переживёт сцену.
            if (!_viaManager && _source != null && _source.isPlaying) _source.Stop();
        }

        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            if (_source != null) _source.volume = volume;

            // Если музыка играет через AudioManager — меняем громкость и там,
            // иначе переключатель в настройках её не затронет.
            if (_viaManager && AudioManager.Instance != null)
                AudioManager.Instance.SetMusicScale(volume);
        }

        /// <summary>Назначить трек из кода (например, из GameController).</summary>
        public void SetClip(AudioClip clip)
        {
            music = clip;
        }
    }
}
