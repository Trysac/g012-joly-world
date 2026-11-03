using System.Collections.Generic;
using JolyWorld.Core;
using UnityEngine;

namespace JolyWorld.Game
{
    public enum GameScreen
    {
        Title,
        Playing,
        Paused,
        Inventory,
        Results,
    }

    /// <summary>
    /// Runs the game: the title over the meadow, the level select, playing a level (input to the
    /// core's <see cref="LevelRun"/>, its events to sounds, effects and the HUD, the camera),
    /// pause, the inventory screen, settings and the results with the records saved on the device.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        const float Step = 1f / 60f;
        const float CelebrateSeconds = 1.8f;

        [SerializeField] GameArt art;
        [SerializeField] Hud hud;

        readonly GameInput input = new GameInput();
        readonly Stack<string> menuStack = new Stack<string>();
        IKeyValueStore store;
        Camera view;
        Effects effects;
        AudioDirector audioDirector;
        LevelView levelView;
        HeroView heroView;
        LevelRun run;
        int levelIndex;
        int inventoryCursor;
        int inventoryHeld = -1;
        bool inventoryFromPause;
        float celebrate;
        float titleTime;
        float shake;
        Vector3 cameraVelocity;
        HeroInput scripted = HeroInput.Idle;

        public GameScreen Screen { get; private set; } = GameScreen.Title;
        public LevelRun Run => run;
        public LevelView LevelView => levelView;
        public HeroView HeroView => heroView;
        public Hud Hud => hud;
        public Effects Effects => effects;
        public AudioDirector Audio => audioDirector;
        public Settings Settings { get; private set; }
        public Progress Progress { get; private set; }
        public int LevelIndex => levelIndex;
        public RecordUpdate LastRecord { get; private set; }
        public LevelResult LastResult { get; private set; }
        public int InventoryCursor => inventoryCursor;
        public int InventoryHeld => inventoryHeld;

        /// <summary>When true the hero ignores the devices and follows <see cref="Scripted"/> (tests and screenshots).</summary>
        public bool ScriptedInput { get; set; }

        /// <summary>The input used while <see cref="ScriptedInput"/> is on; presses last one frame.</summary>
        public HeroInput Scripted
        {
            get => scripted;
            set => scripted = value;
        }

        void Awake()
        {
            view = Camera.main;
            store = new PlayerPrefsStore();
            LoadPreferences();
            effects = new GameObject("Effects").AddComponent<Effects>();
            effects.Init(art);
            audioDirector = new GameObject("Audio").AddComponent<AudioDirector>();
            audioDirector.Init(Settings.Volume / 10f, Settings.Music / 10f);
            hud.Init(art);
            hud.ItemClicked += index =>
            {
                if (hud.Menu != null && index < hud.Menu.Items.Count)
                {
                    hud.Menu.Selected = index;
                    Activate(hud.Menu.Items[index].Id);
                }
            };
            hud.ItemHovered += index =>
            {
                if (hud.Menu != null && hud.Menu.Selected != index)
                {
                    hud.Menu.Selected = index;
                    hud.RefreshMenu();
                }
            };
            hud.MenuButtonClicked += () =>
            {
                if (Screen == GameScreen.Playing || Screen == GameScreen.Paused)
                {
                    TogglePause();
                }
            };
            hud.InventoryButtonClicked += () =>
            {
                if (Screen == GameScreen.Playing)
                {
                    OpenInventory(false);
                }
            };
            hud.HotbarClicked += index =>
            {
                if (Screen == GameScreen.Playing)
                {
                    run.Inventory.Select(index);
                    Use(index);
                }
            };
            hud.InventorySlotClicked += index =>
            {
                if (Screen == GameScreen.Inventory)
                {
                    inventoryCursor = index;
                    PickOrPlace();
                }
            };
        }

        void Start() => ToTitle();

        void LoadPreferences()
        {
            Settings = Settings.Load(store);
            Progress = new Progress(store, Levels.All.Count);
        }

        /// <summary>Keeps the progress and settings in memory only (tests and screenshots).</summary>
        public void UseMemoryStore(IKeyValueStore memory = null)
        {
            store = memory ?? new MemoryStore();
            LoadPreferences();
            ApplySettings(false);
        }

