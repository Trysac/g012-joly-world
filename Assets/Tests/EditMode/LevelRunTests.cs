using System.Collections.Generic;
using NUnit.Framework;

namespace JolyWorld.Core.Tests
{
    public class LevelRunTests
    {
        static HeroInput Walk(float move = 1f) => new HeroInput { Move = move, Use = -1 };

        /// <summary>Flat ground from x = -10 to 60, the hero at x = 0 and the goal at x = 50.</summary>
        static Level Flat()
        {
            var level = new Level { Name = "Test", Spawn = new Vec2(0f, 0f), Goal = new Box(50f, 0f, 52f, 3f), KillY = -4f };
            level.Ground.Add(new Box(-10f, -6f, 60f, 0f));
            return level;
        }

        [Test]
        public void ARunStartsAtTheSpawnWithFullBarsAndTheStarterKit()
        {
            Level level = Flat();
            level.Critters.Add(new CritterSpec(CritterKind.Beetle, new Vec2(20f, 0f), 18f, 22f));
            var run = new LevelRun(level);

            Assert.That(run.Level, Is.SameAs(level));
            Assert.That(run.Hero.Position, Is.EqualTo(level.Spawn));
            Assert.That(run.Vitals.Health.IsFull && run.Vitals.Mana.IsFull && run.Vitals.Stamina.IsFull, Is.True);
            Assert.That(run.Inventory.Count(ItemKind.HealthPotion), Is.EqualTo(2));
            Assert.That(run.Coins, Is.EqualTo(0));
            Assert.That(run.Gems, Is.EqualTo(0));
            Assert.That(run.Seconds, Is.EqualTo(0f));
            Assert.That(run.Checkpoint, Is.EqualTo(-1));
            Assert.That(run.RespawnPoint, Is.EqualTo(level.Spawn));
            Assert.That(run.Critters.Count, Is.EqualTo(1));
            Assert.That(run.Solids.Count, Is.EqualTo(1));
            Assert.That(run.Finished, Is.False);
        }

        [Test]
        public void AGivenInventoryIsUsedInsteadOfTheStarterKit()
        {
            var inventory = new Inventory();
            var run = new LevelRun(Flat(), inventory);

            Assert.That(run.Inventory, Is.SameAs(inventory));
        }

        [Test]
        public void TimeRunsWhileYouPlay()
        {
            var run = new LevelRun(Flat());
            Sim.Run(run, HeroInput.Idle, 0.5f);

            Assert.That(run.Seconds, Is.EqualTo(0.5f).Within(1e-3f));
        }

