using System.Collections.Generic;

namespace JolyWorld.Core
{
    /// <summary>
    /// The four levels of Joly World, from the sunny meadow of the 2021 prototype to the snowy
    /// peaks behind it. Lengths are in metres; ground tops are the heights the hero walks on.
    /// The level design tests check every jump against the hero's motor.
    /// </summary>
    public static class Levels
    {
        public static IReadOnlyList<Level> All { get; } = new[] { Meadow(), Riverside(), Forest(), Mountain() };

        static Level Meadow()
        {
            var b = new Builder(0, "Sunny Meadow", "A walk by the river", Theme.Meadow, 2f, 0f, 60f);
            b.Wall(-12f, -4f)
                .Ground(-4f, 26f, 0f).Coins(5f, 11f, 0.8f, 6).Beetle(18f, 0f, 15f, 22f)
                .Ground(26f, 34f, 1.2f).Arc(22f, 28f, 1.6f, 1.4f, 5).Coins(29.5f, 33f, 2f, 3)
                .Gem(35.5f, 2.6f)
                .Ground(37f, 80f, 0f).Coins(39f, 43f, 0.8f, 5)
                .Ledge(44f, 48f, 1.8f).Coins(44.6f, 47.4f, 2.5f, 3)
                .Ledge(50f, 54f, 3.5f).Item(ItemKind.ManaPotion, 50.8f, 4.2f).Gem(53f, 4.3f)
                .Checkpoint(57f, 0f)
                .Thorns(62f, 64f, 0f).Arc(60f, 66f, 1.3f, 1.5f, 5)
                .Beetle(72f, 0f, 68f, 77f).Coins(69f, 76f, 0.8f, 4)
                .Ground(83f, 92f, 0.8f).Coins(84f, 91f, 1.6f, 6)
                .Ledge(85f, 88f, 2.6f).Gem(86.5f, 3.4f)
                .Ground(95f, 112f, 0f).Coins(96.5f, 101f, 0.8f, 4)
                .Item(ItemKind.HealthPotion, 102f, 0.7f)
                .Goal(106f, 0f)
                .Wall(112f, 118f);
            return b.Level;
        }

        static Level Riverside()
        {
            var b = new Builder(1, "Riverside", "Planks, bees and a locked gate", Theme.Riverside, 2f, 0f, 70f);
            b.Wall(-12f, -4f)
                .Ground(-4f, 20f, 0f).Coins(5f, 10f, 0.8f, 5).Bee(14f, 1.6f, 11f, 17f)
                .Ledge(23f, 27f, 0.2f).Coins(23.5f, 26.5f, 1f, 3)
                .Ledge(29.5f, 33f, 0.8f).Coins(30f, 32.5f, 1.6f, 3)
                .Ledge(31f, 33f, 2.6f).Gem(32f, 3.4f)
                .Ledge(35.5f, 39f, 0.2f).Coins(36f, 38.5f, 1f, 3)
                .Ground(41f, 70f, 0f).Checkpoint(43f, 0f).Item(ItemKind.Key, 45f, 0.7f)
                .Beetle(51f, 0f, 48f, 54f).Coins(47f, 55f, 0.8f, 5)
                .Gate(58f, 0f).Gem(61.5f, 0.8f).Item(ItemKind.StaminaPotion, 64f, 0.7f).Coins(65f, 69f, 0.8f, 3)
                .Ledge(72.5f, 75f, 0.5f).Coins(72.8f, 74.7f, 1.3f, 2).Bee(74f, 2.2f, 72f, 76f)
                .Ledge(77.5f, 80f, 1f).Coins(77.8f, 79.7f, 1.8f, 2)
                .Ground(82.5f, 112f, 0f).Checkpoint(84f, 0f).Coins(85f, 87f, 0.8f, 3)
                .Ledge(88f, 92f, 1.8f).Coins(88.5f, 91.5f, 2.6f, 3)
                .Ledge(94f, 97f, 3.6f).Gem(95.5f, 4.4f).Item(ItemKind.HealthPotion, 96.5f, 4.3f)
                .Bee(100f, 1.7f, 98f, 103f).Coins(99f, 103f, 0.8f, 3)
                .Goal(107f, 0f)
                .Wall(112f, 118f);
            return b.Level;
        }

