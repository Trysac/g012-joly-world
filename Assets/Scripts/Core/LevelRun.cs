using System;
using System.Collections.Generic;

namespace JolyWorld.Core
{
    /// <summary>What the player asks of the hero this step. Presses are true for one step only.</summary>
    public struct HeroInput
    {
        public float Move;
        public bool Jump;
        public bool JumpHeld;
        public bool Sprint;
        public bool Dash;
        public bool Cast;
        /// <summary>The inventory slot to drink from this step, or -1.</summary>
        public int Use;

        public static HeroInput Idle => new HeroInput { Use = -1 };
    }

    /// <summary>The summary of a finished level for the results screen and the records.</summary>
    public struct LevelResult
    {
        public int Level;
        public int Coins;
        public int CoinTotal;
        public int GemMask;
        public int Gems;
        public int GemTotal;
        public float Seconds;
        public int KnockOuts;
    }

    /// <summary>
    /// One attempt at a level: the hero's body, bars and inventory, the critters, the pickups
    /// still lying around, the gates, checkpoints, the river and the goal. The game drives it with
    /// <see cref="Tick"/> and listens to its events for sounds, effects and the HUD.
    /// </summary>
    public sealed class LevelRun : ICollisionWorld
    {
        const float PickupReach = 0.45f;
        const float RespawnSafety = 1.5f;

        readonly List<Critter> critters = new List<Critter>();
        readonly List<Bolt> bolts = new List<Bolt>();
        readonly List<Box> solids = new List<Box>();
        readonly bool[] taken;
        readonly bool[] gateOpen;
        readonly int[] gemIndex;
        int lockedGate = -1;
        float castCooldown;

        public LevelRun(Level level, Inventory inventory = null)
        {
            Level = level;
            Hero = new Motor();
            Vitals = new Vitals();
            Inventory = inventory ?? Inventory.Starter();
            taken = new bool[level.Pickups.Count];
            gateOpen = new bool[level.Gates.Count];
            gemIndex = new int[level.Pickups.Count];
            int gems = 0;
            for (int i = 0; i < level.Pickups.Count; i++)
            {
                gemIndex[i] = level.Pickups[i].Kind == PickupKind.Gem ? gems++ : -1;
            }
            foreach (CritterSpec spec in level.Critters)
            {
                critters.Add(new Critter(spec.Kind, spec.Home, spec.MinX, spec.MaxX));
            }
            Checkpoint = -1;
            RespawnPoint = level.Spawn;
            Hero.Place(level.Spawn);
            RebuildSolids();
        }

        public Level Level { get; }
        public Motor Hero { get; }
        public Vitals Vitals { get; }
        public Inventory Inventory { get; }
        public IReadOnlyList<Critter> Critters => critters;
        public IReadOnlyList<Bolt> Bolts => bolts;

        /// <summary>The ground and the gates that are still shut.</summary>
        public IReadOnlyList<Box> Solids => solids;

        public IReadOnlyList<Box> Platforms => Level.Platforms;
        public int Coins { get; private set; }

        /// <summary>Bit i is set when the level's i-th gem was found in this run.</summary>
        public int GemMask { get; private set; }

        public int Gems
        {
            get
            {
                int count = 0;
                for (int mask = GemMask; mask != 0; mask &= mask - 1)
                {
                    count++;
                }
                return count;
            }
        }

        public float Seconds { get; private set; }
        public int KnockOuts { get; private set; }

        /// <summary>The last checkpoint passed, or -1 for the start.</summary>
        public int Checkpoint { get; private set; }

        public Vec2 RespawnPoint { get; private set; }
        public bool Finished { get; private set; }

        public bool Taken(int pickup) => pickup >= 0 && pickup < taken.Length && taken[pickup];

        public bool GateOpen(int gate) => gate >= 0 && gate < gateOpen.Length && gateOpen[gate];

        public LevelResult Result => new LevelResult
        {
            Level = Level.Index,
            Coins = Coins,
            CoinTotal = Level.CoinCount,
            GemMask = GemMask,
            Gems = Gems,
            GemTotal = Level.GemCount,
            Seconds = Seconds,
            KnockOuts = KnockOuts,
        };

        public event Action<int> PickedUp;
        public event Action<int> Stomped;
        public event Action<int> Popped;
        public event Action Hurt;
        public event Action Fell;
        public event Action KnockedOut;
        public event Action Respawned;
        public event Action<int> GateOpened;
        public event Action<int> GateLocked;
        public event Action<int> CheckpointReached;
        public event Action Completed;
        public event Action Jumped;
        public event Action Landed;
        public event Action Dashed;
        public event Action Cast;
        public event Action CastFailed;
        public event Action<ItemKind> Drank;
        public event Action<UseResult> UseFailed;

