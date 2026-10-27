using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>Plays named sounds from the <see cref="AudioLibrary"/> through a small pool of sources.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] AudioLibrary m_Library;
        [SerializeField] int m_PoolSize = 20;

        AudioSource[] m_Pool;
        AudioSource m_Ui;
        int m_Next;
        /// <summary>Looping sources (wind, machines) with their base volume, for the ambience slider.</summary>
        readonly System.Collections.Generic.List<(AudioSource source, float volume)> m_Loops = new System.Collections.Generic.List<(AudioSource, float)>();

        static float EffectsVolume => GameSettings.Data.effectsVolume;
        static float InterfaceVolume => GameSettings.Data.interfaceVolume;
        static float AmbienceVolume => GameSettings.Data.ambienceVolume;

        void Awake()
        {
            Instance = this;
            if (m_Library == null && Game.Instance != null) m_Library = Game.Instance.Db.audio;
            m_Pool = new AudioSource[m_PoolSize];
            for (int i = 0; i < m_PoolSize; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 2f;
                s.maxDistance = 45f;
                s.dopplerLevel = 0f;
                m_Pool[i] = s;
            }
            m_Ui = gameObject.AddComponent<AudioSource>();
            m_Ui.playOnAwake = false;
            m_Ui.spatialBlend = 0f;
            m_Ui.ignoreListenerPause = true;
            GameSettings.Changed += UpdateLoops;
        }

        void UpdateLoops()
        {
            for (int i = m_Loops.Count - 1; i >= 0; i--)
            {
                if (m_Loops[i].source == null) { m_Loops.RemoveAt(i); continue; }
                m_Loops[i].source.volume = m_Loops[i].volume * AmbienceVolume;
            }
        }

        void OnDestroy()
        {
            GameSettings.Changed -= UpdateLoops;
            if (Instance == this) Instance = null;
        }

        public AudioClip Clip(string name, out float volume)
        {
            volume = 1f;
            var e = m_Library != null ? m_Library.Find(name) : null;
            if (e == null || e.clips == null || e.clips.Length == 0) return null;
            volume = e.volume;
            return e.clips[Random.Range(0, e.clips.Length)];
        }

        /// <summary>3D one-shot at a world position.</summary>
        public void Play(string name, Vector3 position, float volume = 1f, float pitch = 1f, float pitchJitter = 0.06f)
        {
            var clip = Clip(name, out float v);
            if (clip == null) return;
            var s = m_Pool[m_Next];
            m_Next = (m_Next + 1) % m_Pool.Length;
            s.transform.position = position;
            s.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            s.volume = v * volume * EffectsVolume;
            s.clip = clip;
            s.Play();
        }

        /// <summary>Non-positional sound (UI, the player's own axe).</summary>
        public void Play2D(string name, float volume = 1f, float pitch = 1f, float pitchJitter = 0.04f)
        {
            var clip = Clip(name, out float v);
            if (clip == null) return;
            m_Ui.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            m_Ui.PlayOneShot(clip, v * volume * InterfaceVolume);
        }

        public static void Sfx(string name, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            if (Instance != null) Instance.Play(name, pos, volume, pitch);
        }

        public static void Ui(string name, float volume = 1f, float pitch = 1f)
        {
            if (Instance != null) Instance.Play2D(name, volume, pitch);
        }

        /// <summary>Creates a looping 3D source on a machine or ambience object.</summary>
        public AudioSource CreateLoop(string name, Transform parent, float volume, float maxDistance = 30f)
        {
            var clip = Clip(name, out float v);
            if (clip == null) return null;
            var s = parent.gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = v * volume * AmbienceVolume;
            m_Loops.Add((s, v * volume));
            s.spatialBlend = parent == transform ? 0f : 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 2f;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            s.time = Random.Range(0f, clip.length);
            s.Play();
            return s;
        }
    }
}
