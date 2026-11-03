using System.Collections;
using System.Linq;
using JolyWorld.Core;
using JolyWorld.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace JolyWorld.Tests
{
    /// <summary>
    /// The game's flow in the real scene: the title, the level select, playing a level (coins,
    /// potions, the river), pause, the inventory screen, settings, the results and the records
    /// saved for the next visit.
    /// </summary>
    public class GameFlowTests
    {
        GameDirector director;
        MemoryStore store;
        VisualElement ui;

        IEnumerator Load()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            director = Object.FindAnyObjectByType<GameDirector>();
            Assert.That(director, Is.Not.Null);
            store = new MemoryStore();
            director.UseMemoryStore(store);
            ui = director.Hud.GetComponent<UIDocument>().rootVisualElement;
        }

        static bool Visible(VisualElement element) => !element.ClassListContains("hidden");

        IEnumerator Play(int level)
        {
            director.StartLevel(level);
            director.ScriptedInput = true;
            director.Scripted = HeroInput.Idle;
            yield return null;
        }

        IEnumerator Hold(float move, float seconds)
        {
            director.Scripted = new HeroInput { Move = move, Use = -1 };
            yield return new WaitForSeconds(seconds);
            director.Scripted = HeroInput.Idle;
        }

        IEnumerator WaitFor(System.Func<bool> condition, float seconds)
        {
            for (float t = 0f; t < seconds && !condition(); t += Time.deltaTime)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheGameOpensOnTheTitleOverTheMeadow()
        {
            yield return Load();
            yield return new WaitForSeconds(0.5f);

            Assert.That(director.Screen, Is.EqualTo(GameScreen.Title));
            Assert.That(Visible(ui.Q("menu")), Is.True);
            Assert.That(Visible(ui.Q("logo")), Is.True);
            Assert.That(Visible(ui.Q("play-hud")), Is.False);
            Assert.That(director.LevelView.Level.Theme, Is.EqualTo(Theme.Meadow));
            Assert.That(director.Hud.Menu.Items.Select(item => item.Id).Take(3), Is.EqualTo(new[] { "play", "settings", "howto" }));
        }

        [UnityTest]
        public IEnumerator PlayOpensTheLevelSelectWithOnlyTheMeadowOpen()
        {
            yield return Load();
            director.Activate("play");

            MenuModel menu = director.Hud.Menu;
            Assert.That(menu.Title, Is.EqualTo("CHOOSE A LEVEL"));
            Assert.That(menu.Items.Select(item => item.Id), Is.EqualTo(new[] { "level0", "level1", "level2", "level3", "back" }));
            Assert.That(menu.Items[0].Value, Is.EqualTo("NEW"));
            Assert.That(menu.Items[1].Value, Is.EqualTo("LOCKED"));
            Assert.That(menu.Items[1].Disabled, Is.True);

            director.Activate("level1");
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Title), "a locked level does not start");
        }

        [UnityTest]
        public IEnumerator ChoosingTheMeadowStartsItWithTheHudOf2021()
        {
            yield return Load();
            director.Activate("play");
            director.Activate("level0");
            yield return null;

            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(director.Run.Level.Index, Is.EqualTo(0));
            Assert.That(Visible(ui.Q("menu")), Is.False);
            Assert.That(Visible(ui.Q("play-hud")), Is.True);
            Assert.That(director.Hud.BannerText, Is.EqualTo("SUNNY MEADOW"));
            Assert.That(director.Hud.CoinText, Is.EqualTo("0"));
            Assert.That(ui.Q("hotbar").childCount, Is.EqualTo(7));
            Assert.That(director.Hud.SlotCount(0), Is.EqualTo("2"), "two health potions to start");
            Assert.That(director.Hud.SlotEmpty(2), Is.True);
            Assert.That(ui.Q<Label>("vitals-tag").text, Is.EqualTo("JOLY WORLD"));
            Assert.That(ui.Q("menu-button"), Is.Not.Null);
            Assert.That(ui.Q("inventory-button"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator RunningAlongTheMeadowCollectsCoins()
        {
            yield return Load();
            yield return Play(0);

            yield return Hold(1f, 1.6f);

            Assert.That(director.Run.Coins, Is.GreaterThanOrEqualTo(4));
            Assert.That(director.Hud.CoinText, Is.EqualTo(director.Run.Coins.ToString()));
            int first = director.Run.Level.Pickups.FindIndex(p => p.Kind == PickupKind.Coin);
            Assert.That(director.Run.Taken(first), Is.True);
            Assert.That(director.LevelView.Pickups[first].gameObject.activeSelf, Is.False);
            Assert.That(director.Hud.TimerText, Is.Not.EqualTo("0:00.0"));
        }

        [UnityTest]
        public IEnumerator DrinkingFromTheHotbarRestoresHealth()
        {
            yield return Load();
            yield return Play(0);
            director.Run.Vitals.Hurt(50f);

            director.Scripted = new HeroInput { Use = 0 };
            yield return null;
            yield return null;

            Assert.That(director.Run.Vitals.Health.Current, Is.EqualTo(90f));
            Assert.That(director.Hud.HealthFraction, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(director.Hud.SlotCount(0), Is.EqualTo(string.Empty), "one potion left shows no number");
            Assert.That(director.Hud.SlotEmpty(0), Is.False);
        }

        [UnityTest]
        public IEnumerator FallingIntoTheRiverCostsHealthAndBringsTheHeroBack()
        {
            yield return Load();
            yield return Play(0);
            director.Run.Hero.Place(new Vec2(35.5f, 3f));

            yield return WaitFor(() => director.Run.Vitals.Health.Current < Vitals.MaxHealth, 3f);

            Assert.That(director.Run.Vitals.Health.Current, Is.EqualTo(Vitals.MaxHealth - Combat.FallDamage));
            Assert.That(director.Run.Hero.Position, Is.EqualTo(director.Run.Level.Spawn));
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
        }

        [UnityTest]
        public IEnumerator PauseStopsTheClockUntilResumed()
        {
            yield return Load();
            yield return Play(0);
            yield return new WaitForSeconds(0.3f);

            director.TogglePause();
            float paused = director.Run.Seconds;
            yield return new WaitForSeconds(0.4f);

            Assert.That(director.Screen, Is.EqualTo(GameScreen.Paused));
            Assert.That(director.Hud.Menu.Title, Is.EqualTo("PAUSED"));
            Assert.That(director.Hud.Menu.Items.Select(item => item.Id),
                Is.EqualTo(new[] { "resume", "inventory", "restart", "settings", "levels", "title" }));
            Assert.That(director.Run.Seconds, Is.EqualTo(paused));

            director.Activate("resume");
            yield return new WaitForSeconds(0.3f);
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(Visible(ui.Q("menu")), Is.False);
            Assert.That(director.Run.Seconds, Is.GreaterThan(paused));
        }

        [UnityTest]
        public IEnumerator RestartingFromThePauseMenuStartsTheLevelAgain()
        {
            yield return Load();
            yield return Play(0);
            yield return Hold(1f, 1.4f);
            Assert.That(director.Run.Coins, Is.GreaterThan(0));

            director.TogglePause();
            director.Activate("restart");
            yield return null;

            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(director.Run.Coins, Is.EqualTo(0));
            Assert.That(director.Run.Seconds, Is.LessThan(0.2f));
            Assert.That(director.Run.Hero.Position.X, Is.EqualTo(director.Run.Level.Spawn.X).Within(0.2f));
        }

        [UnityTest]
        public IEnumerator TheInventoryScreenPausesAndMovesItems()
        {
            yield return Load();
            yield return Play(0);

            director.OpenInventory(false);
            float seconds = director.Run.Seconds;
            yield return new WaitForSeconds(0.3f);

            Assert.That(director.Screen, Is.EqualTo(GameScreen.Inventory));
            Assert.That(Visible(ui.Q("inventory")), Is.True);
            Assert.That(director.Run.Seconds, Is.EqualTo(seconds));
            Assert.That(ui.Q<Label>("inventory-name").text, Is.EqualTo("HEALTH POTION  x2"));

            director.SelectInventorySlot(0);
            director.PickOrPlace();
            Assert.That(director.InventoryHeld, Is.EqualTo(0));
            director.SelectInventorySlot(9);
            director.PickOrPlace();

            Assert.That(director.InventoryHeld, Is.EqualTo(-1));
            Assert.That(director.Run.Inventory[9].Kind, Is.EqualTo(ItemKind.HealthPotion));
            Assert.That(director.Run.Inventory[0].Empty, Is.True);
            Assert.That(director.Hud.SlotEmpty(0), Is.True, "the hotbar follows the move");

            director.CloseInventory();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(Visible(ui.Q("inventory")), Is.False);
        }

        [UnityTest]
        public IEnumerator ReachingTheRainbowShowsTheResultsAndSavesTheBest()
        {
            yield return Load();
            yield return Play(0);
            director.Run.Hero.Place(new Vec2(101f, 0f));
            yield return Hold(1f, 1f);

            yield return WaitFor(() => director.Screen == GameScreen.Results, 5f);

            Assert.That(director.Screen, Is.EqualTo(GameScreen.Results));
            Assert.That(director.Hud.Menu.Title, Is.EqualTo("LEVEL CLEAR!"));
            Assert.That(director.Hud.Menu.Items.Select(item => item.Id), Is.EqualTo(new[] { "next", "again", "levels" }));
            Assert.That(director.Hud.Menu.Body.Select(row => row.Label), Is.EqualTo(new[] { "COINS", "GEMS", "TIME", "BEST", "KNOCK-OUTS" }));
            Assert.That(director.LastRecord.FirstClear, Is.True);
            Assert.That(director.Progress.Completed(0), Is.True);
            Assert.That(director.Progress.Unlocked(1), Is.True);
            Assert.That(director.Progress.BestSeconds(0), Is.EqualTo(director.LastResult.Seconds).Within(0.002f));
            Assert.That(store.GetInt("joly.v1.level0.cleared", 0), Is.EqualTo(1));
            Assert.That(store.Saves, Is.GreaterThan(0));

            director.Activate("next");
            yield return null;
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(director.Run.Level.Name, Is.EqualTo("Riverside"));
        }

        [UnityTest]
        public IEnumerator TheLevelSelectShowsTheRecordsOfAClearedLevel()
        {
            yield return Load();
            director.Progress.Submit(new LevelResult { Level = 0, Coins = 30, CoinTotal = 41, GemMask = 0b101, Gems = 2, GemTotal = 3, Seconds = 65.3f });

            director.Activate("play");

            MenuModel menu = director.Hud.Menu;
            Assert.That(menu.Items[0].Value, Is.EqualTo($"2/3 GEMS   30/{Levels.All[0].CoinCount}   1:05.3"));
            Assert.That(menu.Items[1].Value, Is.EqualTo("NEW"));
            Assert.That(menu.Selected, Is.EqualTo(1), "the first new level is picked");
            Assert.That(menu.Subtitle, Is.EqualTo("2 OF 12 GEMS FOUND"));
        }

        [UnityTest]
        public IEnumerator TheVolumeSettingIsAppliedAndSaved()
        {
            yield return Load();
            director.Activate("settings");
            Assert.That(director.Hud.Menu.Items.Select(item => item.Id), Is.EqualTo(new[] { "volume", "music", "back" }));

            director.Activate("volume");

            Assert.That(director.Settings.Volume, Is.EqualTo(9));
            Assert.That(AudioListener.volume, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(store.GetInt("joly.settings.volume", 0), Is.EqualTo(9));
            Assert.That(director.Hud.Menu.Items[0].Value, Is.EqualTo("9"));
        }

        [UnityTest]
        public IEnumerator QuittingToTheTitleFromThePauseMenu()
        {
            yield return Load();
            yield return Play(2);
            director.TogglePause();

            director.Activate("title");
            yield return null;

            Assert.That(director.Screen, Is.EqualTo(GameScreen.Title));
            Assert.That(Visible(ui.Q("logo")), Is.True);
            Assert.That(Visible(ui.Q("play-hud")), Is.False);
            Assert.That(director.LevelView.Level.Index, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TheSparkPopsAHedgehogInTheForest()
        {
            yield return Load();
            yield return Play(2);
            Critter hedgehog = director.Run.Critters[0];
            director.Run.Hero.Place(new Vec2(hedgehog.Position.X - 4f, 0f));
            yield return null;

            director.Scripted = new HeroInput { Cast = true, Use = -1 };
            yield return null;
            Assert.That(director.LevelView.VisibleBolts, Is.EqualTo(1));
            yield return WaitFor(() => !hedgehog.Alive, 1f);

            Assert.That(hedgehog.Alive, Is.False);
            Assert.That(director.Run.Vitals.Mana.Current, Is.LessThan(Vitals.MaxMana));
            yield return new WaitForSeconds(0.6f);
            Assert.That(director.LevelView.Critters[0].gameObject.activeSelf, Is.False);
        }
    }
}
