using UnityEngine;

namespace KiviGun
{
    /// <summary>Хранилище прогресса игрока: сохранение, загрузка, покупки, разблокировка уровней.</summary>
    public static class SaveSystem
    {
        private const string SaveKey = "kivi_gun_save_v1";

        private static SaveData _data;

        public static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        public static void Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            SaveData parsed = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { parsed = JsonUtility.FromJson<SaveData>(json); }
                catch (System.Exception e) { Debug.LogWarning("[KiviGun] Сохранение повреждено, начинаем заново: " + e.Message); }
            }
            _data = parsed ?? new SaveData();

            // Миграция старых сохранений: полей qualityAuto/adaptiveFps и полей
            // достижений в них не было. JsonUtility при отсутствующем поле
            // оставляет значение по умолчанию, но на это лучше не полагаться —
            // проверяем по сырому JSON.
            if (!string.IsNullOrEmpty(json))
            {
                if (!json.Contains("\"qualityAuto\"")) _data.qualityAuto = true;
                if (!json.Contains("\"adaptiveFps\"")) _data.adaptiveFps = true;
                if (!json.Contains("\"unlockedAchievements\"")) _data.unlockedAchievements = new System.Collections.Generic.List<string>();
                if (!json.Contains("\"purchasedItems\""))
                {
                    // Старые сохранения: покупки не отмечались отдельно.
                    // Считаем купленным всё, кроме стартовых бесплатных предметов,
                    // иначе достижение «Красаотка утка» нельзя было бы получить
                    // за шапку, купленную до обновления.
                    _data.purchasedItems = new System.Collections.Generic.List<string>();
                    if (_data.ownedItems != null)
                    {
                        for (int i = 0; i < _data.ownedItems.Count; i++)
                        {
                            string id = _data.ownedItems[i];
                            // Сверяемся с каталогом, а не с захардкоженным списком:
                            // бесплатным считается любой предмет с ценой 0
                            // (сейчас это только pistol_base и hat_none —
                            //  стартовые). Неизвестные предметы пропускаем.
                            ShopItem item = ShopCatalog.Get(id);
                            if (item == null) continue;
                            if (item.Price <= 0) continue;
                            if (!_data.purchasedItems.Contains(id)) _data.purchasedItems.Add(id);
                        }
                    }
                }
                if (!json.Contains("\"itemsPurchased\""))
                {
                    // Старые сохранения не считали покупки. Восстанавливаем счётчик
                    // по купленным предметам, иначе игрок с уже купленным предметом
                    // никогда не получил бы достижение «Первая покупка».
                    _data.itemsPurchased = _data.purchasedItems != null ? _data.purchasedItems.Count : 0;
                }
                if (!json.Contains("\"hatEverEquipped\""))
                {
                    // Старые сохранения: если игрок уже носил купленную шапку —
                    // достижение «Красаотка утка» считаем полученным.
                    _data.hatEverEquipped = _data.equippedHat != null &&
                                            _data.equippedHat != "hat_none" &&
                                            _data.purchasedItems != null &&
                                            _data.purchasedItems.Contains(_data.equippedHat);
                }
            }

            _data.Normalize();

            // Миграция уровней: игрок, который прошёл 3-й уровень ДО появления
            // 4-го, должен получить к нему доступ (иначе уровень откроется
            // только после ещё одного прохождения третьего).
            bool migrated = false;
            if (GameConfig.LevelCount > 3 &&
                _data.levelsCompleted.Length > 2 && _data.levelsCompleted[2] &&
                _data.levelsUnlocked.Length > 3 && !_data.levelsUnlocked[3])
            {
                _data.levelsUnlocked[3] = true;
                migrated = true;
                Debug.Log("[KiviGun] Сохранение обновлено: открыт 4-й уровень «Пшеничное поле».");
            }

