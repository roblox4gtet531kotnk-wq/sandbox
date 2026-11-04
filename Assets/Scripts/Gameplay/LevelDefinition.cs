using UnityEngine;

namespace KiviGun
{
    public enum LevelTheme
    {
        City,        // Киви-Сити
        Settlement,  // Киви-Посёлок
        Office,      // Офис Kivi-Microsoft
        Wheatfield   // Пшеничное поле
    }

    /// <summary>Описание уровня одиночной игры.</summary>
    [System.Serializable]
    public class LevelDefinition
    {
        public int Index;
        public string Title;
        public string Subtitle;
        public string Description;
        public LevelTheme Theme;
        public int EnemyCount;
        public float EnemySpeed;
        public float EnemyDamage;
        public int RewardSlices;
        public int ParTimeSeconds;
        public Color ThemeColor;
        public Sprite Preview;
    }

    /// <summary>Все три уровня игры.</summary>
    public static class LevelLibrary
    {
        private static LevelDefinition[] _levels;

        public static LevelDefinition[] All
        {
            get
            {
                if (_levels == null) Build();
                return _levels;
            }
        }

        public static LevelDefinition Get(int index)
        {
            if (index < 0 || index >= All.Length) return All[0];
            return All[index];
        }

        private static void Build()
        {
            _levels = new[]
            {
                new LevelDefinition
                {
                    Index = 0,
                    Title = "КИВИ-СИТИ",
                    Subtitle = "Лёгкий",
                    Description = "Утренний мегаполис киви. Улицы, парк и куча птиц. Разминка для утки.",
                    Theme = LevelTheme.City,
                    EnemyCount = 12,
                    EnemySpeed = 2.1f,
                    EnemyDamage = 8f,
                    RewardSlices = 25,
                    ParTimeSeconds = 120,
                    ThemeColor = new Color32(120, 196, 232, 255)
                },
                new LevelDefinition
                {
                    Index = 1,
                    Title = "КИВИ-ПОСЁЛОК",
                    Subtitle = "Средний",
                    Description = "Деревня за городом: поля, сараи, мельница. Киви здесь злее и быстрее.",
                    Theme = LevelTheme.Settlement,
                    EnemyCount = 18,
                    EnemySpeed = 2.7f,
                    EnemyDamage = 12f,
                    RewardSlices = 45,
                    ParTimeSeconds = 150,
                    ThemeColor = new Color32(140, 198, 32, 255)
                },
                new LevelDefinition
                {
                    Index = 2,
                    Title = "ОФИС KIVI-MICROSOFT",
                    Subtitle = "Сложный",
                    Description = "Штаб-квартира корпорации. Много стекла, света и очень злых киви.",
                    Theme = LevelTheme.Office,
                    EnemyCount = 24,
                    EnemySpeed = 3.2f,
                    EnemyDamage = 16f,
                    RewardSlices = 80,
                    ParTimeSeconds = 180,
                    ThemeColor = new Color32(168, 120, 224, 255)
                },
                new LevelDefinition
                {
                    Index = 3,
                    Title = "ПШЕНИЧНОЕ ПОЛЕ",
                    Subtitle = "Нормальное",
                    Description = "Золотое поле до горизонта, тихая музыка и киви, которые умеют прятаться.",
                    Theme = LevelTheme.Wheatfield,
                    EnemyCount = 20,
                    EnemySpeed = 2.9f,
                    EnemyDamage = 14f,
                    RewardSlices = 65,
                    ParTimeSeconds = 165,
                    ThemeColor = new Color32(226, 182, 72, 255)
                }
            };
        }
    }
}
