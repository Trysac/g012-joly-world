using NUnit.Framework;

namespace JolyWorld.Core.Tests
{
    public class ProgressTests
    {
        static LevelResult Result(int level, int coins, float seconds, int gemMask = 0) => new LevelResult
        {
            Level = level,
            Coins = coins,
            CoinTotal = 40,
            GemMask = gemMask,
            Gems = Bits(gemMask),
            GemTotal = 3,
            Seconds = seconds,
        };

        static int Bits(int mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1)
            {
                count++;
            }
            return count;
        }

        [Test]
        public void OnlyTheFirstLevelIsOpenAtTheStart()
        {
            var progress = new Progress(new MemoryStore(), 4);

            Assert.That(progress.LevelCount, Is.EqualTo(4));
            Assert.That(progress.Unlocked(0), Is.True);
            Assert.That(progress.Unlocked(1), Is.False);
            Assert.That(progress.Unlocked(4), Is.False);
            Assert.That(progress.Unlocked(-1), Is.False);
            Assert.That(progress.Completed(0), Is.False);
            Assert.That(progress.BestCoins(0), Is.EqualTo(0));
            Assert.That(progress.BestSeconds(0), Is.EqualTo(0f));
            Assert.That(progress.TotalGems, Is.EqualTo(0));
        }

        [Test]
        public void TheFirstClearRecordsEverythingOpensTheNextLevelAndSaves()
        {
            var store = new MemoryStore();
            var progress = new Progress(store, 4);

            RecordUpdate update = progress.Submit(Result(0, 12, 65.25f, 0b001));

            Assert.That(update.FirstClear, Is.True);
            Assert.That(update.NewBestCoins, Is.True);
            Assert.That(update.NewBestTime, Is.True);
            Assert.That(update.NewGems, Is.EqualTo(1));
            Assert.That(progress.Completed(0), Is.True);
            Assert.That(progress.Unlocked(1), Is.True);
            Assert.That(progress.Unlocked(2), Is.False);
            Assert.That(progress.BestCoins(0), Is.EqualTo(12));
            Assert.That(progress.BestSeconds(0), Is.EqualTo(65.25f).Within(0.001f));
            Assert.That(store.Saves, Is.EqualTo(1));
        }

        [Test]
        public void OnlyBetterRunsReplaceTheBests()
        {
            var progress = new Progress(new MemoryStore(), 4);
            progress.Submit(Result(0, 12, 60f));

            RecordUpdate worse = progress.Submit(Result(0, 10, 70f));
            Assert.That(worse.FirstClear || worse.NewBestCoins || worse.NewBestTime, Is.False);
            Assert.That(progress.BestCoins(0), Is.EqualTo(12));
            Assert.That(progress.BestSeconds(0), Is.EqualTo(60f).Within(0.001f));

            RecordUpdate better = progress.Submit(Result(0, 20, 55f));
            Assert.That(better.NewBestCoins, Is.True);
            Assert.That(better.NewBestTime, Is.True);
            Assert.That(progress.BestCoins(0), Is.EqualTo(20));
            Assert.That(progress.BestSeconds(0), Is.EqualTo(55f).Within(0.001f));
        }

        [Test]
        public void GemsFoundAddUpAcrossRuns()
        {
            var progress = new Progress(new MemoryStore(), 4);
            progress.Submit(Result(0, 1, 60f, 0b011));

            RecordUpdate update = progress.Submit(Result(0, 1, 60f, 0b110));

            Assert.That(update.NewGems, Is.EqualTo(1));
            Assert.That(progress.GemMask(0), Is.EqualTo(0b111));
            Assert.That(progress.GemsFound(0), Is.EqualTo(3));
            progress.Submit(Result(1, 1, 60f, 0b100));
            Assert.That(progress.TotalGems, Is.EqualTo(4));
        }

        [Test]
        public void ProgressIsReadBackFromTheStore()
        {
            var store = new MemoryStore();
            new Progress(store, 4).Submit(Result(1, 9, 42.5f, 0b101));

            var again = new Progress(store, 4);

            Assert.That(again.Completed(1), Is.True);
            Assert.That(again.Unlocked(2), Is.True);
            Assert.That(again.BestCoins(1), Is.EqualTo(9));
            Assert.That(again.BestSeconds(1), Is.EqualTo(42.5f).Within(0.001f));
            Assert.That(again.GemMask(1), Is.EqualTo(0b101));
        }

        [Test]
        public void AResultForAnUnknownLevelIsIgnored()
        {
            var store = new MemoryStore();
            var progress = new Progress(store, 4);

            RecordUpdate update = progress.Submit(Result(7, 9, 42.5f));

            Assert.That(update.FirstClear, Is.False);
            Assert.That(store.Saves, Is.EqualTo(0));
        }
    }

    public class SettingsTests
    {
        [Test]
        public void TheDefaultsAreEightAndSix()
        {
            Settings settings = Settings.Load(new MemoryStore());

            Assert.That(settings.Volume, Is.EqualTo(8));
            Assert.That(settings.Music, Is.EqualTo(6));
        }

        [Test]
        public void VolumesStepBetweenZeroAndTen()
        {
            Settings settings = Settings.Load(new MemoryStore());
            settings.StepVolume(5);
            Assert.That(settings.Volume, Is.EqualTo(10));
            settings.StepMusic(-9);
            Assert.That(settings.Music, Is.EqualTo(0));
            settings.StepMusic(1);
            Assert.That(settings.Music, Is.EqualTo(1));
        }

        [Test]
        public void SettingsAreSavedAndLoaded()
        {
            var store = new MemoryStore();
            Settings settings = Settings.Load(store);
            settings.StepVolume(-3);
            settings.StepMusic(2);
            settings.Save(store);

            Settings again = Settings.Load(store);

            Assert.That(again.Volume, Is.EqualTo(5));
            Assert.That(again.Music, Is.EqualTo(8));
            Assert.That(store.Saves, Is.EqualTo(1));
        }

        [Test]
        public void OutOfRangeStoredValuesAreClamped()
        {
            var store = new MemoryStore();
            store.SetInt("joly.settings.volume", 40);
            store.SetInt("joly.settings.music", -3);

            Settings settings = Settings.Load(store);

            Assert.That(settings.Volume, Is.EqualTo(10));
            Assert.That(settings.Music, Is.EqualTo(0));
        }
    }

    public class TimeTextTests
    {
        [TestCase(0f, "0:00.0")]
        [TestCase(5.25f, "0:05.2")]
        [TestCase(65.34f, "1:05.3")]
        [TestCase(600f, "10:00.0")]
        [TestCase(-3f, "0:00.0")]
        public void TimesShowMinutesSecondsAndTenths(float seconds, string text)
        {
            Assert.That(TimeText.Format(seconds), Is.EqualTo(text));
        }
    }
}
