using System.Collections.Generic;
using JolyWorld.Core;
using UnityEngine;

namespace JolyWorld.Game
{
    /// <summary>The low-poly meshes of the things in a level, built once in code and shared.</summary>
    public static class Models
    {
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        static Mesh Get(string key, System.Action<MeshBuilder> build, int seed = 7, float variation = 0.05f)
        {
            if (Cache.TryGetValue(key, out Mesh mesh) && mesh != null)
            {
                return mesh;
            }
            var builder = new MeshBuilder(seed) { Variation = variation };
            build(builder);
            mesh = builder.ToMesh(key);
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>A chunky gold coin standing on its edge, facing the camera.</summary>
        public static Mesh Coin => Get("Coin", b =>
        {
            b.Transform = Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f));
            b.Prism(new Vector3(0f, -0.07f, 0f), 0.3f, 0.3f, 0.14f, 10, Palette.GoldDark, Palette.Gold);
            b.Prism(new Vector3(0f, -0.09f, 0f), 0.17f, 0.17f, 0.18f, 10, Palette.Gold, Palette.Shade(Palette.Gold, 0.3f));
        }, variation: 0.02f);

        /// <summary>A long eight-sided gem.</summary>
        public static Mesh Gem(int index) => Get("Gem" + index % 3, b =>
        {
            Color color = Palette.Gem(index);
            Vector3 top = new Vector3(0f, 0.45f, 0f), bottom = new Vector3(0f, -0.45f, 0f);
            const int sides = 6;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.3f;
                Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.3f;
                b.Triangle(p0, top, p1, Palette.Shade(color, i % 2 == 0 ? 0.25f : 0f));
                b.Triangle(p1, bottom, p0, Palette.Shade(color, i % 2 == 0 ? -0.15f : -0.3f));
            }
        }, variation: 0f);

        /// <summary>A round potion bottle with a cork, or a brass key.</summary>
        public static Mesh Item(ItemKind kind) => Get("Item" + kind, b =>
        {
            if (kind == ItemKind.Key)
            {
                b.Transform = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -35f));
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    b.Block(new Vector3(Mathf.Cos(angle) * 0.17f, 0.25f + Mathf.Sin(angle) * 0.17f, 0f), new Vector3(0.09f, 0.09f, 0.08f), Palette.Brass);
                }
                b.Block(new Vector3(0f, -0.08f, 0f), new Vector3(0.08f, 0.5f, 0.08f), Palette.Brass);
                b.Block(new Vector3(0.08f, -0.25f, 0f), new Vector3(0.12f, 0.06f, 0.07f), Palette.Brass);
                b.Block(new Vector3(0.08f, -0.13f, 0f), new Vector3(0.09f, 0.06f, 0.07f), Palette.Brass);
                return;
            }
            Color liquid = Palette.Item(kind);
            b.Blob(new Vector3(0f, -0.05f, 0f), new Vector3(0.26f, 0.26f, 0.26f), liquid, 0.04f);
            b.Prism(new Vector3(0f, 0.15f, 0f), 0.09f, 0.08f, 0.16f, 6, new Color(0.85f, 0.95f, 1f));
            b.Prism(new Vector3(0f, 0.3f, 0f), 0.1f, 0.1f, 0.09f, 6, Palette.WoodLight);
            b.Blob(new Vector3(-0.1f, 0.03f, -0.18f), new Vector3(0.06f, 0.08f, 0.04f), Palette.Shade(liquid, 0.6f), 0f, 0);
        });

        public static Mesh Critter(CritterKind kind) => Get("Critter" + kind, b =>
        {
            switch (kind)
            {
                case CritterKind.Beetle:
                    // A ladybird-like beetle: a red shell with dark spots, a black head and legs.
                    b.TwoToneBlob(new Vector3(0f, 0.3f, 0f), new Vector3(0.48f, 0.32f, 0.42f), Palette.Hex("#2a2030"), Palette.Hex("#e8403a"), -0.1f, 0.06f);
                    for (int i = 0; i < 4; i++)
                    {
                        b.Blob(new Vector3(-0.2f + (i % 2) * 0.32f, 0.5f, -0.18f + (i / 2) * 0.36f), Vector3.one * 0.08f, Palette.Hex("#2a2030"), 0f, 0);
                    }
                    b.Blob(new Vector3(0.42f, 0.25f, 0f), new Vector3(0.18f, 0.16f, 0.2f), Palette.Hex("#2a2030"), 0.05f);
                    b.Blob(new Vector3(0.53f, 0.3f, -0.1f), Vector3.one * 0.05f, Color.white, 0f, 0);
                    b.Blob(new Vector3(0.53f, 0.3f, 0.1f), Vector3.one * 0.05f, Color.white, 0f, 0);
                    for (int i = 0; i < 3; i++)
                    {
                        float x = -0.25f + i * 0.25f;
                        b.Block(new Vector3(x, 0.06f, -0.38f), new Vector3(0.06f, 0.12f, 0.12f), Palette.Hex("#2a2030"));
                        b.Block(new Vector3(x, 0.06f, 0.38f), new Vector3(0.06f, 0.12f, 0.12f), Palette.Hex("#2a2030"));
                    }
                    break;
                case CritterKind.Bee:
                    // A round bumblebee: yellow and black bands, a sting and white wings (animated separately).
                    for (int i = 0; i < 4; i++)
                    {
                        b.Blob(new Vector3(-0.15f + i * 0.1f, 0.25f, 0f), new Vector3(0.12f, 0.24f - Mathf.Abs(i - 1.5f) * 0.04f, 0.24f - Mathf.Abs(i - 1.5f) * 0.04f),
                            i % 2 == 0 ? Palette.Hex("#ffcf2a") : Palette.Hex("#2a2030"), 0.03f);
                    }
                    b.Prism(new Vector3(-0.3f, 0.25f, 0f), 0.05f, 0f, 0.12f, 4, Palette.Hex("#2a2030"));
                    b.Blob(new Vector3(0.24f, 0.3f, -0.1f), Vector3.one * 0.05f, Color.white, 0f, 0);
                    b.Blob(new Vector3(0.24f, 0.3f, 0.1f), Vector3.one * 0.05f, Color.white, 0f, 0);
                    break;
                default:
                    // A hedgehog: a brown body, a fan of spines on its back and a little nose.
                    b.Blob(new Vector3(0f, 0.26f, 0f), new Vector3(0.45f, 0.26f, 0.36f), Palette.Hex("#c8915a"), 0.06f);
                    for (int i = 0; i < 9; i++)
                    {
                        float angle = Mathf.Lerp(-70f, 70f, i / 8f) * Mathf.Deg2Rad;
                        Vector3 root = new Vector3(-Mathf.Sin(angle) * 0.28f - 0.05f, 0.32f + Mathf.Cos(angle) * 0.2f, 0f);
                        for (int z = -1; z <= 1; z++)
                        {
                            b.Transform = Matrix4x4.TRS(root + new Vector3(0f, 0f, z * 0.17f), Quaternion.Euler(z * 25f, 0f, angle * Mathf.Rad2Deg), Vector3.one);
                            b.Prism(Vector3.zero, 0.07f, 0f, 0.3f, 4, Palette.Hex("#5a3a24"));
                        }
                    }
                    b.Transform = Matrix4x4.identity;
                    b.Blob(new Vector3(0.45f, 0.22f, 0f), Vector3.one * 0.08f, Palette.Hex("#2a1a14"), 0f, 0);
                    b.Blob(new Vector3(0.32f, 0.36f, -0.14f), Vector3.one * 0.045f, Palette.Hex("#2a1a14"), 0f, 0);
                    b.Blob(new Vector3(0.32f, 0.36f, 0.14f), Vector3.one * 0.045f, Palette.Hex("#2a1a14"), 0f, 0);
                    break;
            }
        });

        public static Mesh Wing => Get("Wing", b =>
        {
            b.Blob(new Vector3(0f, 0.15f, 0f), new Vector3(0.12f, 0.17f, 0.03f), new Color(0.92f, 0.97f, 1f), 0f, 0);
        }, variation: 0f);

        /// <summary>A wooden gate with a brass lock, 4.2 m tall like its box in the core.</summary>
        public static Mesh Gate => Get("Gate", b =>
        {
            b.Block(new Vector3(0f, 2.1f, -1.2f), new Vector3(0.36f, 4.2f, 0.36f), Palette.WoodDark);
            b.Block(new Vector3(0f, 2.1f, 1.2f), new Vector3(0.36f, 4.2f, 0.36f), Palette.WoodDark);
            for (int i = 0; i < 6; i++)
            {
                b.Block(new Vector3(0f, 0.4f + i * 0.66f, 0f), new Vector3(0.22f, 0.5f, 2.1f), i % 2 == 0 ? Palette.Wood : Palette.WoodLight);
            }
            b.Block(new Vector3(-0.16f, 1.6f, 0f), new Vector3(0.12f, 0.45f, 0.4f), Palette.Brass);
            b.Block(new Vector3(-0.23f, 1.62f, 0f), new Vector3(0.04f, 0.14f, 0.07f), Palette.Hex("#3a2a14"));
        });

        public static Mesh FlagPole => Get("FlagPole", b =>
        {
            b.Prism(Vector3.zero, 0.08f, 0.06f, 3.2f, 6, Palette.Hex("#e8e8f0"));
            b.Blob(new Vector3(0f, 3.25f, 0f), Vector3.one * 0.13f, Palette.Gold, 0f, 0);
            b.Prism(new Vector3(0f, 0f, 0f), 0.35f, 0.3f, 0.15f, 7, Palette.StoneDark, Palette.Stone);
        });

        public static Mesh Flag => Get("Flag", b =>
        {
            b.Variation = 0f;
            b.Triangle(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.8f, 0f), new Vector3(1.1f, 0.4f, 0f), Color.white);
            b.Triangle(new Vector3(0f, 0f, 0f), new Vector3(1.1f, 0.4f, 0f), new Vector3(0f, 0.8f, 0f), Color.white);
        }, variation: 0f);

        /// <summary>The goal: a rainbow arch over the path.</summary>
        public static Mesh Rainbow => Get("Rainbow", b =>
        {
            Color[] bands =
            {
                Palette.Hex("#ff5a5a"), Palette.Hex("#ffa53a"), Palette.Hex("#ffe14a"), Palette.Hex("#6ee05a"),
                Palette.Hex("#4ac0ff"), Palette.Hex("#7a6cff"), Palette.Hex("#c86cff"),
            };
            const int segments = 14;
            for (int band = 0; band < bands.Length; band++)
            {
                float outer = 2.6f - band * 0.16f, inner = outer - 0.16f;
                for (int s = 0; s < segments; s++)
                {
                    float a0 = Mathf.PI * s / segments, a1 = Mathf.PI * (s + 1) / segments;
                    Vector3 o0 = new Vector3(Mathf.Cos(a0) * outer, Mathf.Sin(a0) * outer, 0f);
                    Vector3 o1 = new Vector3(Mathf.Cos(a1) * outer, Mathf.Sin(a1) * outer, 0f);
                    Vector3 i0 = new Vector3(Mathf.Cos(a0) * inner, Mathf.Sin(a0) * inner, 0f);
                    Vector3 i1 = new Vector3(Mathf.Cos(a1) * inner, Mathf.Sin(a1) * inner, 0f);
                    Vector3 front = new Vector3(0f, 0f, -0.25f), back = new Vector3(0f, 0f, 0.25f);
                    b.Quad(i0 + front, o0 + front, o1 + front, i1 + front, bands[band]);
                    b.Quad(o0 + back, i0 + back, i1 + back, o1 + back, bands[band]);
                    if (band == 0)
                    {
                        b.Quad(o0 + front, o0 + back, o1 + back, o1 + front, bands[band]);
                    }
                    if (band == bands.Length - 1)
                    {
                        b.Quad(i1 + front, i1 + back, i0 + back, i0 + front, bands[band]);
                    }
                }
            }
            b.Blob(new Vector3(-2.2f, 0.1f, 0f), new Vector3(0.7f, 0.45f, 0.6f), Color.white, 0.15f);
            b.Blob(new Vector3(2.2f, 0.1f, 0f), new Vector3(0.7f, 0.45f, 0.6f), Color.white, 0.15f);
        });

        /// <summary>A small glowing ball (the magic spark, sparkles).</summary>
        public static Mesh Spark => Get("Spark", b => b.Blob(Vector3.zero, Vector3.one * 0.5f, Color.white, 0.15f, 0), variation: 0f);

        /// <summary>A small cube for debris and puffs.</summary>
        public static Mesh Bit => Get("Bit", b => b.Block(Vector3.zero, Vector3.one, Color.white), variation: 0f);
    }
}