        static Level Forest()
        {
            var b = new Builder(2, "Purple Forest", "Hedgehogs under the violet trees", Theme.Forest, 2f, 0f, 80f);
            b.Wall(-12f, -4f)
                .Ground(-4f, 18f, 0f).Coins(4f, 8f, 0.8f, 4).Hedgehog(12f, 0f, 9f, 15f)
                .Ground(18f, 26f, 1.5f).Coins(19f, 25f, 2.3f, 4)
                .Ground(26f, 34f, 3f).Coins(27f, 33f, 3.8f, 4).Item(ItemKind.ManaPotion, 30f, 3.7f)
                .Ground(37f, 52f, 1f).Checkpoint(39f, 1f)
                .Thorns(42f, 44f, 1f).Arc(40.5f, 45.5f, 2.2f, 1.2f, 5)
                .Hedgehog(48f, 1f, 45.5f, 50.5f)
                .Ground(55f, 80f, 0f).Item(ItemKind.ManaPotion, 57f, 0.7f)
                .Ledge(58f, 61f, 1.8f).Coins(58.5f, 60.5f, 2.6f, 3)
                .Ledge(62f, 65f, 3.4f).Gem(63.5f, 4.2f)
                .Item(ItemKind.Key, 67f, 0.7f).Hedgehog(69f, 0f, 67.5f, 70.5f)
                .Gate(72f, 0f).Gem(74f, 0.8f).Beetle(77f, 0f, 75f, 79f)
                .Gem(81.5f, 3.2f)
                .Ground(83f, 110f, 0.5f).Checkpoint(85f, 0.5f).Coins(86f, 89f, 1.3f, 4)
                .Hedgehog(92f, 0.5f, 90f, 95f).Thorns(97f, 98.5f, 0.5f).Arc(95.5f, 100f, 1.7f, 1.3f, 4)
                .Coins(101f, 103f, 1.3f, 3).Item(ItemKind.HealthPotion, 102f, 2.5f)
                .Goal(106f, 0.5f)
                .Wall(110f, 116f);
            return b.Level;
        }

        static Level Mountain()
        {
            var b = new Builder(3, "Snowy Path", "Up to the peaks behind the meadow", Theme.Mountain, 2f, 0f, 90f);
            b.Wall(-12f, -4f)
                .Ground(-4f, 14f, 0f).Coins(3f, 6f, 0.8f, 3).Beetle(9f, 0f, 6.5f, 12.5f)
                .Ground(14f, 20f, 1.5f).Coins(15f, 19f, 2.3f, 3)
                .Ground(20f, 26f, 3f).Coins(21f, 25f, 3.8f, 3)
                .Ground(29f, 40f, 3.5f).Checkpoint(31f, 3.5f).Bee(34f, 5.3f, 31f, 37f).Hedgehog(37f, 3.5f, 35f, 38.5f)
                .Coins(30f, 33f, 4.3f, 3)
                .Ledge(41.5f, 44f, 5f).Coins(42f, 43.5f, 5.8f, 2)
                .Ledge(46f, 49f, 6.5f).Gem(47.5f, 9f)
                .Ground(51f, 72f, 6f).Coins(52f, 54f, 6.8f, 3)
                .Thorns(55f, 57f, 6f).Arc(53.5f, 58.5f, 7.2f, 1.2f, 4)
                .Bee(60f, 7.9f, 57.5f, 62.5f).Item(ItemKind.Key, 61f, 6.7f)
                .Gate(64f, 6f).Gem(67f, 6.8f).Coins(68f, 71f, 6.8f, 3)
                .Ground(75f, 88f, 4f).Checkpoint(77f, 4f).Item(ItemKind.Elixir, 79f, 4.7f)
                .Hedgehog(84f, 4f, 81f, 86.5f).Coins(80f, 87f, 5.6f, 5)
                .Ledge(89.5f, 92f, 5.5f).Coins(90f, 91.5f, 6.3f, 2)
                .Ledge(93.5f, 96f, 7f).Gem(94.75f, 8.8f)
                .Ground(98f, 115f, 8f).Beetle(105f, 8f, 101f, 109f).Coins(99f, 103f, 8.8f, 4)
                .Item(ItemKind.HealthPotion, 100f, 8.7f)
                .Goal(111f, 8f)
                .Wall(115f, 121f, 16f);
            return b.Level;
        }

