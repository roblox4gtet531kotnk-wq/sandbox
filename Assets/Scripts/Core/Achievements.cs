using System;
using System.Collections.Generic;
using UnityEngine;

namespace KiviGun
{
    /// <summary>По чему считается прогресс достижения.</summary>
    public enum AchievementKind
    {
        Kills,       // по общему числу убитых киви
        Purchases,   // по числу покупок в магазине
        Hat,         // по факту надевания купленной шапки
        Levels       // по числу пройденных уровней
    }

    /// <summary>Одно достижение.</summary>
    public class Achievement
    {
        public string Id;
        public string Title;
        public string Description;
        public AchievementKind Kind;
        public int Target;

        /// <summary>Иконка создаётся лениво — чтобы не грузить спрайты, пока экран не открыт.</summary>
        public Func<Sprite> Icon;
    }

    /// <summary>
    /// Достижения игрока.
    ///
    /// Сейчас их четыре (по ТЗ):
    ///   • «Убито 10 киви»;
    ///   • «Убито 100 киви»;
    ///   • «Куплен первый предмет в магазине»;
    ///   • «Красаотка утка» — игрок надел купленную шапку на утку.
    ///
    /// Событие OnUnlocked: игра показывает всплывашку и играет звук.
    ///
    /// СОМНЕНИЕ: прогресс убийств берётся из SaveSystem.Data.totalKills — он
    /// общий за всю игру (не за уровень). Если захочешь «за текущий уровень» —
    /// это отдельный счётчик, скажи.
    /// </summary>
    public static class Achievements
    {
        public static readonly Achievement[] List =
        {
            new Achievement
            {
                Id = "kills_10",
                Title = "ПЕРВЫЕ ПЕРЬЯ",
                Description = "Убито 10 киви",
                Kind = AchievementKind.Kills,
                Target = 10,
                Icon = () => IconFactory.Skull()
            },
            new Achievement
            {
                Id = "kills_100",
                Title = "ОХОТНИК ПО ПЕРЬЯМ",
                Description = "Убито 100 киви",
                Kind = AchievementKind.Kills,
                Target = 100,
                Icon = () => IconFactory.Target()
            },
            new Achievement
            {
                Id = "first_purchase",
                Title = "ПЕРВАЯ ПОКУПКА",
                Description = "Куплен первый предмет в магазине",
                Kind = AchievementKind.Purchases,
                Target = 1,
                Icon = () => IconFactory.KiwiSlice()
            },
            new Achievement
            {
                Id = "duck_beauty",
                Title = "КРАСАОТКА УТКА",
                Description = "Надень купленную шапку на утку",
                Kind = AchievementKind.Hat,
                Target = 1,
                Icon = () => IconFactory.HatKiwi()
            }
        };

        /// <summary>Сработало при разблокировке достижения.</summary>
        public static event Action<Achievement> OnUnlocked;

        private static bool _ready;

        /// <summary>Подготовка (вызывается один раз при старте). Проверяет и старые сохранения.</summary>
        public static void Init()
        {
            _ready = true;
            int before = SaveSystem.Data.unlockedAchievements.Count;
            CheckAll();
            // Если при проверке что-то разблокировалось — сразу записываем:
            // Unlock() сам не сохраняет (одна операция сохранения), а тут
            // отдельного «события» нет — значит, сохраняем сами.
            if (SaveSystem.Data.unlockedAchievements.Count != before) SaveSystem.Save();
        }

        // ------------------------------------------------------------------
        //  Шаги БЕЗ сохранения — их вызывает код, который сам сохранит ОДИН раз.
        //  (Покупка: сначала меняем данные, потом считаем разблокировки,
        //   потом один Save() — предмет, счётчик и достижение пишутся вместе.)
        // ------------------------------------------------------------------

        /// <summary>Учитывает убийства киви (без сохранения).</summary>
        public static void AddKills(int count)
        {
            if (count <= 0) return;
            SaveSystem.Data.totalKills += count;
            CheckAll();
        }

        /// <summary>Учитывает покупку (без сохранения).</summary>
        public static void AddPurchase()
        {
            SaveSystem.Data.itemsPurchased++;
            CheckAll();
        }

        /// <summary>Учитывает надевание купленной шапки (без сохранения).</summary>
        public static void AddHatEquipped(string hatId)
        {
            if (string.IsNullOrEmpty(hatId) || hatId == "hat_none") return;

            // «Красаотка утка» — только за КУПЛЕННУЮ шапку. Раньше проверялось
            // только Owns(), но стартовая бесплатная шапка тоже лежит
            // в инвентаре — и достижение выдавалось бы за неё.
            if (!SaveSystem.Purchased(hatId)) return;

            SaveSystem.Data.hatEverEquipped = true;
            CheckAll();
        }

        // ------------------------------------------------------------------
        //  Готовые методы: меняют данные И СОХРАНЯЮТ сами (один раз).
        // ------------------------------------------------------------------

        /// <summary>Сообщить об убийстве киви.</summary>
        public static void ReportKill(int count = 1)
        {
            if (count <= 0) return;
            AddKills(count);
            SaveSystem.Save();
        }

        /// <summary>Сообщить о покупке в магазине.</summary>
        public static void ReportPurchase()
        {
            AddPurchase();
            SaveSystem.Save();
        }

        /// <summary>Сообщить, что игрок надел шапку (учитываются только купленные шапки).</summary>
        public static void ReportHatEquipped(string hatId)
        {
            AddHatEquipped(hatId);
            SaveSystem.Save();
        }

        /// <summary>Сообщить о пройденном уровне.</summary>
        public static void ReportLevelCompleted()
        {
            CheckAll();
        }

        /// <summary>Проверяет все достижения и разблокирует выполненные.</summary>
        public static void CheckAll()
        {
            if (!_ready) return;

            for (int i = 0; i < List.Length; i++)
            {
                Achievement a = List[i];
                if (IsUnlocked(a.Id)) continue;
                if (Progress(a) < a.Target) continue;

                Unlock(a);
            }
        }

        /// <summary>Текущий прогресс по достижению.</summary>
        public static int Progress(Achievement a)
        {
            if (a == null) return 0;

            switch (a.Kind)
            {
                case AchievementKind.Kills: return SaveSystem.Data.totalKills;
                case AchievementKind.Purchases: return SaveSystem.Data.itemsPurchased;
                case AchievementKind.Hat: return SaveSystem.Data.hatEverEquipped ? 1 : 0;
                case AchievementKind.Levels:
                {
                    // Без LINQ — меньше зависимостей и аллокаций на мобильном.
                    int done = 0;
                    bool[] completed = SaveSystem.Data.levelsCompleted;
                    if (completed != null)
                        for (int i = 0; i < completed.Length; i++)
                            if (completed[i]) done++;
                    return done;
                }
                default: return 0;
            }
        }

        public static bool IsUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return SaveSystem.Data.unlockedAchievements.Contains(id);
        }

        public static int UnlockedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < List.Length; i++)
                    if (IsUnlocked(List[i].Id)) n++;
                return n;
            }
        }

        private static void Unlock(Achievement a)
        {
            SaveSystem.Data.unlockedAchievements.Add(a.Id);

            // Сохранения здесь НЕТ: разблокировка попадёт в файл вместе с
            // данными, которые её вызвали (одна операция сохранения).
            Debug.Log("[KiviGun] Достижение получено: " + a.Title + " (" + a.Description + ")");
            AudioManager.Instance?.PlaySfx(ProceduralAudio.Sfx.Win, 0.5f, 1.15f);

            if (OnUnlocked != null) OnUnlocked(a);
        }
    }
}
