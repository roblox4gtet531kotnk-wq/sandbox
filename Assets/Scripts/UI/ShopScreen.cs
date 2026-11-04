using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KiviGun
{
    /// <summary>Магазин: пистолеты и шапки за киви-дольки.</summary>
    public class ShopScreen : MonoBehaviour, IScreen
    {
        private enum Tab { Pistols, Hats }

        private RectTransform _root;
        private Tab _tab = Tab.Pistols;
        private Text _currencyText;
        private RectTransform _grid;

        public static ShopScreen Create(Transform parent, System.Action onBack)
        {
            RectTransform rt = UIBuilder.Stretch(parent, "Shop", 0f);
            var screen = rt.gameObject.AddComponent<ShopScreen>();
            screen.Build(rt, onBack);
            return screen;
        }

        private void Build(RectTransform root, System.Action onBack)
        {
            _root = root;

            Image bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.04f, 0.05f, 0.9f);
            bg.raycastTarget = false;

            UIBuilder.Label(root, "МАГАЗИН", 60, Theme.Text, TextAnchor.MiddleCenter,
                new Vector2(1000f, 90f), new Vector2(0f, 410f), FontStyle.Bold);

            // Баланс
            RectTransform currency = UIBuilder.Rect(root, "Currency", new Vector2(340f, 64f), new Vector2(700f, 415f));
            UIBuilder.Panel(currency, "Bg", new Vector2(340f, 64f), Vector2.zero, new Color(0f, 0f, 0f, 0.45f));
            UIBuilder.Picture(currency, IconFactory.KiwiSlice(), new Vector2(46f, 46f), new Vector2(-130f, 0f), Color.white);
            _currencyText = UIBuilder.Label(currency, "0", 30, Theme.AccentLight, TextAnchor.MiddleCenter,
                new Vector2(180f, 50f), new Vector2(60f, 0f), FontStyle.Bold);

            // Вкладки
            UIBuilder.Button(root, "ПИСТОЛЕТЫ", new Vector2(320f, 70f), new Vector2(-190f, 315f), () => SwitchTab(Tab.Pistols),
                null, Theme.Accent);
            UIBuilder.Button(root, "ШАПКИ", new Vector2(320f, 70f), new Vector2(190f, 315f), () => SwitchTab(Tab.Hats),
                null, Theme.PanelLight);

            _grid = UIBuilder.Rect(root, "Grid", new Vector2(1500f, 560f), new Vector2(0f, 20f));

            UIBuilder.Button(root, "НАЗАД", new Vector2(300f, 70f), new Vector2(0f, -350f), () =>
            {
                AudioManager.Instance?.PlayUiBack();
                onBack?.Invoke();
            }, IconFactory.ArrowBack());

            RefreshGrid();
            RefreshCurrency();
        }

        private void SwitchTab(Tab tab)
        {
            _tab = tab;
            AudioManager.Instance?.PlayUiClick();
            RefreshGrid();
        }

        private void RefreshCurrency()
        {
            if (_currencyText != null) _currencyText.text = SaveSystem.Slices.ToString();
        }

        private void RefreshGrid()
        {
            for (int i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);

            List<ShopItem> items = _tab == Tab.Pistols ? ShopCatalog.Pistols : ShopCatalog.Hats;

            int columns = 3;
            int rows = Mathf.CeilToInt(items.Count / (float)columns);
            float cardW = 460f, cardH = 250f;
            float spacingX = 40f, spacingY = 30f;
            float totalW = columns * cardW + (columns - 1) * spacingX;

            for (int i = 0; i < items.Count; i++)
            {
                ShopItem item = items[i];
                int row = i / columns;
                int col = i % columns;

                float x = -totalW * 0.5f + cardW * 0.5f + col * (cardW + spacingX);
                float y = (rows - 1) * (cardH + spacingY) * 0.5f - row * (cardH + spacingY) - 40f;

                RectTransform card = UIBuilder.Rect(_grid, "Item_" + item.Id, new Vector2(cardW, cardH), new Vector2(x, y));
                bool owned = SaveSystem.Owns(item.Id);
                bool equipped = (item.Kind == ItemKind.Pistol && SaveSystem.EquippedWeapon == item.Id)
                                || (item.Kind == ItemKind.Hat && SaveSystem.EquippedHat == item.Id);

                UIBuilder.Panel(card, "Bg", new Vector2(cardW, cardH), Vector2.zero,
                    equipped ? new Color(0.1f, 0.18f, 0.06f, 0.95f) : new Color(0.07f, 0.1f, 0.12f, 0.95f));

                // Иконка
                if (item.Icon != null)
                    UIBuilder.Picture(card, item.Icon, new Vector2(110f, 110f), new Vector2(-160f, 30f), Color.white);

                // Название и описание
                UIBuilder.Label(card, item.Title, 28, Theme.Text, TextAnchor.MiddleLeft,
                    new Vector2(260f, 44f), new Vector2(55f, 65f), FontStyle.Bold);
                UIBuilder.Label(card, item.Description, 19, Theme.TextDim, TextAnchor.UpperLeft,
                    new Vector2(260f, 70f), new Vector2(55f, 5f));

                if (equipped)
                {
                    UIBuilder.Label(card, "НАДЕТО", 24, Theme.AccentLight, TextAnchor.MiddleCenter,
                        new Vector2(240f, 44f), new Vector2(55f, -70f), FontStyle.Bold);
                }
                else if (owned)
                {
                    UIBuilder.Button(card, "НАДЕТЬ", new Vector2(240f, 62f), new Vector2(55f, -70f), () =>
                    {
                        SaveSystem.Equip(item.Id);
                        AudioManager.Instance?.PlayUiClick();
                        RefreshGrid();
                        RefreshCurrency();
                    }, null, Theme.PanelLight, 24);
                }
                else
                {
                    bool affordable = SaveSystem.CanAfford(item.Price);
                    // Цена
                    UIBuilder.Picture(card, IconFactory.KiwiSlice(), new Vector2(30f, 30f), new Vector2(-50f, -72f), Color.white);
                    UIBuilder.Label(card, item.Price.ToString(), 24, affordable ? Theme.AccentLight : Theme.Danger,
                        TextAnchor.MiddleLeft, new Vector2(100f, 34f), new Vector2(-15f, -72f), FontStyle.Bold);

                    UIBuilder.Button(card, affordable ? "КУПИТЬ" : "НЕТ ДОЛЬОК", new Vector2(240f, 62f), new Vector2(55f, -70f), () =>
                    {
                        // Цена берётся из каталога (ShopCatalog) — магазин не диктует её сам.
                        if (SaveSystem.Purchase(item.Id))
                        {
                            SaveSystem.Equip(item.Id);
                            AudioManager.Instance?.PlaySfx(ProceduralAudio.Sfx.Pickup, 0.9f);
                            ToastManager.GetOrCreate().Show("Куплено: " + item.Title);
                        }
                        else
                        {
                            AudioManager.Instance?.PlayUiBack();
                            ToastManager.GetOrCreate().Show("Не хватает киви-дольок");
                        }
                        RefreshGrid();
                        RefreshCurrency();
                    }, null, affordable ? Theme.Accent : new Color(0.35f, 0.2f, 0.2f, 1f), 24);
                }
            }
        }

        public void Dispose()
        {
            if (_root != null) Destroy(_root.gameObject);
        }
    }
}
