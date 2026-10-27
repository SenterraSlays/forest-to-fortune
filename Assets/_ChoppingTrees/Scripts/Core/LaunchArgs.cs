using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Launch flags (-ct-nosave, -ct-smoke, -ct-bench ...). On PC they come from the command line;
    /// on Android from the start intent's "unity" extra:
    ///   adb shell am start -n com.hazelwood.foresttofortune/com.unity3d.player.UnityPlayerGameActivity -e unity "-ct-bench -ct-nosave"
    /// </summary>
    public static class LaunchArgs
    {
        static string[] s_All;

        public static string[] All => s_All ??= Read();

        public static bool Has(string flag) => Array.IndexOf(All, flag) >= 0;

        static string[] Read()
        {
            var list = new List<string>(Environment.GetCommandLineArgs());
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity?.Call<AndroidJavaObject>("getIntent");
                string extra = intent?.Call<string>("getStringExtra", "unity");
                if (!string.IsNullOrEmpty(extra))
                    foreach (var a in extra.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        if (!list.Contains(a)) list.Add(a);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LaunchArgs] " + e.Message);
            }
#endif
            return list.ToArray();
        }
    }
}