        // ---- Levels ----

        void LoadLevel(int index)
        {
            levelIndex = Mathf.Clamp(index, 0, Levels.All.Count - 1);
            if (levelView != null)
            {
                Destroy(levelView.gameObject);
            }
            effects.Clear();
            Level level = Levels.All[levelIndex];
            levelView = LevelView.Create(level, art);
            if (heroView == null)
            {
                heroView = HeroView.Create(art);
            }
            heroView.Cheering = false;
        }

        public void ToTitle()
        {
            Screen = GameScreen.Title;
            LoadLevel(0);
            run = new LevelRun(levelView.Level);
            run.Hero.Place(new Vec2(12.6f, 0f));
            titleTime = 0f;
            hud.ShowPlay(false);
            hud.ShowInventory(false);
            hud.ClearMessages();
            audioDirector.PlayMusic(art.TitleMusic);
            audioDirector.SetDucked(false);
            menuStack.Clear();
            OpenMenu("title");
            SnapCamera();
        }

        public void StartLevel(int index)
        {
            LoadLevel(index);
            run = new LevelRun(levelView.Level);
            Hook(run);
            Screen = GameScreen.Playing;
            celebrate = 0f;
            inventoryHeld = -1;
            scripted = HeroInput.Idle;
            menuStack.Clear();
            hud.HideMenu();
            hud.ShowInventory(false);
            hud.ShowPlay(true);
            Level level = levelView.Level;
            hud.SetLevel($"{level.Index + 1}. {level.Name}", "JOLY WORLD");
            hud.ClearMessages();
            hud.Banner(level.Name.ToUpperInvariant(), level.Tagline, 2.2f, Palette.Text);
            audioDirector.PlayMusic(level.Theme == Theme.Meadow || level.Theme == Theme.Riverside ? art.MeadowMusic : art.PeaksMusic);
            audioDirector.SetDucked(false);
            UpdateHud();
            SnapCamera();
        }

        void Hook(LevelRun current)
        {
            current.PickedUp += OnPickedUp;
            current.Stomped += index =>
            {
                Vector3 at = Vector(current.Critters[index].Position) + Vector3.up * 0.3f;
                effects.Puff(at);
                effects.Sparkle(at, Palette.Gold);
                audioDirector.Play(art.Stomp, 0.8f);
                heroView.Squash(0.85f);
            };
            current.Popped += index =>
            {
                Vector3 at = Vector(current.Critters[index].Position) + Vector3.up * 0.3f;
                effects.Puff(at);
                effects.Sparkle(at, Palette.Hex("#9ad8ff"));
                audioDirector.Play(art.Pop, 0.8f);
            };
            current.Hurt += () =>
            {
                audioDirector.Play(art.Hurt, 0.8f);
                shake = 0.35f;
            };
            current.Fell += () =>
            {
                effects.Splash(new Vector3(current.Hero.Position.X, LevelView.WaterLevel, 0f));
                audioDirector.Play(art.Splash, 0.8f);
            };
            current.KnockedOut += () =>
            {
                audioDirector.Play(art.KnockOut, 0.8f);
                hud.Banner("OOPS!", "BACK TO THE LAST FLAG, WITH FULL BARS", 1.6f, Palette.Warn);
            };
            current.Respawned += () =>
            {
                SnapCamera();
                effects.Sparkle(Vector(current.Hero.Position) + Vector3.up * 0.7f, Color.white);
            };
            current.GateOpened += index =>
            {
                audioDirector.Play(art.Gate, 0.9f);
                hud.Notice("THE KEY OPENS THE GATE");
            };
            current.GateLocked += index =>
            {
                audioDirector.Play(art.Locked, 0.8f);
                hud.Notice("LOCKED - FIND THE BRASS KEY");
            };
            current.CheckpointReached += index =>
            {
                audioDirector.Play(art.Checkpoint, 0.8f);
                hud.Notice("CHECKPOINT!");
            };
            current.Completed += () =>
            {
                audioDirector.Play(art.Goal, 0.9f);
                audioDirector.SetDucked(true);
                hud.Banner("LEVEL CLEAR!", null, CelebrateSeconds, Palette.Gold);
                heroView.Cheering = true;
                Vector3 at = Vector(current.Hero.Position) + Vector3.up * 1.5f;
                effects.Burst(at, Palette.Gold, 24, 6f, 0.16f, 1f, 6f);
                effects.Burst(at, Palette.GemPink, 16, 5f, 0.14f, 1f, 6f);
            };
            current.Jumped += () => audioDirector.Play(art.Jump, 0.45f);
            current.Landed += () =>
            {
                audioDirector.Play(art.Land, 0.35f);
                effects.Dust(Vector(current.Hero.Position));
                heroView.Squash();
            };
            current.Dashed += () => audioDirector.Play(art.Dash, 0.7f);
            current.Cast += () => audioDirector.Play(art.Bolt, 0.7f);
            current.CastFailed += () =>
            {
                audioDirector.Play(art.Fizzle, 0.7f);
                hud.FlashMana();
                hud.Notice("NOT ENOUGH MANA");
            };
            current.Drank += kind =>
            {
                audioDirector.Play(art.Drink, 0.8f);
                effects.Sparkle(Vector(current.Hero.Position) + Vector3.up * 0.8f, Palette.Item(kind));
                hud.Notice(Items.Of(kind).Name.ToUpperInvariant() + "!");
            };
            current.UseFailed += result =>
            {
                audioDirector.Play(art.Move, 0.5f, 0.8f);
                if (result == UseResult.NoEffect)
                {
                    hud.Notice("ALREADY FULL - SAVED FOR LATER");
                }
                else if (result == UseResult.NotUsable)
                {
                    hud.Notice("KEYS OPEN GATES BY THEMSELVES");
                }
            };
        }

