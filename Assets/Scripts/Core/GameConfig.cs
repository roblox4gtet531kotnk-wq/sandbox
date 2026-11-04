using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Единая конфигурация игры: название, версия, движок, платформа, валюта.
    /// Любой экран/система берёт данные отсюда, чтобы конфиг игры был в одном месте.
    /// </summary>
    public static class GameConfig
    {
        public const string GameTitle = "КИВИ ГАН";
        public const string GameTitleLatin = "KIVI GUN";
        public const string Version = "0.0.10";
        public const string Engine = "Unity 7000.12";
        public const string Platform = "Android";
        public const string Company = "Kivi Games";
        public const string BundleId = "com.kivigames.kivigun";
        public const string Currency = "Киви-дольки";

        /// <summary>Количество одиночных уровней.</summary>
        public const int LevelCount = 4;

        /// <summary>Строка, которую видит игрок в углу меню.</summary>
        public static string BuildInfo => GameTitle + "  v" + Version;

        public static string FullInfo => GameTitle + "  v" + Version + "  •  " + Engine + "  •  " + Platform;

        // Разработчики (см. экран «Разработчики»)
        public const string DevDanilName = "Данил";
        public const string DevDanilRole = "Главный разработчик";
        public const string DevDanilLines = "1 000 000 000 строк кода";
        public const string DevDanilFact = "Все баги разбежались. К счастью.";

        public const string DevKolyaName = "Коля";
        public const string DevKolyaRole = "Дизайнер";
        public const string DevKolyaLines = "0 строк кода, 100% вкуса";
        public const string DevKolyaFact = "Рисует киви птиц по 40 часов в сутки.";

        public const string DevAiName = "ИИ";
        public const string DevAiRole = "Основатель";
        public const string DevAiLines = "∞ строк кода";
        public const string DevAiFact = "Основал студию. Сам же и написал игру. И вставил баги.";
    }
}
