using JolyWorld.Core;
using UnityEngine;

namespace JolyWorld.Game
{
    /// <summary>The colours of Joly World, picked from the 2021 prototype's bright meadow.</summary>
    public static class Palette
    {
        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;

        // Ground.
        public static readonly Color Grass = Hex("#5fe04a");
        public static readonly Color GrassDark = Hex("#3cbf3a");
        public static readonly Color Dirt = Hex("#c79a62");
        public static readonly Color DirtDark = Hex("#a87c4c");
        public static readonly Color Stone = Hex("#9aa8b8");
        public static readonly Color StoneDark = Hex("#76849a");
        public static readonly Color Snow = Hex("#f4f8ff");
        public static readonly Color Ice = Hex("#bfe8ff");
        public static readonly Color Wood = Hex("#9c6b3c");
        public static readonly Color WoodDark = Hex("#6e4a2a");
        public static readonly Color WoodLight = Hex("#c08a52");
        public static readonly Color Water = Hex("#46c3f2");
        public static readonly Color WaterDeep = Hex("#2a7fd8");

        // Trees.
        public static readonly Color Leaf = Hex("#4fcf3e");
        public static readonly Color LeafDark = Hex("#2f9e3a");
        public static readonly Color LeafYellow = Hex("#d9e24a");
        public static readonly Color LeafPurple = Hex("#a58ae6");
        public static readonly Color LeafPink = Hex("#e8a6e8");
        public static readonly Color Pine = Hex("#2f7d4a");
        public static readonly Color Trunk = Hex("#b5844e");

        // Things.
        public static readonly Color Gold = Hex("#ffd23a");
        public static readonly Color GoldDark = Hex("#e09a1a");
        public static readonly Color GemPurple = Hex("#b65cff");
        public static readonly Color GemPink = Hex("#ff6fd0");
        public static readonly Color Brass = Hex("#e8b84a");
        public static readonly Color Red = Hex("#ef3b3b");
        public static readonly Color Blue = Hex("#3b6cf0");
        public static readonly Color Yellow = Hex("#ffd84a");
        public static readonly Color Thorn = Hex("#2e6b3a");
        public static readonly Color Berry = Hex("#e8425a");
        public static readonly Color Cloud = Hex("#ffffff");

        // Hero.
        public static readonly Color Skin = Hex("#8a3a2a");
        public static readonly Color Hair = Hex("#2a1a14");
        public static readonly Color Shirt = Hex("#f4f4f4");
        public static readonly Color Shorts = Hex("#d8c39a");
        public static readonly Color Shoes = Hex("#4a8cff");

        // Interface.
        public static readonly Color Health = Hex("#ef3b3b");
        public static readonly Color Mana = Hex("#3b6cf0");
        public static readonly Color Stamina = Hex("#ffd84a");
        public static readonly Color Text = Hex("#fff8e8");
        public static readonly Color Good = Hex("#7be05a");
        public static readonly Color Warn = Hex("#ffb23a");

        public static Color Item(ItemKind kind) => kind switch
        {
            ItemKind.HealthPotion => Red,
            ItemKind.ManaPotion => Blue,
            ItemKind.StaminaPotion => Yellow,
            ItemKind.Elixir => GemPink,
            ItemKind.Key => Brass,
            _ => Color.white,
        };

        /// <summary>The colour of the gem with this index in its level.</summary>
        public static Color Gem(int index) => (index % 3) switch
        {
            0 => GemPurple,
            1 => Hex("#38d6ff"),
            _ => GemPink,
        };

        /// <summary>A colour made a little lighter or darker, for faceted variety.</summary>
        public static Color Shade(Color color, float amount)
        {
            Color shaded = amount >= 0f ? Color.Lerp(color, Color.white, amount) : Color.Lerp(color, Color.black, -amount);
            shaded.a = color.a;
            return shaded;
        }
    }
}
