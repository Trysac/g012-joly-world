using JolyWorld.Core;
using UnityEngine;

namespace JolyWorld.Game
{
    /// <summary>Keeps the progress and the settings on the device with PlayerPrefs.</summary>
    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
