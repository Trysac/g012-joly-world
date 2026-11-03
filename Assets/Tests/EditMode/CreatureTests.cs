using NUnit.Framework;

namespace JolyWorld.Core.Tests
{
    public class CritterTests
    {
        [Test]
        public void ABeetleWalksToTheEndOfItsPathAndTurnsBack()
        {
            var beetle = new Critter(CritterKind.Beetle, new Vec2(5f, 0f), 3f, 7f);
            Assert.That(beetle.Alive, Is.True);
            Assert.That(beetle.Direction, Is.EqualTo(1));
            Assert.That(beetle.Position, Is.EqualTo(new Vec2(5f, 0f)));

            float seconds = 2f / beetle.Speed + 0.5f;
            for (float t = 0f; t < seconds; t += Sim.Step)
            {
                beetle.Tick(Sim.Step);
                Assert.That(beetle.Position.X, Is.InRange(3f, 7f));
            }

            Assert.That(beetle.Direction, Is.EqualTo(-1));
            Assert.That(beetle.Position.X, Is.LessThan(7f));
            Assert.That(beetle.Position.Y, Is.EqualTo(0f));
        }

        [Test]
        public void ABeeBobsUpAndDownAroundItsHome()
        {
            var bee = new Critter(CritterKind.Bee, new Vec2(0f, 2f), -2f, 2f);
            float low = 2f, high = 2f;
            for (int i = 0; i < 300; i++)
            {
                bee.Tick(Sim.Step);
                low = System.Math.Min(low, bee.Position.Y);
                high = System.Math.Max(high, bee.Position.Y);
            }

            Assert.That(high - 2f, Is.EqualTo(0.6f).Within(0.05f));
            Assert.That(2f - low, Is.EqualTo(0.6f).Within(0.05f));
        }

        [Test]
        public void HedgehogsAreSpikyAndCannotBeStomped()
        {
            Assert.That(new Critter(CritterKind.Beetle, Vec2.Zero, 0f, 1f).Stompable, Is.True);
            Assert.That(new Critter(CritterKind.Bee, Vec2.Zero, 0f, 1f).Stompable, Is.True);
            Assert.That(new Critter(CritterKind.Hedgehog, Vec2.Zero, 0f, 1f).Stompable, Is.False);
        }

        [Test]
        public void ADefeatedCritterStopsMoving()
        {
            var beetle = new Critter(CritterKind.Beetle, new Vec2(5f, 0f), 3f, 7f);
            beetle.Defeat();
            beetle.Tick(1f);

            Assert.That(beetle.Alive, Is.False);
            Assert.That(beetle.Position, Is.EqualTo(new Vec2(5f, 0f)));
        }

        [Test]
        public void ABodyBoxStandsOnTheCrittersFeet()
        {
            var beetle = new Critter(CritterKind.Beetle, new Vec2(5f, 1f), 3f, 7f);

            Assert.That(beetle.Bounds.MinY, Is.EqualTo(1f));
            Assert.That(beetle.Bounds.Center.X, Is.EqualTo(5f).Within(1e-5f));
            Assert.That(beetle.Bounds.Height, Is.GreaterThan(0.3f));
        }
    }

    public class CombatTests
    {
        static Motor HeroAt(float x, float y, float vy)
        {
            var hero = new Motor();
            hero.Place(new Vec2(x, y));
            hero.Knock(0f, vy);
            return hero;
        }

        [Test]
        public void FallingOnABeetleStompsIt()
        {
            var beetle = new Critter(CritterKind.Beetle, new Vec2(0f, 0f), -2f, 2f);
            Motor hero = HeroAt(0.1f, beetle.Bounds.MaxY - 0.1f, -5f);

            Assert.That(Combat.Touch(hero, beetle), Is.EqualTo(Contact.Stomp));
        }

        [Test]
        public void WalkingIntoABeetleHurts()
        {
            var beetle = new Critter(CritterKind.Beetle, new Vec2(0f, 0f), -2f, 2f);
            Motor hero = HeroAt(-0.5f, 0f, 0f);

            Assert.That(Combat.Touch(hero, beetle), Is.EqualTo(Contact.Hurt));
        }

        [Test]
        public void RisingIntoABeeFromBelowHurts()
        {
            var bee = new Critter(CritterKind.Bee, new Vec2(0f, 2f), -2f, 2f);
            Motor hero = HeroAt(0f, 1f, 6f);

            Assert.That(Combat.Touch(hero, bee), Is.EqualTo(Contact.Hurt));
        }

        [Test]
        public void FallingOnAHedgehogStillHurts()
        {
            var hedgehog = new Critter(CritterKind.Hedgehog, new Vec2(0f, 0f), -2f, 2f);
            Motor hero = HeroAt(0f, hedgehog.Bounds.MaxY - 0.1f, -5f);

            Assert.That(Combat.Touch(hero, hedgehog), Is.EqualTo(Contact.Hurt));
        }

        [Test]
        public void NoContactWhenApartOrDefeated()
        {
            var beetle = new Critter(CritterKind.Beetle, new Vec2(0f, 0f), -2f, 2f);
            Assert.That(Combat.Touch(HeroAt(4f, 0f, 0f), beetle), Is.EqualTo(Contact.None));

            beetle.Defeat();
            Assert.That(Combat.Touch(HeroAt(0f, 0f, 0f), beetle), Is.EqualTo(Contact.None));
        }
    }

    public class BoltTests
    {
        [Test]
        public void ABoltFliesStraightAndFadesAfterItsLife()
        {
            var bolt = new Bolt(new Vec2(0f, 1f), -1);
            var world = new TestWorld();

            Assert.That(bolt.Tick(0.1f, world), Is.True);
            Assert.That(bolt.Position.X, Is.EqualTo(-Combat.BoltSpeed * 0.1f).Within(1e-4f));
            Assert.That(bolt.Position.Y, Is.EqualTo(1f));
            Assert.That(bolt.Life, Is.EqualTo(Combat.BoltLife - 0.1f).Within(1e-4f));

            for (int i = 0; i < 100 && bolt.Alive; i++)
            {
                bolt.Tick(Sim.Step, world);
            }
            Assert.That(bolt.Alive, Is.False);
            Assert.That(bolt.Position.X, Is.EqualTo(-Combat.BoltSpeed * Combat.BoltLife).Within(0.3f));
        }

        [Test]
        public void ABoltStopsAtAWall()
        {
            var bolt = new Bolt(new Vec2(0f, 1f), 1);
            var world = new TestWorld().Solid(2f, 0f, 3f, 4f);
            bool alive = true;
            for (int i = 0; i < 30 && alive; i++)
            {
                alive = bolt.Tick(Sim.Step, world);
            }

            Assert.That(alive, Is.False);
            Assert.That(bolt.Alive, Is.False);
            Assert.That(bolt.Position.X, Is.LessThan(2.5f));
        }

        [Test]
        public void FizzleEndsABolt()
        {
            var bolt = new Bolt(Vec2.Zero, 1);
            bolt.Fizzle();

            Assert.That(bolt.Alive, Is.False);
            Assert.That(bolt.Tick(0.1f, new TestWorld()), Is.False);
            Assert.That(bolt.Position, Is.EqualTo(Vec2.Zero));
        }
    }
}
