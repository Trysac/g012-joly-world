using System;
using System.IO;
using JolyWorld.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace JolyWorld.EditorTools
{
    /// <summary>
    /// Generates the materials, sky, interface icons, font atlases, art asset, app icon, scene and
    /// player settings from code, and builds the players, so every binary asset can be recreated
    /// from text. Batch mode:
    /// <code>
    /// Unity -batchmode -quit -projectPath . -executeMethod JolyWorld.EditorTools.ProjectBuilder.SetUpProject
    /// Unity -batchmode -quit -projectPath . -executeMethod JolyWorld.EditorTools.ProjectBuilder.BuildWindows -buildOutput &lt;folder&gt;
    /// Unity -batchmode -quit -projectPath . -executeMethod JolyWorld.EditorTools.ProjectBuilder.BuildWebGL -buildOutput &lt;folder&gt;
    /// </code>
    /// </summary>
    public static class ProjectBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string ArtFolder = "Assets/Art";
        const string IconFolder = "Assets/UI/Icons";
        const string ArtPath = "Assets/Art/GameArt.asset";
        const string AppIconPath = "Assets/Art/AppIcon.png";
        const string AudioFolder = "Assets/Audio";
        const string DisplayFontSource = "Assets/Fonts/Exo2-Black.ttf";
        const string DisplayFontPath = "Assets/UI/ExoBlack.asset";
        const string TextFontSource = "Assets/Fonts/Exo2-SemiBold.ttf";
        const string TextFontPath = "Assets/UI/ExoSemiBold.asset";
        const string StylePath = "Assets/UI/Hud.uss";
        const string ThemePath = "Assets/UI/HudTheme.tss";
        const string PanelSettingsPath = "Assets/UI/HudPanelSettings.asset";
        const string Ascii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

        [UnityEditor.MenuItem("Joly World/Regenerate Art, Scene and Player Settings")]
        public static void SetUpProject()
        {
            CreateFont(DisplayFontSource, DisplayFontPath, "ExoBlack");
            CreateFont(TextFontSource, TextFontPath, "ExoSemiBold");
            // The style sheet refers to the fonts by path; re-import it so it picks up the new assets.
            AssetDatabase.ImportAsset(StylePath, ImportAssetOptions.ForceUpdate);
            GameArt art = CreateArt();
            PanelSettings panel = CreatePanelSettings();
            CreateScene(art, panel);
            ConfigurePlayer(CreateAppIcon());
            AssetDatabase.SaveAssets();
            Debug.Log("Joly World project set up.");
        }

        /// <summary>
        /// Enters play mode and lets ScreenshotScenarios capture the README screenshots, then exits.
        /// Run in batch mode without -quit: <c>-executeMethod ...CaptureScreenshots -screenshot &lt;folder&gt;</c>.
        /// </summary>
        public static void CaptureScreenshots()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }

        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "JolyWorld.exe");

        public static void BuildWebGL() => Build(BuildTarget.WebGL, string.Empty);

        // ---- Font ----

        /// <summary>
        /// A static font atlas of the printable ASCII range from an Exo 2 TrueType font (used
        /// unmodified under the SIL Open Font License; the atlas only rasterises it).
        /// </summary>
        static void CreateFont(string sourcePath, string assetPath, string name)
        {
            if (AssetDatabase.LoadAssetAtPath<FontAsset>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            FontAsset font = FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, false);
            font.name = name;
            if (!font.TryAddCharacters(Ascii, out string missing))
            {
                Debug.LogWarning($"{name} is missing: {missing}");
            }
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, assetPath);
            foreach (Texture2D atlas in font.atlasTextures)
            {
                atlas.name = name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            font.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
        }

        // ---- Art ----

        static GameArt CreateArt()
        {
            Directory.CreateDirectory(ArtFolder);
            Directory.CreateDirectory(IconFolder);
            var art = AssetDatabase.LoadAssetAtPath<GameArt>(ArtPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<GameArt>();
                AssetDatabase.CreateAsset(art, ArtPath);
            }

            Shader lowPoly = Shader.Find("Joly World/Low Poly");
            Shader water = Shader.Find("Joly World/Water");
            art.LowPoly = MaterialAt("LowPoly", lowPoly);
            art.LowPoly.SetFloat("_Emission", 0f);
            art.LowPoly.enableInstancing = true;
            art.Glowing = MaterialAt("Glowing", lowPoly);
            art.Glowing.SetFloat("_Emission", 0.35f);
            art.Glowing.enableInstancing = true;
            art.Water = MaterialAt("Water", water);
            art.Water.SetColor("_Color", new Color(1f, 1f, 1f, 0.82f));
            art.Sky = CreateSky();
            foreach (Material material in new[] { art.LowPoly, art.Glowing, art.Water })
            {
                EditorUtility.SetDirty(material);
            }

            art.CoinIcon = SaveTexture("Coin", IconFolder, 128, CoinPixel);
            art.GemIcon = SaveTexture("Gem", IconFolder, 128, (x, y) => GemPixel(x, y, true));
            art.GemEmptyIcon = SaveTexture("GemEmpty", IconFolder, 128, (x, y) => GemPixel(x, y, false));
            art.HealthPotionIcon = SaveTexture("HealthPotion", IconFolder, 128, (x, y) => PotionPixel(x, y, Palette.Red));
            art.ManaPotionIcon = SaveTexture("ManaPotion", IconFolder, 128, (x, y) => PotionPixel(x, y, Palette.Blue));
            art.StaminaPotionIcon = SaveTexture("StaminaPotion", IconFolder, 128, (x, y) => PotionPixel(x, y, Palette.Yellow));
            art.ElixirIcon = SaveTexture("Elixir", IconFolder, 128, (x, y) => PotionPixel(x, y, Color.HSVToRGB(Mathf.Repeat(y * 1.6f + 0.9f, 1f), 0.6f, 1f)));
            art.KeyIcon = SaveTexture("Key", IconFolder, 128, KeyPixel);
            art.MenuIcon = SaveTexture("Menu", IconFolder, 64, MenuPixel);

            art.Coin = Clip("coin");
            art.Gem = Clip("gem");
            art.Item = Clip("item");
            art.Jump = Clip("jump");
            art.Land = Clip("land");
            art.Dash = Clip("dash");
            art.Bolt = Clip("bolt");
            art.Fizzle = Clip("fizzle");
            art.Pop = Clip("pop");
            art.Stomp = Clip("stomp");
            art.Hurt = Clip("hurt");
            art.Splash = Clip("splash");
            art.Drink = Clip("drink");
            art.Gate = Clip("gate");
            art.Locked = Clip("locked");
            art.Checkpoint = Clip("checkpoint");
            art.Goal = Clip("goal");
            art.KnockOut = Clip("knockout");
            art.Move = Clip("move");
            art.Select = Clip("select");
            art.Pause = Clip("pause");
            art.TitleMusic = Clip("title_theme", music: true);
            art.MeadowMusic = Clip("meadow_theme", music: true);
            art.PeaksMusic = Clip("peaks_theme", music: true);
            EditorUtility.SetDirty(art);
            return art;
        }

        static Material MaterialAt(string name, Shader shader)
        {
            string path = $"{ArtFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
        }

        /// <summary>A bright day sky on Unity's six-sided skybox shader: deep blue overhead, pale at the horizon.</summary>
        static Material CreateSky()
        {
            Texture2D side = SaveTexture("SkySide", ArtFolder, 256, SkySide);
            Texture2D top = SaveTexture("SkyTop", ArtFolder, 64, (u, v) => SkySide(u, 1f));
            Texture2D bottom = SaveTexture("SkyBottom", ArtFolder, 64, (u, v) => Palette.Hex("#bfe6ff"));
            Material sky = MaterialAt("Sky", Shader.Find("Skybox/6 Sided"));
            sky.SetTexture("_FrontTex", side);
            sky.SetTexture("_BackTex", side);
            sky.SetTexture("_LeftTex", side);
            sky.SetTexture("_RightTex", side);
            sky.SetTexture("_UpTex", top);
            sky.SetTexture("_DownTex", bottom);
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            sky.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        static Color SkySide(float u, float v)
        {
            Color horizon = Palette.Hex("#d4efff");
            Color middle = Palette.Hex("#7cc4f4");
            Color high = Palette.Hex("#3a8ee6");
            float h = Mathf.Clamp01((v - 0.5f) * 2f);
            Color color = v < 0.5f ? horizon : h < 0.4f ? Color.Lerp(horizon, middle, h / 0.4f) : Color.Lerp(middle, high, (h - 0.4f) / 0.6f);
            color.a = 1f;
            return color;
        }

        static Texture2D SaveTexture(string name, string folder, int size, Func<float, float, Color> pixel)
        {
            string path = $"{folder}/{name}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = !name.StartsWith("Sky") && folder == ArtFolder;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- Icons, drawn with signed distances on a 0..1 square (y up) ----

        const float Pixel = 1f / 128f;

        static float Circle(float x, float y, float cx, float cy, float r) => Length(x - cx, y - cy) - r;

        static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance / (Pixel * 1.2f));

        static Color Layer(Color below, Color above, float amount)
        {
            float a = above.a * amount;
            float outA = a + below.a * (1f - a);
            if (outA <= 0f)
            {
                return Color.clear;
            }
            Color rgb = (above * a + below * below.a * (1f - a)) / outA;
            rgb.a = outA;
            return rgb;
        }

        static Color Outline => Palette.Hex("#4a2a14");

        static Color CoinPixel(float x, float y)
        {
            Color c = Color.clear;
            c = Layer(c, Outline, Coverage(Circle(x, y, 0.5f, 0.48f, 0.42f)));
            c = Layer(c, Palette.GoldDark, Coverage(Circle(x, y, 0.5f, 0.48f, 0.37f)));
            c = Layer(c, Palette.Gold, Coverage(Circle(x, y, 0.5f, 0.5f, 0.33f)));
            c = Layer(c, Palette.GoldDark, Coverage(Mathf.Abs(Circle(x, y, 0.5f, 0.5f, 0.22f)) - 0.025f));
            c = Layer(c, new Color(1f, 1f, 0.9f, 0.85f), Coverage(Circle(x, y, 0.38f, 0.64f, 0.06f)));
            return c;
        }

        static Color GemPixel(float x, float y, bool filled)
        {
            float dx = Mathf.Abs(x - 0.5f), dy = y - 0.5f;
            float diamond = dx / 0.34f + Mathf.Abs(dy) / (dy > 0f ? 0.3f : 0.42f) - 1f;
            float d = diamond * 0.3f;
            Color c = Color.clear;
            c = Layer(c, filled ? Outline : new Color(0.15f, 0.1f, 0.25f, 0.7f), Coverage(d - 0.035f));
            if (!filled)
            {
                return Layer(c, new Color(0.35f, 0.3f, 0.45f, 0.75f), Coverage(d));
            }
            Color body = x < 0.5f ? Palette.Shade(Palette.GemPurple, 0.25f) : Palette.GemPurple;
            if (dy > 0.1f)
            {
                body = Palette.Shade(body, 0.2f);
            }
            c = Layer(c, body, Coverage(d));
            c = Layer(c, new Color(1f, 1f, 1f, 0.8f), Coverage(Circle(x, y, 0.42f, 0.66f, 0.05f)));
            return c;
        }

        static Color PotionPixel(float x, float y, Color liquid)
        {
            float body = Circle(x, y, 0.5f, 0.38f, 0.3f);
            float neck = Box(x - 0.5f, y - 0.72f, 0.09f, 0.12f);
            float glass = Mathf.Min(body, neck);
            float cork = Box(x - 0.5f, y - 0.87f, 0.11f, 0.06f);
            Color c = Color.clear;
            c = Layer(c, Outline, Coverage(Mathf.Min(glass, cork) - 0.03f));
            c = Layer(c, new Color(0.88f, 0.96f, 1f, 1f), Coverage(glass));
            float fill = Mathf.Max(Circle(x, y, 0.5f, 0.38f, 0.26f), y - 0.5f);
            c = Layer(c, liquid, Coverage(fill));
            c = Layer(c, Palette.Shade(liquid, -0.25f), Coverage(Mathf.Max(fill, -(y - 0.22f))) * 0.6f);
            c = Layer(c, Palette.WoodLight, Coverage(cork));
            c = Layer(c, new Color(1f, 1f, 1f, 0.8f), Coverage(Circle(x, y, 0.38f, 0.45f, 0.055f)));
            return c;
        }

        static Color KeyPixel(float x, float y)
        {
            // Rotated 45 degrees: the ring top left, the bit bottom right.
            float rx = (x - 0.5f + y - 0.5f) * 0.7071f, ry = (y - 0.5f - (x - 0.5f)) * 0.7071f;
            float ring = Mathf.Abs(Circle(rx, ry, -0.2f, 0f, 0.14f)) - 0.05f;
            float shaft = Box(rx - 0.1f, ry, 0.2f, 0.04f);
            float tooth1 = Box(rx - 0.22f, ry + 0.08f, 0.03f, 0.06f);
            float tooth2 = Box(rx - 0.12f, ry + 0.07f, 0.03f, 0.05f);
            float key = Mathf.Min(Mathf.Min(ring, shaft), Mathf.Min(tooth1, tooth2));
            Color c = Color.clear;
            c = Layer(c, Outline, Coverage(key - 0.03f));
            c = Layer(c, Palette.Brass, Coverage(key));
            c = Layer(c, new Color(1f, 0.95f, 0.7f, 0.9f), Coverage(Mathf.Abs(Circle(rx, ry, -0.2f, 0f, 0.14f)) - 0.015f) * (ry > 0f ? 1f : 0f));
            return c;
        }

        static Color MenuPixel(float x, float y)
        {
            float bars = 1f;
            foreach (float row in new[] { 0.3f, 0.5f, 0.7f })
            {
                bars = Mathf.Min(bars, Box(x - 0.5f, y - row, 0.3f, 0.055f) - 0.02f);
            }
            return Layer(Color.clear, Palette.Hex("#fff4dc"), Mathf.Clamp01(0.5f - bars / (2f / 64f)));
        }

        static float Box(float x, float y, float halfWidth, float halfHeight)
        {
            float qx = Mathf.Abs(x) - halfWidth, qy = Mathf.Abs(y) - halfHeight;
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f);
        }

        static float Length(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static AudioClip Clip(string name, bool music = false)
        {
            string path = $"{AudioFolder}/{name}.ogg";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                Debug.LogWarning($"Missing {path}: run python tools/audio/build.py first.");
                return null;
            }
            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            sample.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = sample;
            importer.forceToMono = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ---- UI ----

        static PanelSettings CreatePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        // ---- Scene ----

        static void CreateScene(GameArt art, PanelSettings panel)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = art.Sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.Hex("#c8dcff");
            RenderSettings.ambientEquatorColor = Palette.Hex("#b8d8b0");
            RenderSettings.ambientGroundColor = Palette.Hex("#7a9a6a");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.Hex("#cfeaff");
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 260f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            var sun = new GameObject("Sun");
            sun.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Palette.Hex("#fff2dc");
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            RenderSettings.sun = light;

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 400f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.position = new Vector3(8f, 3f, -15f);
            camera.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            cameraObject.AddComponent<AudioListener>();

            var ui = new GameObject("HUD");
            var document = ui.AddComponent<UIDocument>();
            document.panelSettings = panel;
            Hud hud = ui.AddComponent<Hud>();

            var game = new GameObject("Game");
            var director = game.AddComponent<GameDirector>();
            var serialized = new SerializedObject(director);
            serialized.FindProperty("art").objectReferenceValue = art;
            serialized.FindProperty("hud").objectReferenceValue = hud;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayer(Texture2D icon)
        {
            PlayerSettings.companyName = "Trysac";
            PlayerSettings.productName = "Joly World";
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            var standaloneIcons = new Texture2D[sizes.Length];
            Array.Fill(standaloneIcons, icon);
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, standaloneIcons, IconKind.Any);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            // Static hosts such as GitHub Pages do not send Content-Encoding for .br/.gz files.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            QualitySettings.shadowDistance = 45f;
        }

        // ---- App icon: the meadow under a blue sky, with a round tree, a coin and a gem ----

        static Texture2D CreateAppIcon()
        {
            const int size = 1024;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = AppIconPixel((x + 0.5f) / size, (y + 0.5f) / size);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(AppIconPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(AppIconPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AppIconPath);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
        }

        static Color AppIconPixel(float x, float y)
        {
            const float aa = 1.5f / 1024f;
            float tile = Box(x - 0.5f, y - 0.5f, 0.46f - 0.16f, 0.46f - 0.16f) - 0.16f;
            Color c = Color.Lerp(Palette.Hex("#d4efff"), Palette.Hex("#3a8ee6"), Mathf.Clamp01((y - 0.35f) / 0.6f));
            // Snowy peaks.
            float peak1 = Mathf.Abs(x - 0.3f) * 1.4f + (y - 0.72f);
            float peak2 = Mathf.Abs(x - 0.72f) * 1.2f + (y - 0.66f);
            float mountain = Mathf.Min(peak1, peak2);
            c = Over(c, Palette.Hex("#b8c8e0"), Mathf.Clamp01(-mountain / aa));
            c = Over(c, Palette.Snow, Mathf.Clamp01(-mountain / aa) * (y > 0.6f ? 1f : 0f));
            // The meadow, the river and the fringe of grass.
            float hill = y - (0.36f + 0.04f * Mathf.Sin(x * 7f));
            c = Over(c, Palette.Grass, Mathf.Clamp01(-hill / aa));
            float river = Mathf.Abs(y - 0.2f) - 0.05f;
            c = Over(c, Palette.Water, Mathf.Clamp01(-river / aa) * (y < 0.3f ? 1f : 0f));
            // A round purple tree and a green one.
            c = Over(c, Palette.Trunk, Mathf.Clamp01(-(Box(x - 0.22f, y - 0.44f, 0.025f, 0.1f)) / aa));
            c = Over(c, Palette.LeafPurple, Mathf.Clamp01(-(Circle(x, y, 0.22f, 0.58f, 0.13f)) / aa));
            c = Over(c, Palette.Trunk, Mathf.Clamp01(-(Box(x - 0.8f, y - 0.42f, 0.02f, 0.08f)) / aa));
            c = Over(c, Palette.Leaf, Mathf.Clamp01(-(Circle(x, y, 0.8f, 0.53f, 0.1f)) / aa));
            // A big coin and a gem.
            float coin = Circle(x, y, 0.5f, 0.42f, 0.17f);
            c = Over(c, Outline, Mathf.Clamp01(-(coin - 0.012f) / aa));
            c = Over(c, Palette.Gold, Mathf.Clamp01(-coin / aa));
            c = Over(c, Palette.GoldDark, Mathf.Clamp01(-(Mathf.Abs(Circle(x, y, 0.5f, 0.42f, 0.1f)) - 0.018f) / aa));
            float gem = (Mathf.Abs(x - 0.66f) / 0.08f + Mathf.Abs(y - 0.24f) / 0.12f - 1f) * 0.08f;
            c = Over(c, Outline, Mathf.Clamp01(-(gem - 0.01f) / aa));
            c = Over(c, Palette.GemPurple, Mathf.Clamp01(-gem / aa));
            c.a = Mathf.Clamp01(-tile / aa);
            return c;
        }

        static Color Over(Color below, Color above, float amount)
        {
            Color mixed = Color.Lerp(below, above, amount);
            mixed.a = below.a;
            return mixed;
        }

        // ---- Builds ----

        static void Build(BuildTarget target, string fileName)
        {
            string folder = Argument("-buildOutput");
            if (string.IsNullOrEmpty(folder))
            {
                throw new ArgumentException("Pass -buildOutput <folder> outside the repository.");
            }
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = target,
                locationPathName = string.IsNullOrEmpty(fileName) ? folder : Path.Combine(folder, fileName),
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"Build {target}: {summary.result}, {summary.totalSize / (1024 * 1024f):0.0} MB, {summary.totalErrors} errors.");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