        /// <summary>Short-hand for laying out a level from left to right.</summary>
        sealed class Builder
        {
            const float Bottom = -8f;

            public readonly Level Level;

            public Builder(int index, string name, string tagline, Theme theme, float spawnX, float spawnY, float par)
            {
                Level = new Level
                {
                    Index = index,
                    Name = name,
                    Tagline = tagline,
                    Theme = theme,
                    Spawn = new Vec2(spawnX, spawnY),
                    ParSeconds = par,
                };
            }

            public Builder Ground(float minX, float maxX, float top)
            {
                Level.Ground.Add(new Box(minX, Bottom, maxX, top));
                return this;
            }

            /// <summary>A cliff too tall to climb, closing an end of the level.</summary>
            public Builder Wall(float minX, float maxX, float top = 9f) => Ground(minX, maxX, top);

            /// <summary>A one-way plank or branch you can jump up through.</summary>
            public Builder Ledge(float minX, float maxX, float top)
            {
                Level.Platforms.Add(new Box(minX, top - 0.35f, maxX, top));
                return this;
            }

            public Builder Coins(float fromX, float toX, float y, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    float t = count == 1 ? 0.5f : i / (count - 1f);
                    Level.Pickups.Add(new PickupSpec(PickupKind.Coin, ItemKind.None, new Vec2(fromX + (toX - fromX) * t, y)));
                }
                return this;
            }

            /// <summary>Coins along the arc of a jump, <paramref name="height"/> above <paramref name="y"/> at the middle.</summary>
            public Builder Arc(float fromX, float toX, float y, float height, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    float t = i / (count - 1f);
                    float lift = 4f * t * (1f - t) * height;
                    Level.Pickups.Add(new PickupSpec(PickupKind.Coin, ItemKind.None, new Vec2(fromX + (toX - fromX) * t, y + lift)));
                }
                return this;
            }

            public Builder Gem(float x, float y)
            {
                Level.Pickups.Add(new PickupSpec(PickupKind.Gem, ItemKind.None, new Vec2(x, y)));
                return this;
            }

            public Builder Item(ItemKind item, float x, float y)
            {
                Level.Pickups.Add(new PickupSpec(PickupKind.Item, item, new Vec2(x, y)));
                return this;
            }

            public Builder Thorns(float minX, float maxX, float y)
            {
                Level.Spikes.Add(new Box(minX, y, maxX, y + 0.6f));
                return this;
            }

            /// <summary>A locked gate taller than any jump, standing on the ground at <paramref name="y"/>.</summary>
            public Builder Gate(float x, float y)
            {
                Level.Gates.Add(new Box(x - 0.2f, y, x + 0.2f, y + 4.2f));
                return this;
            }

            public Builder Beetle(float x, float y, float minX, float maxX) => Critter(CritterKind.Beetle, x, y, minX, maxX);

            public Builder Bee(float x, float y, float minX, float maxX) => Critter(CritterKind.Bee, x, y, minX, maxX);

            public Builder Hedgehog(float x, float y, float minX, float maxX) => Critter(CritterKind.Hedgehog, x, y, minX, maxX);

            Builder Critter(CritterKind kind, float x, float y, float minX, float maxX)
            {
                Level.Critters.Add(new CritterSpec(kind, new Vec2(x, y), minX, maxX));
                return this;
            }

            public Builder Checkpoint(float x, float y)
            {
                Level.Checkpoints.Add(new Vec2(x, y));
                return this;
            }

            public Builder Goal(float x, float y)
            {
                Level.Goal = new Box(x - 1f, y, x + 1f, y + 3f);
                return this;
            }
        }
    }
}
