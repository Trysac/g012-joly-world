using System.Collections.Generic;
using JolyWorld.Core;
using UnityEngine;

namespace JolyWorld.Game
{
    /// <summary>
    /// Builds a level's world from its core data: the grassy ground and cliffs of the path, the
    /// plank and mushroom ledges, the river, a hilly backdrop with round green, yellow and purple
    /// trees, fences, snowy mountains and clouds (merged into a few static meshes), and the coins,
    /// gems, potions, critters, gates, flags and the rainbow goal, which it keeps in step with the
    /// <see cref="LevelRun"/>.
    /// </summary>
    public sealed class LevelView : MonoBehaviour
    {
        public const float WaterLevel = -1.2f;
        const float LaneFront = -2.2f;
        const float LaneBack = 2.4f;

        sealed class ThemeLook
        {
            public Color Grass, GrassSide, Dirt, DirtDark, Skirt;
            public Color[] Leaves;
            public bool Fences, Pines, Mushrooms, River;
            public float Hills;
        }

        GameArt art;
        Level level;
        ThemeLook look;
        readonly List<Transform> pickups = new List<Transform>();
        readonly List<Vector3> pickupHomes = new List<Vector3>();
        readonly List<Transform> critters = new List<Transform>();
        readonly List<Transform> wings = new List<Transform>();
        readonly List<float> critterGone = new List<float>();
        readonly List<Transform> gates = new List<Transform>();
        readonly List<float> gateOpenAt = new List<float>();
        readonly List<Transform> flags = new List<Transform>();
        readonly List<bool> flagRaised = new List<bool>();
        readonly List<Transform> clouds = new List<Transform>();
        readonly List<Transform> bolts = new List<Transform>();
        Transform goal;
        float time;

        public Level Level => level;

        /// <summary>The pickup objects, in the level's pickup order (tests read their visibility).</summary>
        public IReadOnlyList<Transform> Pickups => pickups;

        public IReadOnlyList<Transform> Critters => critters;
        public IReadOnlyList<Transform> Gates => gates;

        public static LevelView Create(Level level, GameArt art, Transform parent = null)
        {
            var view = new GameObject("Level " + level.Name).AddComponent<LevelView>();
            view.transform.SetParent(parent, false);
            view.Build(level, art);
            return view;
        }

        static ThemeLook LookOf(Theme theme) => theme switch
        {
            Theme.Riverside => new ThemeLook
            {
                Grass = Palette.Grass, GrassSide = Palette.GrassDark, Dirt = Palette.Hex("#c4a87a"), DirtDark = Palette.Hex("#a08a62"),
                Skirt = Palette.GrassDark, Leaves = new[] { Palette.Leaf, Palette.Leaf, Palette.LeafPurple, Palette.LeafPink, Palette.LeafDark },
                Fences = true, River = true, Hills = 2.5f,
            },
            Theme.Forest => new ThemeLook
            {
                Grass = Palette.Hex("#4cc84a"), GrassSide = Palette.Hex("#2f9a3c"), Dirt = Palette.Hex("#9a7a62"), DirtDark = Palette.Hex("#7a5e4a"),
                Skirt = Palette.Hex("#2f9a3c"), Leaves = new[] { Palette.LeafPurple, Palette.LeafPurple, Palette.LeafPink, Palette.Leaf, Palette.LeafYellow, Palette.LeafDark },
                Mushrooms = true, Hills = 2.5f,
            },
            Theme.Mountain => new ThemeLook
            {
                Grass = Palette.Snow, GrassSide = Palette.Hex("#dfe8f4"), Dirt = Palette.Stone, DirtDark = Palette.StoneDark,
                Skirt = Palette.Ice, Leaves = new[] { Palette.Pine, Palette.Pine, Palette.Pine, Palette.Leaf },
                Pines = true, Hills = 4.5f,
            },
            _ => new ThemeLook
            {
                Grass = Palette.Grass, GrassSide = Palette.GrassDark, Dirt = Palette.Hex("#d6b07a"), DirtDark = Palette.Hex("#b08c5c"),
                Skirt = Palette.GrassDark, Leaves = new[] { Palette.Leaf, Palette.Leaf, Palette.LeafDark, Palette.LeafYellow, Palette.LeafPurple },
                Fences = true, River = true, Hills = 2f,
            },
        };

