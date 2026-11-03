using System;
using System.Collections.Generic;

namespace JolyWorld.Core
{
    /// <summary>Somewhere to keep whole numbers between sessions (PlayerPrefs in the game).</summary>
    public interface IKeyValueStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        void Save();
    }

    /// <summary>A store that forgets everything when the game closes (tests and screenshots).</summary>
    public sealed class MemoryStore : IKeyValueStore
    {
        readonly Dictionary<string, int> values = new Dictionary<string, int>();

        /// <summary>How many times <see cref="Save"/> was called.</summary>
        public int Saves { get; private set; }

        public int GetInt(string key, int fallback) => values.TryGetValue(key, out int value) ? value : fallback;
        public void SetInt(string key, int value) => values[key] = value;
        public void Save() => Saves++;
    }

    /// <summary>What a finished level changed in the records.</summary>
    public struct RecordUpdate
    {
        public bool FirstClear;
        public bool NewBestCoins;
        public bool NewBestTime;
        /// <summary>Gems found in this run that had never been found before.</summary>
        public int NewGems;
    }

    /// <summary>
    /// The player's progress on this device: which levels are cleared (each one opens the next),
    /// the most coins and the best time on each, and every gem ever found.
    /// </summary>
    public sealed class Progress
    {
        const string Prefix = "joly.v1.level";

        readonly IKeyValueStore store;

        public Progress(IKeyValueStore store, int levelCount)
        {
            this.store = store;
            LevelCount = levelCount;
        }

        public int LevelCount { get; }

        static string Key(int level, string field) => $"{Prefix}{level}.{field}";

        bool Valid(int level) => level >= 0 && level < LevelCount;

        /// <summary>A stored record, where a negative (corrupt) value counts as no record.</summary>
        int Record(int level, string field) => Math.Max(0, store.GetInt(Key(level, field), 0));

        public bool Completed(int level) => Valid(level) && store.GetInt(Key(level, "cleared"), 0) == 1;

        public bool Unlocked(int level) => Valid(level) && (level == 0 || Completed(level - 1));

        public int BestCoins(int level) => Valid(level) ? Record(level, "coins") : 0;

        /// <summary>The fastest clear in seconds, or 0 when the level was never cleared.</summary>
        public float BestSeconds(int level) => Valid(level) ? Record(level, "millis") / 1000f : 0f;

        public int GemMask(int level) => Valid(level) ? Record(level, "gems") : 0;

        public int GemsFound(int level) => Bits(GemMask(level));

        public int TotalGems
        {
            get
            {
                int total = 0;
                for (int level = 0; level < LevelCount; level++)
                {
                    total += GemsFound(level);
                }
                return total;
            }
        }

        /// <summary>Records a cleared level and saves.</summary>
        public RecordUpdate Submit(LevelResult result)
        {
            var update = new RecordUpdate();
            int level = result.Level;
            if (!Valid(level))
            {
                return update;
            }
            update.FirstClear = !Completed(level);
            store.SetInt(Key(level, "cleared"), 1);

            if (update.FirstClear || result.Coins > BestCoins(level))
            {
                update.NewBestCoins = result.Coins > BestCoins(level) || (update.FirstClear && result.Coins > 0);
                store.SetInt(Key(level, "coins"), Math.Max(result.Coins, BestCoins(level)));
            }

            int millis = Math.Max(1, (int)Math.Round(result.Seconds * 1000f));
            int best = Record(level, "millis");
            if (best == 0 || millis < best)
            {
                update.NewBestTime = true;
                store.SetInt(Key(level, "millis"), millis);
            }

            int known = GemMask(level);
            update.NewGems = Bits(result.GemMask & ~known);
            store.SetInt(Key(level, "gems"), known | result.GemMask);
            store.Save();
            return update;
        }

        static int Bits(int mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1)
            {
                count++;
            }
            return count;
        }
    }

    /// <summary>The volume settings, 0..10 each.</summary>
    public sealed class Settings
    {
        public const int MaxVolume = 10;
        const string VolumeKey = "joly.settings.volume";
        const string MusicKey = "joly.settings.music";

        public int Volume { get; private set; } = 8;
        public int Music { get; private set; } = 6;

        public static Settings Load(IKeyValueStore store) => new Settings
        {
            Volume = Clamp(store.GetInt(VolumeKey, 8)),
            Music = Clamp(store.GetInt(MusicKey, 6)),
        };

        public void Save(IKeyValueStore store)
        {
            store.SetInt(VolumeKey, Volume);
            store.SetInt(MusicKey, Music);
            store.Save();
        }

        public void StepVolume(int step) => Volume = Clamp(Volume + step);

        public void StepMusic(int step) => Music = Clamp(Music + step);

        static int Clamp(int value) => Math.Max(0, Math.Min(MaxVolume, value));
    }

    public static class TimeText
    {
        /// <summary>Minutes, seconds and tenths, such as 1:05.3.</summary>
        public static string Format(float seconds)
        {
            int tenths = (int)Math.Floor(Math.Max(0f, seconds) * 10f + 1e-4f);
            int whole = tenths / 10;
            return $"{whole / 60}:{whole % 60:00}.{tenths % 10}";
        }
    }
}
