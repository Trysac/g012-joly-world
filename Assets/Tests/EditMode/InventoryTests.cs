using NUnit.Framework;

namespace JolyWorld.Core.Tests
{
    public class ItemTests
    {
        [Test]
        public void PotionsRestoreTheirOwnBar()
        {
            Assert.That(Items.Of(ItemKind.HealthPotion).Health, Is.EqualTo(40f));
            Assert.That(Items.Of(ItemKind.ManaPotion).Mana, Is.EqualTo(50f));
            Assert.That(Items.Of(ItemKind.StaminaPotion).Stamina, Is.EqualTo(100f));
            ItemInfo elixir = Items.Of(ItemKind.Elixir);
            Assert.That(elixir.Health, Is.EqualTo(Vitals.MaxHealth));
            Assert.That(elixir.Mana, Is.EqualTo(Vitals.MaxMana));
            Assert.That(elixir.Stamina, Is.EqualTo(Vitals.MaxStamina));
        }

        [Test]
        public void EveryItemHasANameAndAStackLimit()
        {
            foreach (ItemKind kind in new[] { ItemKind.HealthPotion, ItemKind.ManaPotion, ItemKind.StaminaPotion, ItemKind.Elixir, ItemKind.Key })
            {
                ItemInfo info = Items.Of(kind);
                Assert.That(info.Kind, Is.EqualTo(kind));
                Assert.That(info.Name, Is.Not.Empty);
                Assert.That(info.Description, Is.Not.Empty);
                Assert.That(info.StackLimit, Is.GreaterThan(0));
            }
            Assert.That(Items.Of(ItemKind.HealthPotion).StackLimit, Is.EqualTo(5));
            Assert.That(Items.Of(ItemKind.Key).StackLimit, Is.EqualTo(3));
            Assert.That(Items.Of(ItemKind.Key).Usable, Is.False);
            Assert.That(Items.Of(ItemKind.None).StackLimit, Is.EqualTo(0));
        }
    }

    public class InventoryTests
    {
        [Test]
        public void TheHotbarHasSevenSlotsAndTheBagFourteen()
        {
            var inventory = new Inventory();

            Assert.That(Inventory.HotbarSize, Is.EqualTo(7));
            Assert.That(Inventory.Size, Is.EqualTo(21));
            for (int i = 0; i < Inventory.Size; i++)
            {
                Assert.That(inventory[i].Empty, Is.True);
            }
            Assert.That(inventory.Selected, Is.EqualTo(0));
        }

        [Test]
        public void TheStarterKitIsTwoHealthPotionsAndAStaminaPotion()
        {
            Inventory inventory = Inventory.Starter();

            Assert.That(inventory[0].Kind, Is.EqualTo(ItemKind.HealthPotion));
            Assert.That(inventory[0].Count, Is.EqualTo(2));
            Assert.That(inventory[1].Kind, Is.EqualTo(ItemKind.StaminaPotion));
            Assert.That(inventory[1].Count, Is.EqualTo(1));
            Assert.That(inventory[2].Empty, Is.True);
        }

        [Test]
        public void AddStacksFirstThenFillsTheHotbarThenTheBag()
        {
            var inventory = new Inventory();

            Assert.That(inventory.Add(ItemKind.HealthPotion, 7), Is.EqualTo(7));
            Assert.That(inventory[0].Count, Is.EqualTo(5), "a stack holds five potions");
            Assert.That(inventory[1].Kind, Is.EqualTo(ItemKind.HealthPotion));
            Assert.That(inventory[1].Count, Is.EqualTo(2));

            inventory.Add(ItemKind.ManaPotion);
            Assert.That(inventory[2].Kind, Is.EqualTo(ItemKind.ManaPotion));
            inventory.Add(ItemKind.HealthPotion);
            Assert.That(inventory[1].Count, Is.EqualTo(3), "tops up the open stack");
            Assert.That(inventory.Count(ItemKind.HealthPotion), Is.EqualTo(8));
        }

        [Test]
        public void AddReturnsOnlyWhatFits()
        {
            var inventory = new Inventory();
            Assert.That(inventory.Add(ItemKind.Key, 3 * Inventory.Size), Is.EqualTo(3 * Inventory.Size));

            Assert.That(inventory.Add(ItemKind.Key), Is.EqualTo(0));
            Assert.That(inventory.Add(ItemKind.ManaPotion), Is.EqualTo(0));
            Assert.That(inventory.Add(ItemKind.None), Is.EqualTo(0));
            Assert.That(inventory.Add(ItemKind.ManaPotion, 0), Is.EqualTo(0));
        }