        void Build(Level data, GameArt gameArt)
        {
            level = data;
            art = gameArt;
            look = LookOf(level.Theme);
            int seed = 101 + level.Index * 17;

            var ground = new MeshBuilder(seed);
            foreach (Box box in level.Ground)
            {
                if (box.MaxY > 8.5f)
                {
                    Cliff(ground, box);
                }
                else
                {
                    Terrain(ground, box);
                }
            }
            foreach (Box ledge in level.Platforms)
            {
                Ledge(ground, ledge);
            }
            foreach (Box spikes in level.Spikes)
            {
                Thorns(ground, spikes);
            }
            Static("Path", ground, art.LowPoly, true);

            var backdrop = new MeshBuilder(seed + 1) { Variation = 0.04f };
            Backdrop(backdrop);
            Static("Backdrop", backdrop, art.LowPoly, true);

            var trees = new MeshBuilder(seed + 2);
            Trees(trees);
            Fences(trees);
            Static("Trees", trees, art.LowPoly, true);

            var far = new MeshBuilder(seed + 3) { Variation = 0.03f };
            Mountains(far);
            Static("Mountains", far, art.LowPoly, false);

            var water = new MeshBuilder(seed + 4) { Variation = 0f };
            Water(water);
            Static("River", water, art.Water, false);

            Clouds(seed + 5);
            Things();
        }

