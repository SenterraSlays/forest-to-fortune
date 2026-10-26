using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChoppingTrees.EditorTools
{
    /// <summary>
    /// Synthesises every sound in the game into WAV files and fills the AudioLibrary asset
    /// (Tools > Chopping Trees > 4. Generate Audio). All original: filtered noise, decaying
    /// partials and simple envelopes. Deterministic (fixed seed). Loops are cross-faded so they
    /// repeat without clicks.
    /// </summary>
    public static class AudioSynth
    {
        public const string Dir = ProjectSetup.Root + "/Audio";
        public const string LibraryPath = ProjectSetup.Root + "/Data/AudioLibrary.asset";
        const int SR = 44100;
        static System.Random s_Rng = new System.Random(1337);

        static float RandS() => (float)s_Rng.NextDouble() * 2f - 1f;
        static float Rand01() => (float)s_Rng.NextDouble();
        static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * SR)];

        static readonly Dictionary<string, List<string>> s_Groups = new Dictionary<string, List<string>>();

        [MenuItem("Tools/Chopping Trees/4. Generate Audio", priority = 4)]
        public static string GenerateAll()
        {
            s_Rng = new System.Random(1337);
            s_Groups.Clear();
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));

            Add("axe_swing", "axe_swing_1", Swish(0.22f, 650f, 0.8f));
            Add("axe_swing", "axe_swing_2", Swish(0.2f, 780f, 0.8f));
            for (int i = 0; i < 4; i++) Add("axe_chop", "axe_chop_" + (i + 1), Chop(i));
            Add("axe_thud", "axe_thud", Thud(0.14f, 120f, 0.8f));
            Add("axe_clang", "axe_clang", Clang());
            Add("tree_crack", "tree_crack", Crack());
            Add("tree_fall", "tree_fall", Fall());
            Add("log_break", "log_break", LogBreak());
            Add("collect", "collect", Pop());
            Add("sell", "sell", Coins());
            Add("purchase", "purchase", Arp(new[] { 523f, 659f, 784f }, 0.075f, 0.5f));
            Add("unlock", "unlock", Arp(new[] { 392f, 523f, 659f, 784f, 1046f }, 0.09f, 0.9f));
            Add("objective", "objective", Arp(new[] { 659f, 988f }, 0.1f, 0.45f));
            Add("error", "error", ErrorBuzz());
            Add("ui_click", "ui_click", Tone(new[] { 1400f, 2800f }, new[] { 1f, 0.2f }, 0.04f, 110f, true), 0.6f);
            Add("ui_hover", "ui_hover", Tone(new[] { 2300f }, new[] { 1f }, 0.025f, 180f, false), 0.3f);
            Add("menu_open", "menu_open", MenuOpen());
            for (int i = 0; i < 4; i++) Add("footstep", "footstep_" + (i + 1), Footstep(i));
            Add("jump", "jump", Swish(0.18f, 500f, 0.5f));
            Add("land", "land", Thud(0.16f, 90f, 0.8f));
            Add("chopper_loop", "chopper_loop", MakeLoop(Engine(2.4f, 58f, 950f, 0.35f), 0.25f), 0.8f);
            Add("sawmill_loop", "sawmill_loop", MakeLoop(Engine(2.4f, 46f, 1450f, 0.55f), 0.25f), 0.8f);
            Add("machine_cut", "machine_cut", SawBite());
            Add("build", "build", Build());
            Add("gate_open", "gate_open", Creak(1.6f, 0.9f));
            Add("wind", "wind", MakeLoop(Wind(9f), 1.5f), 0.9f);
            for (int i = 0; i < 3; i++) Add("bird", "bird_" + (i + 1), Bird(i));
            AssetDatabase.Refresh();
            var lib = BuildLibrary();
            return $"{CountFiles()} sounds in {s_Groups.Count} groups -> {AssetDatabase.GetAssetPath(lib)}";
        }

        static int CountFiles()
        {
            int n = 0;
            foreach (var g in s_Groups.Values) n += g.Count;
            return n;
        }

        static void Add(string group, string file, float[] data, float peak = 0.9f)
        {
            Write(file, data, peak);
            if (!s_Groups.TryGetValue(group, out var list)) s_Groups[group] = list = new List<string>();
            list.Add(file);
        }

        static readonly Dictionary<string, float> s_Volumes = new Dictionary<string, float>
        {
            ["axe_swing"] = 0.7f, ["footstep"] = 0.8f, ["wind"] = 0.5f, ["bird"] = 0.6f, ["chopper_loop"] = 0.8f, ["sawmill_loop"] = 0.8f,
        };

        static AudioLibrary BuildLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            var entries = new List<AudioLibrary.Entry>();
            foreach (var kv in s_Groups)
            {
                var clips = new List<AudioClip>();
                foreach (var f in kv.Value) clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>($"{Dir}/{f}.wav"));
                entries.Add(new AudioLibrary.Entry { name = kv.Key, clips = clips.ToArray(), volume = s_Volumes.TryGetValue(kv.Key, out var v) ? v : 1f });
            }
            lib.entries = entries.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        // ---------- output ----------

        static void Write(string name, float[] x, float peak)
        {
            bool loop = name.EndsWith("_loop") || name == "wind";
            float hp = 0f, prev = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float y = 0.995f * (hp + x[i] - prev);
                prev = x[i];
                hp = y;
                x[i] = y;
            }
            if (!loop)
            {
                int fadeIn = Mathf.Min(x.Length, 16), fadeOut = Mathf.Min(x.Length, SR / 100);
                for (int i = 0; i < fadeIn; i++) x[i] *= i / (float)fadeIn;
                for (int i = 0; i < fadeOut; i++) x[x.Length - 1 - i] *= i / (float)fadeOut;
            }
            float max = 1e-6f;
            foreach (var s in x) max = Mathf.Max(max, Mathf.Abs(s));
            float g = peak / max;
            string path = $"{Dir}/{name}.wav";
            using (var w = new BinaryWriter(new FileStream(path, FileMode.Create)))
            {
                int bytes = x.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' }); w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(SR); w.Write(SR * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (var s in x) w.Write((short)Mathf.Clamp(s * g * 32767f, -32768f, 32767f));
            }
            AssetDatabase.ImportAsset(path);
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = imp.defaultSampleSettings;
            settings.loadType = x.Length > SR * 3 ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            imp.defaultSampleSettings = settings;
            imp.forceToMono = true;
            imp.SaveAndReimport();
        }

        /// <summary>Cross-fades the tail into the head so the clip loops seamlessly.</summary>
        static float[] MakeLoop(float[] x, float fadeSeconds)
        {
            int f = Mathf.Min((int)(fadeSeconds * SR), x.Length / 3);
            int n = x.Length - f;
            var y = new float[n];
            Array.Copy(x, y, n);
            for (int i = 0; i < f; i++)
            {
                float t = i / (float)f;
                y[i] = x[i] * Mathf.Sqrt(t) + x[n + i] * Mathf.Sqrt(1f - t);
            }
            return y;
        }

        // ---------- DSP ----------

        sealed class Filter
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;

            public static Filter Band(float f, float q) => new Filter().Set(f, q, 0);
            public static Filter Low(float f, float q = 0.707f) => new Filter().Set(f, q, 1);
            public static Filter High(float f, float q = 0.707f) => new Filter().Set(f, q, 2);

            public Filter Set(float f, float q, int type)
            {
                float w = 2f * Mathf.PI * Mathf.Clamp(f, 10f, SR * 0.45f) / SR, al = Mathf.Sin(w) / (2f * q), c = Mathf.Cos(w), a0 = 1f + al;
                switch (type)
                {
                    case 0: b0 = al / a0; b1 = 0; b2 = -al / a0; break;
                    case 1: b0 = (1 - c) / 2 / a0; b1 = (1 - c) / a0; b2 = b0; break;
                    default: b0 = (1 + c) / 2 / a0; b1 = -(1 + c) / a0; b2 = b0; break;
                }
                a1 = -2f * c / a0;
                a2 = (1f - al) / a0;
                return this;
            }

            public float Run(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        static float Env(float t, float attack, float decayRate) => t < attack ? t / attack : Mathf.Exp(-(t - attack) * decayRate);
        static float SoftClip(float x) => (float)Math.Tanh(x);


        static void Mix(float[] dst, float[] src, float at, float gain)
        {
            int s0 = (int)(at * SR);
            for (int i = 0; i < src.Length && s0 + i < dst.Length; i++) dst[s0 + i] += src[i] * gain;
        }

        static float[] Clicks(float[] times, float freq, float decay)
        {
            var x = Buf(times[times.Length - 1] + 0.08f);
            foreach (float start in times)
            {
                var bp = Filter.Band(freq, 3f);
                int s0 = (int)(start * SR);
                for (int i = s0; i < x.Length; i++)
                {
                    float t = (i - s0) / (float)SR;
                    x[i] += bp.Run(RandS()) * Mathf.Exp(-t / decay) * 2f + (t < 0.0008f ? 0.6f : 0f);
                }
            }
            return x;
        }

        static float[] Scrape(float len, float freq)
        {
            var x = Buf(len);
            var bp = Filter.Band(freq, 1.2f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI);
                x[i] = bp.Run(RandS()) * env * (0.6f + 0.4f * Mathf.Sin(t * 180f));
            }
            return x;
        }

        static float[] Tone(float[] freqs, float[] amps, float len, float decay, bool click)
        {
            var x = Buf(len);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float s = 0f;
                for (int k = 0; k < freqs.Length; k++) s += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * amps[k] * Mathf.Exp(-t * decay * (1f + k * 0.6f));
                x[i] = s * Mathf.Clamp01(t / 0.0015f);
            }
            if (click) Mix(x, Clicks(new[] { 0f }, 4000f, 0.003f), 0f, 0.25f);
            return x;
        }

        static float[] Swish(float len, float freq, float gain)
        {
            var x = Buf(len);
            var bp = Filter.Band(freq, 0.9f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                x[i] = bp.Run(RandS()) * Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI) * gain;
            }
            return x;
        }

        static float[] Thud(float len, float cutoff, float gain)
        {
            var x = Buf(len);
            var lp = Filter.Low(cutoff * 4f);
            float phase = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                phase += 2f * Mathf.PI * Mathf.Lerp(cutoff * 0.5f, cutoff, Mathf.Exp(-t * 40f)) / SR;
                x[i] = (Mathf.Sin(phase) * 1.2f + lp.Run(RandS()) * 0.8f) * Env(t, 0.002f, 6f / len) * gain;
            }
            return x;
        }

        static float[] Impact(float freq, float len, float metal)
        {
            var x = Buf(len + metal * 0.35f + 0.05f);
            var bp = Filter.Band(freq, 1f);
            var lp = Filter.Low(1200f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float n = RandS();
                float s = bp.Run(n) * Env(t, 0.0005f, 1f / len * 4f) * 2f + lp.Run(n) * Env(t, 0.001f, 40f) * 0.7f;
                if (metal > 0f)
                    s += (Mathf.Sin(2f * Mathf.PI * 2350f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 3720f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 5180f * t)) * Mathf.Exp(-t * 12f) * 0.35f * metal;
                x[i] = s;
            }
            return x;
        }


        // ---------- game sounds ----------

        /// <summary>Axe biting into wood: a woody knock (low partials), a crisp transient and chip crackle.</summary>
        static float[] Chop(int v)
        {
            var x = Buf(0.42f);
            float body = 180f + v * 22f;
            var bp = Filter.Band(1100f + v * 180f, 1.4f);
            var lp = Filter.Low(2600f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float n = RandS();
                float knock = (Mathf.Sin(2f * Mathf.PI * body * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * body * 2.7f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * body * 4.1f * t)) * Mathf.Exp(-t * 28f);
                float crack = bp.Run(n) * Mathf.Exp(-t * 45f) * 2.2f;
                float click = (t < 0.002f ? 1f - t / 0.002f : 0f) * 1.2f;
                float crackle = lp.Run(n) * Mathf.Exp(-t * 14f) * (Rand01() < 0.02f ? 1.5f : 0.15f);
                x[i] = SoftClip((knock * 1.1f + crack + click + crackle) * 1.3f);
            }
            Mix(x, Thud(0.12f, 95f + v * 10f, 0.9f), 0f, 0.7f);
            return x;
        }

        static float[] Clang()
        {
            var x = Impact(2800f, 0.05f, 1f);
            Mix(x, Thud(0.1f, 160f, 0.6f), 0f, 0.5f);
            return x;
        }

        /// <summary>Creaking fibres: a slowly wandering resonant tone with grainy snaps.</summary>
        static float[] Creak(float len, float pitch)
        {
            var x = Buf(len);
            float phase = 0f;
            var bp = Filter.Band(700f * pitch, 6f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float f = (90f + 40f * Mathf.Sin(t * 3.1f) + 25f * Mathf.Sin(t * 7.3f)) * pitch;
                phase += 2f * Mathf.PI * f / SR;
                // A train of tiny stick-slip pulses gives the wooden creak texture.
                float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase)), 12f);
                float env = Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI);
                x[i] = bp.Run(pulse * 4f + RandS() * 0.05f) * env * 2f;
            }
            return x;
        }

        static float[] Crack()
        {
            var x = Creak(1.1f, 1.1f);
            float[] snaps = { 0.55f, 0.72f, 0.81f, 0.95f, 1.0f };
            foreach (var s in snaps) Mix(x, Clicks(new[] { 0f }, 1200f + Rand01() * 900f, 0.02f), s, 0.9f);
            Mix(x, Chop(1), 0.93f, 0.6f);
            return x;
        }

        static float[] Fall()
        {
            var x = Buf(2.2f);
            Mix(x, Swish(0.9f, 1600f, 0.45f), 0f, 1f);
            var leaves = Buf(1.6f);
            var hp = Filter.High(2500f);
            for (int i = 0; i < leaves.Length; i++)
            {
                float t = i / (float)SR;
                leaves[i] = hp.Run(RandS()) * Mathf.Sin(Mathf.Clamp01(t / 1.6f) * Mathf.PI) * (0.5f + 0.5f * Mathf.Sin(t * 37f)) * 0.5f;
            }
            Mix(x, leaves, 0.1f, 1f);
            Mix(x, Thud(0.9f, 48f, 1f), 0.75f, 1.6f);
            Mix(x, Thud(0.35f, 110f, 0.8f), 0.77f, 0.8f);
            Mix(x, Thud(0.3f, 70f, 0.6f), 1.12f, 0.5f);
            return x;
        }

        static float[] LogBreak()
        {
            var x = Buf(0.6f);
            for (int k = 0; k < 4; k++) Mix(x, Chop(k), k * 0.09f + Rand01() * 0.03f, 0.5f);
            return x;
        }

        static float[] Pop()
        {
            var x = Buf(0.16f);
            float phase = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                phase += 2f * Mathf.PI * Mathf.Lerp(520f, 900f, Mathf.Clamp01(t / 0.05f)) / SR;
                x[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 30f);
            }
            Mix(x, Thud(0.06f, 200f, 0.5f), 0f, 0.4f);
            return x;
        }

        static float[] Coins()
        {
            var x = Buf(1.0f);
            for (int k = 0; k < 7; k++)
            {
                float f = 2400f + Rand01() * 1800f;
                Mix(x, Tone(new[] { f, f * 1.51f, f * 2.3f }, new[] { 1f, 0.5f, 0.25f }, 0.35f, 14f, true), k * 0.055f + Rand01() * 0.02f, 0.5f);
            }
            Mix(x, Tone(new[] { 1318f, 2637f }, new[] { 1f, 0.35f }, 0.6f, 5f, true), 0.36f, 0.8f);
            return x;
        }

        static float[] Arp(float[] notes, float step, float len)
        {
            var x = Buf(step * notes.Length + len);
            for (int k = 0; k < notes.Length; k++)
                Mix(x, Tone(new[] { notes[k], notes[k] * 2f, notes[k] * 3f }, new[] { 1f, 0.3f, 0.12f }, len, 6f, k == 0), k * step, 0.8f);
            return x;
        }

        static float[] ErrorBuzz()
        {
            var x = Buf(0.3f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float f = t < 0.13f ? 196f : 165f;
                float gate = (t < 0.11f || (t > 0.14f && t < 0.27f)) ? 1f : 0f;
                float s = Mathf.Sin(2f * Mathf.PI * f * t);
                x[i] = (s > 0 ? 1f : -1f) * 0.3f * gate + s * 0.5f * gate;
            }
            var lp = Filter.Low(1400f);
            for (int i = 0; i < x.Length; i++) x[i] = lp.Run(x[i]);
            return x;
        }

        static float[] MenuOpen()
        {
            var x = Swish(0.25f, 900f, 0.5f);
            Mix(x, Thud(0.08f, 300f, 0.5f), 0.04f, 0.6f);
            return x;
        }

        /// <summary>Footstep on forest floor: soft thump plus leaf/grass crunch.</summary>
        static float[] Footstep(int v)
        {
            var x = Buf(0.24f);
            var lp = Filter.Low(500f + v * 60f);
            var crunch = Filter.Band(3200f + v * 400f, 0.9f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float n = RandS();
                float thump = lp.Run(n) * Mathf.Exp(-t * 40f) * 1.6f;
                float c = crunch.Run(n) * Mathf.Exp(-t * 22f) * (0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 400f + v))) * 0.45f;
                x[i] = thump + c;
            }
            return x;
        }

        /// <summary>Motor hum with harmonics plus a modulated saw whine (used as a loop).</summary>
        static float[] Engine(float len, float hum, float whine, float whineLevel)
        {
            var x = Buf(len);
            var bp = Filter.Band(whine, 3f);
            var lp = Filter.Low(400f);
            float ph = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                ph += 2f * Mathf.PI * hum / SR;
                float m = Mathf.Sin(ph) + 0.6f * Mathf.Sin(ph * 2f) + 0.35f * Mathf.Sin(ph * 3f) + 0.2f * Mathf.Sin(ph * 5f);
                float putter = 0.7f + 0.3f * Mathf.Sin(ph * 0.5f);
                float w = bp.Run(RandS()) * (0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 1.25f)) * whineLevel * 3f;
                x[i] = m * putter * 0.45f + lp.Run(RandS()) * 0.4f + w;
            }
            return x;
        }

        static float[] SawBite()
        {
            var x = Buf(0.5f);
            var bp = Filter.Band(1700f, 2.5f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI);
                x[i] = bp.Run(RandS()) * env * (0.7f + 0.3f * Mathf.Sin(t * 2f * Mathf.PI * 60f)) * 2f;
            }
            Mix(x, Chop(2), 0.3f, 0.3f);
            return x;
        }

        static float[] Build()
        {
            var x = Buf(0.9f);
            Mix(x, Clicks(new[] { 0f, 0.07f, 0.14f, 0.21f }, 2600f, 0.012f), 0f, 0.8f);
            Mix(x, Thud(0.3f, 70f, 1f), 0.3f, 1.2f);
            Mix(x, Impact(2200f, 0.05f, 0.6f), 0.31f, 0.5f);
            return x;
        }

        /// <summary>Gusty wind bed: low-passed noise with slow amplitude and brightness drift.</summary>
        static float[] Wind(float len)
        {
            var x = Buf(len);
            var lp = Filter.Low(500f);
            var bp = Filter.Band(900f, 0.7f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)SR;
                float gust = 0.55f + 0.3f * Mathf.Sin(t * 0.7f) + 0.15f * Mathf.Sin(t * 1.9f + 1f);
                if (i % 64 == 0) bp.Set(700f + 500f * gust, 0.7f, 0);
                float n = RandS();
                x[i] = (lp.Run(n) * 1.2f + bp.Run(n) * 0.5f) * gust;
            }
            return x;
        }

        static float[] Bird(int v)
        {
            float[] lens = { 0.5f, 0.7f, 0.45f };
            var x = Buf(lens[v] + 0.1f);
            int notes = 3 + v;
            for (int k = 0; k < notes; k++)
            {
                float start = k * (lens[v] / notes);
                float f0 = 2600f + v * 500f + Rand01() * 700f, f1 = f0 * (v == 1 ? 0.7f : 1.35f);
                float len = lens[v] / notes * 0.8f;
                var chirp = Buf(len);
                float ph = 0f;
                for (int i = 0; i < chirp.Length; i++)
                {
                    float t = i / (float)SR;
                    float f = Mathf.Lerp(f0, f1, t / len) + 120f * Mathf.Sin(t * 2f * Mathf.PI * 38f);
                    ph += 2f * Mathf.PI * f / SR;
                    chirp[i] = Mathf.Sin(ph) * Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI);
                }
                Mix(x, chirp, start, 0.8f);
            }
            return x;
        }
    }
}
