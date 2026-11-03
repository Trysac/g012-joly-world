using System;

namespace JolyWorld.Core
{
    public enum ItemKind
    {
        None,
        HealthPotion,
        ManaPotion,
        StaminaPotion,
        Elixir,
        Key,
    }

    /// <summary>What an item is called, how many fit in a slot and what drinking it restores.</summary>
    public readonly struct ItemInfo
    {
        public readonly ItemKind Kind;
        public readonly string Name;
        public readonly string Description;
        public readonly int StackLimit;
        public readonly float Health;
        public readonly float Mana;
        public readonly float Stamina;

        public ItemInfo(ItemKind kind, string name, string description, int stackLimit, float health, float mana, float stamina)
        {
            Kind = kind;
            Name = name;
            Description = description;
            StackLimit = stackLimit;
            Health = health;
            Mana = mana;
            Stamina = stamina;
        }

        public bool Usable => Health > 0f || Mana > 0f || Stamina > 0f;
    }

    public static class Items
    {
        static readonly ItemInfo[] Catalog =
        {
            new ItemInfo(ItemKind.None, string.Empty, string.Empty, 0, 0f, 0f, 0f),
            new ItemInfo(ItemKind.HealthPotion, "Health Potion", "A red berry tonic. Restores 40 health.", 5, 40f, 0f, 0f),
            new ItemInfo(ItemKind.ManaPotion, "Mana Potion", "Bottled starlight. Restores 50 mana.", 5, 0f, 50f, 0f),
            new ItemInfo(ItemKind.StaminaPotion, "Stamina Potion", "Honey and lemon. Refills your stamina.", 5, 0f, 0f, Vitals.MaxStamina),
            new ItemInfo(ItemKind.Elixir, "Rainbow Elixir", "Rare and fizzy. Refills all three bars.", 3, Vitals.MaxHealth, Vitals.MaxMana, Vitals.MaxStamina),
            new ItemInfo(ItemKind.Key, "Brass Key", "Opens one locked gate. Used up when it does.", 3, 0f, 0f, 0f),
        };

        public static ItemInfo Of(ItemKind kind)
        {
            int index = (int)kind;
            return index >= 0 && index < Catalog.Length ? Catalog[index] : Catalog[0];
        }
    }

    public struct Slot
    {
        public ItemKind Kind;
        public int Count;

        public bool Empty => Count <= 0 || Kind == ItemKind.None;
    }

    public enum UseResult
    {
        Used,
        EmptySlot,
        NotUsable,
        /// <summary>The bars it restores are already full, so it was kept.</summary>
        NoEffect,
    }

    /// <summary>
    /// A 7-slot hotbar (slots 0..6, as in the 2021 HUD) followed by a 14-slot bag. Items stack up
    /// to their limit; new items fill open stacks first, then the hotbar, then the bag.
    /// </summary>
    public sealed class Inventory
    {
        public const int HotbarSize = 7;
        public const int BagSize = 14;
        public const int Size = HotbarSize + BagSize;

        readonly Slot[] slots = new Slot[Size];

        /// <summary>The selected hotbar slot, 0..6.</summary>
        public int Selected { get; private set; }

        public Slot this[int index] => index >= 0 && index < Size ? slots[index] : default;

        public static Inventory Starter()
        {
            var inventory = new Inventory();
            inventory.Add(ItemKind.HealthPotion, 2);
            inventory.Add(ItemKind.StaminaPotion, 1);
            return inventory;
        }

        /// <summary>Adds up to <paramref name="count"/> items and returns how many fitted.</summary>
        public int Add(ItemKind kind, int count = 1)
        {
            int limit = Items.Of(kind).StackLimit;
            if (kind == ItemKind.None || count <= 0 || limit <= 0)
            {
                return 0;
            }
            int left = count;
            for (int i = 0; i < Size && left > 0; i++)
            {
                if (slots[i].Kind == kind && !slots[i].Empty && slots[i].Count < limit)
                {
                    int put = Math.Min(left, limit - slots[i].Count);
                    slots[i].Count += put;
                    left -= put;
                }
            }
            for (int i = 0; i < Size && left > 0; i++)
            {
                if (slots[i].Empty)
                {
                    int put = Math.Min(left, limit);
                    slots[i] = new Slot { Kind = kind, Count = put };
                    left -= put;
                }
            }
            return count - left;
        }

        public int Count(ItemKind kind)
        {
            int total = 0;
            foreach (Slot slot in slots)
            {
                if (slot.Kind == kind && !slot.Empty)
                {
                    total += slot.Count;
                }
            }
            return total;
        }

        /// <summary>Removes one item of a kind, from the bag before the hotbar; false when there is none.</summary>
        public bool Take(ItemKind kind)
        {
            for (int i = Size - 1; i >= 0; i--)
            {
                if (slots[i].Kind == kind && !slots[i].Empty)
                {
                    RemoveOne(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Moves the item in <paramref name="from"/> to <paramref name="to"/>, merging stacks of the same item.</summary>
        public void Swap(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= Size || to >= Size)
            {
                return;
            }
            Slot a = slots[from];
            Slot b = slots[to];
            if (!a.Empty && !b.Empty && a.Kind == b.Kind)
            {
                int limit = Items.Of(a.Kind).StackLimit;
                int moved = Math.Min(a.Count, limit - b.Count);
                slots[to].Count += moved;
                slots[from].Count -= moved;
                if (slots[from].Count <= 0)
                {
                    slots[from] = default;
                }
                return;
            }
            slots[from] = b;
            slots[to] = a;
        }

        /// <summary>Drinks the item in a slot when it would restore something.</summary>
        public UseResult Use(int index, Vitals vitals)
        {
            Slot slot = this[index];
            if (slot.Empty)
            {
                return UseResult.EmptySlot;
            }
            ItemInfo info = Items.Of(slot.Kind);
            if (!info.Usable)
            {
                return UseResult.NotUsable;
            }
            bool helps = (info.Health > 0f && !vitals.Health.IsFull) || (info.Mana > 0f && !vitals.Mana.IsFull) ||
                (info.Stamina > 0f && !vitals.Stamina.IsFull);
            if (!helps)
            {
                return UseResult.NoEffect;
            }
            vitals.Health.Restore(info.Health);
            vitals.Mana.Restore(info.Mana);
            vitals.Stamina.Restore(info.Stamina);
            RemoveOne(index);
            return UseResult.Used;
        }

        public void Select(int hotbarIndex)
        {
            if (hotbarIndex >= 0 && hotbarIndex < HotbarSize)
            {
                Selected = hotbarIndex;
            }
        }

        public void SelectStep(int step) => Selected = ((Selected + step) % HotbarSize + HotbarSize) % HotbarSize;

        void RemoveOne(int index)
        {
            slots[index].Count--;
            if (slots[index].Count <= 0)
            {
                slots[index] = default;
            }
        }
    }
}
