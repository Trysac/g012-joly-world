using System;

namespace JolyWorld.Core
{
    public enum CritterKind
    {
        /// <summary>Walks back and forth; a jump on its shell defeats it.</summary>
        Beetle,
        /// <summary>Flies back and forth, bobbing up and down; can be stomped.</summary>
        Bee,
        /// <summary>Spiky: stomping it hurts, so it takes a magic bolt (or a jump over it).</summary>
        Hedgehog,
    }

    /// <summary>A small creature patrolling between two points.</summary>
    public sealed class Critter
    {
        public const float BobHeight = 0.6f;
        const float BobRate = 2.6f;

        float age;

        public Critter(CritterKind kind, Vec2 home, float minX, float maxX)
        {
            Kind = kind;
            Home = home;
            Position = home;
            MinX = Math.Min(minX, maxX);
            MaxX = Math.Max(minX, maxX);
            Direction = 1;
            Alive = true;
        }

        public CritterKind Kind { get; }
        public Vec2 Home { get; }

        /// <summary>The middle of the critter's feet.</summary>
        public Vec2 Position { get; private set; }

        public float MinX { get; }
        public float MaxX { get; }
        public int Direction { get; private set; }
        public bool Alive { get; private set; }

        public float Speed => Kind switch
        {
            CritterKind.Bee => 2f,
            CritterKind.Hedgehog => 1.1f,
            _ => 1.6f,
        };

        public bool Stompable => Kind != CritterKind.Hedgehog;

        public Box Bounds => Kind switch
        {
            CritterKind.Bee => Box.FromFeet(Position, 0.35f, 0.5f),
            CritterKind.Hedgehog => Box.FromFeet(Position, 0.45f, 0.55f),
            _ => Box.FromFeet(Position, 0.45f, 0.6f),
        };

        public void Tick(float dt)
        {
            if (!Alive)
            {
                return;
            }
            age += dt;
            float x = Position.X + Direction * Speed * dt;
            if (x >= MaxX)
            {
                x = MaxX;
                Direction = -1;
            }
            else if (x <= MinX)
            {
                x = MinX;
                Direction = 1;
            }
            float y = Kind == CritterKind.Bee ? Home.Y + BobHeight * (float)Math.Sin(age * BobRate) : Home.Y;
            Position = new Vec2(x, y);
        }

        public void Defeat() => Alive = false;
    }

    public enum Contact
    {
        None,
        /// <summary>The hero came down on top of the critter and defeats it.</summary>
        Stomp,
        /// <summary>The critter hurts the hero.</summary>
        Hurt,
    }

    /// <summary>The hero's magic: a small spark that flies straight and pops the first critter it meets.</summary>
    public sealed class Bolt
    {
        const float Radius = 0.25f;
        const float MaxStep = 0.2f;

        public Bolt(Vec2 position, int direction)
        {
            Position = position;
            Direction = direction >= 0 ? 1 : -1;
            Life = Combat.BoltLife;
            Alive = true;
        }

        /// <summary>The centre of the spark.</summary>
        public Vec2 Position { get; private set; }

        public int Direction { get; }
        public float Life { get; private set; }
        public bool Alive { get; private set; }
        public Box Bounds => Box.Around(Position, Radius, Radius);

        /// <summary>Flies on; false once the bolt has faded or hit a wall.</summary>
        public bool Tick(float dt, ICollisionWorld world)
        {
            if (!Alive)
            {
                return false;
            }
            float distance = Combat.BoltSpeed * dt;
            int steps = Math.Max(1, (int)Math.Ceiling(distance / MaxStep));
            for (int i = 0; i < steps; i++)
            {
                Position = new Vec2(Position.X + Direction * distance / steps, Position.Y);
                foreach (Box solid in world.Solids)
                {
                    if (Bounds.Overlaps(solid))
                    {
                        Alive = false;
                        return false;
                    }
                }
            }
            Life -= dt;
            if (Life <= 0f)
            {
                Alive = false;
            }
            return Alive;
        }

        public void Fizzle() => Alive = false;
    }

    public static class Combat
    {
        public const float TouchDamage = 20f;
        public const float SpikeDamage = 25f;
        public const float FallDamage = 25f;
        public const float StompBounce = 10f;
        public const float KnockbackX = 7f;
        public const float KnockbackY = 7f;
        public const float BoltCost = 25f;
        public const float BoltSpeed = 15f;
        public const float BoltLife = 0.8f;
        public const float BoltCooldown = 0.3f;

        /// <summary>What happens when the hero and a critter meet.</summary>
        public static Contact Touch(Motor hero, Critter critter)
        {
            if (!critter.Alive || !hero.Bounds.Overlaps(critter.Bounds))
            {
                return Contact.None;
            }
            bool fromAbove = hero.Velocity.Y < 0f && hero.Position.Y >= critter.Bounds.Center.Y;
            return fromAbove && critter.Stompable ? Contact.Stomp : Contact.Hurt;
        }
    }
}
