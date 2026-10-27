#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace ChoppingTrees.EditorTools
{
    /// <summary>
    /// Info.plist additions for the iOS export: ProMotion iPhones only run above 60 FPS with
    /// CADisableMinimumFrameDurationOnPhone, and declaring no export-regulated encryption keeps
    /// signing and upload tools from asking.
    /// </summary>
    public static class IosPostBuild
    {
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