        void OnPickedUp(int index)
        {
            PickupSpec pickup = run.Level.Pickups[index];
            Vector3 at = Vector(pickup.Position);
            switch (pickup.Kind)
            {
                case PickupKind.Coin:
                    audioDirector.Play(art.Coin, 0.55f);
                    effects.Sparkle(at, Palette.Gold);
                    break;
                case PickupKind.Gem:
                    audioDirector.Play(art.Gem, 0.85f);
                    effects.Burst(at, Palette.GemPurple, 18, 4f, 0.13f, 0.7f, 2f);
                    hud.Notice($"GEM FOUND! {run.Gems} OF {run.Level.GemCount}");
                    break;
                default:
                    audioDirector.Play(art.Item, 0.75f);
                    effects.Sparkle(at, Palette.Item(pickup.Item));
                    hud.Notice("+ " + Items.Of(pickup.Item).Name.ToUpperInvariant());
                    break;
            }
        }

        static Vector3 Vector(Vec2 point) => new Vector3(point.X, point.Y, 0f);

        public void TogglePause()
        {
            if (Screen == GameScreen.Playing)
            {
                Screen = GameScreen.Paused;
                hud.ClearMessages();
                audioDirector.SetDucked(true);
                audioDirector.Play(art.Pause, 0.7f);
                menuStack.Clear();
                OpenMenu("pause");
            }
            else if (Screen == GameScreen.Paused)
            {
                Screen = GameScreen.Playing;
                audioDirector.SetDucked(false);
                audioDirector.Play(art.Pause, 0.7f, 1.2f);
                menuStack.Clear();
                hud.HideMenu();
            }
        }

        public void OpenInventory(bool fromPause)
        {
            if (run == null || (Screen != GameScreen.Playing && Screen != GameScreen.Paused))
            {
                return;
            }
            inventoryFromPause = fromPause;
            Screen = GameScreen.Inventory;
            inventoryCursor = run.Inventory.Selected;
            inventoryHeld = -1;
            hud.HideMenu();
            hud.ShowInventory(true);
            audioDirector.SetDucked(true);
            audioDirector.Play(art.Pause, 0.6f, 1.3f);
            RefreshInventory();
        }