        GameObject Static(string name, MeshBuilder builder, Material material, bool shadows)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);
            host.AddComponent<MeshFilter>().sharedMesh = builder.ToMesh(name);
            var renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = shadows;
            return host;
        }

        /// <summary>A piece of the path: earth under a thick grass (or snow) top with a jagged fringe, as in 2021.</summary>
        void Terrain(MeshBuilder b, Box box)
        {
            float top = box.MaxY;
            float bottom = Mathf.Max(box.MinY, WaterLevel - 3f);
            const float slab = 0.32f;
            b.Box(new Vector3(box.MinX, bottom, LaneFront), new Vector3(box.MaxX, top - slab, LaneBack + 3f), look.Dirt, look.Grass);
            // Earth strata on the front face.
            for (float y = top - slab - 0.7f; y > bottom + 0.3f; y -= 0.9f)
            {
                b.Box(new Vector3(box.MinX - 0.02f, y - 0.12f, LaneFront - 0.04f), new Vector3(box.MaxX + 0.02f, y, LaneFront + 0.2f), look.DirtDark);
            }
            for (float x = box.MinX + 0.6f; x < box.MaxX - 0.4f; x += b.Range(1.4f, 2.6f))
            {
                float y = b.Range(bottom + 0.6f, top - slab - 0.4f);
                if (y > WaterLevel - 0.4f)
                {
                    b.Blob(new Vector3(x, y, LaneFront - 0.05f), new Vector3(0.28f, 0.2f, 0.18f), b.Range(0f, 1f) < 0.5f ? Palette.Stone : Palette.StoneDark, 0.2f, 0);
                }
            }
            // The grass slab, a touch wider than the earth, and its fringe of points.
            b.Box(new Vector3(box.MinX - 0.06f, top - slab, LaneFront - 0.08f), new Vector3(box.MaxX + 0.06f, top, LaneBack + 3f), look.GrassSide, look.Grass);
            for (float x = box.MinX; x < box.MaxX - 0.05f; x += 0.42f)
            {
                float width = Mathf.Min(0.42f, box.MaxX - x);
                float drop = b.Range(0.28f, 0.55f);
                b.Triangle(new Vector3(x, top - slab, LaneFront - 0.081f), new Vector3(x + width * 0.5f, top - slab - drop, LaneFront - 0.081f),
                    new Vector3(x + width, top - slab, LaneFront - 0.081f), look.Skirt);
            }
            // Tufts and flowers along the path's back.
            if (level.Theme != Theme.Mountain)
            {
                for (float x = box.MinX + 0.5f; x < box.MaxX - 0.5f; x += b.Range(1.5f, 3.5f))
                {
                    float z = b.Range(1.2f, 2.2f);
                    if (b.Range(0f, 1f) < 0.5f)
                    {
                        b.Blob(new Vector3(x, top + 0.15f, z), new Vector3(0.4f, 0.28f, 0.35f), look.GrassSide, 0.2f, 0);
                    }
                    else
                    {
                        Color petal = b.Range(0f, 1f) < 0.5f ? Palette.Hex("#ffffff") : Palette.Hex("#ff9ad0");
                        b.Prism(new Vector3(x, top, z), 0.03f, 0.03f, 0.35f, 4, Palette.GrassDark);
                        b.Blob(new Vector3(x, top + 0.38f, z), Vector3.one * 0.1f, petal, 0f, 0);
                    }
                }
            }
            else
            {
                for (float x = box.MinX + 0.8f; x < box.MaxX - 0.5f; x += b.Range(2f, 4f))
                {
                    b.Blob(new Vector3(x, top + 0.1f, b.Range(1.2f, 2.2f)), new Vector3(0.45f, 0.3f, 0.4f), Palette.Stone, 0.25f, 0);
                }
            }
        }

        /// <summary>A rocky cliff closing an end of the level, grassy (or snowy) on top.</summary>
        void Cliff(MeshBuilder b, Box box)
        {
            Color rock = level.Theme == Theme.Mountain ? Palette.Hex("#8a9ab8") : Palette.Hex("#a8a8b4");
            b.Box(new Vector3(box.MinX, WaterLevel - 3f, LaneFront + 0.4f), new Vector3(box.MaxX, box.MaxY - 0.4f, LaneBack + 4f), rock, look.Grass);
            for (float y = WaterLevel; y < box.MaxY; y += 1.6f)
            {
                for (float x = box.MinX; x <= box.MaxX; x += 2f)
                {
                    float r = b.Range(1.1f, 1.6f);
                    b.Blob(new Vector3(x + b.Range(-0.4f, 0.4f), y, LaneFront + b.Range(0.3f, 0.9f)), new Vector3(r, r * 0.8f, r), Palette.Shade(rock, b.Range(-0.12f, 0.08f)), 0.25f, 1);
                }
            }
            b.Blob(new Vector3(box.Center.X, box.MaxY - 0.2f, 0.5f), new Vector3(box.Width * 0.75f, 0.7f, 3.4f), look.Grass, 0.15f, 1);
        }

        void Ledge(MeshBuilder b, Box ledge)
        {
            float top = ledge.MaxY;
            if (level.Theme == Theme.Mountain)
            {
                b.Box(new Vector3(ledge.MinX, top - 0.5f, -1.4f), new Vector3(ledge.MaxX, top - 0.12f, 1.4f), Palette.Stone, Palette.Stone);
                b.Box(new Vector3(ledge.MinX - 0.05f, top - 0.12f, -1.45f), new Vector3(ledge.MaxX + 0.05f, top, 1.45f), Palette.Hex("#dfe8f4"), Palette.Snow);
                b.Blob(new Vector3(ledge.Center.X, top - 0.6f, 0f), new Vector3(ledge.Width * 0.4f, 0.45f, 1.1f), Palette.StoneDark, 0.2f);
                return;
            }
            if (look.Mushrooms)
            {
                // A giant mushroom: the cap is the ledge, the stem runs down to the ground.
                float x = ledge.Center.X;
                b.Prism(new Vector3(x, WaterLevel - 2f, 0.6f), 0.35f, 0.3f, top - 0.3f - (WaterLevel - 2f), 6, Palette.Hex("#f2e6d0"));
                b.TwoToneBlob(new Vector3(x, top - 0.25f, 0f), new Vector3(ledge.Width * 0.55f, 0.45f, 1.5f), Palette.Hex("#f2e6d0"), Palette.Hex("#c25ad8"), 0.25f, 0.05f);
                b.Box(new Vector3(ledge.MinX, top - 0.08f, -1.2f), new Vector3(ledge.MaxX, top, 1.2f), Palette.Hex("#b04ac8"), Palette.Hex("#d070e8"));
                for (int i = 0; i < 4; i++)
                {
                    b.Blob(new Vector3(ledge.MinX + (i + 0.5f) * ledge.Width / 4f, top + 0.02f, b.Range(-0.8f, 0.8f)), new Vector3(0.14f, 0.05f, 0.14f), Color.white, 0f, 0);
                }
                return;
            }
            // A wooden plank bridge with posts down into the water.
            b.Box(new Vector3(ledge.MinX, top - 0.22f, -1.3f), new Vector3(ledge.MaxX, top, 1.3f), Palette.Wood, Palette.WoodLight);
            for (float x = ledge.MinX + 0.4f; x < ledge.MaxX; x += 0.7f)
            {
                b.Box(new Vector3(x - 0.03f, top - 0.005f, -1.31f), new Vector3(x + 0.03f, top + 0.005f, 1.31f), Palette.WoodDark);
            }
            foreach (float x in new[] { ledge.MinX + 0.25f, ledge.MaxX - 0.25f })
            {
                b.Box(new Vector3(x - 0.12f, WaterLevel - 2f, -1.25f), new Vector3(x + 0.12f, top - 0.22f, -1.05f), Palette.WoodDark);
                b.Box(new Vector3(x - 0.12f, WaterLevel - 2f, 1.05f), new Vector3(x + 0.12f, top - 0.22f, 1.25f), Palette.WoodDark);
            }
        }

        void Thorns(MeshBuilder b, Box spikes)
        {
            bool ice = level.Theme == Theme.Mountain;
            Color body = ice ? Palette.Ice : Palette.Thorn;
            for (float x = spikes.MinX + 0.15f; x < spikes.MaxX; x += 0.35f)
            {
                for (int row = -1; row <= 1; row++)
                {
                    float height = b.Range(0.55f, 0.95f);
                    b.Transform = Matrix4x4.TRS(new Vector3(x + b.Range(-0.1f, 0.1f), spikes.MinY, row * 0.6f),
                        Quaternion.Euler(b.Range(-15f, 15f), 0f, b.Range(-15f, 15f)), Vector3.one);
                    b.Prism(Vector3.zero, 0.16f, 0f, height, 5, body);
                    if (!ice && row == -1)
                    {
                        b.Blob(new Vector3(0.05f, height * 0.45f, -0.12f), Vector3.one * 0.07f, Palette.Berry, 0f, 0);
                    }
                }
            }
            b.Transform = Matrix4x4.identity;
        }

        /// <summary>The height of the path at <paramref name="x"/> (the top of the ground there, walls ignored).</summary>
        float PathHeight(float x)
        {
            float best = WaterLevel - 1.5f;
            foreach (Box box in level.Ground)
            {
                if (box.MaxY < 8.5f && x >= box.MinX - 3f && x <= box.MaxX + 3f)
                {
                    best = Mathf.Max(best, box.MaxY);
                }
            }
            return best;
        }

        float Height(float x, float z)
        {
            float near = PathHeight(x) - 0.4f;
            float hills = look.Hills * Mathf.PerlinNoise(x * 0.035f + level.Index * 10f, z * 0.06f) + (z - 6f) * 0.06f * look.Hills;
            float rise = Mathf.Clamp01((z - 5f) / 18f);
            float h = Mathf.Lerp(near, Mathf.Max(near, hills + near * 0.5f), rise);
            if (look.River)
            {
                float river = Mathf.Abs(z - (16f + 4f * Mathf.Sin(x * 0.05f)));
                if (river < 4.5f)
                {
                    h = Mathf.Lerp(WaterLevel - 1.5f, h, Mathf.SmoothStep(0f, 1f, (river - 2f) / 2.5f));
                }
            }
            return h;
        }

        /// <summary>A hilly low-poly field behind the path, rising towards the mountains.</summary>
        void Backdrop(MeshBuilder b)
        {
            const float step = 3f;
            float minX = -45f, maxX = level.Width + 45f;
            int columns = Mathf.CeilToInt((maxX - minX) / step);
            float[] zs = { 4f, 7f, 10f, 13f, 16f, 19f, 22f, 26f, 30f, 35f, 41f, 48f, 56f, 66f, 78f };
            var heights = new float[columns + 1, zs.Length];
            for (int i = 0; i <= columns; i++)
            {
                for (int j = 0; j < zs.Length; j++)
                {
                    float x = minX + i * step + (j % 2) * 0.8f;
                    heights[i, j] = Height(x, zs[j]) + b.Range(-0.25f, 0.25f);
                }
            }
            for (int i = 0; i < columns; i++)
            {
                for (int j = 0; j < zs.Length - 1; j++)
                {
                    Vector3 a = new Vector3(minX + i * step, heights[i, j], zs[j]);
                    Vector3 c = new Vector3(minX + (i + 1) * step, heights[i + 1, j], zs[j]);
                    Vector3 d = new Vector3(minX + i * step, heights[i, j + 1], zs[j + 1]);
                    Vector3 e = new Vector3(minX + (i + 1) * step, heights[i + 1, j + 1], zs[j + 1]);
                    b.Triangle(a, d, e, FieldColor(a, d, e));
                    b.Triangle(a, e, c, FieldColor(a, e, c));
                }
            }
        }

        Color FieldColor(Vector3 a, Vector3 b, Vector3 c)
        {
            float y = (a.y + b.y + c.y) / 3f;
            float slope = Vector3.Cross(b - a, c - a).normalized.y;
            if (y < WaterLevel + 0.2f)
            {
                return Palette.Hex("#d8c890");
            }
            if (level.Theme == Theme.Mountain)
            {
                return y > 13f || (y > 9f && slope > 0.85f) ? Palette.Snow : slope < 0.7f ? Palette.Hex("#8a9ab8") : Palette.Hex("#7fae7a");
            }
            if (slope < 0.6f)
            {
                return Palette.Shade(look.GrassSide, -0.05f);
            }
            return level.Theme == Theme.Forest ? Palette.Hex("#3fae44") : Palette.Shade(look.Grass, -0.05f);
        }

        void Trees(MeshBuilder b)
        {
            float area = level.Width + 60f;
            int count = Mathf.RoundToInt(area / (look.Mushrooms ? 1.1f : level.Theme == Theme.Mountain ? 2f : 3f));
            for (int i = 0; i < count; i++)
            {
                float x = b.Range(-30f, level.Width + 30f);
                float z = 5.5f + Mathf.Pow(b.Range(0f, 1f), 1.6f) * 34f;
                float y = Height(x, z);
                if (y < WaterLevel + 0.3f)
                {
                    continue;
                }
                Color leaves = look.Leaves[(int)b.Range(0f, look.Leaves.Length - 0.001f)];
                float scale = b.Range(0.8f, 1.35f) * (look.Mushrooms ? 1.2f : 1f);
                if (look.Pines && leaves == Palette.Pine)
                {
                    Pine(b, new Vector3(x, y - 0.2f, z), scale);
                }
                else
                {
                    RoundTree(b, new Vector3(x, y - 0.2f, z), scale, leaves);
                }
            }
            if (look.Mushrooms)
            {
                for (int i = 0; i < count / 3; i++)
                {
                    float x = b.Range(-20f, level.Width + 20f), z = b.Range(3.5f, 9f);
                    float y = Height(x, z);
                    float s = b.Range(0.5f, 1.1f);
                    b.Prism(new Vector3(x, y, z), 0.16f * s, 0.12f * s, 0.8f * s, 5, Palette.Hex("#f2e6d0"));
                    b.TwoToneBlob(new Vector3(x, y + 0.85f * s, z), new Vector3(0.55f, 0.3f, 0.55f) * s, Palette.Hex("#f2e6d0"),
                        b.Range(0f, 1f) < 0.5f ? Palette.Hex("#e04a5a") : Palette.Hex("#c25ad8"), 0.2f, 0.05f);
                }
            }
        }

        static void RoundTree(MeshBuilder b, Vector3 foot, float scale, Color leaves)
        {
            float height = b.Range(2.4f, 3.6f) * scale;
            float lean = b.Range(-10f, 10f);
            Matrix4x4 keep = b.Transform;
            b.Transform = Matrix4x4.TRS(foot, Quaternion.Euler(b.Range(-4f, 4f), 0f, lean), Vector3.one);
            b.Prism(Vector3.zero, 0.24f * scale, 0.16f * scale, height, 5, Palette.Trunk);
            Vector3 top = Vector3.up * height;
            float r = b.Range(1.3f, 1.8f) * scale;
            b.Blob(top + new Vector3(0f, r * 0.5f, 0f), new Vector3(r, r * 0.85f, r), leaves, 0.14f);
            int extra = (int)b.Range(0f, 2.99f);
            for (int i = 0; i < extra; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float rr = r * b.Range(0.55f, 0.75f);
                b.Blob(top + new Vector3(side * r * 0.75f, r * b.Range(0f, 0.4f), b.Range(-0.4f, 0.4f)), new Vector3(rr, rr * 0.85f, rr),
                    Palette.Shade(leaves, b.Range(-0.08f, 0.08f)), 0.14f);
            }
            b.Transform = keep;
        }

        void Pine(MeshBuilder b, Vector3 foot, float scale)
        {
            b.Prism(foot, 0.2f * scale, 0.15f * scale, 1.2f * scale, 5, Palette.Trunk);
            float y = 0.8f * scale;
            float radius = 1.6f * scale;
            for (int tier = 0; tier < 3; tier++)
            {
                float h = (1.9f - tier * 0.3f) * scale;
                b.Prism(foot + Vector3.up * y, radius, 0f, h, 7, Palette.Pine, null, b.Range(0f, 1f));
                if (level.Theme == Theme.Mountain)
                {
                    b.Prism(foot + Vector3.up * (y + h * 0.62f), radius * 0.4f, 0f, h * 0.38f + 0.02f, 7, Palette.Snow, null, b.Range(0f, 1f));
                }
                y += h * 0.55f;
                radius *= 0.72f;
            }
        }

        /// <summary>Wooden fences along the back of the path, like the 2021 meadow's.</summary>
        void Fences(MeshBuilder b)
        {
            if (!look.Fences)
            {
                return;
            }
            Color post = Palette.Hex("#8a6a48");
            Color rail = Palette.Hex("#6a5a4a");
            foreach (Box box in level.Ground)
            {
                if (box.MaxY > 8.5f || box.Width < 3f)
                {
                    continue;
                }
                float top = box.MaxY;
                const float z = 1.9f;
                for (float x = box.MinX + 0.5f; x <= box.MaxX - 0.4f; x += 2.2f)
                {
                    b.Box(new Vector3(x - 0.11f, top - 0.1f, z - 0.11f), new Vector3(x + 0.11f, top + 1.05f, z + 0.11f), post, Palette.WoodLight);
                }
                b.Box(new Vector3(box.MinX + 0.4f, top + 0.42f, z - 0.05f), new Vector3(box.MaxX - 0.4f, top + 0.56f, z + 0.05f), rail);
                b.Box(new Vector3(box.MinX + 0.4f, top + 0.78f, z - 0.05f), new Vector3(box.MaxX - 0.4f, top + 0.92f, z + 0.05f), rail);
            }
        }

        void Mountains(MeshBuilder b)
        {
            float span = level.Width + 160f;
            int count = Mathf.CeilToInt(span / 38f);
            for (int i = 0; i < count; i++)
            {
                float x = -80f + i * 38f + b.Range(-10f, 10f);
                float height = b.Range(32f, 58f) * (level.Theme == Theme.Mountain ? 1.25f : 1f);
                float radius = height * b.Range(0.75f, 1f);
                float z = b.Range(105f, 140f);
                b.Mountain(new Vector3(x, -6f, z), radius, height, 7, Palette.Hex("#cddcf0"), Palette.Snow, height * 0.45f);
            }
            for (int i = 0; i < count; i++)
            {
                float x = -60f + i * 38f + b.Range(0f, 20f);
                float height = b.Range(18f, 30f);
                b.Mountain(new Vector3(x, -8f, b.Range(88f, 100f)), height * 0.9f, height, 6, Palette.Hex("#bccdea"), Palette.Snow, height * 0.6f);
            }
        }

        void Water(MeshBuilder b)
        {
            const float step = 3f;
            float minX = -60f, maxX = level.Width + 60f;
            float[] zs = { -60f, -36f, -22f, -14f, -9f, -6f, -4f, -2.6f, -1f, 2f, 6f, 10f, 14f, 18f, 22f, 28f, 40f, 60f, 90f };
            for (float x = minX; x < maxX; x += step)
            {
                for (int j = 0; j < zs.Length - 1; j++)
                {
                    Vector3 a = new Vector3(x, WaterLevel, zs[j]);
                    Vector3 c = new Vector3(x + step, WaterLevel, zs[j]);
                    Vector3 d = new Vector3(x, WaterLevel, zs[j + 1]);
                    Vector3 e = new Vector3(x + step, WaterLevel, zs[j + 1]);
                    Color tone = Palette.Shade(Palette.Water, b.Range(-0.05f, 0.08f));
                    tone.a = 0.85f;
                    b.Triangle(a, d, e, tone);
                    b.Triangle(a, e, c, Palette.Shade(tone, b.Range(-0.04f, 0.06f)));
                }
            }
        }

        void Clouds(int seed)
        {
            var random = new System.Random(seed);
            float R(float min, float max) => min + (max - min) * (float)random.NextDouble();
            int count = Mathf.CeilToInt((level.Width + 120f) / 22f);
            for (int i = 0; i < count; i++)
            {
                var b = new MeshBuilder(seed + i) { Variation = 0.03f };
                int puffs = 3 + random.Next(3);
                for (int p = 0; p < puffs; p++)
                {
                    float r = R(2.2f, 3.6f);
                    b.Blob(new Vector3(p * 2.6f - puffs * 1.3f, R(-0.5f, 1f), R(-1f, 1f)), new Vector3(r, r * 0.7f, r * 0.8f), Palette.Cloud, 0.15f);
                }
                GameObject cloud = Static("Cloud", b, art.LowPoly, false);
                cloud.transform.localPosition = new Vector3(-50f + i * 22f + R(-6f, 6f), R(20f, 32f), R(50f, 85f));
                clouds.Add(cloud.transform);
            }
        }

        // ---- Things in the level ----

        Transform Thing(string name, Mesh mesh, Material material, Vector3 position, Transform parent = null)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent != null ? parent : transform, false);
            host.transform.localPosition = position;
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return host.transform;
        }

        void Things()
        {
            int gem = 0;
            foreach (PickupSpec spec in level.Pickups)
            {
                Vector3 at = new Vector3(spec.Position.X, spec.Position.Y, 0f);
                Transform t = spec.Kind switch
                {
                    PickupKind.Coin => Thing("Coin", Models.Coin, art.Glowing, at),
                    PickupKind.Gem => Thing("Gem", Models.Gem(gem++), art.Glowing, at),
                    _ => Thing(spec.Item.ToString(), Models.Item(spec.Item), art.LowPoly, at),
                };
                if (spec.Kind == PickupKind.Gem)
                {
                    t.localScale = Vector3.one * 1.1f;
                }
                pickups.Add(t);
                pickupHomes.Add(at);
            }
            foreach (CritterSpec spec in level.Critters)
            {
                Transform t = Thing(spec.Kind.ToString(), Models.Critter(spec.Kind), art.LowPoly, new Vector3(spec.Home.X, spec.Home.Y, 0f));
                if (spec.Kind == CritterKind.Bee)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Transform wing = Thing("Wing", Models.Wing, art.LowPoly, new Vector3(0f, 0.42f, side * 0.12f), t);
                        wings.Add(wing);
                    }
                }
                critters.Add(t);
                critterGone.Add(-1f);
            }
            foreach (Box box in level.Gates)
            {
                gates.Add(Thing("Gate", Models.Gate, art.LowPoly, new Vector3(box.Center.X, box.MinY, 0f)));
                gateOpenAt.Add(-1f);
            }
            foreach (Vec2 point in level.Checkpoints)
            {
                Transform pole = Thing("Checkpoint", Models.FlagPole, art.LowPoly, new Vector3(point.X, point.Y, 1.3f));
                Transform flag = Thing("Flag", Models.Flag, art.LowPoly, new Vector3(0.05f, 0.6f, 0f), pole);
                Tint(flag, Palette.Hex("#b8b8c8"));
                flags.Add(flag);
                flagRaised.Add(false);
            }
            goal = Thing("Goal", Models.Rainbow, art.Glowing, new Vector3(level.Goal.Center.X, level.Goal.MinY, 0f));
            goal.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        public static void Tint(Component target, Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            target.GetComponent<Renderer>().SetPropertyBlock(block);
        }

        /// <summary>Keeps the things in step with the run and animates them.</summary>
        public void Sync(LevelRun run, float dt)
        {
            time += dt;
            for (int i = 0; i < pickups.Count; i++)
            {
                bool show = run == null || !run.Taken(i);
                if (pickups[i].gameObject.activeSelf != show)
                {
                    pickups[i].gameObject.SetActive(show);
                }
                if (!show)
                {
                    continue;
                }
                float phase = pickupHomes[i].x * 0.7f;
                pickups[i].localPosition = pickupHomes[i] + Vector3.up * Mathf.Sin(time * 2.5f + phase) * 0.08f;
                pickups[i].localRotation = Quaternion.Euler(0f, time * 120f + phase * 30f, level.Pickups[i].Kind == PickupKind.Item ? Mathf.Sin(time * 2f + phase) * 8f : 0f);
            }
            for (int c = 0; c < critters.Count; c++)
            {
                Critter critter = run?.Critters[c];
                Transform t = critters[c];
                if (critter == null)
                {
                    continue;
                }
                if (!critter.Alive)
                {
                    if (critterGone[c] < 0f)
                    {
                        critterGone[c] = time;
                    }
                    float age = time - critterGone[c];
                    t.localScale = new Vector3(1.3f, Mathf.Max(0.05f, 1f - age * 5f), 1.3f) * Mathf.Max(0f, 1f - age * 2.5f);
                    if (age > 0.45f && t.gameObject.activeSelf)
                    {
                        t.gameObject.SetActive(false);
                    }
                    continue;
                }
                float step = critter.Kind == CritterKind.Bee ? 0f : Mathf.Abs(Mathf.Sin(time * 10f + c)) * 0.06f;
                t.localPosition = new Vector3(critter.Position.X, critter.Position.Y + step, 0f);
                float yaw = critter.Direction > 0 ? 20f : 160f;
                t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(0f, yaw, 0f), Mathf.Clamp01(dt * 10f));
            }
            for (int w = 0; w < wings.Count; w++)
            {
                wings[w].localRotation = Quaternion.Euler((w % 2 == 0 ? 1f : -1f) * (35f + Mathf.Sin(time * 40f) * 30f), 0f, 0f);
            }
            for (int g = 0; g < gates.Count; g++)
            {
                if (run != null && run.GateOpen(g) && gateOpenAt[g] < 0f)
                {
                    gateOpenAt[g] = time;
                }
                if (gateOpenAt[g] >= 0f)
                {
                    float t = Mathf.Clamp01((time - gateOpenAt[g]) / 0.9f);
                    Box box = level.Gates[g];
                    gates[g].localPosition = new Vector3(box.Center.X, box.MinY - 4.6f * t * t, 0f);
                    gates[g].gameObject.SetActive(t < 1f);
                }
            }
            for (int f = 0; f < flags.Count; f++)
            {
                bool raised = run != null && run.Checkpoint >= f;
                if (raised && !flagRaised[f])
                {
                    flagRaised[f] = true;
                    Tint(flags[f], Palette.Hex("#ff6a4a"));
                }
                Vector3 p = flags[f].localPosition;
                float target = flagRaised[f] ? 2.35f : 0.6f;
                flags[f].localPosition = new Vector3(p.x, Mathf.MoveTowards(p.y, target, dt * 3f), p.z);
                flags[f].localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 3f + f) * 12f, 0f);
            }
            foreach (Transform cloud in clouds)
            {
                cloud.localPosition += Vector3.right * dt * 0.6f;
            }
            SyncBolts(run);
        }

        void SyncBolts(LevelRun run)
        {
            int count = run?.Bolts.Count ?? 0;
            while (bolts.Count < count)
            {
                Transform spark = Thing("Bolt", Models.Spark, art.Glowing, Vector3.zero);
                Tint(spark, Palette.Hex("#bff4ff"));
                for (int i = 0; i < 4; i++)
                {
                    Transform ray = Thing("Ray", Models.Bit, art.Glowing, Vector3.zero, spark);
                    ray.localScale = new Vector3(0.18f, 0.18f, 1.2f);
                    ray.localRotation = Quaternion.Euler(i * 45f, 90f, 0f);
                    Tint(ray, Palette.Hex("#6fd8ff"));
                }
                bolts.Add(spark);
            }
            for (int i = 0; i < bolts.Count; i++)
            {
                bool used = i < count;
                bolts[i].gameObject.SetActive(used);
                if (used)
                {
                    Bolt bolt = run.Bolts[i];
                    bolts[i].localPosition = new Vector3(bolt.Position.X, bolt.Position.Y, 0f);
                    bolts[i].localScale = Vector3.one * (0.65f + Mathf.Sin(time * 40f + i) * 0.1f);
                    bolts[i].localRotation = Quaternion.Euler(time * 400f, time * 300f, 0f);
                }
            }
        }

        /// <summary>The bolts on screen (tests).</summary>
        public int VisibleBolts
        {
            get
            {
                int count = 0;
                foreach (Transform bolt in bolts)
                {
                    count += bolt.gameObject.activeSelf ? 1 : 0;
                }
                return count;
            }
        }
    }
}
