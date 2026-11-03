using System;
using System.Collections.Generic;

namespace JolyWorld.Core
{
    /// <summary>What the player asks the hero's body to do this step.</summary>
    public struct MotorInput
    {
        /// <summary>-1 (left) .. 1 (right).</summary>
        public float Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool Sprint;
        public bool DashPressed;
    }

    [Flags]
    public enum MotorEvents
    {
        None = 0,
        Jumped = 1,
        Landed = 2,
        Dashed = 4,
        Bumped = 8,
    }

    /// <summary>The solid boxes and the one-way platforms a body collides with.</summary>
    public interface ICollisionWorld
    {
        IReadOnlyList<Box> Solids { get; }

        /// <summary>Platforms you can jump up through and stand on.</summary>
        IReadOnlyList<Box> Platforms { get; }
    }

    /// <summary>
    /// A platformer body on the side-view plane: it runs, sprints on stamina, jumps with coyote
    /// time, a jump buffer and a shorter hop when the button is let go early, dashes, and stops
    /// against the level's solid boxes and one-way platforms.
    /// </summary>
    public sealed class Motor
    {
        public const float HalfWidth = 0.32f;
        public const float Height = 1.3f;
        public const float RunSpeed = 6f;
        public const float SprintSpeed = 9.5f;
        public const float GroundAcceleration = 70f;
        public const float AirAcceleration = 40f;
        public const float Gravity = 32f;
        public const float FallGravityScale = 1.7f;
        public const float JumpSpeed = 12.4f;
        public const float JumpCutScale = 0.45f;
        public const float MaxFallSpeed = 20f;
        public const float CoyoteSeconds = 0.1f;
        public const float JumpBufferSeconds = 0.12f;
        public const float DashSpeed = 17f;
        public const float DashSeconds = 0.16f;
        public const float DashCooldown = 0.45f;
        public const float DashCost = 30f;
        public const float SprintCostPerSecond = 22f;
        /// <summary>How long a knock keeps the controls from steering the body.</summary>
        public const float KnockSeconds = 0.25f;

        const float MaxStep = 0.2f;
        const float Skin = 0.002f;
        const float LandingSpeed = -2f;

        float sinceGrounded = CoyoteSeconds;
        float jumpBuffer;
        bool jumping;
        bool jumpedSinceGround;
        float dashLeft;
        float dashCooldownLeft;
        float knockLeft;

        public Vec2 Position { get; private set; }
        public Vec2 Velocity { get; private set; }
        public bool Grounded { get; private set; }

        /// <summary>1 when the body faces right, -1 when it faces left.</summary>
        public int Facing { get; private set; } = 1;

        public bool Dashing => dashLeft > 0f;
        public bool Sprinting { get; private set; }
        public Box Bounds => Box.FromFeet(Position, HalfWidth, Height);

        /// <summary>The highest a full jump lifts the feet.</summary>
        public static float MaxJumpHeight => JumpSpeed * JumpSpeed / (2f * Gravity);

        /// <summary>
        /// How far a full jump at <paramref name="speed"/> carries the body sideways before its
        /// feet come back down to <paramref name="rise"/> above where it took off; 0 when that
        /// height is out of reach.
        /// </summary>
        public static float JumpReach(float rise, float speed = RunSpeed)
        {
            if (rise > MaxJumpHeight)
            {
                return 0f;
            }
            float up = JumpSpeed / Gravity;
            float down = (float)Math.Sqrt(2f * (MaxJumpHeight - rise) / (Gravity * FallGravityScale));
            return speed * (up + down);
        }

        /// <summary>Puts the body down at <paramref name="feet"/>, standing still.</summary>
        public void Place(Vec2 feet)
        {
            Position = feet;
            Velocity = Vec2.Zero;
            Grounded = false;
            sinceGrounded = CoyoteSeconds;
            jumpBuffer = 0f;
            jumping = false;
            dashLeft = 0f;
            knockLeft = 0f;
        }

        /// <summary>Launches the body upwards, as when it lands on a critter.</summary>
        public void Bounce(float speed)
        {
            Velocity = new Vec2(Velocity.X, speed);
            Grounded = false;
            jumping = false;
            sinceGrounded = CoyoteSeconds;
            dashLeft = 0f;
        }

        /// <summary>Throws the body, as when it is hurt; the controls wait a moment.</summary>
        public void Knock(float vx, float vy)
        {
            Velocity = new Vec2(vx, vy);
            Grounded = false;
            jumping = false;
            sinceGrounded = CoyoteSeconds;
            dashLeft = 0f;
            knockLeft = KnockSeconds;
        }