        public void CloseInventory()
        {
            if (Screen != GameScreen.Inventory)
            {
                return;
            }
            hud.ShowInventory(false);
            inventoryHeld = -1;
            audioDirector.Play(art.Pause, 0.6f, 1.1f);
            if (inventoryFromPause)
            {
                Screen = GameScreen.Paused;
                OpenMenu("pause");
            }
            else
            {
                Screen = GameScreen.Playing;
                audioDirector.SetDucked(false);
            }
        }

        /// <summary>Moves the inventory cursor to a slot (the mouse, tests and screenshots).</summary>
        public void SelectInventorySlot(int index)
        {
            inventoryCursor = Mathf.Clamp(index, 0, Inventory.Size - 1);
            if (Screen == GameScreen.Inventory)
            {
                RefreshInventory();
            }
        }

        /// <summary>Picks up the item under the cursor, or puts the held one down there (swapping).</summary>
        public void PickOrPlace()
        {
            if (inventoryHeld < 0)
            {
                if (!run.Inventory[inventoryCursor].Empty)
                {
                    inventoryHeld = inventoryCursor;
                    audioDirector.Play(art.Select, 0.5f);
                }
            }
            else
            {
                run.Inventory.Swap(inventoryHeld, inventoryCursor);
                inventoryHeld = -1;
                audioDirector.Play(art.Item, 0.5f);
            }
            RefreshInventory();
        }

        void RefreshInventory()
        {
            string hint = input.Source == InputSource.Gamepad
                ? "D-PAD  MOVE     A  PICK UP / PLACE     X  USE     B  CLOSE"
                : "ARROWS OR CLICK  MOVE     ENTER  PICK UP / PLACE     E  USE     I  CLOSE";
            hud.SetInventory(run.Inventory, inventoryCursor, inventoryHeld, hint);
            hud.SetHotbar(run.Inventory);
            hud.SetVitals(run.Vitals);
        }

        void Use(int slot) => run.Drink(slot);