        [Test]
        public void TakeUsesTheBagBeforeTheHotbar()
        {
            var inventory = new Inventory();
            inventory.Add(ItemKind.Key, 3);
            inventory.Swap(0, Inventory.HotbarSize + 2);
            inventory.Add(ItemKind.Key, 1);
            Assert.That(inventory[0].Kind, Is.EqualTo(ItemKind.Key), "the full bag stack is skipped");

            Assert.That(inventory.Take(ItemKind.Key), Is.True);
            Assert.That(inventory[Inventory.HotbarSize + 2].Count, Is.EqualTo(2));
            Assert.That(inventory[0].Count, Is.EqualTo(1));
            inventory.Take(ItemKind.Key);
            inventory.Take(ItemKind.Key);
            Assert.That(inventory[Inventory.HotbarSize + 2].Empty, Is.True);
            Assert.That(inventory.Take(ItemKind.Key), Is.True);
            Assert.That(inventory[0].Empty, Is.True);
            Assert.That(inventory[0].Kind, Is.EqualTo(ItemKind.None), "an emptied slot forgets its item");
            Assert.That(inventory.Take(ItemKind.Key), Is.False);
        }

        [Test]
        public void SwapExchangesTwoSlots()
        {
            Inventory inventory = Inventory.Starter();
            inventory.Swap(0, 10);

            Assert.That(inventory[0].Empty, Is.True);
            Assert.That(inventory[10].Kind, Is.EqualTo(ItemKind.HealthPotion));
            Assert.That(inventory[10].Count, Is.EqualTo(2));

            inventory.Swap(1, 10);
            Assert.That(inventory[1].Kind, Is.EqualTo(ItemKind.HealthPotion));
            Assert.That(inventory[10].Kind, Is.EqualTo(ItemKind.StaminaPotion));
        }

        [Test]
        public void SwapMergesTheSameItemUpToTheStackLimit()
        {
            var inventory = new Inventory();
            inventory.Add(ItemKind.HealthPotion, 8);

            inventory.Swap(0, 1);

            Assert.That(inventory[1].Count, Is.EqualTo(5));
            Assert.That(inventory[0].Count, Is.EqualTo(3));
            Assert.That(inventory.Count(ItemKind.HealthPotion), Is.EqualTo(8));
        }

        [Test]
        public void SwapIgnoresSlotsOutsideTheInventory()
        {
            Inventory inventory = Inventory.Starter();
            inventory.Swap(0, Inventory.Size);
            inventory.Swap(-1, 0);
            inventory.Swap(0, 0);

            Assert.That(inventory[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void UsingAPotionRestoresTheBarAndUsesItUp()
        {
            Inventory inventory = Inventory.Starter();
            var vitals = new Vitals();
            vitals.Hurt(60f);

            Assert.That(inventory.Use(0, vitals), Is.EqualTo(UseResult.Used));
            Assert.That(vitals.Health.Current, Is.EqualTo(80f));
            Assert.That(inventory[0].Count, Is.EqualTo(1));

            vitals.Stamina.Spend(100f);
            Assert.That(inventory.Use(1, vitals), Is.EqualTo(UseResult.Used));
            Assert.That(vitals.Stamina.IsFull, Is.True);
            Assert.That(inventory[1].Empty, Is.True);
        }

        [Test]
        public void APotionIsKeptWhenItWouldDoNothing()
        {
            Inventory inventory = Inventory.Starter();
            var vitals = new Vitals();

            Assert.That(inventory.Use(0, vitals), Is.EqualTo(UseResult.NoEffect));
            Assert.That(inventory[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void AnElixirRestoresAllThreeBars()
        {
            var inventory = new Inventory();
            inventory.Add(ItemKind.Elixir);
            var vitals = new Vitals();
            vitals.Hurt(50f);
            vitals.Mana.Spend(80f);
            vitals.Stamina.Spend(90f);

            Assert.That(inventory.Use(0, vitals), Is.EqualTo(UseResult.Used));
            Assert.That(vitals.Health.IsFull && vitals.Mana.IsFull && vitals.Stamina.IsFull, Is.True);
        }

        [Test]
        public void KeysAndEmptySlotsCannotBeUsed()
        {
            var inventory = new Inventory();
            inventory.Add(ItemKind.Key);
            var vitals = new Vitals();

            Assert.That(inventory.Use(0, vitals), Is.EqualTo(UseResult.NotUsable));
            Assert.That(inventory[0].Count, Is.EqualTo(1));
            Assert.That(inventory.Use(3, vitals), Is.EqualTo(UseResult.EmptySlot));
            Assert.That(inventory.Use(99, vitals), Is.EqualTo(UseResult.EmptySlot));
        }

        [Test]
        public void TheSelectionMovesAlongTheHotbarAndWraps()
        {
            var inventory = new Inventory();
            inventory.Select(4);
            Assert.That(inventory.Selected, Is.EqualTo(4));
            inventory.SelectStep(3);
            Assert.That(inventory.Selected, Is.EqualTo(0));
            inventory.SelectStep(-1);
            Assert.That(inventory.Selected, Is.EqualTo(6));
            inventory.Select(7);
            Assert.That(inventory.Selected, Is.EqualTo(6), "the bag cannot be selected");
            inventory.Select(-2);
            Assert.That(inventory.Selected, Is.EqualTo(6));
        }
    }
}