            // Миграцию сразу записываем, чтобы не повторять её каждый запуск.
            if (migrated) Save();
        }

        public static void Save()
        {
            if (_data == null) return;
            _data.Normalize();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
        }

        // ---------------- Валюта ----------------

        public static int Slices => Data.kiwiSlices;

        public static void AddSlices(int amount)
        {
            if (amount == 0) return;
            Data.kiwiSlices = Mathf.Max(0, Data.kiwiSlices + amount);
            Save();
        }

        public static bool CanAfford(int price) => Data.kiwiSlices >= price;

        // ---------------- Уровни ----------------

        public static bool IsLevelUnlocked(int index)
        {
            if (index <= 0) return true;
            if (index >= GameConfig.LevelCount) return false;
            return Data.levelsUnlocked[index];
        }

        public static void UnlockLevel(int index)
        {
            if (index < 0 || index >= GameConfig.LevelCount) return;
            Data.levelsUnlocked[index] = true;
            Save();
        }

        public static bool IsLevelCompleted(int index)
        {
            if (index < 0 || index >= GameConfig.LevelCount) return false;
            return Data.levelsCompleted[index];
        }

        /// <summary>Отмечает уровень пройденным, обновляет рекорд и звёзды.</summary>
        public static void CompleteLevel(int index, float timeSeconds, int stars)
        {
            if (index < 0 || index >= GameConfig.LevelCount) return;
            Data.levelsCompleted[index] = true;
            int secs = Mathf.Max(0, Mathf.RoundToInt(timeSeconds));
            if (Data.bestTimes[index] == 0 || secs < Data.bestTimes[index])
                Data.bestTimes[index] = secs;
            if (stars > Data.starsEarned[index])
                Data.starsEarned[index] = Mathf.Clamp(stars, 0, 3);
            if (index + 1 < GameConfig.LevelCount)
                Data.levelsUnlocked[index + 1] = true;
            Data.levelsPlayed++;
            // Проверка достижений — до сохранения, чтобы разблокировка
            // (например, «пройдено N уровней») ушла в файл вместе с уровнем.
            Achievements.ReportLevelCompleted();
            Save();
        }

        public static int Stars(int index)
        {
            if (index < 0 || index >= GameConfig.LevelCount) return 0;
            return Data.starsEarned[index];
        }

        // ---------------- Магазин ----------------

        public static bool Owns(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            return Data.ownedItems.Contains(itemId);
        }

        /// <summary>
        /// Покупает предмет по ЦЕНЕ ИЗ КАТАЛОГА.
        /// Возвращает false, если предмета нет, не хватает дольков или он уже куплен.
        /// </summary>
        public static bool Purchase(string itemId)
        {
            ShopItem item = ShopCatalog.Get(itemId);
            if (item == null)
            {
                Debug.LogWarning("[KiviGun] Покупка неизвестного предмета: " + itemId);
                return false;
            }
            return Purchase(itemId, item.Price);
        }

        /// <summary>
        /// Покупает предмет. Возвращает false, если не хватает дольек или уже куплено.
        ///
        /// Цена сверяется с каталогом: вызывающий код не должен указывать свою.
        /// </summary>
        public static bool Purchase(string itemId, int price)
        {
            // Защита от кривых данных: пустой id или отрицательная цена
            // (при отрицательной цене покупка РАЗДАВАЛА бы дольки).
            if (string.IsNullOrEmpty(itemId)) return false;
            if (price < 0) return false;

            ShopItem item = ShopCatalog.Get(itemId);
            if (item == null)
            {
                Debug.LogWarning("[KiviGun] Покупка неизвестного предмета: " + itemId);
                return false;
            }
            if (item.Price != price)
            {
                Debug.LogWarning("[KiviGun] Цена предмета " + itemId + " не совпадает с каталогом (" +
                                 price + " вместо " + item.Price + ") — берём цену из каталога.");
                price = item.Price;
            }

            if (Owns(itemId)) return false;
            if (!CanAfford(price)) return false;

            Data.kiwiSlices -= price;
            Data.ownedItems.Add(itemId);

            if (Data.purchasedItems == null) Data.purchasedItems = new System.Collections.Generic.List<string>();
            // Бесплатные предметы (цена 0) в «купленных за дольки» не идут.
            if (price > 0 && !Data.purchasedItems.Contains(itemId)) Data.purchasedItems.Add(itemId);

            // Достижение «Куплен первый предмет в магазине».
            // Считаем ДО сохранения: предмет, счётчик покупок и разблокировка
            // достижения запишутся ОДНИМ Save() ниже.
            AchievementSystem.RegisterPurchase();

            Save();
            return true;
        }

        /// <summary>Куплен ли предмет именно за дольки (а не просто «есть в инвентаре»).</summary>
        public static bool Purchased(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            if (Data.purchasedItems == null) return false;
            return Data.purchasedItems.Contains(itemId);
        }

        public static void Equip(string itemId)
        {
            if (!Owns(itemId)) return;
            if (ShopCatalog.IsHat(itemId))
            {
                Data.equippedHat = itemId;
                // Достижение «Красаотка утка» — только за купленную шапку.
                // Проверка «куплена и надета на утку» остаётся здесь,
                // AchievementSystem только фиксирует результат.
                AchievementSystem.RegisterDuckHatEquipped();
            }
            else if (ShopCatalog.IsPistol(itemId))
            {
                Data.equippedWeapon = itemId;
            }
            Save();
        }

        public static string EquippedHat => Data.equippedHat;
        public static string EquippedWeapon => Data.equippedWeapon;

        // ---------------- Сброс ----------------

        public static void ResetProgress()
        {
            _data = new SaveData();
            _data.Normalize();
            Save();
        }
    }
}
