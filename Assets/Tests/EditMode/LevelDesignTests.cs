using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace JolyWorld.Core.Tests
{
    public class ReachabilityTests
    {
        static Box Top(float minX, float maxX, float top) => new Box(minX, top - 1f, maxX, top);

        [Test]
        public void AGapWithinTheJumpCanBeCrossed()
        {
            float reach = Motor.JumpReach(0f) * Reachability.Margin;

            Assert.That(Reachability.CanJump(Top(0f, 5f, 0f), Top(5f + reach - 0.1f, 20f, 0f)), Is.True);
            Assert.That(Reachability.CanJump(Top(0f, 5f, 0f), Top(5f + reach + 0.1f, 20f, 0f)), Is.False);
        }

        [Test]
        public void TooHighIsTooHighEvenRightAbove()
        {
            float high = Motor.MaxJumpHeight * Reachability.Margin;

            Assert.That(Reachability.CanJump(Top(0f, 5f, 0f), Top(2f, 4f, high - 0.1f)), Is.True);
            Assert.That(Reachability.CanJump(Top(0f, 5f, 0f), Top(2f, 4f, high + 0.1f)), Is.False);
        }

        [Test]
        public void DroppingDownReachesFurther()
        {
            float flat = Motor.JumpReach(0f) * Reachability.Margin;

            Assert.That(Reachability.CanJump(Top(0f, 5f, 3f), Top(5f + flat + 0.5f, 20f, 0f)), Is.True);
        }

        [Test]
        public void ADashAddsDistance()
        {
            float gap = Motor.JumpReach(0f) * Reachability.Margin + 1f;

            Assert.That(Reachability.CanJump(Top(0f, 5f, 0f), Top(5f + gap, 20f, 0f)), Is.False);
            Assert.That(Reachability.CanJump(Top(0f, 5f, 0f), Top(5f + gap, 20f, 0f), dash: true), Is.True);
            Assert.That(Reachability.DashDistance, Is.EqualTo(Motor.DashSpeed * Motor.DashSeconds * Reachability.Margin).Within(1e-4f));
        }

        [Test]
        public void SurfacesAreTheTopsOfTheGroundAndThePlatforms()
        {
            var level = new Level { Spawn = new Vec2(1f, 0f) };
            level.Ground.Add(new Box(0f, -6f, 10f, 0f));
            level.Platforms.Add(new Box(3f, 1.5f, 5f, 1.8f));

            List<Box> surfaces = Reachability.Surfaces(level);

            Assert.That(surfaces.Count, Is.EqualTo(2));
            Assert.That(surfaces.Select(s => s.MaxY), Is.EquivalentTo(new[] { 0f, 1.8f }));
            Assert.That(Reachability.ReachableSurfaces(level).Count, Is.EqualTo(2));
            Assert.That(Reachability.CanReach(level, new Vec2(4f, 3f)), Is.True);
            Assert.That(Reachability.CanReach(level, new Vec2(4f, 9f)), Is.False);
            Assert.That(Reachability.CanReach(level, new Vec2(30f, 0.5f)), Is.False);
        }
    }

    public class LevelDesignTests
    {
        static IEnumerable<Level> All => Levels.All;

        [Test]
        public void ThereAreFourLevelsFromTheMeadowToTheMountain()
        {
            Assert.That(Levels.All.Count, Is.EqualTo(4));
            Assert.That(Levels.All.Select(l => l.Theme),
                Is.EqualTo(new[] { Theme.Meadow, Theme.Riverside, Theme.Forest, Theme.Mountain }));
            for (int i = 0; i < Levels.All.Count; i++)
            {
                Assert.That(Levels.All[i].Index, Is.EqualTo(i));
                Assert.That(Levels.All[i].Name, Is.Not.Empty);
                Assert.That(Levels.All[i].Tagline, Is.Not.Empty);
                Assert.That(Levels.All[i].ParSeconds, Is.GreaterThan(20f));
            }
            Assert.That(Levels.All.Select(l => l.Name).Distinct().Count(), Is.EqualTo(4));
        }

        [Test]
        public void EveryLevelHidesThreeGemsAndPlentyOfCoins()
        {
            foreach (Level level in All)
            {
                Assert.That(level.GemCount, Is.EqualTo(3), level.Name);
                Assert.That(level.CoinCount, Is.InRange(25, 80), level.Name);
                Assert.That(level.CoinCount, Is.EqualTo(level.Pickups.Count(p => p.Kind == PickupKind.Coin)));
                Assert.That(level.Width, Is.GreaterThan(80f), level.Name);
            }
        }

        [Test]
        public void TheHeroStartsOnTheGround()
        {
            foreach (Level level in All)
            {
                Assert.That(Reachability.Surfaces(level).Any(s => s.MaxY == level.Spawn.Y && s.MinX < level.Spawn.X && s.MaxX > level.Spawn.X),
                    Is.True, level.Name);
            }
        }

        [Test]
        public void TheGoalCanBeReachedWithPlainJumps()
        {
            foreach (Level level in All)
            {
                List<Box> surfaces = Reachability.Surfaces(level);
                HashSet<int> reachable = Reachability.ReachableSurfaces(level);
                bool goal = reachable.Any(i => surfaces[i].MaxY == level.Goal.MinY &&
                    surfaces[i].MinX < level.Goal.MaxX && surfaces[i].MaxX > level.Goal.MinX);
                Assert.That(goal, Is.True, level.Name);
            }
        }

        [Test]
        public void EveryPickupCanBeReached()
        {
            foreach (Level level in All)
            {
                foreach (PickupSpec pickup in level.Pickups)
                {
                    bool dash = pickup.Kind == PickupKind.Gem;
                    Assert.That(Reachability.CanReach(level, pickup.Position, dash), Is.True,
                        $"{level.Name}: {pickup.Kind} at {pickup.Position}");
                }
            }
        }

        [Test]
        public void NoPickupIsBuriedInTheGround()
        {
            foreach (Level level in All)
            {
                foreach (PickupSpec pickup in level.Pickups)
                {
                    Assert.That(level.Ground.Any(g => g.Contains(pickup.Position)), Is.False, $"{level.Name}: {pickup.Position}");
                    Assert.That(level.Spikes.Any(s => s.Grow(0.3f, 0.3f).Contains(pickup.Position)), Is.False, $"{level.Name}: {pickup.Position}");
                }
            }
        }

        [Test]
        public void EveryCheckpointStandsOnReachableGround()
        {
            foreach (Level level in All)
            {
                List<Box> surfaces = Reachability.Surfaces(level);
                HashSet<int> reachable = Reachability.ReachableSurfaces(level);
                Assert.That(level.Checkpoints.Count, Is.InRange(1, 3), level.Name);
                foreach (Vec2 point in level.Checkpoints)
                {
                    Assert.That(reachable.Any(i => surfaces[i].MaxY == point.Y && surfaces[i].MinX < point.X && surfaces[i].MaxX > point.X),
                        Is.True, $"{level.Name}: checkpoint {point}");
                }
                Assert.That(level.Checkpoints.Select(c => c.X), Is.Ordered);
            }
        }

        [Test]
        public void EveryGateHasAKeyBeforeIt()
        {
            foreach (Level level in All)
            {
                List<PickupSpec> keys = level.Pickups.Where(p => p.Item == ItemKind.Key).OrderBy(p => p.Position.X).ToList();
                List<Box> gates = level.Gates.OrderBy(g => g.MinX).ToList();
                Assert.That(keys.Count, Is.EqualTo(gates.Count), level.Name);
                for (int i = 0; i < gates.Count; i++)
                {
                    Assert.That(keys[i].Position.X, Is.LessThan(gates[i].MinX), $"{level.Name}: gate {i}");
                    Assert.That(gates[i].Height, Is.GreaterThan(Motor.MaxJumpHeight + Motor.Height), "too tall to jump over");
                }
            }
        }

        [Test]
        public void WalkersPatrolOnSolidGround()
        {
            foreach (Level level in All)
            {
                foreach (CritterSpec critter in level.Critters.Where(c => c.Kind != CritterKind.Bee))
                {
                    bool supported = Reachability.Surfaces(level).Any(s => s.MaxY == critter.Home.Y &&
                        s.MinX <= critter.MinX - 0.3f && s.MaxX >= critter.MaxX + 0.3f);
                    Assert.That(supported, Is.True, $"{level.Name}: {critter.Kind} at {critter.Home}");
                    Assert.That(critter.Home.X, Is.InRange(critter.MinX, critter.MaxX));
                }
            }
        }

        [Test]
        public void TheLaterLevelsBringNewCritters()
        {
            Assert.That(Levels.All[0].Critters.All(c => c.Kind == CritterKind.Beetle), Is.True);
            Assert.That(Levels.All[1].Critters.Any(c => c.Kind == CritterKind.Bee), Is.True);
            Assert.That(Levels.All[2].Critters.Any(c => c.Kind == CritterKind.Hedgehog), Is.True);
            Assert.That(Levels.All[3].Critters.Select(c => c.Kind).Distinct().Count(), Is.EqualTo(3));
        }
    }
}
