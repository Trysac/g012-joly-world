using System.Collections.Generic;

namespace JolyWorld.Core.Tests
{
    /// <summary>A hand-made collision world for the motor tests.</summary>
    public sealed class TestWorld : ICollisionWorld
    {
        public readonly List<Box> SolidList = new List<Box>();
        public readonly List<Box> PlatformList = new List<Box>();

        public IReadOnlyList<Box> Solids => SolidList;
        public IReadOnlyList<Box> Platforms => PlatformList;

        /// <summary>Flat ground with its top at y = 0 from x = -50 to x = 50.</summary>
        public static TestWorld Flat()
        {
            var world = new TestWorld();
            world.SolidList.Add(new Box(-50f, -5f, 50f, 0f));
            return world;
        }

        public TestWorld Solid(float minX, float minY, float maxX, float maxY)
        {
            SolidList.Add(new Box(minX, minY, maxX, maxY));
            return this;
        }

        public TestWorld Platform(float minX, float maxX, float top)
        {
            PlatformList.Add(new Box(minX, top - 0.3f, maxX, top));
            return this;
        }
    }

    public static class Sim
    {
        public const float Step = 1f / 60f;

        /// <summary>Ticks the motor for a while with the same input, collecting every event.</summary>
        public static MotorEvents Run(Motor motor, MotorInput input, float seconds, ICollisionWorld world, Meter stamina = null)
        {
            stamina ??= new Meter(100f);
            MotorEvents all = MotorEvents.None;
            int steps = (int)System.Math.Round(seconds / Step);
            for (int i = 0; i < steps; i++)
            {
                all |= motor.Tick(input, Step, world, stamina);
                input.JumpPressed = false;
                input.DashPressed = false;
            }
            return all;
        }

        /// <summary>Ticks the level run for a while with the same input; presses happen on the first step only.</summary>
        public static void Run(LevelRun run, HeroInput input, float seconds)
        {
            int steps = (int)System.Math.Round(seconds / Step);
            for (int i = 0; i < steps; i++)
            {
                run.Tick(input, Step);
                input.Jump = false;
                input.Dash = false;
                input.Cast = false;
                input.Use = -1;
            }
        }
    }
}