        public void Tick(HeroInput input, float dt)
        {
            if (Finished)
            {
                return;
            }
            Seconds += dt;
            Vitals.Tick(dt);
            castCooldown = Math.Max(0f, castCooldown - dt);

            if (input.Use >= 0)
            {
                ItemKind kind = Inventory[input.Use].Kind;
                UseResult used = Inventory.Use(input.Use, Vitals);
                if (used == UseResult.Used)
                {
                    Drank?.Invoke(kind);
                }
                else
                {
                    UseFailed?.Invoke(used);
                }
            }

            var motorInput = new MotorInput
            {
                Move = input.Move,
                JumpPressed = input.Jump,
                JumpHeld = input.JumpHeld,
                Sprint = input.Sprint,
                DashPressed = input.Dash,
            };
            MotorEvents events = Hero.Tick(motorInput, dt, this, Vitals.Stamina);
            if ((events & MotorEvents.Jumped) != 0) Jumped?.Invoke();
            if ((events & MotorEvents.Landed) != 0) Landed?.Invoke();
            if ((events & MotorEvents.Dashed) != 0) Dashed?.Invoke();

            if (input.Cast)
            {
                TryCast();
            }
            TickBolts(dt);
            TickCritters(dt);
            CheckSpikes();
            CheckPickups();
            CheckGates();
            CheckCheckpoints();

            if (Hero.Position.Y < Level.KillY)
            {
                Vitals.Health.Drain(Combat.FallDamage);
                Fell?.Invoke();
                if (!Vitals.Dead)
                {
                    Respawn();
                }
            }
            if (Vitals.Dead)
            {
                KnockOuts++;
                Vitals.Revive();
                KnockedOut?.Invoke();
                Respawn();
            }
            if (!Finished && Hero.Bounds.Overlaps(Level.Goal))
            {
                Finished = true;
                Completed?.Invoke();
            }
        }

        void TryCast()
        {
            if (castCooldown > 0f)
            {
                return;
            }
            if (!Vitals.Mana.Spend(Combat.BoltCost))
            {
                CastFailed?.Invoke();
                return;
            }
            castCooldown = Combat.BoltCooldown;
            Vec2 from = Hero.Position + new Vec2(Hero.Facing * (Motor.HalfWidth + 0.2f), Motor.Height * 0.55f);
            bolts.Add(new Bolt(from, Hero.Facing));
            Cast?.Invoke();
        }

        void TickBolts(float dt)
        {
            for (int b = bolts.Count - 1; b >= 0; b--)
            {
                Bolt bolt = bolts[b];
                bool alive = bolt.Tick(dt, this);
                if (alive)
                {
                    for (int c = 0; c < critters.Count; c++)
                    {
                        if (critters[c].Alive && bolt.Bounds.Overlaps(critters[c].Bounds))
                        {
                            critters[c].Defeat();
                            bolt.Fizzle();
                            alive = false;
                            Popped?.Invoke(c);
                            break;
                        }
                    }
                }
                if (!alive)
                {
                    bolts.RemoveAt(b);
                }
            }
        }

        void TickCritters(float dt)
        {
            for (int c = 0; c < critters.Count; c++)
            {
                Critter critter = critters[c];
                critter.Tick(dt);
                switch (Combat.Touch(Hero, critter))
                {
                    case Contact.Stomp:
                        critter.Defeat();
                        Hero.Bounce(Combat.StompBounce);
                        Stomped?.Invoke(c);
                        break;
                    case Contact.Hurt:
                        HurtHero(Combat.TouchDamage, critter.Position.X);
                        break;
                }
            }
        }

        void CheckSpikes()
        {
            foreach (Box spikes in Level.Spikes)
            {
                if (Hero.Bounds.Overlaps(spikes))
                {
                    HurtHero(Combat.SpikeDamage, spikes.Center.X);
                    return;
                }
            }
        }

        void HurtHero(float damage, float fromX)
        {
            if (!Vitals.Hurt(damage))
            {
                return;
            }
            float away = Hero.Position.X < fromX ? -1f : 1f;
            Hero.Knock(away * Combat.KnockbackX, Combat.KnockbackY);
            Hurt?.Invoke();
        }

        void CheckPickups()
        {
            Box reach = Hero.Bounds.Grow(PickupReach * 0.5f, PickupReach * 0.5f);
            for (int i = 0; i < taken.Length; i++)
            {
                if (taken[i] || !reach.Contains(Level.Pickups[i].Position))
                {
                    continue;
                }
                PickupSpec pickup = Level.Pickups[i];
                switch (pickup.Kind)
                {
                    case PickupKind.Coin:
                        Coins++;
                        break;
                    case PickupKind.Gem:
                        GemMask |= 1 << gemIndex[i];
                        break;
                    case PickupKind.Item:
                        if (Inventory.Add(pickup.Item) == 0)
                        {
                            continue;
                        }
                        break;
                }
                taken[i] = true;
                PickedUp?.Invoke(i);
            }
        }

        void CheckGates()
        {
            Box touch = Hero.Bounds.Grow(0.08f, -0.05f);
            int pushing = -1;
            for (int g = 0; g < gateOpen.Length; g++)
            {
                if (gateOpen[g] || !touch.Overlaps(Level.Gates[g]))
                {
                    continue;
                }
                if (Inventory.Take(ItemKind.Key))
                {
                    gateOpen[g] = true;
                    RebuildSolids();
                    GateOpened?.Invoke(g);
                }
                else
                {
                    pushing = g;
                    if (lockedGate != g)
                    {
                        GateLocked?.Invoke(g);
                    }
                }
            }
            lockedGate = pushing;
        }

        void CheckCheckpoints()
        {
            for (int i = Checkpoint + 1; i < Level.Checkpoints.Count; i++)
            {
                Vec2 point = Level.Checkpoints[i];
                if (Hero.Position.X >= point.X && Hero.Grounded)
                {
                    Checkpoint = i;
                    RespawnPoint = point;
                    CheckpointReached?.Invoke(i);
                }
            }
        }

        void Respawn()
        {
            Hero.Place(RespawnPoint);
            Vitals.Protect(RespawnSafety);
            bolts.Clear();
            Respawned?.Invoke();
        }

        void RebuildSolids()
        {
            solids.Clear();
            solids.AddRange(Level.Ground);
            for (int g = 0; g < gateOpen.Length; g++)
            {
                if (!gateOpen[g])
                {
                    solids.Add(Level.Gates[g]);
                }
            }
        }
    }
}