        public MotorEvents Tick(MotorInput input, float dt, ICollisionWorld world, Meter stamina)
        {
            MotorEvents events = MotorEvents.None;
            bool steering = knockLeft <= 0f;
            knockLeft = Math.Max(0f, knockLeft - dt);
            float move = steering ? Math.Max(-1f, Math.Min(1f, input.Move)) : 0f;
            if (Math.Abs(move) > 0.01f && !Dashing)
            {
                Facing = move > 0f ? 1 : -1;
            }
            jumpBuffer = input.JumpPressed ? JumpBufferSeconds : Math.Max(0f, jumpBuffer - dt);
            dashCooldownLeft = Math.Max(0f, dashCooldownLeft - dt);

            if (steering && input.DashPressed && !Dashing && dashCooldownLeft <= 0f && stamina.Spend(DashCost))
            {
                dashLeft = DashSeconds;
                dashCooldownLeft = DashSeconds + DashCooldown;
                jumping = false;
                events |= MotorEvents.Dashed;
            }

            float vx = Velocity.X;
            float vy = Velocity.Y;
            float dy;
            if (Dashing)
            {
                dashLeft -= dt;
                vx = Facing * DashSpeed;
                vy = 0f;
                dy = 0f;
                Sprinting = false;
                if (dashLeft <= 0f)
                {
                    dashLeft = 0f;
                    vx = Facing * SprintSpeed;
                }
            }
            else
            {
                Sprinting = steering && input.Sprint && Math.Abs(move) > 0.01f && !stamina.IsEmpty;
                if (Sprinting)
                {
                    stamina.Drain(SprintCostPerSecond * dt);
                }
                float target = move * (Sprinting ? SprintSpeed : RunSpeed);
                float acceleration = (Grounded ? GroundAcceleration : AirAcceleration) * dt;
                if (steering)
                {
                    vx = MoveTowards(vx, target, acceleration);
                }

                bool jump = jumpBuffer > 0f && !jumpedSinceGround && (Grounded || sinceGrounded < CoyoteSeconds);
                if (jump)
                {
                    vy = JumpSpeed;
                    jumpBuffer = 0f;
                    jumping = true;
                    jumpedSinceGround = true;
                    Grounded = false;
                    events |= MotorEvents.Jumped;
                }
                else if (jumping && !input.JumpHeld && vy > 0f)
                {
                    vy *= JumpCutScale;
                    jumping = false;
                }

                float start = vy;
                float gravity = Gravity * (vy <= 0f ? FallGravityScale : 1f);
                vy = Math.Max(-MaxFallSpeed, vy - gravity * dt);
                dy = (start + vy) * 0.5f * dt;
            }

            bool wasGrounded = Grounded;
            float fallSpeed = vy;
            float dx = vx * dt;
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) / MaxStep));
            for (int i = 0; i < steps; i++)
            {
                if (MoveX(dx / steps, world))
                {
                    vx = 0f;
                }
                int hit = MoveY(dy / steps, world);
                if (hit != 0)
                {
                    if (hit > 0 && vy > 0f)
                    {
                        events |= MotorEvents.Bumped;
                        jumping = false;
                    }
                    vy = 0f;
                    dy = 0f;
                }
            }

            Grounded = vy <= 0f && Supported(world);
            if (Grounded)
            {
                vy = 0f;
                sinceGrounded = 0f;
                jumpedSinceGround = false;
                jumping = false;
                if (!wasGrounded && fallSpeed < LandingSpeed)
                {
                    events |= MotorEvents.Landed;
                }
            }
            else
            {
                sinceGrounded += dt;
            }
            Velocity = new Vec2(vx, vy);
            return events;
        }

        static float MoveTowards(float value, float target, float step)
        {
            if (Math.Abs(target - value) <= step)
            {
                return target;
            }
            return value + Math.Sign(target - value) * step;
        }

        /// <summary>Moves sideways; true when a wall stopped the body.</summary>
        bool MoveX(float dx, ICollisionWorld world)
        {
            if (dx == 0f)
            {
                return false;
            }
            float x = Position.X + dx;
            Box body = new Box(x - HalfWidth, Position.Y + Skin, x + HalfWidth, Position.Y + Height - Skin);
            bool hit = false;
            foreach (Box solid in world.Solids)
            {
                if (!body.Overlaps(solid))
                {
                    continue;
                }
                x = dx > 0f ? Math.Min(x, solid.MinX - HalfWidth) : Math.Max(x, solid.MaxX + HalfWidth);
                hit = true;
            }
            Position = new Vec2(x, Position.Y);
            return hit;
        }

        /// <summary>Moves up or down; 1 when a ceiling stopped the body, -1 when the floor did, 0 otherwise.</summary>
        int MoveY(float dy, ICollisionWorld world)
        {
            if (dy == 0f)
            {
                return 0;
            }
            float bottom = Position.Y;
            float y = bottom + dy;
            Box body = new Box(Position.X - HalfWidth + Skin, y, Position.X + HalfWidth - Skin, y + Height);
            int hit = 0;
            foreach (Box solid in world.Solids)
            {
                if (!body.Overlaps(solid))
                {
                    continue;
                }
                if (dy > 0f)
                {
                    y = Math.Min(y, solid.MinY - Height);
                    hit = 1;
                }
                else
                {
                    y = Math.Max(y, solid.MaxY);
                    hit = -1;
                }
            }
            if (dy < 0f)
            {
                foreach (Box platform in world.Platforms)
                {
                    if (bottom >= platform.MaxY - Skin && y < platform.MaxY &&
                        body.MinX < platform.MaxX && body.MaxX > platform.MinX)
                    {
                        y = Math.Max(y, platform.MaxY);
                        hit = -1;
                    }
                }
            }
            Position = new Vec2(Position.X, y);
            return hit;
        }

        /// <summary>True when the feet rest on a solid or a platform.</summary>
        bool Supported(ICollisionWorld world)
        {
            float bottom = Position.Y;
            var probe = new Box(Position.X - HalfWidth + Skin, bottom - 0.02f, Position.X + HalfWidth - Skin, bottom + Skin);
            foreach (Box solid in world.Solids)
            {
                if (probe.Overlaps(solid) && solid.MaxY <= bottom + Skin)
                {
                    return true;
                }
            }
            foreach (Box platform in world.Platforms)
            {
                if (probe.Overlaps(platform) && Math.Abs(platform.MaxY - bottom) <= 0.02f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
