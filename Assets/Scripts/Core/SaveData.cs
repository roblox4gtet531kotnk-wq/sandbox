using System;
using System.Collections.Generic;
using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Данные игрока, которые сохраняются между запусками.
    /// Сериализуется в JSON и хранится в PlayerPrefs.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        // ---- Прогресс ----
        public int kiwiSlices = 0;          // киви-дольки (валюта)
        public int totalKills = 0;
        public int totalDeaths = 0;
        public int levelsPlayed = 0;
        public int itemsPurchased = 0;      // сколько раз купили предмет в магазине
        public bool hatEverEquipped = false;// надевали ли купленную шапку (достижение «Красаотка утка»)

        public bool[] levelsUnlocked = new bool[GameConfig.LevelCount];
        public bool[] levelsCompleted = new bool[GameConfig.LevelCount];
        public int[] bestTimes = new int[GameConfig.LevelCount];      // секунды, 0 = нет рекорда
        public int[] starsEarned = new int[GameConfig.LevelCount];    // 0..3

        // ---- Магазин ----
        public List<string> ownedItems = new List<string>();

        /// <summary>
        /// Что игрок КУПИЛ за дольки (не просто «есть в инвентаре»).
        /// Отдельно от ownedItems: стартовая бесплатная шапка тоже лежит
        /// в ownedItems, а достижение «Красаотка утка» нужно за купленную.
        /// </summary>
        public List<string> purchasedItems = new List<string>();

        public List<string> unlockedAchievements = new List<string>();
        public string equippedHat = "hat_none";
        public string equippedWeapon = "pistol_base";

        // ---- Настройки ----
        public float masterVolume = 1f;
        public float musicVolume = 0.7f;
        public float sfxVolume = 1f;
        public float sensitivity = 1.6f;
        public float fov = 70f;
        public int qualityLevel = 1;       // 0 — низкое, 1 — среднее, 2 — высокое
        public bool qualityAuto = true;    // true — качество выбирается само по устройству
        public bool adaptiveFps = true;    // true — лимит кадров подстраивается под реальный FPS
        public bool firstPerson = false;
        public bool screenShake = true;
        public bool showFps = false;
        public bool invertY = false;
        public bool touchControls = true;
        public bool vibration = true;        // вибрация при попадании (мобильные)

        public int LastLevel
        {
            get { return Mathf.Clamp(lastLevel, 0, GameConfig.LevelCount - 1); }
            set { lastLevel = Mathf.Clamp(value, 0, GameConfig.LevelCount - 1); }
        }

        // JsonUtility сериализует только публичные поля и поля с [SerializeField].
        // Без атрибута «последний уровень» никогда бы не сохранялся.
        [SerializeField] private int lastLevel = 0;

        /// <summary>Приводит данные в корректное состояние (после обновления игры или битой JSON).</summary>
        public void Normalize()
        {
            // ВАЖНО: массивы расширяются С СОХРАНЕНИЕМ старых значений.
            // Раньше при смене количества уровней (3 → 4) массивы просто
            // пересоздавались — и игрок терял весь прогресс по уровням.
            // Пересоздаём только когда длина реально не совпадает (чтобы
            // не плодить мусор при каждом сохранении).
            if (levelsUnlocked == null || levelsUnlocked.Length != GameConfig.LevelCount)
                levelsUnlocked = Resize(levelsUnlocked, GameConfig.LevelCount);
            if (levelsCompleted == null || levelsCompleted.Length != GameConfig.LevelCount)
                levelsCompleted = Resize(levelsCompleted, GameConfig.LevelCount);
            if (bestTimes == null || bestTimes.Length != GameConfig.LevelCount)
                bestTimes = Resize(bestTimes, GameConfig.LevelCount);
            if (starsEarned == null || starsEarned.Length != GameConfig.LevelCount)
                starsEarned = Resize(starsEarned, GameConfig.LevelCount);

            if (ownedItems == null)
                ownedItems = new List<string>();
            if (purchasedItems == null)
                purchasedItems = new List<string>();
            if (unlockedAchievements == null)
                unlockedAchievements = new List<string>();

            levelsUnlocked[0] = true;

            if (!ownedItems.Contains("pistol_base")) ownedItems.Add("pistol_base");
            if (!ownedItems.Contains("hat_none")) ownedItems.Add("hat_none");

            if (string.IsNullOrEmpty(equippedWeapon) || !ownedItems.Contains(equippedWeapon))
                equippedWeapon = "pistol_base";
            if (string.IsNullOrEmpty(equippedHat) || !ownedItems.Contains(equippedHat))
                equippedHat = "hat_none";

            masterVolume = Mathf.Clamp01(masterVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            sensitivity = Mathf.Clamp(sensitivity, 0.2f, 5f);
            fov = Mathf.Clamp(fov, 50f, 110f);
            qualityLevel = Mathf.Clamp(qualityLevel, 0, 2);
        }

        /// <summary>Расширяет массив до нужной длины, сохраняя старые значения.</summary>
        private static bool[] Resize(bool[] old, int size)
        {
            var result = new bool[size];
            if (old == null) return result;
            int count = Mathf.Min(old.Length, size);
            for (int i = 0; i < count; i++) result[i] = old[i];
            return result;
        }

        /// <summary>Расширяет массив до нужной длины, сохраняя старые значения.</summary>
        private static int[] Resize(int[] old, int size)
        {
            var result = new int[size];
            if (old == null) return result;
            int count = Mathf.Min(old.Length, size);
            for (int i = 0; i < count; i++) result[i] = old[i];
            return result;
        }
    }
}
