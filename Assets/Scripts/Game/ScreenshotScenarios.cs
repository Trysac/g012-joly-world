using System;
using System.Collections;
using System.IO;
using JolyWorld.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace JolyWorld.Game
{
    /// <summary>
    /// Opt-in capture of README screenshots: <c>JolyWorld.exe -screenshot &lt;folder&gt; -scenario title,meadow,...</c>.
    /// Without the -screenshot argument nothing is created, so normal runs and WebGL builds
    /// (which have no command line) are unaffected. The same hook runs inside the editor
    /// (ProjectBuilder.CaptureScreenshots), which renders the camera and the HUD off screen and
    /// blends them, for machines that cannot run the player.
    /// Adding <c>-frames N [-fps F]</c> records N frames per scenario at a fixed timestep instead of
    /// one shot, into <c>&lt;folder&gt;/&lt;scenario&gt;/frame_0000.png...</c>; the <c>gameplay</c>
    /// scenario runs, jumps and casts through the meadow while it records (for the README GIF).
    /// </summary>
    public sealed class ScreenshotScenarios : MonoBehaviour
    {
        const string DefaultScenarios = "title,levels,meadow,riverside,forest,mountain,inventory,pause,results,howto";
        const int Width = 1280;
        const int Height = 720;

        string folder;
        string[] scenarios;
        int frames;
        int fps = 15;
        RenderTexture sceneTarget;
        RenderTexture uiTarget;
        GameDirector director;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartWhenRequested()
        {
            string target = Argument("-screenshot");
            if (string.IsNullOrEmpty(target))
            {
                return;
            }
            var host = new GameObject(nameof(ScreenshotScenarios)).AddComponent<ScreenshotScenarios>();
            host.folder = target;
            host.scenarios = (Argument("-scenario") ?? DefaultScenarios).Split(',');
            host.frames = int.TryParse(Argument("-frames"), out int frames) ? frames : 0;
            host.fps = int.TryParse(Argument("-fps"), out int fps) && fps > 0 ? fps : 15;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            director = FindAnyObjectByType<GameDirector>();
            var store = new MemoryStore();
            director.UseMemoryStore(store);
            if (Application.isEditor)
            {
                RenderOffscreen();
            }
            yield return new WaitForSecondsRealtime(0.5f);
            foreach (string raw in scenarios)
            {
                string scenario = raw.Trim();
                director.ScriptedInput = false;
                yield return Stage(scenario);
                if (frames > 0)
                {
                    yield return Record(scenario);
                    continue;
                }
                Texture2D shot;
                if (Application.isEditor)
                {
                    yield return null;
                    shot = CaptureOffscreen();
                }
                else
                {
                    yield return new WaitForEndOfFrame();
                    shot = ScreenCapture.CaptureScreenshotAsTexture();
                }
                File.WriteAllBytes(Path.Combine(folder, scenario + ".png"), shot.EncodeToPNG());
                Destroy(shot);
                Debug.Log($"Saved screenshot {scenario}.png");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        void Progress()
        {
            // Two levels cleared, so the level select shows records and a fresh level.
            director.Progress.Submit(new LevelResult { Level = 0, Coins = 37, CoinTotal = Levels.All[0].CoinCount, GemMask = 0b111, Gems = 3, GemTotal = 3, Seconds = 71.4f });
            director.Progress.Submit(new LevelResult { Level = 1, Coins = 29, CoinTotal = Levels.All[1].CoinCount, GemMask = 0b011, Gems = 2, GemTotal = 3, Seconds = 88.2f });
        }

        IEnumerator Stage(string scenario)
        {
            switch (scenario)
            {
                case "title":
                    director.ToTitle();
                    yield return new WaitForSecondsRealtime(4f);
                    break;
                case "levels":
                    Progress();
                    director.ToTitle();
                    director.Activate("play");
                    yield return new WaitForSecondsRealtime(1.5f);
                    break;
                case "howto":
                    director.ToTitle();
                    director.Activate("howto");
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                case "meadow":
                    yield return Level(0, 4.5f);
                    yield return Walk(1f, 1.05f);
                    Press(jump: true);
                    yield return Walk(1f, 0.3f);
                    break;
                case "riverside":
                    yield return Level(1, 25f, 0.2f);
                    yield return Walk(1f, 0.28f);
                    Press(jump: true);
                    yield return Walk(1f, 0.38f);
                    break;
                case "forest":
                    yield return Level(2, 3.5f);
                    yield return Walk(1f, 0.3f);
                    yield return Walk(0f, 0.25f);
                    Press(cast: true);
                    yield return Walk(0f, 0.18f);
                    break;
                case "mountain":
                    yield return Level(3, 52.6f, 6f);
                    yield return Walk(1f, 0.3f);
                    Press(jump: true);
                    yield return Walk(1f, 0.32f);
                    break;
                case "inventory":
                    yield return Level(0, 40f);
                    director.Run.Inventory.Add(ItemKind.ManaPotion, 2);
                    director.Run.Inventory.Add(ItemKind.Key);
                    director.Run.Inventory.Add(ItemKind.Elixir);
                    director.Run.Inventory.Swap(4, 9);
                    director.Run.Vitals.Hurt(35f);
                    director.Run.Vitals.Mana.Spend(40f);
                    yield return Walk(0f, 0.5f);
                    director.OpenInventory(false);
                    director.PickOrPlace();
                    yield return new WaitForSecondsRealtime(0.4f);
                    break;
                case "gameplay":
                    yield return Level(0, 6f);
                    StartCoroutine(RunMeadow());
                    break;
                case "pause":
                    yield return Level(1, 44f);
                    yield return Walk(1f, 0.6f);
                    yield return Walk(0f, 0.4f);
                    director.TogglePause();
                    yield return new WaitForSecondsRealtime(0.5f);
                    break;
                case "results":
                    yield return Level(0, 2f);
                    LevelRun run = director.Run;
                    for (int i = 0; i < 150; i++)
                    {
                        run.Tick(HeroInput.Idle, 0.5f);
                    }
                    for (int i = 0; i < run.Level.Pickups.Count; i++)
                    {
                        PickupSpec pickup = run.Level.Pickups[i];
                        if ((pickup.Kind == PickupKind.Coin && i % 7 == 3) || (pickup.Kind == PickupKind.Gem && pickup.Position.X > 80f))
                        {
                            continue;
                        }
                        run.Hero.Place(new Vec2(pickup.Position.X, pickup.Position.Y - 0.6f));
                        run.Tick(HeroInput.Idle, 0f);
                    }
                    run.Hero.Place(new Vec2(103f, 0f));
                    director.SnapCamera();
                    yield return Walk(1f, 1.2f);
                    float waited = 0f;
                    while (director.Screen != GameScreen.Results && waited < 6f)
                    {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    yield return new WaitForSecondsRealtime(0.8f);
                    break;
                default:
                    Debug.LogWarning($"Unknown screenshot scenario '{scenario}'.");
                    break;
            }
        }

        /// <summary>Writes the given number of frames at a fixed timestep (Time.captureFramerate).</summary>
        IEnumerator Record(string scenario)
        {
            string target = Path.Combine(folder, scenario);
            Directory.CreateDirectory(target);
            Time.captureFramerate = fps;
            for (int i = 0; i < frames; i++)
            {
                Texture2D shot;
                if (Application.isEditor)
                {
                    yield return null;
                    shot = CaptureOffscreen();
                }
                else
                {
                    yield return new WaitForEndOfFrame();
                    shot = ScreenCapture.CaptureScreenshotAsTexture();
                }
                File.WriteAllBytes(Path.Combine(target, $"frame_{i:0000}.png"), shot.EncodeToPNG());
                Destroy(shot);
            }
            Time.captureFramerate = 0;
            Debug.Log($"Saved {frames} frames of {scenario}");
        }

        /// <summary>
        /// Plays the Sunny Meadow for the recording: runs right, jumps at the step, the gap and the
        /// two ledges, and casts a spark at any critter just ahead.
        /// </summary>
        IEnumerator RunMeadow()
        {
            float[] jumpAt = { 24.4f, 33.4f, 42.6f, 48.4f };
            float stuckWait = 0.5f;
            int next = 0;
            float castWait = 0f;
            while (director.Run != null && !director.Run.Finished && director.ScriptedInput)
            {
                Vec2 hero = director.Run.Hero.Position;
                HeroInput input = director.Scripted;
                input.Move = 1f;
                input.JumpHeld = true;
                stuckWait -= Time.deltaTime;
                if (next < jumpAt.Length && hero.X >= jumpAt[next])
                {
                    input.Jump = true;
                    next++;
                }
                else if (stuckWait <= 0f && director.Run.Hero.Velocity.X < 0.5f)
                {
                    // Blocked by a step: hop onto it.
                    input.Jump = true;
                    stuckWait = 0.5f;
                }
                castWait -= Time.deltaTime;
                if (castWait <= 0f && CritterAhead(hero))
                {
                    input.Cast = true;
                    castWait = 0.7f;
                }
                director.Scripted = input;
                yield return null;
            }
        }

        bool CritterAhead(Vec2 hero)
        {
            foreach (Critter critter in director.Run.Critters)
            {
                float ahead = critter.Position.X - hero.X;
                if (critter.Alive && ahead > 0f && ahead < 7f && Mathf.Abs(critter.Position.Y - hero.Y) < 1.8f)
                {
                    return true;
                }
            }
            return false;
        }

        IEnumerator Level(int index, float x, float y = float.NaN)
        {
            director.StartLevel(index);
            director.ScriptedInput = true;
            director.Scripted = HeroInput.Idle;
            Level level = Levels.All[index];
            float feet = float.IsNaN(y) ? level.Spawn.Y : y;
            director.Run.Hero.Place(new Vec2(x, feet));
            director.SnapCamera();
            yield return new WaitForSecondsRealtime(0.4f);
            director.Hud.ClearMessages();
        }

        IEnumerator Walk(float move, float seconds)
        {
            HeroInput input = director.Scripted;
            input.Move = move;
            input.JumpHeld = true;
            director.Scripted = input;
            yield return new WaitForSecondsRealtime(seconds);
        }

        void Press(bool jump = false, bool cast = false)
        {
            HeroInput input = director.Scripted;
            input.Jump = jump;
            input.Cast = cast;
            director.Scripted = input;
        }

        /// <summary>Points the camera and the HUD panel at render textures of a fixed size.</summary>
        void RenderOffscreen()
        {
            sceneTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            uiTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            Camera.main.targetTexture = sceneTarget;
            var document = director.Hud.GetComponent<UIDocument>();
            PanelSettings panel = Instantiate(document.panelSettings);
            panel.targetTexture = uiTarget;
            panel.clearColor = true;
            panel.colorClearValue = Color.clear;
            document.panelSettings = panel;
        }

        /// <summary>Blends the HUD over the scene, like the screen would show them.</summary>
        Texture2D CaptureOffscreen()
        {
            Color[] scene = Read(sceneTarget);
            Color[] ui = Read(uiTarget);
            for (int i = 0; i < scene.Length; i++)
            {
                Color over = ui[i];
                Color result = Color.Lerp(scene[i], new Color(over.r, over.g, over.b), over.a);
                result.a = 1f;
                scene[i] = result;
            }
            var shot = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            shot.SetPixels(scene);
            shot.Apply();
            return shot;
        }

        static Color[] Read(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            Color[] pixels = texture.GetPixels();
            Destroy(texture);
            return pixels;
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
