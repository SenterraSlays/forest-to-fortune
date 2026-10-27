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
