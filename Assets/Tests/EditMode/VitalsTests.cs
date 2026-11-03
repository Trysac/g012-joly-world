using NUnit.Framework;

namespace JolyWorld.Core.Tests
{
    public class MeterTests
    {
        [Test]
        public void ANewMeterStartsFull()
        {
            var meter = new Meter(100f, 5f, 1f);

            Assert.That(meter.Max, Is.EqualTo(100f));
            Assert.That(meter.Current, Is.EqualTo(100f));
            Assert.That(meter.Fraction, Is.EqualTo(1f));
            Assert.That(meter.IsFull, Is.True);
            Assert.That(meter.IsEmpty, Is.False);
        }

        [Test]
        public void SpendTakesTheWholeAmountOrNothing()
        {
            var meter = new Meter(100f);

            Assert.That(meter.Spend(30f), Is.True);
            Assert.That(meter.Current, Is.EqualTo(70f));
            Assert.That(meter.Spend(80f), Is.False);
            Assert.That(meter.Current, Is.EqualTo(70f), "a failed spend leaves the meter alone");
            Assert.That(meter.Spend(70f), Is.True);
            Assert.That(meter.IsEmpty, Is.True);
        }

        [Test]
        public void DrainTakesWhatIsLeftAndSaysHowMuch()
        {
            var meter = new Meter(100f);
            meter.Spend(90f);

            Assert.That(meter.Drain(25f), Is.EqualTo(10f));
            Assert.That(meter.Current, Is.EqualTo(0f));
            Assert.That(meter.Drain(5f), Is.EqualTo(0f));
        }

        [Test]
        public void RestoreStopsAtTheMaximumAndSaysHowMuchItGave()
        {
            var meter = new Meter(100f);
            meter.Spend(30f);

            Assert.That(meter.Restore(20f), Is.EqualTo(20f));
            Assert.That(meter.Restore(50f), Is.EqualTo(10f));
            Assert.That(meter.Current, Is.EqualTo(100f));
            Assert.That(meter.Restore(5f), Is.EqualTo(0f));
        }

        [Test]
        public void ItRegeneratesOnlyAfterTheDelaySinceItWasLastUsed()
        {
            var meter = new Meter(100f, 10f, 1f);
            meter.Spend(50f);

            meter.Tick(0.9f);
            Assert.That(meter.Current, Is.EqualTo(50f), "still inside the delay");
            meter.Tick(0.6f);
            Assert.That(meter.Current, Is.EqualTo(55f).Within(0.01f), "0.5 s past the delay at 10 a second");

            meter.Spend(5f);
            meter.Tick(0.5f);
            Assert.That(meter.Current, Is.EqualTo(50f).Within(0.01f), "spending restarts the delay");
            meter.Tick(10f);
            Assert.That(meter.Current, Is.EqualTo(100f), "never past the maximum");
        }

        [Test]
        public void AMeterWithoutRegenerationStaysPut()
        {
            var meter = new Meter(100f);
            meter.Spend(40f);
            meter.Tick(60f);

            Assert.That(meter.Current, Is.EqualTo(60f));
        }

        [Test]
        public void FillRefillsIt()
        {
            var meter = new Meter(80f);
            meter.Spend(80f);
            meter.Fill();

            Assert.That(meter.Current, Is.EqualTo(80f));
        }
    }

    public class VitalsTests
    {
        [Test]
        public void HealthManaAndStaminaStartFullAt100()
        {
            var vitals = new Vitals();

            Assert.That(vitals.Health.Current, Is.EqualTo(Vitals.MaxHealth));
            Assert.That(vitals.Mana.Current, Is.EqualTo(Vitals.MaxMana));
            Assert.That(vitals.Stamina.Current, Is.EqualTo(Vitals.MaxStamina));
            Assert.That(vitals.Invulnerable, Is.False);
            Assert.That(vitals.Dead, Is.False);
        }

        [Test]
        public void AHitTakesHealthAndGivesAMomentOfSafety()
        {
            var vitals = new Vitals();

            Assert.That(vitals.Hurt(20f), Is.True);
            Assert.That(vitals.Health.Current, Is.EqualTo(80f));
            Assert.That(vitals.Invulnerable, Is.True);
            Assert.That(vitals.InvulnerableLeft, Is.EqualTo(Vitals.InvulnerableSeconds));

            Assert.That(vitals.Hurt(20f), Is.False, "a second hit straight away is ignored");
            Assert.That(vitals.Health.Current, Is.EqualTo(80f));

            vitals.Tick(Vitals.InvulnerableSeconds + 0.01f);
            Assert.That(vitals.Invulnerable, Is.False);
            Assert.That(vitals.Hurt(20f), Is.True);
            Assert.That(vitals.Health.Current, Is.EqualTo(60f));
        }

        [Test]
        public void ProtectGivesSafetyWithoutDamage()
        {
            var vitals = new Vitals();
            vitals.Protect(2f);

            Assert.That(vitals.Hurt(10f), Is.False);
            Assert.That(vitals.Health.Current, Is.EqualTo(100f));
        }

        [Test]
        public void NoHealthMeansDeadAndTheDeadCannotBeHurt()
        {
            var vitals = new Vitals();
            vitals.Hurt(150f);

            Assert.That(vitals.Health.Current, Is.EqualTo(0f));
            Assert.That(vitals.Dead, Is.True);
            vitals.Tick(5f);
            Assert.That(vitals.Hurt(10f), Is.False);
        }

        [Test]
        public void ManaAndStaminaComeBackButHealthDoesNot()
        {
            var vitals = new Vitals();
            vitals.Hurt(50f);
            vitals.Mana.Spend(50f);
            vitals.Stamina.Spend(90f);

            vitals.Tick(Vitals.ManaRegenDelay + 1f);

            Assert.That(vitals.Health.Current, Is.EqualTo(50f));
            Assert.That(vitals.Mana.Current, Is.EqualTo(50f + Vitals.ManaRegen).Within(0.01f));
            float staminaTime = Vitals.ManaRegenDelay + 1f - Vitals.StaminaRegenDelay;
            Assert.That(vitals.Stamina.Current, Is.EqualTo(10f + Vitals.StaminaRegen * staminaTime).Within(0.01f));
        }

        [Test]
        public void ReviveFillsEverythingAndEndsTheSafety()
        {
            var vitals = new Vitals();
            vitals.Hurt(200f);
            vitals.Mana.Spend(100f);
            vitals.Stamina.Spend(100f);

            vitals.Revive();

            Assert.That(vitals.Dead, Is.False);
            Assert.That(vitals.Health.IsFull && vitals.Mana.IsFull && vitals.Stamina.IsFull, Is.True);
            Assert.That(vitals.Invulnerable, Is.False);
        }
    }
}
