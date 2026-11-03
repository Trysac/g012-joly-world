using System;
using System.Collections.Generic;
using JolyWorld.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace JolyWorld.Game
{
    /// <summary>One row of a menu: an action, or a setting with a value that left/right changes.</summary>
    public sealed class MenuItem
    {
        public string Id;
        public string Label;
        public string Value;
        public bool Disabled;
    }

    /// <summary>Everything a menu screen shows.</summary>
    public sealed class MenuModel
    {
        public bool Logo;
        public string Title;
        /// <summary>0 plain, 1 good (green).</summary>
        public int TitleTone;
        public string Subtitle;
        public readonly List<(string Label, string Value, bool Highlight)> Body = new List<(string, string, bool)>();
        public readonly List<MenuItem> Items = new List<MenuItem>();
        public int Selected;
        public string Hint;
        public bool LightDim;

        public MenuItem Add(string id, string label, string value = null, bool disabled = false)
        {
            var item = new MenuItem { Id = id, Label = label, Value = value, Disabled = disabled };
            Items.Add(item);
            return item;
        }

        public int IndexOf(string id) => Items.FindIndex(item => item.Id == id);
    }

    /// <summary>
    /// The UI Toolkit interface, built in code and styled by Hud.uss, laid out like the 2021
    /// prototype: coins top left, the round menu button top right, the Health / Mana / Stamina
    /// board bottom left, the 7-slot hotbar with an Inventory button along the bottom; plus the
    /// inventory screen, banners and a generic menu renderer.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class Hud : MonoBehaviour
    {
        GameArt art;
        VisualElement root;
        VisualElement play;
        Label coins;
        readonly VisualElement[] gems = new VisualElement[3];
        Label timer;
        Label levelName;
        VisualElement healthFill, manaFill, staminaFill, manaBar;
        Label vitalsTag;
        readonly VisualElement[] hotbar = new VisualElement[Inventory.HotbarSize];
        Label banner, bannerSub, notice;
        VisualElement inventory;
        readonly VisualElement[] bagSlots = new VisualElement[Inventory.Size];
        Label inventoryName, inventoryText, inventoryHint;
        VisualElement menu, menuDim, logo;
        Label menuTitle, menuSubtitle, menuHint, credit;
        VisualElement menuBody, menuItems;
        readonly List<VisualElement> rows = new List<VisualElement>();
        float bannerHideAt = float.MaxValue;
        float noticeHideAt;
        float manaFlashUntil;

        public event Action<int> ItemClicked;
        public event Action<int> ItemHovered;
        public event Action MenuButtonClicked;
        public event Action InventoryButtonClicked;
        public event Action<int> HotbarClicked;
        public event Action<int> InventorySlotClicked;

        public MenuModel Menu { get; private set; }
        public VisualElement Root => root;

        public void Init(GameArt gameArt)
        {
            art = gameArt;
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            Build();
        }

        static VisualElement E(VisualElement parent, string classes, string name = null, bool pick = false)
        {
            var element = new VisualElement { name = name, pickingMode = pick ? PickingMode.Position : PickingMode.Ignore };
            foreach (string c in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                element.AddToClassList(c);
            }
            parent?.Add(element);
            return element;
        }

        static Label L(VisualElement parent, string classes, string text = "", string name = null)
        {
            var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
            foreach (string c in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                label.AddToClassList(c);
            }
            parent?.Add(label);
            return label;
        }

        static void Icon(VisualElement element, Texture2D texture) =>
            element.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);

        void Build()
        {
            play = E(root, "layer", "play-hud");

            VisualElement purse = E(play, "purse", "purse");
            VisualElement coinRow = E(purse, "purse-row");
            Icon(E(coinRow, "purse-icon"), art.CoinIcon);
            coins = L(coinRow, "purse-value display shadow", "0", "coins");
            VisualElement gemRow = E(purse, "gem-row");
            for (int i = 0; i < gems.Length; i++)
            {
                gems[i] = E(gemRow, "gem-icon", "gem" + i);
            }

            VisualElement top = E(play, "top-center");
            levelName = L(top, "level-name shadow", "", "level-name");
            timer = L(top, "timer display shadow", "0:00.0", "timer");

            VisualElement menuButton = E(play, "round-button", "menu-button", true);
            Icon(E(menuButton, "round-button__icon"), art.MenuIcon);
            menuButton.RegisterCallback<ClickEvent>(_ => MenuButtonClicked?.Invoke());

            VisualElement vitals = E(play, "board vitals", "vitals");
            healthFill = Bar(vitals, "HEALTH", "health", out _);
            manaFill = Bar(vitals, "MANA", "mana", out manaBar);
            staminaFill = Bar(vitals, "STAMINA", "stamina", out _);
            vitalsTag = L(vitals, "vitals-tag", "JOLY WORLD", "vitals-tag");

            VisualElement bar = E(play, "hotbar", "hotbar");
            for (int i = 0; i < hotbar.Length; i++)
            {
                int index = i;
                hotbar[i] = Slot(bar, i + 1);
                hotbar[i].name = "slot" + i;
                hotbar[i].pickingMode = PickingMode.Position;
                hotbar[i].RegisterCallback<ClickEvent>(_ => HotbarClicked?.Invoke(index));
            }
            VisualElement bagButton = E(play, "board-button", "inventory-button", true);
            L(bagButton, "board-button__label", "INVENTORY");
            bagButton.RegisterCallback<ClickEvent>(_ => InventoryButtonClicked?.Invoke());

            banner = L(play, "banner display shadow hidden", "", "banner");
            bannerSub = L(play, "banner-sub shadow hidden", "", "banner-sub");
            notice = L(play, "notice shadow hidden", "", "notice");

            inventory = E(root, "layer hidden", "inventory");
            E(inventory, "layer menu-dim menu-dim--light");
            VisualElement panel = E(inventory, "board inventory-panel");
            L(panel, "menu-title display", "INVENTORY");
            for (int row = 0; row < 3; row++)
            {
                L(panel, "inventory-row-label", row == 0 ? "HOTBAR" : row == 1 ? "BAG" : string.Empty);
                VisualElement line = E(panel, "inventory-row");
                for (int col = 0; col < Inventory.HotbarSize; col++)
                {
                    int index = row * Inventory.HotbarSize + col;
                    VisualElement slot = Slot(line, row == 0 ? col + 1 : 0);
                    slot.name = "bag" + index;
                    slot.pickingMode = PickingMode.Position;
                    slot.RegisterCallback<ClickEvent>(_ => InventorySlotClicked?.Invoke(index));
                    bagSlots[index] = slot;
                }
            }
            VisualElement info = E(panel, "inventory-info");
            inventoryName = L(info, "inventory-name", "", "inventory-name");
            inventoryText = L(info, "inventory-text", "", "inventory-text");
            inventoryHint = L(panel, "menu-hint menu-hint--inline", "", "inventory-hint");

            menu = E(root, "layer hidden", "menu");
            menuDim = E(menu, "layer menu-dim");
            VisualElement menuPanel = E(menu, "menu-panel");
            logo = E(menuPanel, "logo", "logo");
            VisualElement letters = E(logo, "logo__letters");
            string[] palette = { "#ff5a5a", "#ffa53a", "#ffe14a", "#6ee05a", "#ffffff", "#4ac0ff", "#7a8cff", "#c86cff", "#ff6fd0", "#ffa53a" };
            string word = "JOLY WORLD";
            for (int i = 0; i < word.Length; i++)
            {
                Label letter = L(letters, "logo__letter display", word[i].ToString());
                letter.style.color = Palette.Hex(palette[i % palette.Length]);
                letter.style.rotate = new Rotate(((i * 7) % 5 - 2) * 2.5f);
            }
            L(logo, "logo__tag", "A LOW-POLY ADVENTURE IN FOUR LITTLE LANDS");
            menuTitle = L(menuPanel, "menu-title display", "", "menu-title");
            menuSubtitle = L(menuPanel, "menu-subtitle", "", "menu-subtitle");
            menuBody = E(menuPanel, "menu-body", "menu-body");
            menuItems = E(menuPanel, "menu-items", "menu-items");
            menuHint = L(menu, "menu-hint", "", "menu-hint");
            credit = L(menu, "credit", "Rebuilt in Unity 6 from Trysac's 2021 prototype", "credit");
        }

        VisualElement Bar(VisualElement parent, string label, string name, out VisualElement track)
        {
            VisualElement row = E(parent, "vital");
            L(row, "vital__label", label);
            track = E(row, "vital__bar", name + "-bar");
            return E(track, "vital__fill vital__fill--" + name, name + "-fill");
        }

        static VisualElement Slot(VisualElement parent, int number)
        {
            VisualElement slot = E(parent, "slot");
            E(slot, "slot__icon");
            L(slot, "slot__count shadow", string.Empty);
            if (number > 0)
            {
                L(slot, "slot__number", number.ToString());
            }
            return slot;
        }

        public static void SetVisible(VisualElement element, bool visible) => element.EnableInClassList("hidden", !visible);

        public static bool Visible(VisualElement element) => !element.ClassListContains("hidden");

        // ---- Play HUD ----

        public void ShowPlay(bool visible) => SetVisible(play, visible);

        public bool PlayVisible => Visible(play);

        public void SetLevel(string name, string tag)
        {
            levelName.text = name.ToUpperInvariant();
            vitalsTag.text = tag;
        }

        public void SetPurse(int coinCount, int gemMask, float seconds)
        {
            coins.text = coinCount.ToString();
            for (int i = 0; i < gems.Length; i++)
            {
                bool found = (gemMask & (1 << i)) != 0;
                Icon(gems[i], found ? art.GemIcon : art.GemEmptyIcon);
                gems[i].EnableInClassList("gem-icon--found", found);
            }
            timer.text = TimeText.Format(seconds);
        }

        public string CoinText => coins.text;
        public string TimerText => timer.text;

        public int GemsShown
        {
            get
            {
                int count = 0;
                foreach (VisualElement gem in gems)
                {
                    count += gem.ClassListContains("gem-icon--found") ? 1 : 0;
                }
                return count;
            }
        }

        public void SetVitals(Vitals vitals)
        {
            healthFill.style.width = Length.Percent(vitals.Health.Fraction * 100f);
            manaFill.style.width = Length.Percent(vitals.Mana.Fraction * 100f);
            staminaFill.style.width = Length.Percent(vitals.Stamina.Fraction * 100f);
            healthFill.EnableInClassList("vital__fill--low", vitals.Health.Fraction <= 0.25f);
            manaBar.EnableInClassList("vital__bar--flash", Time.unscaledTime < manaFlashUntil && Mathf.Repeat(Time.unscaledTime * 8f, 1f) < 0.5f);
        }

        public float HealthFraction => healthFill.style.width.value.value / 100f;
        public float ManaFraction => manaFill.style.width.value.value / 100f;
        public float StaminaFraction => staminaFill.style.width.value.value / 100f;

        /// <summary>Flashes the mana bar, when there is too little mana to cast.</summary>
        public void FlashMana() => manaFlashUntil = Time.unscaledTime + 0.6f;

        void FillSlot(VisualElement slot, Slot content)
        {
            Icon(slot.Q(className: "slot__icon"), content.Empty ? null : art.ItemIcon(content.Kind));
            slot.Q<Label>(className: "slot__count").text = content.Empty || content.Count <= 1 ? string.Empty : content.Count.ToString();
            slot.EnableInClassList("slot--empty", content.Empty);
        }

        public void SetHotbar(Inventory items)
        {
            for (int i = 0; i < hotbar.Length; i++)
            {
                FillSlot(hotbar[i], items[i]);
                hotbar[i].EnableInClassList("slot--selected", i == items.Selected);
            }
        }

        public int SelectedSlot
        {
            get
            {
                for (int i = 0; i < hotbar.Length; i++)
                {
                    if (hotbar[i].ClassListContains("slot--selected"))
                    {
                        return i;
                    }
                }
                return -1;
            }
        }

        public string SlotCount(int index) => hotbar[index].Q<Label>(className: "slot__count").text;

        public bool SlotEmpty(int index) => hotbar[index].ClassListContains("slot--empty");

        public void Banner(string text, string sub, float seconds, Color color)
        {
            banner.text = text;
            banner.style.color = color;
            bannerSub.text = sub ?? string.Empty;
            SetVisible(banner, true);
            SetVisible(bannerSub, !string.IsNullOrEmpty(sub));
            banner.AddToClassList("banner--pop");
            banner.schedule.Execute(() => banner.RemoveFromClassList("banner--pop")).StartingIn(120);
            bannerHideAt = Time.unscaledTime + seconds;
        }

        public void HideBanner()
        {
            SetVisible(banner, false);
            SetVisible(bannerSub, false);
            bannerHideAt = float.MaxValue;
        }

        public string BannerText => Visible(banner) ? banner.text : string.Empty;

        public void Notice(string text, float seconds = 1.8f)
        {
            notice.text = text;
            SetVisible(notice, true);
            noticeHideAt = Time.unscaledTime + seconds;
        }

        public void ClearMessages()
        {
            HideBanner();
            SetVisible(notice, false);
        }

        public string NoticeText => Visible(notice) ? notice.text : string.Empty;

        // ---- Inventory screen ----

        public void ShowInventory(bool visible) => SetVisible(inventory, visible);

        public bool InventoryVisible => Visible(inventory);

        public void SetInventory(Inventory items, int cursor, int held, string hint)
        {
            for (int i = 0; i < bagSlots.Length; i++)
            {
                FillSlot(bagSlots[i], items[i]);
                bagSlots[i].EnableInClassList("slot--selected", i == cursor);
                bagSlots[i].EnableInClassList("slot--held", i == held);
            }
            Slot under = items[cursor];
            ItemInfo info = Items.Of(under.Kind);
            inventoryName.text = under.Empty ? "EMPTY SLOT" : $"{info.Name.ToUpperInvariant()}  x{under.Count}";
            inventoryText.text = under.Empty ? "Move items here to sort them." : info.Description;
            inventoryHint.text = hint;
        }

        public string InventoryTitle => inventoryName.text;

        void Update()
        {
            if (Time.unscaledTime >= bannerHideAt)
            {
                HideBanner();
            }
            if (Visible(notice) && Time.unscaledTime >= noticeHideAt)
            {
                SetVisible(notice, false);
            }
        }

        // ---- Menus ----

        public void ShowMenu(MenuModel model)
        {
            Menu = model;
            SetVisible(menu, true);
            play.AddToClassList("menu-open");
            menuDim.EnableInClassList("menu-dim--light", model.LightDim);
            SetVisible(logo, model.Logo);
            SetVisible(credit, model.Logo);
            menuTitle.text = model.Title ?? string.Empty;
            SetVisible(menuTitle, !string.IsNullOrEmpty(model.Title));
            menuTitle.EnableInClassList("menu-title--good", model.TitleTone > 0);
            menuSubtitle.text = model.Subtitle ?? string.Empty;
            SetVisible(menuSubtitle, !string.IsNullOrEmpty(model.Subtitle));
            menuBody.Clear();
            foreach ((string label, string value, bool highlight) in model.Body)
            {
                VisualElement row = E(menuBody, highlight ? "body-row body-row--highlight" : "body-row");
                L(row, "body-row__label", label);
                L(row, "body-row__value", value);
            }
            SetVisible(menuBody, model.Body.Count > 0);
            menuItems.Clear();
            rows.Clear();
            for (int i = 0; i < model.Items.Count; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.AddToClassList("menu-item");
                row.EnableInClassList("menu-item--disabled", model.Items[i].Disabled);
                var label = new Label(model.Items[i].Label) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("menu-item__label");
                row.Add(label);
                var value = new Label(model.Items[i].Value ?? string.Empty) { pickingMode = PickingMode.Ignore };
                value.AddToClassList("menu-item__value");
                row.Add(value);
                row.RegisterCallback<ClickEvent>(_ => ItemClicked?.Invoke(index));
                row.RegisterCallback<PointerEnterEvent>(_ => ItemHovered?.Invoke(index));
                menuItems.Add(row);
                rows.Add(row);
            }
            menuHint.text = model.Hint ?? string.Empty;
            menuHint.EnableInClassList("menu-hint--raised", PlayVisible);
            RefreshMenu();
        }

        public void RefreshMenu()
        {
            if (Menu == null)
            {
                return;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].EnableInClassList("menu-item--selected", i == Menu.Selected);
                ((Label)rows[i][1]).text = Menu.Items[i].Value ?? string.Empty;
            }
        }

        public void HideMenu()
        {
            Menu = null;
            SetVisible(menu, false);
            play.RemoveFromClassList("menu-open");
        }

        public bool MenuVisible => Visible(menu);
    }
}
