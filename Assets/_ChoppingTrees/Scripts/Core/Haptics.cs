using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Short phone vibrations for touch play (a tick on each hit, a thud when a tree falls).
    /// Android 8+ gets amplitude-controlled one-shots; elsewhere this does nothing.
    /// </summary>
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject s_Vibrator;
        static bool s_Tried, s_Amplitude;

        static AndroidJavaObject Vibrator
        {
            get
            {
                if (s_Tried) return s_Vibrator;
                s_Tried = true;
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    s_Vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using var version = new AndroidJavaClass("android.os.Build$VERSION");
                    s_Amplitude = version.GetStatic<int>("SDK_INT") >= 26 && s_Vibrator != null && s_Vibrator.Call<bool>("hasAmplitudeControl");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Haptics] " + e.Message);
                    s_Vibrator = null;
                }
                return s_Vibrator;
            }
        }
#endif

        /// <summary>Vibrates for <paramref name="ms"/> milliseconds at a strength of 0..1.</summary>
        public static void Play(int ms, float strength)
        {
            if (!GameSettings.Data.vibration || ms <= 0) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            var v = Vibrator;
            if (v == null) return;
            try
            {
                if (s_Amplitude)
                {
                    using var effect = new AndroidJavaClass("android.os.VibrationEffect");
                    int amp = Mathf.Clamp(Mathf.RoundToInt(strength * 255f), 1, 255);
                    using var oneShot = effect.CallStatic<AndroidJavaObject>("createOneShot", (long)ms, amp);
                    v.Call("vibrate", oneShot);
                }
                else if (strength > 0.3f) v.Call("vibrate", (long)ms);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] " + e.Message);
            }
#endif
        }

#if UNITY_ANDROID
        // Referencing Handheld.Vibrate makes Unity add the VIBRATE permission to the manifest.
        static void RequestPermission() => Handheld.Vibrate();
#endif
    }
}
