using System.Collections.Generic;
using UnityEngine;

namespace KiviGun
{
    public enum ItemKind
    {
        Pistol,
        Hat
    }

    /// <summary>Параметры оружия.</summary>
    [System.Serializable]
    public class WeaponSpec
    {
        public string Id;
        public string Title;
        public string Description;
        public int Price;
        public int Damage = 18;
        public float FireDelay = 0.22f;
        public int Magazine = 14;
        public int Reserve = -1;          // -1 = бесконечный
        public float Spread = 1.1f;
        public float Recoil = 1.4f;
        public float ReloadTime = 0.9f;
        public bool Automatic = true;
        public bool Revolver;
        public Color BodyColor = new Color32(58, 66, 74, 255);
        public Color AccentColor = new Color32(140, 198, 32, 255);
    }

    /// <summary>Предмет магазина: пистолет или шапка.</summary>
    public class ShopItem
    {
        public string Id;
        public ItemKind Kind;
        public string Title;
        public string Description;
        public int Price;
        public Sprite Icon;
        public WeaponSpec Weapon;
    }

    /// <summary>Каталог магазина: пистолеты и шапки за киви-дольки.</summary>
    public static class ShopCatalog
    {
        private static List<ShopItem> _items;

        public static List<ShopItem> Items
        {
            get
            {
                if (_items == null) Build();
                return _items;
            }
        }

        public static List<ShopItem> Pistols
        {
            get
            {
                var list = new List<ShopItem>();
                foreach (ShopItem item in Items) if (item.Kind == ItemKind.Pistol) list.Add(item);
                return list;
            }
        }

        public static List<ShopItem> Hats
        {
            get
            {
                var list = new List<ShopItem>();
                foreach (ShopItem item in Items) if (item.Kind == ItemKind.Hat) list.Add(item);
                return list;
            }
        }

        public static ShopItem Get(string id)
        {
            foreach (ShopItem item in Items) if (item.Id == id) return item;
            return null;
        }

        public static bool IsHat(string id)
        {
            ShopItem item = Get(id);
            return item != null && item.Kind == ItemKind.Hat;
        }

        public static bool IsPistol(string id)
        {
            ShopItem item = Get(id);
            return item != null && item.Kind == ItemKind.Pistol;
        }

        public static WeaponSpec WeaponFor(string id)
        {
            ShopItem item = Get(id);
            return item != null ? item.Weapon : null;
        }

