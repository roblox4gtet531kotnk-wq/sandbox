using System;
using UnityEngine;

namespace KiviGun
{
    /// <summary>
    /// Достижения — тонкий фасад над <see cref="Achievements"/>.
    ///
    /// Зачем фасад, если уже есть Achievements:
    ///   • единое хранилище. Все прогресс-данные лежат в SaveData (JSON в PlayerPrefs),
    ///     а НЕ в отдельных ключах PlayerPrefs. Иначе «сбросить прогресс» в настройках
    ///     стирал бы достижения, но не стирал статистику (и наоборот);
    ///   • один источник истины: и экран достижений, и счётчики, и всплывашки
    ///     читают одни и те же числа;
    ///   • этот класс — тонкая обёртка: никакой своей логики прогресса здесь нет.
    ///
    /// Событие <see cref="Unlocked"/> приходит с НАЗВАНИЕМ достижения (строка),
    /// как в ожидаемом API. Реальные определения — в Achievements.List
    /// (там же иконки, описания и цели).
    /// </summary>
    public static class AchievementSystem
    {
        /// <summary>Сработало, когда получено новое достижение (передаётся название).</summary>
        public static event Action<string> Unlocked;

        /// <summary>Сколько киви убито за всю игру.</summary>
        public static int KiwiKills => SaveSystem.Data.totalKills;

        /// <summary>Покупал ли игрок хоть что-то в магазине.</summary>
        public static bool FirstPurchase => SaveSystem.Data.itemsPurchased > 0;

        /// <summary>Надевал ли игрок купленную шапку на утку.</summary>
        public static bool DuckHat => SaveSystem.Data.hatEverEquipped;

        /// <summary>Сколько всего достижений получено (из N).</summary>
        public static int UnlockedCount => Achievements.UnlockedCount;

        /// <summary>Всего достижений в игре.</summary>
        public static int TotalCount => Achievements.List.Length;

        static AchievementSystem()
        {
            // Подписка ровно один раз: событие статическое, живёт между сценами.
            Achievements.OnUnlocked -= ForwardUnlock;
            Achievements.OnUnlocked += ForwardUnlock;
        }

        private static void ForwardUnlock(Achievement a)
        {
            if (a == null) return;
            Unlocked?.Invoke(a.Title);
        }

        /// <summary>Вызывать ровно один раз при смерти каждого киви.</summary>
        public static void RegisterKiwiKill()
        {
            RegisterKiwiKills(1);
        }

        /// <summary>
        /// То же, но пачкой — так удобнее считать разницу «сколько убито сейчас»
        /// (иначе счётчик прыгал бы на +1 каждый кадр).
        /// </summary>
        public static void RegisterKiwiKills(int count)
        {
            if (count <= 0) return;
            Achievements.ReportKill(count);
        }

        /// <summary>
        /// Вызывать после УСПЕШНОЙ покупки, а не после нажатия на кнопку.
        /// (Проверку «хватает ли дольков» делает магазин — здесь только фиксируем факт.)
        ///
        /// ВАЖНО: сохранения здесь нет — его делает магазин (SaveSystem.Purchase),
        /// чтобы предмет, счётчик покупок и разблокировка достижения записались
        /// ОДНИМ сохранением.
        /// </summary>
        public static void RegisterPurchase()
        {
            Achievements.AddPurchase();
        }

        /// <summary>
        /// Вызывать, когда купленная шапка действительно надета на утку.
        /// Проверка «это купленная шапка и она надета на утку» остаётся в магазине
        /// (SaveSystem.Equip) — здесь только фиксируем результат.
        ///
        /// ВАЖНО: сохранения здесь нет — его делает магазин (SaveSystem.Equip).
        /// </summary>
        public static void RegisterDuckHatEquipped()
        {
            Achievements.AddHatEquipped(SaveSystem.Data.equippedHat);
        }
    }
}
