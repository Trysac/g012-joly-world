using System;

namespace JolyWorld.Core
{
    /// <summary>A point or a velocity on the side-view plane: x to the right, y up, in metres.</summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero => new Vec2(0f, 0f);

        public float Length => (float)Math.Sqrt(X * X + Y * Y);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float k) => new Vec2(a.X * k, a.Y * k);

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }

    /// <summary>An axis-aligned rectangle on the side-view plane.</summary>
    public readonly struct Box
    {
        public readonly float MinX;
        public readonly float MinY;
        public readonly float MaxX;
        public readonly float MaxY;

        public Box(float minX, float minY, float maxX, float maxY)
        {
            MinX = Math.Min(minX, maxX);
            MinY = Math.Min(minY, maxY);
            MaxX = Math.Max(minX, maxX);
            MaxY = Math.Max(minY, maxY);
        }

        /// <summary>A body standing with its feet (bottom centre) at <paramref name="feet"/>.</summary>
        public static Box FromFeet(Vec2 feet, float halfWidth, float height) =>
            new Box(feet.X - halfWidth, feet.Y, feet.X + halfWidth, feet.Y + height);

        public static Box Around(Vec2 center, float halfWidth, float halfHeight) =>
            new Box(center.X - halfWidth, center.Y - halfHeight, center.X + halfWidth, center.Y + halfHeight);

        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;
        public Vec2 Center => new Vec2((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);

        /// <summary>True when the boxes share some area; boxes that only touch do not overlap.</summary>
        public bool Overlaps(Box other) =>
            MinX < other.MaxX && other.MinX < MaxX && MinY < other.MaxY && other.MinY < MaxY;

        public bool Contains(Vec2 point) =>
            point.X >= MinX && point.X <= MaxX && point.Y >= MinY && point.Y <= MaxY;

        /// <summary>The box grown by <paramref name="x"/> on the left and right and <paramref name="y"/> above and below.</summary>
        public Box Grow(float x, float y) => new Box(MinX - x, MinY - y, MaxX + x, MaxY + y);

        public override string ToString() => $"[{MinX:0.##}..{MaxX:0.##} x {MinY:0.##}..{MaxY:0.##}]";
    }
}
