using System;

namespace JolyWorld.Core
{
    /// <summary>
    /// A resource bar such as health, mana or stamina: it can be spent, drained and restored, and
    /// it may refill on its own a while after it was last used.
    /// </summary>
    public sealed class Meter
    {
        float sinceUsed;

        public Meter(float max, float regenPerSecond = 0f, float regenDelay = 0f)
        {
            Max = max;
            Current = max;
            RegenPerSecond = regenPerSecond;
            RegenDelay = regenDelay;
        }

        public float Max { get; }
        public float Current { get; private set; }
        public float RegenPerSecond { get; }
        public float RegenDelay { get; }

        public float Fraction => Max > 0f ? Current / Max : 0f;
        public bool IsFull => Current >= Max;
        public bool IsEmpty => Current <= 0f;

        /// <summary>Takes the whole amount, or nothing when there is not enough.</summary>
        public bool Spend(float amount)
        {
            if (amount < 0f || amount > Current + 1e-4f)
            {
                return false;
            }
            Current = Math.Max(0f, Current - amount);
            sinceUsed = 0f;
            return true;
        }

        /// <summary>Takes up to <paramref name="amount"/> and returns how much it took.</summary>
        public float Drain(float amount)
        {
            float taken = Math.Min(Math.Max(0f, amount), Current);
            if (taken > 0f)
            {
                Current -= taken;
                sinceUsed = 0f;
            }
            return taken;
        }

        /// <summary>Adds up to <paramref name="amount"/> without passing the maximum; returns what it added.</summary>
        public float Restore(float amount)
        {
            float gained = Math.Min(Math.Max(0f, amount), Max - Current);
            Current += gained;
            return gained;
        }

        public void Fill() => Current = Max;

        public void Tick(float dt)
        {
            sinceUsed += dt;
            if (RegenPerSecond <= 0f || sinceUsed <= RegenDelay)
            {
                return;
            }
            float regenTime = Math.Min(dt, sinceUsed - RegenDelay);
            Current = Math.Min(Max, Current + RegenPerSecond * regenTime);
        }
    }

    /// <summary>The hero's health, mana and stamina, and the moment of safety after a hit.</summary>
    public sealed class Vitals
    {
        public const float MaxHealth = 100f;
        public const float MaxMana = 100f;
        public const float MaxStamina = 100f;
        public const float ManaRegen = 6f;
        public const float ManaRegenDelay = 1.5f;
        public const float StaminaRegen = 40f;
        public const float StaminaRegenDelay = 0.6f;
        public const float InvulnerableSeconds = 1.2f;

        public Meter Health { get; } = new Meter(MaxHealth);
        public Meter Mana { get; } = new Meter(MaxMana, ManaRegen, ManaRegenDelay);
        public Meter Stamina { get; } = new Meter(MaxStamina, StaminaRegen, StaminaRegenDelay);

        public float InvulnerableLeft { get; private set; }
        public bool Invulnerable => InvulnerableLeft > 0f;
        public bool Dead => Health.IsEmpty;

        /// <summary>Takes health unless the hero is still safe from the last hit; true when it landed.</summary>
        public bool Hurt(float damage)
        {
            if (damage <= 0f || Invulnerable || Dead)
            {
                return false;
            }
            Health.Drain(damage);
            InvulnerableLeft = InvulnerableSeconds;
            return true;
        }

        /// <summary>Makes the hero safe from hits for a while (after coming back, for example).</summary>
        public void Protect(float seconds) => InvulnerableLeft = Math.Max(InvulnerableLeft, seconds);

        public void Tick(float dt)
        {
            InvulnerableLeft = Math.Max(0f, InvulnerableLeft - dt);
            Mana.Tick(dt);
            Stamina.Tick(dt);
        }

        public void Revive()
        {
            Health.Fill();
            Mana.Fill();
            Stamina.Fill();
            InvulnerableLeft = 0f;
        }
    }
}
