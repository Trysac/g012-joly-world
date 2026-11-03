using NUnit.Framework;

namespace JolyWorld.Core.Tests
{
    public class MotorTests
    {
        static readonly MotorInput Idle = new MotorInput();
        static MotorInput Right => new MotorInput { Move = 1f };

        static Motor OnGround(float x = 0f)
        {
            var motor = new Motor();
            motor.Place(new Vec2(x, 0f));
            return motor;
        }

        [Test]
        public void PlaceStandsTheBodyStillAtItsFeet()
        {
            var motor = new Motor();
            motor.Knock(3f, 4f);
            motor.Place(new Vec2(2f, 1f));

            Assert.That(motor.Position, Is.EqualTo(new Vec2(2f, 1f)));
            Assert.That(motor.Velocity, Is.EqualTo(Vec2.Zero));
            Assert.That(motor.Bounds.MinX, Is.EqualTo(2f - Motor.HalfWidth).Within(1e-5f));
            Assert.That(motor.Bounds.MaxY, Is.EqualTo(1f + Motor.Height).Within(1e-5f));
        }

        [Test]
        public void StandingOnTheGroundStaysOnIt()
        {
            Motor motor = OnGround();
            MotorEvents events = Sim.Run(motor, Idle, 1f, TestWorld.Flat());

            Assert.That(motor.Grounded, Is.True);
            Assert.That(motor.Position.Y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(events, Is.EqualTo(MotorEvents.None));
        }

        [Test]
        public void ABodyInTheAirFallsAndLandsOnce()
        {
            var motor = new Motor();
            motor.Place(new Vec2(0f, 3f));
            int landings = 0;
            var world = TestWorld.Flat();
            var stamina = new Meter(100f);
            for (int i = 0; i < 90; i++)
            {
                MotorEvents events = motor.Tick(Idle, Sim.Step, world, stamina);
                landings += (events & MotorEvents.Landed) != 0 ? 1 : 0;
                if (i == 3)
                {
                    Assert.That(motor.Velocity.Y, Is.LessThan(0f));
                    Assert.That(motor.Grounded, Is.False);
                }
            }

            Assert.That(landings, Is.EqualTo(1));
            Assert.That(motor.Grounded, Is.True);
            Assert.That(motor.Position.Y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void FallingSpeedIsCapped()
        {
            var motor = new Motor();
            motor.Place(new Vec2(0f, 100f));
            Sim.Run(motor, Idle, 2f, new TestWorld());

            Assert.That(motor.Velocity.Y, Is.EqualTo(-Motor.MaxFallSpeed).Within(1e-4f));
        }

        [Test]
        public void RunningReachesRunSpeedAndFacesTheWayItGoes()
        {
            Motor motor = OnGround();
            Sim.Run(motor, Right, 0.5f, TestWorld.Flat());

            Assert.That(motor.Velocity.X, Is.EqualTo(Motor.RunSpeed).Within(1e-4f));
            Assert.That(motor.Facing, Is.EqualTo(1));
            Assert.That(motor.Position.X, Is.GreaterThan(2f));

            Sim.Run(motor, new MotorInput { Move = -1f }, 0.5f, TestWorld.Flat());
            Assert.That(motor.Velocity.X, Is.EqualTo(-Motor.RunSpeed).Within(1e-4f));
            Assert.That(motor.Facing, Is.EqualTo(-1));

            Sim.Run(motor, Idle, 0.5f, TestWorld.Flat());
            Assert.That(motor.Velocity.X, Is.EqualTo(0f));
            Assert.That(motor.Facing, Is.EqualTo(-1), "letting go keeps the last facing");
        }

        [Test]
        public void SprintingIsFasterAndCostsStamina()
        {
            Motor motor = OnGround(-40f);
            var stamina = new Meter(100f);
            Sim.Run(motor, new MotorInput { Move = 1f, Sprint = true }, 1f, TestWorld.Flat(), stamina);

            Assert.That(motor.Sprinting, Is.True);
            Assert.That(motor.Velocity.X, Is.EqualTo(Motor.SprintSpeed).Within(1e-4f));
            Assert.That(stamina.Current, Is.EqualTo(100f - Motor.SprintCostPerSecond).Within(0.5f));
        }

        [Test]
        public void WithoutStaminaASprintIsARun()
        {
            Motor motor = OnGround(-40f);
            var stamina = new Meter(100f);
            stamina.Spend(100f);
            Sim.Run(motor, new MotorInput { Move = 1f, Sprint = true }, 1f, TestWorld.Flat(), stamina);

            Assert.That(motor.Sprinting, Is.False);
            Assert.That(motor.Velocity.X, Is.EqualTo(Motor.RunSpeed).Within(1e-4f));
        }

        [Test]
        public void AJumpRisesToTheMaximumJumpHeight()
        {
            Motor motor = OnGround();
            var world = TestWorld.Flat();
            var stamina = new Meter(100f);
            Sim.Run(motor, Idle, 0.1f, world, stamina);
            MotorEvents first = motor.Tick(new MotorInput { JumpPressed = true, JumpHeld = true }, Sim.Step, world, stamina);
            float apex = 0f;
            for (int i = 0; i < 60; i++)
            {
                motor.Tick(new MotorInput { JumpHeld = true }, Sim.Step, world, stamina);
                apex = System.Math.Max(apex, motor.Position.Y);
            }

            Assert.That(first & MotorEvents.Jumped, Is.EqualTo(MotorEvents.Jumped));
            Assert.That(Motor.MaxJumpHeight, Is.EqualTo(Motor.JumpSpeed * Motor.JumpSpeed / (2f * Motor.Gravity)).Within(1e-4f));
            Assert.That(apex, Is.EqualTo(Motor.MaxJumpHeight).Within(0.12f));
        }

        [Test]
        public void LettingGoOfJumpEarlyMakesAShortHop()
        {
            Motor motor = OnGround();
            var world = TestWorld.Flat();
            var stamina = new Meter(100f);
            motor.Tick(new MotorInput { JumpPressed = true, JumpHeld = true }, Sim.Step, world, stamina);
            motor.Tick(new MotorInput { JumpHeld = true }, Sim.Step, world, stamina);
            float apex = 0f;
            for (int i = 0; i < 60; i++)
            {
                motor.Tick(Idle, Sim.Step, world, stamina);
                apex = System.Math.Max(apex, motor.Position.Y);
            }

            Assert.That(apex, Is.LessThan(Motor.MaxJumpHeight * 0.4f));
            Assert.That(apex, Is.GreaterThan(0.1f));
        }

        [Test]
        public void YouCannotJumpInTheAir()
        {
            var motor = new Motor();
            motor.Place(new Vec2(0f, 5f));
            var world = TestWorld.Flat();
            Sim.Run(motor, Idle, 0.3f, world);
            MotorEvents events = Sim.Run(motor, new MotorInput { JumpPressed = true, JumpHeld = true }, Sim.Step, world);

            Assert.That(events & MotorEvents.Jumped, Is.EqualTo(MotorEvents.None));
        }

        [Test]
        public void AJumpJustAfterRunningOffALedgeStillWorks()
        {
            var world = new TestWorld().Solid(-20f, -5f, 0f, 0f);
            Motor motor = OnGround(-1f);
            var stamina = new Meter(100f);
            int stepsInAir = 0;
            while (motor.Grounded || stepsInAir == 0)
            {
                motor.Tick(Right, Sim.Step, world, stamina);
                if (!motor.Grounded)
                {
                    stepsInAir++;
                }
            }
            Sim.Run(motor, Right, 0.05f, world, stamina);
            MotorEvents late = motor.Tick(new MotorInput { Move = 1f, JumpPressed = true, JumpHeld = true }, Sim.Step, world, stamina);

            Assert.That(late & MotorEvents.Jumped, Is.EqualTo(MotorEvents.Jumped), "inside the coyote time");
            Assert.That(motor.Velocity.Y, Is.GreaterThan(Motor.JumpSpeed * 0.9f));
        }

        [Test]
        public void TooLongAfterTheLedgeThereIsNoJump()
        {
            var world = new TestWorld().Solid(-20f, -5f, 0f, 0f);
            Motor motor = OnGround(-0.2f);
            var stamina = new Meter(100f);
            Sim.Run(motor, Right, 0.35f, world, stamina);
            Assert.That(motor.Grounded, Is.False);
            MotorEvents late = motor.Tick(new MotorInput { Move = 1f, JumpPressed = true, JumpHeld = true }, Sim.Step, world, stamina);

            Assert.That(late & MotorEvents.Jumped, Is.EqualTo(MotorEvents.None));
        }

        [Test]
        public void AJumpPressedJustBeforeLandingHappensOnLanding()
        {
            var motor = new Motor();
            motor.Place(new Vec2(0f, 0.3f));
            var world = TestWorld.Flat();
            var stamina = new Meter(100f);
            motor.Knock(0f, -4f);
            motor.Tick(new MotorInput { JumpPressed = true, JumpHeld = true }, Sim.Step, world, stamina);
            Assert.That(motor.Velocity.Y, Is.LessThan(0f), "still falling when the button goes down");
            MotorEvents events = Sim.Run(motor, new MotorInput { JumpHeld = true }, 0.08f, world, stamina);

            Assert.That(events & MotorEvents.Jumped, Is.EqualTo(MotorEvents.Jumped));
            Assert.That(motor.Velocity.Y, Is.GreaterThan(0f));
        }

        [Test]
        public void AWallStopsTheBody()
        {
            var world = TestWorld.Flat().Solid(3f, 0f, 4f, 5f);
            Motor motor = OnGround();
            Sim.Run(motor, Right, 1.5f, world);

            Assert.That(motor.Position.X, Is.EqualTo(3f - Motor.HalfWidth).Within(1e-3f));
            Assert.That(motor.Velocity.X, Is.EqualTo(0f));
        }

        [Test]
        public void ACeilingStopsAJump()
        {
            var world = TestWorld.Flat().Solid(-2f, 2f, 2f, 3f);
            Motor motor = OnGround();
            MotorEvents events = Sim.Run(motor, new MotorInput { JumpPressed = true, JumpHeld = true }, 0.3f, world);

            Assert.That(events & MotorEvents.Bumped, Is.EqualTo(MotorEvents.Bumped));
            Assert.That(motor.Bounds.MaxY, Is.LessThanOrEqualTo(2f + 1e-3f));
        }

        [Test]
        public void APlatformCanBeJumpedThroughFromBelowAndStoodOn()
        {
            var world = TestWorld.Flat().Platform(-2f, 2f, 1.8f);
            Motor motor = OnGround();
            Sim.Run(motor, new MotorInput { JumpPressed = true, JumpHeld = true }, 1.2f, world);

            Assert.That(motor.Grounded, Is.True);
            Assert.That(motor.Position.Y, Is.EqualTo(1.8f).Within(1e-3f));
        }

        [Test]
        public void ADashBurstsForwardForStaminaAndIgnoresGravity()
        {
            var motor = new Motor();
            motor.Place(new Vec2(0f, 5f));
            var stamina = new Meter(100f);
            var world = TestWorld.Flat();
            MotorEvents events = motor.Tick(new MotorInput { DashPressed = true }, Sim.Step, world, stamina);
            for (int i = 1; i < 9; i++)
            {
                motor.Tick(Idle, Sim.Step, world, stamina);
            }

            Assert.That(events & MotorEvents.Dashed, Is.EqualTo(MotorEvents.Dashed));
            Assert.That(stamina.Current, Is.EqualTo(100f - Motor.DashCost));
            Assert.That(motor.Position.X, Is.EqualTo(Motor.DashSpeed * 9 * Sim.Step).Within(0.05f));
            Assert.That(motor.Position.Y, Is.EqualTo(5f).Within(1e-3f), "no falling while dashing");
            Assert.That(motor.Dashing, Is.True);
            Sim.Run(motor, Idle, 0.2f, world, stamina);
            Assert.That(motor.Dashing, Is.False);
        }

        [Test]
        public void ADashFollowsTheFacingAndWaitsForItsCooldown()
        {
            Motor motor = OnGround();
            var stamina = new Meter(100f);
            var world = TestWorld.Flat();
            Sim.Run(motor, new MotorInput { Move = -1f }, 0.1f, world, stamina);
            float start = motor.Position.X;
            Sim.Run(motor, new MotorInput { DashPressed = true }, Motor.DashSeconds, world, stamina);
            Assert.That(motor.Position.X, Is.LessThan(start - 2f));

            MotorEvents again = motor.Tick(new MotorInput { DashPressed = true }, Sim.Step, world, stamina);
            Assert.That(again & MotorEvents.Dashed, Is.EqualTo(MotorEvents.None), "still cooling down");
            Sim.Run(motor, Idle, Motor.DashCooldown, world, stamina);
            again = motor.Tick(new MotorInput { DashPressed = true }, Sim.Step, world, stamina);
            Assert.That(again & MotorEvents.Dashed, Is.EqualTo(MotorEvents.Dashed));
        }

        [Test]
        public void NoDashWithoutEnoughStamina()
        {
            Motor motor = OnGround();
            var stamina = new Meter(100f);
            stamina.Spend(100f - Motor.DashCost + 1f);
            MotorEvents events = motor.Tick(new MotorInput { DashPressed = true }, Sim.Step, TestWorld.Flat(), stamina);

            Assert.That(events & MotorEvents.Dashed, Is.EqualTo(MotorEvents.None));
            Assert.That(stamina.Current, Is.EqualTo(Motor.DashCost - 1f));
        }

        [Test]
        public void KnockPushesTheBodyIntoTheAir()
        {
            Motor motor = OnGround();
            motor.Knock(-5f, 6f);
            motor.Tick(Idle, Sim.Step, TestWorld.Flat(), new Meter(100f));

            Assert.That(motor.Grounded, Is.False);
            Assert.That(motor.Position.X, Is.LessThan(0f));
            Assert.That(motor.Position.Y, Is.GreaterThan(0f));
        }

        [Test]
        public void BounceLaunchesUpwards()
        {
            Motor motor = OnGround();
            motor.Bounce(9f);

            Assert.That(motor.Velocity.Y, Is.EqualTo(9f));
            Assert.That(motor.Grounded, Is.False);
        }

        /// <summary>Runs at full speed off the edge of x = 0 towards ground that starts at x = gap.</summary>
        static bool ClearsGap(float gap, float rise)
        {
            var world = new TestWorld().Solid(-40f, -5f, 0f, 0f).Solid(gap, -5f, gap + 30f, rise);
            Motor motor = OnGround(-12f);
            var stamina = new Meter(100f);
            bool jumped = false;
            for (int i = 0; i < 600; i++)
            {
                bool press = !jumped && motor.Position.X >= -0.05f;
                motor.Tick(new MotorInput { Move = 1f, JumpPressed = press, JumpHeld = true }, Sim.Step, world, stamina);
                jumped |= press;
                if (motor.Position.Y < -2f)
                {
                    return false;
                }
                if (jumped && motor.Grounded && motor.Position.X > gap)
                {
                    return true;
                }
            }
            return false;
        }

        [Test]
        public void TheJumpReachMatchesTheBody([Values(0f, 1f, 1.8f)] float rise)
        {
            float reach = Motor.JumpReach(rise);

            Assert.That(ClearsGap(reach * 0.9f, rise), Is.True, "a gap a little shorter than the reach is cleared");
            Assert.That(ClearsGap(reach * 1.15f + 2f * Motor.HalfWidth, rise), Is.False, "a longer one is not");
        }

        [Test]
        public void TheJumpReachShrinksWithHeightAndIsZeroAboveTheMaximum()
        {
            Assert.That(Motor.JumpReach(0f), Is.GreaterThan(Motor.JumpReach(1.5f)));
            Assert.That(Motor.JumpReach(-2f), Is.GreaterThan(Motor.JumpReach(0f)));
            Assert.That(Motor.JumpReach(Motor.MaxJumpHeight + 0.1f), Is.EqualTo(0f));
            Assert.That(Motor.JumpReach(0f, Motor.SprintSpeed), Is.GreaterThan(Motor.JumpReach(0f)));
        }
    }
}