        // ---- Frame ----

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            input.Read();
            switch (Screen)
            {
                case GameScreen.Title:
                    NavigateMenu();
                    titleTime += dt;
                    run.Tick(HeroInput.Idle, dt);
                    break;
                case GameScreen.Playing:
                    if (input.Pause && !ScriptedInput)
                    {
                        TogglePause();
                        return;
                    }
                    if (input.Inventory && !ScriptedInput)
                    {
                        OpenInventory(false);
                        return;
                    }
                    Play(dt);
                    break;
                case GameScreen.Inventory:
                    NavigateInventory();
                    break;
                default:
                    NavigateMenu();
                    break;
            }
            if (run != null)
            {
                levelView.Sync(run, dt);
                heroView.Sync(run.Hero, run.Vitals, dt);
            }
            if (heroView != null)
            {
                heroView.Cheering = Screen == GameScreen.Title || Screen == GameScreen.Results || (run != null && run.Finished);
            }
            MoveCamera(dt);
        }

        void Play(float dt)
        {
            HeroInput wanted;
            if (ScriptedInput)
            {
                wanted = scripted;
                scripted.Jump = false;
                scripted.Dash = false;
                scripted.Cast = false;
                scripted.Use = -1;
            }
            else
            {
                wanted = new HeroInput
                {
                    Move = input.Move,
                    Jump = input.Jump,
                    JumpHeld = input.JumpHeld,
                    Sprint = input.Sprint,
                    Dash = input.Dash,
                    Cast = input.Cast,
                    Use = -1,
                };
                if (input.PickSlot >= 0)
                {
                    run.Inventory.Select(input.PickSlot);
                }
                if (input.SlotStep != 0)
                {
                    run.Inventory.SelectStep(input.SlotStep);
                    audioDirector.Play(art.Move, 0.4f);
                }
                if (input.UseSelected)
                {
                    wanted.Use = run.Inventory.Selected;
                }
            }

            if (!run.Finished)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(dt / Step - 0.01f));
                float h = dt / steps;
                for (int i = 0; i < steps; i++)
                {
                    run.Tick(wanted, h);
                    wanted.Jump = false;
                    wanted.Dash = false;
                    wanted.Cast = false;
                    wanted.Use = -1;
                }
                if (run.Hero.Dashing)
                {
                    effects.Trail(Vector(run.Hero.Position) + Vector3.up * 0.7f, Palette.Hex("#bfe8ff"));
                }
                foreach (Bolt bolt in run.Bolts)
                {
                    effects.Trail(Vector(bolt.Position), Palette.Hex("#8fe4ff"));
                }
            }
            else
            {
                celebrate += dt;
                if (celebrate >= CelebrateSeconds)
                {
                    ShowResults();
                }
            }
            UpdateHud();
        }

        void UpdateHud()
        {
            hud.SetPurse(run.Coins, run.GemMask, run.Seconds);
            hud.SetVitals(run.Vitals);
            hud.SetHotbar(run.Inventory);
        }

        void ShowResults()
        {
            Screen = GameScreen.Results;
            LastResult = run.Result;
            LastRecord = Progress.Submit(LastResult);
            hud.ShowPlay(false);
            audioDirector.PlayMusic(art.TitleMusic);
            audioDirector.SetDucked(false);
            menuStack.Clear();
            OpenMenu("results");
        }

        // ---- Camera ----

        Vector3 CameraTarget()
        {
            if (Screen == GameScreen.Title)
            {
                float x = 18f + 3f * Mathf.Sin(titleTime * 0.15f);
                return new Vector3(x, 3.2f, -16.5f);
            }
            Motor hero = run.Hero;
            float lead = hero.Facing * 1.8f + hero.Velocity.X * 0.15f;
            float minX = 7f, maxX = run.Level.Width - 17f;
            float focusX = Mathf.Clamp(hero.Position.X + lead, minX, Mathf.Max(minX, maxX));
            float focusY = Mathf.Max(hero.Position.Y, LevelView.WaterLevel + 0.5f);
            return new Vector3(focusX, focusY + 3f, -15f);
        }

        void MoveCamera(float dt)
        {
            if (view == null || run == null)
            {
                return;
            }
            Vector3 target = CameraTarget();
            view.transform.position = Vector3.SmoothDamp(view.transform.position, target, ref cameraVelocity, 0.25f, 60f, Time.unscaledDeltaTime);
            shake = Mathf.MoveTowards(shake, 0f, dt);
            Vector3 jitter = shake > 0f ? Random.insideUnitSphere * shake * 0.25f : Vector3.zero;
            view.transform.position += jitter;
            view.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
        }

        public void SnapCamera()
        {
            if (view == null || run == null)
            {
                return;
            }
            cameraVelocity = Vector3.zero;
            view.transform.position = CameraTarget();
            view.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
        }

        // ---- Menus ----

        void OpenMenu(string id)
        {
            menuStack.Push(id);
            hud.ShowMenu(BuildMenu(id));
        }

        void Back()
        {
            if (menuStack.Count <= 1)
            {
                if (Screen == GameScreen.Paused)
                {
                    TogglePause();
                }
                return;
            }
            menuStack.Pop();
            Settings.Save(store);
            hud.ShowMenu(BuildMenu(menuStack.Peek()));
            audioDirector.Play(art.Move, 0.5f, 0.9f);
        }

        string Hint => input.Source == InputSource.Gamepad
            ? "D-PAD  CHOOSE     LEFT / RIGHT  CHANGE     A  SELECT     B  BACK"
            : "ARROWS OR MOUSE  CHOOSE     LEFT / RIGHT  CHANGE     ENTER  SELECT     ESC  BACK";

        MenuModel BuildMenu(string id)
        {
            var model = new MenuModel { Hint = Hint };
            switch (id)
            {
                case "title":
                    model.Logo = true;
                    model.Add("play", "PLAY");
                    model.Add("settings", "SETTINGS");
                    model.Add("howto", "HOW TO PLAY");
                    if (!Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer)
                    {
                        model.Add("quit", "QUIT");
                    }
                    break;
                case "levels":
                    model.Title = "CHOOSE A LEVEL";
                    model.Subtitle = $"{Progress.TotalGems} OF {Levels.All.Count * 3} GEMS FOUND";
                    for (int i = 0; i < Levels.All.Count; i++)
                    {
                        Level level = Levels.All[i];
                        string value;
                        if (!Progress.Unlocked(i))
                        {
                            value = "LOCKED";
                        }
                        else if (!Progress.Completed(i))
                        {
                            value = "NEW";
                        }
                        else
                        {
                            value = $"{Progress.GemsFound(i)}/3 GEMS   {Progress.BestCoins(i)}/{level.CoinCount}   {TimeText.Format(Progress.BestSeconds(i))}";
                        }
                        model.Add("level" + i, $"{i + 1}. {level.Name.ToUpperInvariant()}", value, !Progress.Unlocked(i));
                    }
                    model.Add("back", "BACK");
                    model.Selected = Mathf.Clamp(FirstOpenLevel(), 0, Levels.All.Count - 1);
                    break;
                case "settings":
                    model.Title = "SETTINGS";
                    model.Add("volume", "VOLUME", Settings.Volume.ToString());
                    model.Add("music", "MUSIC", Settings.Music.ToString());
                    model.Add("back", "BACK");
                    model.LightDim = Screen == GameScreen.Paused;
                    break;
                case "howto":
                    model.Title = "HOW TO PLAY";
                    model.Subtitle = "Reach the rainbow at the end of each land. Collect coins, find the three gems.";
                    model.Body.Add(("RUN / JUMP", "A D OR ARROWS, SPACE  -  STICK, A", false));
                    model.Body.Add(("SPRINT (STAMINA)", "SHIFT  -  RT", false));
                    model.Body.Add(("DASH (30 STAMINA)", "K OR X  -  B", false));
                    model.Body.Add(("MAGIC SPARK (25 MANA)", "J OR F  -  X", true));
                    model.Body.Add(("HOTBAR: CHOOSE / DRINK", "1-7, Q R OR WHEEL / E  -  LB RB / Y", false));
                    model.Body.Add(("INVENTORY / PAUSE", "I OR TAB / ESC  -  VIEW / START", false));
                    model.Body.Add(("CRITTERS", "STOMP BEETLES AND BEES, SPARK HEDGEHOGS", true));
                    model.Body.Add(("GATES", "A BRASS KEY OPENS EACH LOCKED GATE", false));
                    model.Add("back", "BACK");
                    break;
                case "pause":
                    model.Title = "PAUSED";
                    model.Subtitle = $"{levelView.Level.Index + 1}. {levelView.Level.Name.ToUpperInvariant()}";
                    model.LightDim = true;
                    model.Add("resume", "RESUME");
                    model.Add("inventory", "INVENTORY");
                    model.Add("restart", "RESTART LEVEL");
                    model.Add("settings", "SETTINGS");
                    model.Add("levels", "LEVEL SELECT");
                    model.Add("title", "QUIT TO TITLE");
                    break;
                case "results":
                {
                    LevelResult result = LastResult;
                    RecordUpdate record = LastRecord;
                    bool last = result.Level >= Levels.All.Count - 1;
                    model.Title = "LEVEL CLEAR!";
                    model.TitleTone = 1;
                    model.Subtitle = last
                        ? "You climbed all the way to the snowy peaks. Thanks for playing!"
                        : $"{Levels.All[result.Level].Name} done. {Levels.All[result.Level + 1].Name} is open.";
                    model.LightDim = true;
                    model.Body.Add(("COINS", $"{result.Coins} / {result.CoinTotal}" + (record.NewBestCoins && !record.FirstClear ? "   NEW BEST!" : string.Empty), record.NewBestCoins));
                    model.Body.Add(("GEMS", $"{result.Gems} / {result.GemTotal}" + (record.NewGems > 0 ? $"   +{record.NewGems} NEW" : string.Empty), record.NewGems > 0));
                    model.Body.Add(("TIME", TimeText.Format(result.Seconds) + (record.NewBestTime && !record.FirstClear ? "   NEW BEST!" : string.Empty), record.NewBestTime));
                    model.Body.Add(("BEST", $"{Progress.BestCoins(result.Level)} COINS   {TimeText.Format(Progress.BestSeconds(result.Level))}", false));
                    model.Body.Add(("KNOCK-OUTS", result.KnockOuts.ToString(), false));
                    if (!last)
                    {
                        model.Add("next", "NEXT LEVEL");
                    }
                    model.Add("again", "PLAY AGAIN");
                    model.Add("levels", "LEVEL SELECT");
                    if (last)
                    {
                        model.Add("title", "TITLE SCREEN");
                    }
                    break;
                }
            }
            return model;
        }

        int FirstOpenLevel()
        {
            for (int i = 0; i < Levels.All.Count; i++)
            {
                if (Progress.Unlocked(i) && !Progress.Completed(i))
                {
                    return i;
                }
            }
            return 0;
        }

        void NavigateMenu()
        {
            MenuModel menu = hud.Menu;
            if (menu == null || menu.Items.Count == 0)
            {
                return;
            }
            int vertical = input.VerticalStep();
            if (vertical != 0)
            {
                menu.Selected = (menu.Selected + vertical + menu.Items.Count) % menu.Items.Count;
                hud.RefreshMenu();
                audioDirector.Play(art.Move, 0.5f);
            }
            int horizontal = input.HorizontalStep();
            if (horizontal != 0 && Change(menu.Items[menu.Selected].Id, horizontal))
            {
                audioDirector.Play(art.Move, 0.5f, 1.1f);
            }
            if (input.Confirm)
            {
                Activate(menu.Items[menu.Selected].Id);
            }
            else if (input.Back || (input.Pause && Screen == GameScreen.Paused && menuStack.Count == 1))
            {
                Back();
            }
        }

        void NavigateInventory()
        {
            int horizontal = input.HorizontalStep();
            int vertical = input.VerticalStep();
            if (horizontal != 0 || vertical != 0)
            {
                int row = inventoryCursor / Inventory.HotbarSize, column = inventoryCursor % Inventory.HotbarSize;
                int rows = Inventory.Size / Inventory.HotbarSize;
                column = (column + horizontal + Inventory.HotbarSize) % Inventory.HotbarSize;
                row = (row + vertical + rows) % rows;
                inventoryCursor = row * Inventory.HotbarSize + column;
                audioDirector.Play(art.Move, 0.4f);
                RefreshInventory();
            }
            if (input.Confirm)
            {
                PickOrPlace();
            }
            else if (input.UseInMenu)
            {
                Use(inventoryCursor);
                RefreshInventory();
            }
            else if (input.CloseInventory)
            {
                CloseInventory();
            }
        }

        /// <summary>Steps a setting left or right; false when the row is not a setting.</summary>
        bool Change(string id, int step)
        {
            switch (id)
            {
                case "volume": Settings.StepVolume(step); break;
                case "music": Settings.StepMusic(step); break;
                default: return false;
            }
            ApplySettings(true);
            MenuModel fresh = BuildMenu(menuStack.Peek());
            fresh.Selected = hud.Menu.Selected;
            hud.ShowMenu(fresh);
            return true;
        }

        void ApplySettings(bool save)
        {
            audioDirector.SetVolume(Settings.Volume / 10f, Settings.Music / 10f);
            if (save)
            {
                Settings.Save(store);
            }
        }

        /// <summary>Runs a menu row's action, as if it had been chosen.</summary>
        public void Activate(string id)
        {
            if (id.StartsWith("level"))
            {
                int index = int.Parse(id.Substring(5));
                if (!Progress.Unlocked(index))
                {
                    audioDirector.Play(art.Locked, 0.7f);
                    return;
                }
                audioDirector.Play(art.Select, 0.6f);
                StartLevel(index);
                return;
            }
            audioDirector.Play(art.Select, 0.6f);
            switch (id)
            {
                case "play":
                case "settings":
                case "howto":
                    OpenMenu(id == "play" ? "levels" : id);
                    break;
                case "back":
                    Back();
                    break;
                case "resume":
                    TogglePause();
                    break;
                case "inventory":
                    OpenInventory(true);
                    break;
                case "restart":
                case "again":
                    StartLevel(levelIndex);
                    break;
                case "next":
                    StartLevel(levelIndex + 1);
                    break;
                case "levels":
                    ToTitle();
                    OpenMenu("levels");
                    break;
                case "title":
                    ToTitle();
                    break;
                case "quit":
                    Application.Quit();
                    break;
                default:
                    Change(id, 1);
                    break;
            }
        }
    }
}
