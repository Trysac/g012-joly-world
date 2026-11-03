using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JolyWorld.Game
{
    /// <summary>
    /// Builds flat-shaded low-poly meshes in code: every triangle gets its own three vertices, so
    /// each face keeps its own normal and colour. Colours are given in sRGB and stored linear, for
    /// the "Joly World/Low Poly" shader in a linear-colour project.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();
        readonly System.Random random;

        /// <summary>How much each face's colour may randomly vary (0 = none).</summary>
        public float Variation = 0.05f;

        /// <summary>When set, every vertex is moved by this matrix (to place parts of a model).</summary>
        public Matrix4x4 Transform = Matrix4x4.identity;

        public MeshBuilder(int seed = 1)
        {
            random = new System.Random(seed);
        }

        public int VertexCount => vertices.Count;

        float Next() => (float)random.NextDouble();

        /// <summary>A random number from <paramref name="min"/> to <paramref name="max"/>, from the builder's seed.</summary>
        public float Range(float min, float max) => min + (max - min) * Next();

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            Color face = Variation > 0f ? Palette.Shade(color, (Next() * 2f - 1f) * Variation) : color;
            Color linear = face.linear;
            int start = vertices.Count;
            vertices.Add(Transform.MultiplyPoint3x4(a));
            vertices.Add(Transform.MultiplyPoint3x4(b));
            vertices.Add(Transform.MultiplyPoint3x4(c));
            colors.Add(linear);
            colors.Add(linear);
            colors.Add(linear);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        /// <summary>A quad a-b-c-d, wound clockwise when seen from its front.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            Color face = Variation > 0f ? Palette.Shade(color, (Next() * 2f - 1f) * Variation) : color;
            float keep = Variation;
            Variation = 0f;
            Triangle(a, b, c, face);
            Triangle(a, c, d, face);
            Variation = keep;
        }

        /// <summary>A box from <paramref name="min"/> to <paramref name="max"/> with its own top and side colours.</summary>
        public void Box(Vector3 min, Vector3 max, Color side, Color? top = null, Color? bottom = null)
        {
            Color up = top ?? side;
            Color down = bottom ?? Palette.Shade(side, -0.25f);
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                p[i] = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            }
            Quad(p[0], p[2], p[3], p[1], side);       // front (z min)
            Quad(p[5], p[7], p[6], p[4], side);       // back (z max)
            Quad(p[4], p[6], p[2], p[0], Palette.Shade(side, -0.06f)); // left
            Quad(p[1], p[3], p[7], p[5], Palette.Shade(side, -0.06f)); // right
            Quad(p[2], p[6], p[7], p[3], up);         // top
            Quad(p[4], p[0], p[1], p[5], down);       // bottom
        }

        /// <summary>A box centred on <paramref name="center"/>.</summary>
        public void Block(Vector3 center, Vector3 size, Color side, Color? top = null) =>
            Box(center - size * 0.5f, center + size * 0.5f, side, top);

        /// <summary>A prism standing on <paramref name="bottom"/> (a cylinder, or a cone when the top radius is 0).</summary>
        public void Prism(Vector3 bottom, float radius, float topRadius, float height, int sides, Color side, Color? top = null, float twist = 0f)
        {
            Color cap = top ?? side;
            Vector3 up = bottom + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f + twist;
                float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f + twist;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 b0 = bottom + d0 * radius, b1 = bottom + d1 * radius;
                if (topRadius <= 0.001f)
                {
                    Triangle(b0, up, b1, side);
                }
                else
                {
                    Vector3 t0 = up + d0 * topRadius, t1 = up + d1 * topRadius;
                    Quad(b0, t0, t1, b1, side);
                    Triangle(up, t1, t0, cap);
                }
                Triangle(bottom, b0, b1, Palette.Shade(side, -0.3f));
            }
        }

        /// <summary>
        /// A lumpy low-poly ball (an icosahedron, split once, with seeded bumps): tree crowns,
        /// bushes, rocks, clouds, critters and heads.
        /// </summary>
        public void Blob(Vector3 center, Vector3 radii, Color color, float bumps = 0.12f, int detail = 1)
        {
            List<Vector3> points = Icosphere(detail, out List<int> faces);
            var moved = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                float bump = 1f + (Next() * 2f - 1f) * bumps;
                moved[i] = center + Vector3.Scale(points[i] * bump, radii);
            }
            for (int f = 0; f < faces.Count; f += 3)
            {
                Triangle(moved[faces[f]], moved[faces[f + 1]], moved[faces[f + 2]], color);
            }
        }

        /// <summary>A blob whose lower faces take one colour and upper faces another (snow caps, shells).</summary>
        public void TwoToneBlob(Vector3 center, Vector3 radii, Color low, Color high, float split, float bumps = 0.12f, int detail = 1)
        {
            List<Vector3> points = Icosphere(detail, out List<int> faces);
            var moved = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                float bump = 1f + (Next() * 2f - 1f) * bumps;
                moved[i] = center + Vector3.Scale(points[i] * bump, radii);
            }
            for (int f = 0; f < faces.Count; f += 3)
            {
                float y = (points[faces[f]].y + points[faces[f + 1]].y + points[faces[f + 2]].y) / 3f;
                Triangle(moved[faces[f]], moved[faces[f + 1]], moved[faces[f + 2]], y > split ? high : low);
            }
        }

        /// <summary>A jagged mountain: rings of jittered points rising to a peak, snowy above a height.</summary>
        public void Mountain(Vector3 bottom, float radius, float height, int sides, Color rock, Color snow, float snowLine)
        {
            int rings = 3;
            var ring = new Vector3[rings + 1, sides];
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float ringRadius = radius * (1f - t) * (r == 0 ? 1f : Range(0.85f, 1.1f));
                float y = height * (1f - (1f - t) * (1f - t)) * 0.92f;
                for (int s = 0; s < sides; s++)
                {
                    float angle = (s + Range(-0.25f, 0.25f)) / sides * Mathf.PI * 2f;
                    ring[r, s] = bottom + new Vector3(Mathf.Cos(angle) * ringRadius, y + (r > 0 ? Range(-0.06f, 0.06f) * height : 0f),
                        Mathf.Sin(angle) * ringRadius);
                }
            }
            Vector3 peak = bottom + Vector3.up * height + new Vector3(Range(-0.08f, 0.08f) * radius, 0f, 0f);
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int n = (s + 1) % sides;
                    Vector3 a = ring[r, s], b = ring[r, n];
                    Vector3 c = r + 1 < rings ? ring[r + 1, n] : peak;
                    Vector3 d = r + 1 < rings ? ring[r + 1, s] : peak;
                    Color ca = (a.y + b.y + c.y) / 3f - bottom.y > snowLine ? snow : rock;
                    Triangle(a, d, c, ca);
                    if (r + 1 < rings)
                    {
                        Color cb = (a.y + c.y + b.y) / 3f - bottom.y > snowLine ? snow : rock;
                        Triangle(a, c, b, cb);
                    }
                    else
                    {
                        Triangle(a, peak, b, snow);
                    }
                }
            }
        }

        public void Append(MeshBuilder other)
        {
            int offset = vertices.Count;
            vertices.AddRange(other.vertices);
            colors.AddRange(other.colors);
            foreach (int index in other.triangles)
            {
                triangles.Add(index + offset);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static List<Vector3> cachedPoints;
        static List<int> cachedFaces;
        static int cachedDetail = -1;

        /// <summary>The points and faces of a unit icosphere split <paramref name="detail"/> times.</summary>
        static List<Vector3> Icosphere(int detail, out List<int> faces)
        {
            if (cachedDetail == detail)
            {
                faces = cachedFaces;
                return cachedPoints;
            }
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < points.Count; i++)
            {
                points[i] = points[i].normalized;
            }
            var list = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int d = 0; d < detail; d++)
            {
                var middles = new Dictionary<long, int>();
                var next = new List<int>();
                for (int f = 0; f < list.Count; f += 3)
                {
                    int a = list[f], b = list[f + 1], c = list[f + 2];
                    int ab = Middle(points, middles, a, b), bc = Middle(points, middles, b, c), ca = Middle(points, middles, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                list = next;
            }
            // In Unity's left-handed space this winding already faces outwards.
            cachedPoints = points;
            cachedFaces = list;
            cachedDetail = detail;
            faces = list;
            return points;
        }

        static int Middle(List<Vector3> points, Dictionary<long, int> middles, int a, int b)
        {
            long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            if (middles.TryGetValue(key, out int index))
            {
                return index;
            }
            points.Add(((points[a] + points[b]) * 0.5f).normalized);
            middles[key] = points.Count - 1;
            return points.Count - 1;
        }
    }
}