        private static void Build()
        {
            _items = new List<ShopItem>();

            // ---- Пистолеты ----
            _items.Add(new ShopItem
            {
                Id = "pistol_base",
                Kind = ItemKind.Pistol,
                Title = "Утиный пистолет",
                Description = "Стандартное оружие. Надёжный, быстрый, всегда с тобой.",
                Price = 0,
                Icon = IconFactory.Pistol(),
                Weapon = new WeaponSpec
                {
                    Id = "pistol_base",
                    Title = "Утиный пистолет",
                    Description = "Стандартное оружие. Надёжный, быстрый, всегда с тобой.",
                    Price = 0,
                    Damage = 18,
                    FireDelay = 0.2f,
                    Magazine = 14,
                    Spread = 1.2f,
                    Recoil = 1.2f,
                    BodyColor = new Color32(62, 70, 78, 255),
                    AccentColor = new Color32(140, 198, 32, 255)
                }
            });

            _items.Add(new ShopItem
            {
                Id = "pistol_lime",
                Kind = ItemKind.Pistol,
                Title = "Лаймовый пистолет",
                Description = "Кислотный выстрел. Киви после него даже не успевают удивляться.",
                Price = 90,
                Icon = IconFactory.Pistol(),
                Weapon = new WeaponSpec
                {
                    Id = "pistol_lime",
                    Title = "Лаймовый пистолет",
                    Price = 90,
                    Damage = 26,
                    FireDelay = 0.24f,
                    Magazine = 16,
                    Spread = 1.4f,
                    Recoil = 1.5f,
                    BodyColor = new Color32(122, 196, 40, 255),
                    AccentColor = new Color32(238, 243, 214, 255)
                }
            });

            _items.Add(new ShopItem
            {
                Id = "pistol_berry",
                Kind = ItemKind.Pistol,
                Title = "Клубничный пистолет",
                Description = "Сладкий гром и ягодный аромат. Бьёт сильнее лаймового.",
                Price = 150,
                Icon = IconFactory.Pistol(),
                Weapon = new WeaponSpec
                {
                    Id = "pistol_berry",
                    Title = "Клубничный пистолет",
                    Price = 150,
                    Damage = 34,
                    FireDelay = 0.3f,
                    Magazine = 18,
                    Spread = 1.0f,
                    Recoil = 2.2f,
                    BodyColor = new Color32(206, 62, 78, 255),
                    AccentColor = new Color32(255, 176, 186, 255)
                }
            });

            _items.Add(new ShopItem
            {
                Id = "pistol_gold",
                Kind = ItemKind.Pistol,
                Title = "Золотой револьвер",
                Description = "Шесть громких аргументов. Один киви — один выстрел.",
                Price = 280,
                Icon = IconFactory.Revolver(),
                Weapon = new WeaponSpec
                {
                    Id = "pistol_gold",
                    Title = "Золотой револьвер",
                    Price = 280,
                    Damage = 55,
                    FireDelay = 0.48f,
                    Magazine = 6,
                    Spread = 0.7f,
                    Recoil = 3.4f,
                    Automatic = false,
                    Revolver = true,
                    BodyColor = new Color32(198, 160, 62, 255),
                    AccentColor = new Color32(247, 214, 130, 255)
                }
            });

            _items.Add(new ShopItem
            {
                Id = "pistol_ice",
                Kind = ItemKind.Pistol,
                Title = "Ледяной пистолет",
                Description = "Замороженный киви-нектар. Стреляет быстрее, чем ты моргаешь.",
                Price = 460,
                Icon = IconFactory.Pistol(),
                Weapon = new WeaponSpec
                {
                    Id = "pistol_ice",
                    Title = "Ледяной пистолет",
                    Price = 460,
                    Damage = 21,
                    FireDelay = 0.1f,
                    Magazine = 26,
                    Spread = 2.1f,
                    Recoil = 0.7f,
                    BodyColor = new Color32(120, 200, 232, 255),
                    AccentColor = new Color32(226, 250, 255, 255)
                }
            });

            // ---- Шапки ----
            _items.Add(new ShopItem
            {
                Id = "hat_none",
                Kind = ItemKind.Hat,
                Title = "Без шапки",
                Description = "Классическая уточка. Голова голая, но с характером.",
                Price = 0,
                Icon = IconFactory.Duck()
            });

            _items.Add(new ShopItem
            {
                Id = "hat_kiwi",
                Kind = ItemKind.Hat,
                Title = "Шляпа-киви",
                Description = "Долька киви на голове. Стильно и вкусно.",
                Price = 45,
                Icon = IconFactory.HatKiwi()
            });

            _items.Add(new ShopItem
            {
                Id = "hat_top",
                Kind = ItemKind.Hat,
                Title = "Цилиндр",
                Description = "Для тех, кто уважает себя и киви-птиц.",
                Price = 95,
                Icon = IconFactory.HatTop()
            });

            _items.Add(new ShopItem
            {
                Id = "hat_propeller",
                Kind = ItemKind.Hat,
                Title = "Кепка с пропеллером",
                Description = "Пропеллер крутится, когда уточка бежит.",
                Price = 135,
                Icon = IconFactory.HatPropeller()
            });

            _items.Add(new ShopItem
            {
                Id = "hat_party",
                Kind = ItemKind.Hat,
                Title = "Праздничный колпак",
                Description = "Выиграй уровень — надень колпак. Мы разрешаем.",
                Price = 185,
                Icon = IconFactory.HatParty()
            });

            _items.Add(new ShopItem
            {
                Id = "hat_crown",
                Kind = ItemKind.Hat,
                Title = "Корона",
                Description = "Абсолютная власть над посёлком и офисом.",
                Price = 340,
                Icon = IconFactory.HatCrown()
            });
        }
    }
}
