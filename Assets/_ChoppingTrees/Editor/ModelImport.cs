using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ChoppingTrees.EditorTools
{
    /// <summary>
    /// Import rules for the Blender models in Art/Models: bake the axis conversion, remap M_*
    /// material slots to the project materials, turn COL_* boxes into BoxColliders, never import
    /// cameras, lights or animation. Also runs the Blender generator
    /// (Tools > Chopping Trees > 3. Build Models in Blender).
    /// </summary>
    public class ModelImport : AssetPostprocessor
    {
        public const string ModelDir = ProjectSetup.Root + "/Art/Models";
        /// <summary>The generator lives outside Assets with the .blend sources.</summary>
        public const string Script = "Blender/ct_models.py";

        static readonly string[] BlenderCandidates =
        {
            @"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe",
            @"C:\Program Files\Blender Foundation\Blender 4.2\blender.exe",
        };

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelDir)) return;
            var imp = (ModelImporter)assetImporter;
            imp.bakeAxisConversion = true;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importAnimation = false;
            imp.importBlendShapes = false;
            imp.animationType = ModelImporterAnimationType.None;
            imp.addCollider = false;
            imp.isReadable = false;
            imp.importNormals = ModelImporterNormals.Import;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            imp.generateSecondaryUV = false;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ModelDir)) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("COL_")) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string n = mats[i].name;
                    int dot = n.IndexOf('.');
                    if (dot > 0) n = n.Substring(0, dot);
                    if (!MaterialLibrary.Exists(n)) continue;
                    var m = MaterialLibrary.Get(n);
                    if (m != null) mats[i] = m;
                }
                r.sharedMaterials = mats;
            }

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.StartsWith("COL_") || mf.sharedMesh == null) continue;
                var go = mf.gameObject;
                var bounds = mf.sharedMesh.bounds;
                var bc = go.AddComponent<BoxCollider>();
                bc.center = bounds.center;
                bc.size = bounds.size;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) Object.DestroyImmediate(mr);
                Object.DestroyImmediate(mf);
            }
        }

        public static string BlenderPath
        {
            get
            {
                foreach (var p in BlenderCandidates) if (File.Exists(p)) return p;
                return null;
            }
        }

        [MenuItem("Tools/Chopping Trees/3. Build Models in Blender", priority = 3)]
        public static string BuildModels()
        {
            string blender = BlenderPath;
            if (blender == null)
            {
                string msg0 = "Blender not found (looked for 5.2 and 4.2 in Program Files). The committed FBX files are used as they are.";
                Debug.LogWarning("[ModelImport] " + msg0);
                return msg0;
            }
            Directory.CreateDirectory(ModelDir);
            string script = Path.GetFullPath(Script);
            string outDir = Path.GetFullPath(ModelDir);
            var psi = new ProcessStartInfo(blender, $"-b --factory-startup -P \"{script}\" -- \"{outDir}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            int exported = 0, failed = 0;
            using (var p = Process.Start(psi))
            {
                p.ErrorDataReceived += (s, e) => { };
                p.BeginErrorReadLine();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(600000);
                foreach (var line in output.Split('\n'))
                {
                    if (line.StartsWith("EXPORTED")) exported++;
                    if (line.StartsWith("FAILED")) { failed++; Debug.LogError("[Blender] " + line); }
                }
            }
            AssetDatabase.Refresh();
            ReimportModels();
            string msg = $"Blender exported {exported} models, {failed} failed";
            Debug.Log("[ModelImport] " + msg);
            return msg;
        }

        public static string ReimportModels()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
                n++;
            }
            return $"{n} models reimported";
        }

        public static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
    }
}
