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
    }
}
