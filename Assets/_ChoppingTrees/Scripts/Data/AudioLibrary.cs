using System;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Named sound clips. Several clips under one name are picked at random.</summary>
    [CreateAssetMenu(menuName = "Chopping Trees/Audio Library", fileName = "AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string name;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
        }

        public Entry[] entries;

        public Entry Find(string name)
        {
            if (entries == null) return null;
            foreach (var e in entries) if (e != null && e.name == name) return e;
            return null;
        }
    }
}
