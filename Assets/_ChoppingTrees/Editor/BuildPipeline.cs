using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChoppingTrees.EditorTools
{
    /// <summary>
    /// One-click regeneration of all generated content and the Windows build
    /// (Tools > Chopping Trees > Build Everything / Build Windows Player).
    /// </summary>
    public static class BuildPipeline
    {
        public const string PlayerPath = "Builds/Windows/ForestToFortune.exe";
        public const string AndroidPath = "Builds/Android/ForestToFortune.apk";
        public const string IosPath = "Builds/iOS";

        [MenuItem("Tools/Forest to Fortune/Build Everything", priority = 20)]
        public static void BuildEverythingMenu() => BuildEverything(true);

        /// <summary>Setup, materials, (Blender models), audio, data and the scene, in order.</summary>
        public static string BuildEverything(bool runBlender)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine(ProjectSetup.Run());
            log.AppendLine(MaterialLibrary.BuildAll());
            log.AppendLine(runBlender ? ModelImport.BuildModels() : ModelImport.ReimportModels());
            log.AppendLine(AudioSynth.GenerateAll());
            log.AppendLine(ContentBuilder.BuildAll());
            log.AppendLine(SceneBuilder.Build());
            Debug.Log("[BuildEverything]\n" + log);
            return log.ToString();
        }

        [MenuItem("Tools/Forest to Fortune/Build Windows Player", priority = 21)]
        public static string BuildWindows()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            string msg = $"[BuildScript] {report.summary.result}: {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors, {report.summary.totalTime.TotalSeconds:0}s -> {PlayerPath}";
            Debug.Log(msg);
            return msg;
        }

        /// <summary>
        /// Android APK (ARM64, IL2CPP) for sideloading, signed with the debug key. Switches the
        /// editor to Android for the build; <see cref="SwitchToWindows"/> switches back.
        /// </summary>
        [MenuItem("Tools/Forest to Fortune/Build Android APK", priority = 22)]
        public static string BuildAndroid()
        {
            // The editor must already be on Android. Built straight from Windows (Unity switching
            // inside BuildPlayer), URP's shader stripping kept 0 variants of the Lit shader's main
            // pass and nothing drew on phones. So switch first, and build once the scripts reload.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                SessionState.SetBool(ResumeAndroidKey, true);
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                EditorApplication.delayCall += ResumeAndroidBuild; // in case the switch didn't reload scripts
                const string wait = "[BuildScript] Switched the editor to Android; the APK builds after the reload.";
                Debug.Log(wait);
                return wait;
            }
            ProjectSetup.AndroidSettings();
            Directory.CreateDirectory(Path.GetDirectoryName(AndroidPath));
            EditorUserBuildSettings.buildAppBundle = false;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = AndroidPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            string msg = $"[BuildScript] Android {report.summary.result}: {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors, {report.summary.totalTime.TotalSeconds:0}s -> {AndroidPath}";
            Debug.Log(msg);
            return msg;
        }

        const string ResumeAndroidKey = "ForestToFortune.ResumeAndroidBuild";

        /// <summary>Finishes a Build Android APK that had to switch the editor to Android first.</summary>
        [InitializeOnLoadMethod]
        static void ResumeAndroidBuild()
        {
            if (!SessionState.GetBool(ResumeAndroidKey, false)) return;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) return;
            SessionState.EraseBool(ResumeAndroidKey);
            EditorApplication.delayCall += () => BuildAndroid();
        }

        /// <summary>
        /// iOS Xcode project. Windows can't compile or sign an IPA, so the GitHub Actions workflow
        /// (.github/workflows/ios.yml) runs this on Linux and builds the unsigned IPA on a cloud
        /// Mac. From the menu it switches the editor to iOS first; run it again after the reload.
        /// </summary>
        [MenuItem("Tools/Forest to Fortune/Export iOS Xcode Project", priority = 24)]
        public static string BuildIos()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
                const string wait = "[BuildScript] Switched the editor to iOS; export again after the reload.";
                Debug.Log(wait);
                return wait;
            }
            ProjectSetup.IosSettings();
            Directory.CreateDirectory(IosPath);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = IosPath,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            string msg = $"[BuildScript] iOS {report.summary.result}: {report.summary.totalErrors} errors, {report.summary.totalTime.TotalSeconds:0}s -> {IosPath}";
            Debug.Log(msg);
            // CI (batch mode) needs a failing exit code to stop the workflow.
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
            return msg;
        }

        /// <summary>Back to Windows after an Android build. The package assemblies are rebuilt
        /// from scratch: otherwise they can keep Android's defines (no upscaler framework), and the
        /// Windows build then fails with "script class layout is incompatible".</summary>
        [MenuItem("Tools/Forest to Fortune/Switch Editor to Windows", priority = 23)]
        public static bool SwitchToWindows()
        {
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(UnityEditor.Compilation.RequestScriptCompilationOptions.CleanBuildCache);
            return ok;
        }
    }
}
