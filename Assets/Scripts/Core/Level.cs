using System;
using System.Collections.Generic;

namespace JolyWorld.Core
{
    public enum Theme
    {
        Meadow,
        Riverside,
        Forest,
        Mountain,
    }

    public enum PickupKind
    {
        Coin,
        Gem,
        Item,
    }

    public readonly struct PickupSpec
    {
        public readonly PickupKind Kind;
        public readonly ItemKind Item;
        public readonly Vec2 Position;

        public PickupSpec(PickupKind kind, ItemKind item, Vec2 position)
        {
            Kind = kind;
            Item = item;
            Position = position;
        }
    }

    public readonly struct CritterSpec
    {
        public readonly CritterKind Kind;
        public readonly Vec2 Home;
        public readonly float MinX;
        public readonly float MaxX;

        public CritterSpec(CritterKind kind, Vec2 home, float minX, float maxX)
        {
            Kind = kind;
            Home = home;
            MinX = minX;
            MaxX = maxX;
        }
    }

    /// <summary>One level as data: the ground, platforms, hazards, pickups, critters and the goal.</summary>
    public sealed class Level
    {
        public int Index;
        public string Name;
        public string Tagline;
        public Theme Theme;
        public Vec2 Spawn;
        public Box Goal;
        public float KillY = -4f;
        public float ParSeconds;
        public readonly List<Box> Ground = new List<Box>();
        public readonly List<Box> Platforms = new List<Box>();
        public readonly List<Box> Spikes = new List<Box>();
        public readonly List<Box> Gates = new List<Box>();
        public readonly List<Vec2> Checkpoints = new List<Vec2>();
        public readonly List<PickupSpec> Pickups = new List<PickupSpec>();
        public readonly List<CritterSpec> Critters = new List<CritterSpec>();

        /// <summary>Where the last piece of ground ends.</summary>
        public float Width
        {
            get
            {
                float right = 0f;
                foreach (Box box in Ground)
                {
                    right = Math.Max(right, box.MaxX);
                }
                return right;
            }
        }

        public int CoinCount => Count(PickupKind.Coin);
        public int GemCount => Count(PickupKind.Gem);

        int Count(PickupKind kind)
        {
            int count = 0;
            foreach (PickupSpec pickup in Pickups)
            {
                count += pickup.Kind == kind ? 1 : 0;
            }
            return count;
        }
    }
}
