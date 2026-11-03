using System;
using System.Collections.Generic;

namespace JolyWorld.Core
{
    /// <summary>
    /// A level design check built on the motor's jump: which surfaces the hero can get to from the
    /// spawn with ordinary running jumps (or with a dash too), and which points are within reach.
    /// Distances keep a safety margin so every jump the design asks for is comfortable.
    /// </summary>
    public static class Reachability
    {
        public const float Margin = 0.8f;

        /// <summary>The extra distance a dash adds to a jump, with the margin.</summary>
        public static float DashDistance => Motor.DashSpeed * Motor.DashSeconds * Margin;

        /// <summary>Everything the hero can stand on: the ground boxes, then the platforms.</summary>
        public static List<Box> Surfaces(Level level)
        {
            var surfaces = new List<Box>(level.Ground);
            surfaces.AddRange(level.Platforms);
            return surfaces;
        }

        /// <summary>True when a running jump from the top of <paramref name="from"/> lands on the top of <paramref name="to"/>.</summary>
        public static bool CanJump(Box from, Box to, bool dash = false)
        {
            float rise = to.MaxY - from.MaxY;
            if (rise > Motor.MaxJumpHeight * Margin)
            {
                return false;
            }
            float gap = Math.Max(0f, Math.Max(to.MinX - from.MaxX, from.MinX - to.MaxX));
            float reach = Motor.JumpReach(rise) * Margin + (dash ? DashDistance : 0f);
            return gap <= reach;
        }

        /// <summary>Indices into <see cref="Surfaces"/> the hero can get to from the spawn.</summary>
        public static HashSet<int> ReachableSurfaces(Level level, bool dash = false)
        {
            List<Box> surfaces = Surfaces(level);
            var reached = new HashSet<int>();
            var open = new Queue<int>();
            for (int i = 0; i < surfaces.Count; i++)
            {
                Box s = surfaces[i];
                if (Math.Abs(s.MaxY - level.Spawn.Y) < 0.01f && s.MinX <= level.Spawn.X && s.MaxX >= level.Spawn.X)
                {
                    reached.Add(i);
                    open.Enqueue(i);
                }
            }
            while (open.Count > 0)
            {
                Box from = surfaces[open.Dequeue()];
                for (int i = 0; i < surfaces.Count; i++)
                {
                    if (!reached.Contains(i) && CanJump(from, surfaces[i], dash) && !Blocked(level, from, surfaces[i]))
                    {
                        reached.Add(i);
                        open.Enqueue(i);
                    }
                }
            }
            return reached;
        }

        /// <summary>True when the hero can touch <paramref name="point"/> from a reachable surface.</summary>
        public static bool CanReach(Level level, Vec2 point, bool dash = false)
        {
            List<Box> surfaces = Surfaces(level);
            float above = Motor.MaxJumpHeight * Margin + Motor.Height + 0.2f;
            float aside = Motor.JumpReach(0f) * Margin * 0.5f + (dash ? DashDistance : 0f);
            foreach (int i in ReachableSurfaces(level, dash))
            {
                Box s = surfaces[i];
                float dy = point.Y - s.MaxY;
                if (dy >= -0.3f && dy <= above && point.X >= s.MinX - aside && point.X <= s.MaxX + aside)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A ground box standing tall between two surfaces walls the jump off.</summary>
        static bool Blocked(Level level, Box from, Box to)
        {
            float left = Math.Min(from.MaxX, to.MaxX);
            float right = Math.Max(from.MinX, to.MinX);
            float top = Math.Max(from.MaxY, to.MaxY) + 0.5f;
            foreach (Box wall in level.Ground)
            {
                if (wall.MinX >= left && wall.MaxX <= right && wall.MaxY > top)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