        [Test]
        public void ACoinIsCollectedOnceWhenTouched()
        {
            Level level = Flat();
            level.Pickups.Add(new PickupSpec(PickupKind.Coin, ItemKind.None, new Vec2(3f, 0.6f)));
            var run = new LevelRun(level);
            var picked = new List<int>();
            run.PickedUp += picked.Add;

            Sim.Run(run, Walk(), 1f);
            Sim.Run(run, Walk(-1f), 0.4f);
            Sim.Run(run, Walk(), 0.4f);

            Assert.That(run.Coins, Is.EqualTo(1));
            Assert.That(run.Taken(0), Is.True);
            Assert.That(picked, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void GemsAreCountedAndRememberedByTheirOrder()
        {
            Level level = Flat();
            level.Pickups.Add(new PickupSpec(PickupKind.Coin, ItemKind.None, new Vec2(30f, 0.6f)));
            level.Pickups.Add(new PickupSpec(PickupKind.Gem, ItemKind.None, new Vec2(3f, 0.6f)));
            level.Pickups.Add(new PickupSpec(PickupKind.Gem, ItemKind.None, new Vec2(30f, 0.6f)));
            level.Pickups.Add(new PickupSpec(PickupKind.Gem, ItemKind.None, new Vec2(6f, 0.6f)));
            var run = new LevelRun(level);

            Sim.Run(run, Walk(), 1.5f);

            Assert.That(run.Gems, Is.EqualTo(2));
            Assert.That(run.GemMask, Is.EqualTo(0b101), "the first and the third gem");
            Assert.That(run.Taken(1) && run.Taken(3), Is.True);
            Assert.That(run.Taken(2), Is.False);
        }

        [Test]
        public void AnItemGoesIntoTheInventory()
        {
            Level level = Flat();
            level.Pickups.Add(new PickupSpec(PickupKind.Item, ItemKind.ManaPotion, new Vec2(3f, 0.6f)));
            var run = new LevelRun(level);

            Sim.Run(run, Walk(), 1f);

            Assert.That(run.Inventory.Count(ItemKind.ManaPotion), Is.EqualTo(1));
            Assert.That(run.Taken(0), Is.True);
        }

        [Test]
        public void AnItemStaysWhereItIsWhenTheInventoryIsFull()
        {
            Level level = Flat();
            level.Pickups.Add(new PickupSpec(PickupKind.Item, ItemKind.ManaPotion, new Vec2(3f, 0.6f)));
            var full = new Inventory();
            full.Add(ItemKind.Key, 3 * Inventory.Size);
            var run = new LevelRun(level, full);

            Sim.Run(run, Walk(), 1f);

            Assert.That(run.Taken(0), Is.False);
            Assert.That(run.Inventory.Count(ItemKind.ManaPotion), Is.EqualTo(0));
        }

        [Test]
        public void JumpingOnABeetleDefeatsItAndBouncesTheHero()
        {
            Level level = Flat();
            level.Critters.Add(new CritterSpec(CritterKind.Beetle, new Vec2(3f, 0f), 3f, 3f));
            var run = new LevelRun(level);
            var stomped = new List<int>();
            run.Stomped += stomped.Add;
            run.Hero.Place(new Vec2(3f, 2.5f));

            float bounce = 0f;
            for (int i = 0; i < 60 && stomped.Count == 0; i++)
            {
                run.Tick(HeroInput.Idle, Sim.Step);
                bounce = run.Hero.Velocity.Y;
            }

            Assert.That(stomped, Is.EqualTo(new[] { 0 }));
            Assert.That(run.Critters[0].Alive, Is.False);
            Assert.That(bounce, Is.GreaterThan(0f));
            Assert.That(run.Vitals.Health.IsFull, Is.True);

            Sim.Run(run, Walk(), 1f);
            Assert.That(run.Vitals.Health.IsFull, Is.True, "a defeated beetle is harmless");
        }

        [Test]
        public void WalkingIntoACritterHurtsAndKnocksTheHeroBack()
        {
            Level level = Flat();
            level.Critters.Add(new CritterSpec(CritterKind.Beetle, new Vec2(3f, 0f), 3f, 3f));
            var run = new LevelRun(level);
            int hurts = 0;
            run.Hurt += () => hurts++;

            float pushed = 0f;
            for (int i = 0; i < 90 && hurts == 0; i++)
            {
                run.Tick(Walk(), Sim.Step);
                pushed = run.Hero.Velocity.X;
            }

            Assert.That(hurts, Is.EqualTo(1));
            Assert.That(run.Vitals.Health.Current, Is.EqualTo(Vitals.MaxHealth - Combat.TouchDamage));
            Assert.That(pushed, Is.LessThan(0f));
            Assert.That(run.Vitals.Invulnerable, Is.True);
            Assert.That(run.Critters[0].Alive, Is.True);
        }

        [Test]
        public void ThornsHurt()
        {
            Level level = Flat();
            level.Spikes.Add(new Box(3f, 0f, 4f, 0.6f));
            var run = new LevelRun(level);
            int hurts = 0;
            run.Hurt += () => hurts++;

            Sim.Run(run, Walk(), 1f);

            Assert.That(hurts, Is.EqualTo(1));
            Assert.That(run.Vitals.Health.Current, Is.EqualTo(Vitals.MaxHealth - Combat.SpikeDamage));
        }

        [Test]
        public void AGateStaysShutWithoutAKey()
        {
            Level level = Flat();
            level.Gates.Add(new Box(5f, 0f, 5.4f, 4f));
            var run = new LevelRun(level);
            var locked = new List<int>();
            run.GateLocked += locked.Add;

            Sim.Run(run, Walk(), 2f);

            Assert.That(run.GateOpen(0), Is.False);
            Assert.That(run.Hero.Position.X, Is.LessThanOrEqualTo(5f - Motor.HalfWidth + 1e-3f));
            Assert.That(locked, Is.EqualTo(new[] { 0 }), "said once while pushing against it");
            Assert.That(run.Solids.Count, Is.EqualTo(2));
        }

        [Test]
        public void AKeyOpensAGateAndIsUsedUp()
        {
            Level level = Flat();
            level.Gates.Add(new Box(5f, 0f, 5.4f, 4f));
            level.Pickups.Add(new PickupSpec(PickupKind.Item, ItemKind.Key, new Vec2(2f, 0.6f)));
            var run = new LevelRun(level);
            var opened = new List<int>();
            run.GateOpened += opened.Add;

            Sim.Run(run, Walk(), 2f);

            Assert.That(opened, Is.EqualTo(new[] { 0 }));
            Assert.That(run.GateOpen(0), Is.True);
            Assert.That(run.Inventory.Count(ItemKind.Key), Is.EqualTo(0));
            Assert.That(run.Hero.Position.X, Is.GreaterThan(7f));
            Assert.That(run.Solids.Count, Is.EqualTo(1));
        }

        [Test]
        public void PassingACheckpointMovesTheRespawnPoint()
        {
            Level level = Flat();
            level.Checkpoints.Add(new Vec2(4f, 0f));
            level.Checkpoints.Add(new Vec2(30f, 0f));
            var run = new LevelRun(level);
            var reached = new List<int>();
            run.CheckpointReached += reached.Add;

            Sim.Run(run, Walk(), 1.2f);

            Assert.That(reached, Is.EqualTo(new[] { 0 }));
            Assert.That(run.Checkpoint, Is.EqualTo(0));
            Assert.That(run.RespawnPoint, Is.EqualTo(new Vec2(4f, 0f)));
        }

        [Test]
        public void FallingIntoTheRiverCostsHealthAndReturnsToTheCheckpoint()
        {
            var level = new Level { Spawn = new Vec2(0f, 0f), Goal = new Box(50f, 0f, 52f, 3f), KillY = -4f };
            level.Ground.Add(new Box(-10f, -6f, 4f, 0f));
            level.Ground.Add(new Box(8f, -6f, 60f, 0f));
            level.Pickups.Add(new PickupSpec(PickupKind.Coin, ItemKind.None, new Vec2(2f, 0.6f)));
            var run = new LevelRun(level);
            int fell = 0, respawned = 0;
            run.Fell += () => fell++;
            run.Respawned += () => respawned++;

            Sim.Run(run, Walk(), 1.4f);

            Assert.That(fell, Is.EqualTo(1));
            Assert.That(respawned, Is.EqualTo(1));
            Assert.That(run.Vitals.Health.Current, Is.EqualTo(Vitals.MaxHealth - Combat.FallDamage));
            Assert.That(run.Coins, Is.EqualTo(1), "coins are kept");
            Assert.That(run.KnockOuts, Is.EqualTo(0));
            Assert.That(run.Vitals.Invulnerable, Is.True, "a moment of safety after coming back");
            Assert.That(run.Hero.Position.X, Is.LessThan(4f));
            Assert.That(run.Hero.Position.Y, Is.GreaterThan(-1f));
        }

        [Test]
        public void RunningOutOfHealthKnocksTheHeroOutBackToTheCheckpoint()
        {
            Level level = Flat();
            var run = new LevelRun(level);
            int knocked = 0, respawned = 0;
            run.KnockedOut += () => knocked++;
            run.Respawned += () => respawned++;
            Sim.Run(run, Walk(), 1f);
            run.Vitals.Mana.Spend(60f);

            run.Vitals.Hurt(500f);
            run.Tick(HeroInput.Idle, Sim.Step);

            Assert.That(knocked, Is.EqualTo(1));
            Assert.That(respawned, Is.EqualTo(1));
            Assert.That(run.KnockOuts, Is.EqualTo(1));
            Assert.That(run.Vitals.Health.IsFull && run.Vitals.Mana.IsFull, Is.True);
            Assert.That(run.Hero.Position, Is.EqualTo(level.Spawn));
        }

        [Test]
        public void CastingSendsABoltTheWayTheHeroFacesForMana()
        {
            var run = new LevelRun(Flat());
            int casts = 0;
            run.Cast += () => casts++;
            Sim.Run(run, Walk(-1f), 0.1f);

            run.Tick(new HeroInput { Cast = true, Use = -1 }, Sim.Step);

            Assert.That(casts, Is.EqualTo(1));
            Assert.That(run.Vitals.Mana.Current, Is.EqualTo(Vitals.MaxMana - Combat.BoltCost));
            Assert.That(run.Bolts.Count, Is.EqualTo(1));
            Assert.That(run.Bolts[0].Direction, Is.EqualTo(-1));

            run.Tick(new HeroInput { Cast = true, Use = -1 }, Sim.Step);
            Assert.That(casts, Is.EqualTo(1), "the spell needs a moment between casts");
        }

        [Test]
        public void WithoutManaTheSpellFails()
        {
            var run = new LevelRun(Flat());
            int failed = 0, casts = 0;
            run.CastFailed += () => failed++;
            run.Cast += () => casts++;
            run.Vitals.Mana.Spend(Vitals.MaxMana - Combat.BoltCost + 1f);

            run.Tick(new HeroInput { Cast = true, Use = -1 }, Sim.Step);

            Assert.That(failed, Is.EqualTo(1));
            Assert.That(casts, Is.EqualTo(0));
            Assert.That(run.Bolts.Count, Is.EqualTo(0));
        }

        [Test]
        public void ABoltPopsAHedgehog()
        {
            Level level = Flat();
            level.Critters.Add(new CritterSpec(CritterKind.Hedgehog, new Vec2(5f, 0f), 5f, 5f));
            var run = new LevelRun(level);
            var popped = new List<int>();
            run.Popped += popped.Add;

            run.Tick(new HeroInput { Cast = true, Use = -1 }, Sim.Step);
            Sim.Run(run, HeroInput.Idle, 0.5f);

            Assert.That(popped, Is.EqualTo(new[] { 0 }));
            Assert.That(run.Critters[0].Alive, Is.False);
            Assert.That(run.Bolts.Count, Is.EqualTo(0));
            Assert.That(run.Vitals.Health.IsFull, Is.True);
        }

        [Test]
        public void UsingAHotbarSlotDrinksThePotion()
        {
            var run = new LevelRun(Flat());
            var drank = new List<ItemKind>();
            var failed = new List<UseResult>();
            run.Drank += drank.Add;
            run.UseFailed += failed.Add;

            run.Tick(new HeroInput { Use = 0 }, Sim.Step);
            Assert.That(failed, Is.EqualTo(new[] { UseResult.NoEffect }), "health is already full");

            run.Vitals.Hurt(50f);
            run.Tick(new HeroInput { Use = 0 }, Sim.Step);

            Assert.That(drank, Is.EqualTo(new[] { ItemKind.HealthPotion }));
            Assert.That(run.Vitals.Health.Current, Is.EqualTo(90f));
            Assert.That(run.Inventory[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void ReachingTheGoalFinishesTheLevelOnce()
        {
            var run = new LevelRun(Flat());
            int completed = 0;
            run.Completed += () => completed++;

            Sim.Run(run, Walk(), 12f);
            float seconds = run.Seconds;
            Sim.Run(run, Walk(), 1f);

            Assert.That(run.Finished, Is.True);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(run.Seconds, Is.EqualTo(seconds), "the clock stops at the goal");
            Assert.That(seconds, Is.InRange(8f, 9f));
        }

        [Test]
        public void TheResultSumsUpTheRun()
        {
            Level level = Flat();
            level.Index = 2;
            level.Pickups.Add(new PickupSpec(PickupKind.Coin, ItemKind.None, new Vec2(3f, 0.6f)));
            level.Pickups.Add(new PickupSpec(PickupKind.Coin, ItemKind.None, new Vec2(4f, 0.6f)));
            level.Pickups.Add(new PickupSpec(PickupKind.Coin, ItemKind.None, new Vec2(40f, 5f)));
            level.Pickups.Add(new PickupSpec(PickupKind.Gem, ItemKind.None, new Vec2(40f, 5f)));
            level.Pickups.Add(new PickupSpec(PickupKind.Gem, ItemKind.None, new Vec2(10f, 0.6f)));
            var run = new LevelRun(level);
            run.Vitals.Hurt(500f);
            Sim.Run(run, Walk(), 12f);

            LevelResult result = run.Result;

            Assert.That(result.Level, Is.EqualTo(2));
            Assert.That(result.Coins, Is.EqualTo(2));
            Assert.That(result.CoinTotal, Is.EqualTo(3));
            Assert.That(result.Gems, Is.EqualTo(1));
            Assert.That(result.GemMask, Is.EqualTo(0b10));
            Assert.That(result.GemTotal, Is.EqualTo(2));
            Assert.That(result.Seconds, Is.EqualTo(run.Seconds));
            Assert.That(result.KnockOuts, Is.EqualTo(1));
        }
    }
}
