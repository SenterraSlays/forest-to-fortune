using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChoppingTrees.EditorTools
{
    /// <summary>
    /// Builds the game scene from code (Tools > Chopping Trees > 6. Build Scene): ground, forests,
    /// the yard with market, workbench and machine slots, gates, props, lighting, post-processing,
    /// the player and all game systems. Deterministic (fixed seeds), so rebuilding gives the same world.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = ProjectSetup.Root + "/Scenes/Main.unity";
        const string PrefabDir = ProjectSetup.Root + "/Prefabs";
        const string TexDir = ProjectSetup.Root + "/Art/Textures";

        static System.Random s_Rng;
        static float R(float a, float b) => a + (float)s_Rng.NextDouble() * (b - a);
        static readonly List<Vector3> s_Blocked = new List<Vector3>(); // x, z, radius

        [MenuItem("Tools/Forest to Fortune/6. Build Scene", priority = 6)]
        public static string Build()
        {
            s_Rng = new System.Random(20261026);
            s_Blocked.Clear();
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentBuilder.DatabasePath);
            if (db == null) return "Build the game data first.";
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(TexDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            var prefabs = BuildPrefabs();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene unloads unreferenced assets, so load the data again before using it.
            db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentBuilder.DatabasePath);

            var env = new GameObject("Environment").transform;
            Lighting(env);
            Ground(env);
            Bounds(env);

            // ---- systems
            var systems = new GameObject("Game");
            var game = systems.AddComponent<Game>();
            Set(game, "m_Database", db);
            var audio = systems.AddComponent<AudioManager>();
            Set(audio, "m_Library", db.audio);
            var fx = systems.AddComponent<Effects>();
            Set(fx, "m_ParticleMaterial", MaterialLibrary.Get("M_Particle"));
            systems.AddComponent<AmbientAudio>();
            systems.AddComponent<GraphicsService>();
            systems.AddComponent<PerfStats>();
            var pickups = new GameObject("Pickups").AddComponent<PickupSpawner>();
            Set(pickups, "m_Prefab", prefabs["LogPickup"]);

            // ---- yard
            var yard = new GameObject("Yard").transform;
            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(yard);
            spawn.transform.position = WorldLayout.OnGround(WorldLayout.Spawn.x, WorldLayout.Spawn.z) + Vector3.up * 0.1f;
            spawn.AddComponent<PlayerSpawn>();
            Block(WorldLayout.Spawn.x, WorldLayout.Spawn.z, 9f);

            var market = Place("Market", yard, -9.5f, -13f, 35f);
            market.AddComponent<SellPoint>();
            Set(market.GetComponent<SellPoint>(), "m_EffectPoint", Find(market.transform, "EffectPoint"));
            SignText(Find(market.transform, "SignAnchor"), market.transform, "LUMBER MARKET", 2.4f, 0.5f);
            Block(-9.5f, -13f, 4f);

            var bench = Place("Workbench", yard, 9.5f, -13f, -35f);
            bench.AddComponent<UpgradeStation>();
            SignText(Find(bench.transform, "SignAnchor"), bench.transform, "WORKBENCH", 1.7f, 0.4f);
            Block(9.5f, -13f, 3.5f);

            StockPile(yard, "WoodPile", "LogPiece", GameModel.Wood, new Vector3(-14.8f, 0, -9.6f), 35f, 21, 5f, 1.35f, 0.36f, 0.95f);
            StockPile(yard, "PlankPile", "PlankPiece", "planks", new Vector3(-15.6f, 0, -15.8f), 35f, 12, 3f, 1.45f, 0.13f, 0f);
            foreach (var (x, z) in new[] { (-5.5f, -9f), (5.5f, -9f), (0f, -19f), (-3f, 46f), (-3f, 116f), (-34f, -19f), (34f, -21.5f), (-30f, -33f), (29f, -32f) })
            {
                var lamp = Place("Lantern_Post", yard, x, z, 0f);
                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(Find(lamp.transform, "LightPoint"), false);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.72f, 0.42f);
                light.range = 7f;
                light.intensity = 2.2f;
                light.shadows = LightShadows.None;
            }
            Place("Cabin", yard, -24f, -39f, 25f);
            Block(-24f, -39f, 5f);
            foreach (var (x, z, yaw) in new[] { (-12.5f, -16.5f, 10f), (-13.2f, -17.6f, 40f), (12.4f, -15.5f, 20f), (4f, -38f, 0f) })
                Place("Crate", yard, x, z, yaw);
            Place("Signpost", yard, 5.5f, 1.5f, -20f);
            Place("Signpost", yard, 3.2f, 47f, 10f);
            Place("Signpost", yard, -3.2f, 117f, -10f);

            // Furniture and cabin kit stock, and the buildings around the bigger yard: a red barn, a
            // water tower, lumber racks, wagons, a well and two more cabins.
            GoodsPile(yard, "FurniturePile", "FurniturePiece", "furniture", -33.5f, -16f, 90f, new Vector2(1.45f, 1.2f), 1.18f, 2f, 1.5f);
            GoodsPile(yard, "CabinKitPile", "CabinKit", "cabins", 33.5f, -16.5f, -90f, new Vector2(1.85f, 1.4f), 0.88f, 1f, 1.5f);
            Place("Warehouse", yard, 30f, -39.5f, 0f);
            Block(30f, -39.5f, 7f);
            Place("WaterTower", yard, -33f, -40f, 15f);
            Block(-33f, -40f, 3f);
            foreach (var (x, z, yaw) in new[] { (-33.5f, -26.5f, 90f), (15f, -39.5f, 0f) })
            {
                Place("LumberRack", yard, x, z, yaw);
                Block(x, z, 2.5f);
            }
            foreach (var (x, z, yaw) in new[] { (-26f, -12f, 25f), (35f, -8.5f, -150f) })
            {
                Place("Wagon", yard, x, z, yaw);
                Block(x, z, 2.2f);
            }
            Place("Well", yard, 21f, -10.5f, 0f);
            Block(21f, -10.5f, 1.6f);
            Place("Cabin", yard, -45f, -40.5f, 15f);
            Block(-45f, -40.5f, 4.5f);
            Place("Cabin", yard, -45f, -5.5f, 165f);
            Block(-45f, -5.5f, 4.5f);
            foreach (var (x, z, yaw) in new[] { (-36.5f, -36f, 30f), (-36f, -9f, 0f), (37f, -40.5f, 15f), (25.5f, -14f, 45f) })
                Place("Crate", yard, x, z, yaw);

            var yardComp = MachineYardBuild(yard, prefabs);

            // ---- gates and fences
            var grove = db.areas.First(a => a.id == "grove");
            var ridge = db.areas.First(a => a.id == "ridge");
            GateAndFence(env, WorldLayout.Gate1Z, grove);
            GateAndFence(env, WorldLayout.Gate2Z, ridge);

            // ---- forests and props
            var forest = new GameObject("Forest").transform;
            int trees = 0;
            trees += Forest(forest, db.areas.First(a => a.id == "clearing"), -55f, 55f, -4f, 47f, 4.2f, 120);
            // The yard's west and east sides are the carpentry quarter and the builders' yard now;
            // only a thin line of trees stays along the walls behind them.
            trees += Forest(forest, db.areas.First(a => a.id == "clearing"), -55f, -48f, -42f, -6f, 4.5f, 6);
            trees += Forest(forest, db.areas.First(a => a.id == "clearing"), 48f, 55f, -42f, -6f, 4.5f, 6);
            trees += Forest(forest, grove, -55f, 55f, 54f, 117f, 5.6f, 95);
            trees += Forest(forest, ridge, -55f, 55f, 124f, 189f, 6.2f, 75, "goldbark", 4);
            Backdrop(env, db);
            int props = Props(env);

            // ---- player and cameras
            var player = PlayerRig(spawn.transform, db, out var axe, out var interactor, out var playerCam);
            var menuCam = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener));
            menuCam.tag = "MainCamera";
            var mc = menuCam.GetComponent<Camera>();
            mc.fieldOfView = 55f;
            mc.nearClipPlane = 0.3f;
            mc.farClipPlane = 700f;
            mc.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            menuCam.AddComponent<CameraGraphics>();
            var menu = menuCam.AddComponent<MenuCamera>();
            Set(menu, "m_PlayerCamera", playerCam);
            Set(menu, "m_Focus", new Vector3(0f, 3f, -6f));
            Set(menu, "m_Radius", 20f);
            Set(menu, "m_Height", 13f);

            var ui = new GameObject("UI").AddComponent<UIRoot>();
            Set(ui, "m_Axe", axe);
            Set(ui, "m_Interactor", interactor);

            Lightmapping.lightingSettings = LightingAsset();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            try { Lightmapping.Bake(); } catch (System.Exception e) { Debug.LogWarning("Environment bake skipped: " + e.Message); }
            EditorSceneManager.SaveScene(scene, ScenePath);
            string msg = $"[SceneBuilder] {ScenePath}: {trees} trees, {props} props, {yardComp} machine slots";
            Debug.Log(msg);
            return msg;
        }

        // ------------------------------------------------------------------ helpers

        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[SceneBuilder] {target.GetType().Name}.{field} not found"); return; }
            switch (value)
            {
                case Object o: p.objectReferenceValue = o; break;
                case Vector3 v: p.vector3Value = v; break;
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case string s: p.stringValue = s; break;
                case bool b: p.boolValue = b; break;
                case Color c: p.colorValue = c; break;
                case Object[] arr:
                    p.arraySize = arr.Length;
                    for (int k = 0; k < arr.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = arr[k];
                    break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var f = Find(c, name);
                if (f != null) return f;
            }
            return null;
        }

        static void Block(float x, float z, float r) => s_Blocked.Add(new Vector3(x, z, r));

        static bool IsBlocked(float x, float z, float pad)
        {
            foreach (var b in s_Blocked)
            {
                float dx = x - b.x, dz = z - b.y;
                if (dx * dx + dz * dz < (b.z + pad) * (b.z + pad)) return true;
            }
            return false;
        }

        /// <summary>A clean wrapper object holding an instance of a Blender model.</summary>
        static GameObject Model(string model, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, string name = null)
        {
            var src = ModelImport.Load(model);
            var root = new GameObject(name ?? model);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, rot);
            root.transform.localScale = scale;
            if (src == null) { Debug.LogError("[SceneBuilder] missing model " + model); return root; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            inst.name = model + "_Model";
            return root;
        }

        static GameObject Place(string model, Transform parent, float x, float z, float yaw, float scale = 1f, float sink = 0f)
            => Model(model, parent, WorldLayout.OnGround(x, z, sink), Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);

        static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static void SetStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        static void NoShadows(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>World-space text on a sign board. Imported empties carry the axis-conversion
        /// rotation, so the text is oriented from the owner's facing instead of the anchor's.</summary>
        static TextMeshPro SignText(Transform anchor, Transform owner, string text, float width, float height)
        {
            if (anchor == null) return null;
            var go = new GameObject("SignText", typeof(RectTransform));
            go.transform.SetParent(owner, false);
            Vector3 front = owner.forward;
            // TMP text reads correctly when its +Z points away from the viewer, i.e. into the board.
            go.transform.SetPositionAndRotation(anchor.position + front * 0.04f, Quaternion.LookRotation(-front, Vector3.up));
            var t = go.AddComponent<TextMeshPro>();
            t.rectTransform.sizeDelta = new Vector2(width, height);
            t.text = text;
            t.font = ProjectSetup.Font("Inter-SemiBold");
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.5f;
            t.fontSizeMax = 6f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.98f, 0.9f, 0.7f);
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        // ------------------------------------------------------------------ prefabs

        static Dictionary<string, GameObject> BuildPrefabs()
        {
            var d = new Dictionary<string, GameObject>();

            var log = Model("LogPiece", null, Vector3.zero, Quaternion.identity, Vector3.one, "LogPickup");
            log.AddComponent<WoodPickup>();
            SetLayer(log, GameLayers.Pickup);
            NoShadows(log);
            d["LogPickup"] = Save(log, "LogPickup");

            var marker = Model("SlotMarker", null, Vector3.zero, Quaternion.identity, Vector3.one, "SlotMarker");
            d["SlotMarker"] = Save(marker, "SlotMarker");

            d["AutoChopper"] = Machine("AutoChopper", "chopper", 1);
            d["AutoChopper_Mk2"] = Machine("AutoChopper_Mk2", "chopper", 2);
            d["AutoChopper_Mk3"] = Machine("AutoChopper_Mk3", "chopper", 3);
            d["Sawmill"] = Machine("Sawmill", "sawmill", 0);
            d["Workshop"] = Machine("Workshop", "workshop", 0);
            d["CabinBuilder"] = Machine("CabinBuilder", "builder", 0);
            return d;
        }

        static GameObject Save(GameObject go, string name)
        {
            var p = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return p;
        }

        static GameObject Machine(string model, string id, int blades)
        {
            var root = Model(model, null, Vector3.zero, Quaternion.identity, Vector3.one, model);
            var inst = root.transform.GetChild(0).gameObject;
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var parts = new List<MachineAnimator.Part>();
            void Add(string n, MachineAnimator.Motion m, Vector3 axis, float amount, float phase = 0f)
            {
                var t = Find(root.transform, n);
                if (t == null) { Debug.LogWarning($"[SceneBuilder] {model}: part {n} missing"); return; }
                parts.Add(new MachineAnimator.Part { transform = t, motion = m, axis = axis, amount = amount, phase = phase });
            }
            Color chip = new Color(0.9f, 0.75f, 0.5f);
            if (id == "chopper")
            {
                Add("Arm", MachineAnimator.Motion.Swing, Vector3.right, 22f, 0.45f);
                Add("Blade", MachineAnimator.Motion.Spin, Vector3.right, 900f);
                if (blades > 1) Add("Blade2", MachineAnimator.Motion.Spin, Vector3.right, 900f);
                if (blades > 2) Add("Blade3", MachineAnimator.Motion.Spin, Vector3.right, 900f);
                Add("Log", MachineAnimator.Motion.Slide, Vector3.right, 0.35f);
                var anim = root.AddComponent<MachineAnimator>();
                anim.Configure(id, parts.ToArray(), Find(root.transform, "WorkPoint"), chip, "chopper_loop", "machine_cut", 3f);
            }
            else if (id == "workshop")
            {
                Add("Spindle", MachineAnimator.Motion.Spin, Vector3.right, 720f);
                Add("Wheel", MachineAnimator.Motion.Spin, Vector3.right, 540f);
                Add("Board", MachineAnimator.Motion.Slide, Vector3.forward, -0.6f);
                Add("Hammer", MachineAnimator.Motion.Swing, Vector3.right, -50f, 0.3f);
                var anim = root.AddComponent<MachineAnimator>();
                anim.Configure(id, parts.ToArray(), Find(root.transform, "WorkPoint"), new Color(0.93f, 0.8f, 0.58f), "sawmill_loop", "machine_cut", 2.2f);
            }
            else if (id == "builder")
            {
                Add("Trolley", MachineAnimator.Motion.Bob, Vector3.right, 1.6f);
                Add("Hook", MachineAnimator.Motion.Bob, Vector3.up, 0.35f, 0.25f);
                var anim = root.AddComponent<MachineAnimator>();
                anim.Configure(id, parts.ToArray(), Find(root.transform, "WorkPoint"), new Color(0.8f, 0.6f, 0.4f), "chopper_loop", "build", 4f);
            }
            else
            {
                Add("Blade", MachineAnimator.Motion.Spin, Vector3.right, 1000f);
                for (int i = 0; i < 6; i++) Add("Roller_" + i, MachineAnimator.Motion.Spin, Vector3.right, 360f);
                Add("Flywheel", MachineAnimator.Motion.Spin, Vector3.right, 300f);
                Add("Log", MachineAnimator.Motion.Slide, Vector3.forward, 1.3f);
                Add("Plank", MachineAnimator.Motion.Slide, Vector3.forward, 1.0f);
                var anim = root.AddComponent<MachineAnimator>();
                anim.Configure(id, parts.ToArray(), Find(root.transform, "WorkPoint"), new Color(0.95f, 0.85f, 0.6f), "sawmill_loop", "machine_cut", 2.5f);
            }
            return Save(root, model);
        }

        static int MachineYardBuild(Transform parent, Dictionary<string, GameObject> prefabs)
        {
            var go = new GameObject("MachineYard");
            go.transform.SetParent(parent);
            var yard = go.AddComponent<MachineYard>();
            var chopperSlots = new List<Transform>();
            foreach (var z in new[] { -22f, -31f })
                foreach (var x in new[] { -17f, 17f, -24f, 24f })
                    chopperSlots.Add(Slot(go.transform, "ChopperSlot", x, z, 0f));
            // Order: fill near the middle first, front row before back row.
            chopperSlots = chopperSlots.OrderBy(t => t.position.z < -26f ? 1 : 0).ThenBy(t => Mathf.Abs(t.position.x)).ThenBy(t => t.position.x).ToList();
            var sawSlots = new List<Transform>();
            foreach (var x in new[] { 0f, -7.5f, 7.5f })
                sawSlots.Add(Slot(go.transform, "SawmillSlot", x, -32f, 90f));
            // Carpentry quarter on the west side and the builders' yard on the east, both facing the middle.
            var shopSlots = new List<Transform>();
            foreach (var z in new[] { -13f, -22.5f, -32f })
                shopSlots.Add(Slot(go.transform, "WorkshopSlot", -40f, z, 90f));
            var builderSlots = new List<Transform>();
            foreach (var z in new[] { -14f, -25f, -36f })
            {
                builderSlots.Add(Slot(go.transform, "BuilderSlot", 40.5f, z, -90f));
                Block(40.5f, z, 4.6f);
            }

            var so = new SerializedObject(yard);
            var groups = so.FindProperty("m_Groups");
            groups.arraySize = 4;
            Group(groups.GetArrayElementAtIndex(0), "chopper", chopperSlots, new[] { prefabs["AutoChopper"], prefabs["AutoChopper_Mk2"], prefabs["AutoChopper_Mk3"] });
            Group(groups.GetArrayElementAtIndex(1), "sawmill", sawSlots, new[] { prefabs["Sawmill"] });
            Group(groups.GetArrayElementAtIndex(2), "workshop", shopSlots, new[] { prefabs["Workshop"] });
            Group(groups.GetArrayElementAtIndex(3), "builder", builderSlots, new[] { prefabs["CabinBuilder"] });
            so.FindProperty("m_SlotMarkerPrefab").objectReferenceValue = prefabs["SlotMarker"];
            so.ApplyModifiedPropertiesWithoutUndo();
            return chopperSlots.Count + sawSlots.Count + shopSlots.Count + builderSlots.Count;
        }

        static Transform Slot(Transform parent, string name, float x, float z, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent);
            t.SetPositionAndRotation(WorldLayout.OnGround(x, z), Quaternion.Euler(0f, yaw, 0f));
            Block(x, z, 4f);
            return t;
        }

        static void Group(SerializedProperty g, string id, List<Transform> slots, GameObject[] tiers)
        {
            g.FindPropertyRelative("machineId").stringValue = id;
            var s = g.FindPropertyRelative("slots");
            s.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++) s.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            var p = g.FindPropertyRelative("tierPrefabs");
            p.arraySize = tiers.Length;
            for (int i = 0; i < tiers.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = tiers[i];
        }

        static void StockPile(Transform parent, string name, string model, string resource, Vector3 pos, float yaw, int count, float firstAt, float growth, float step, float rowShrink)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent);
            root.SetPositionAndRotation(WorldLayout.OnGround(pos.x, pos.z), Quaternion.Euler(0f, yaw, 0f));
            var pieces = new List<Transform>();
            // Pyramid stack: rows of logs, each row one shorter and resting in the gaps below.
            int row = 0, inRow = 6;
            float height = model == "LogPiece" ? 0.17f : 0.03f;
            while (pieces.Count < count && inRow > 0)
            {
                for (int i = 0; i < inRow && pieces.Count < count; i++)
                {
                    float off = (i - (inRow - 1) * 0.5f) * step;
                    var p = model == "LogPiece"
                        ? Model(model, root, Vector3.zero, Quaternion.identity, Vector3.one * 1.3f).transform
                        : Model(model, root, Vector3.zero, Quaternion.identity, Vector3.one).transform;
                    p.localPosition = model == "LogPiece" ? new Vector3(0f, height + row * 0.36f * rowShrink * 1.05f, off) : new Vector3(0f, row * 0.13f, 0f);
                    p.localRotation = model == "LogPiece" ? Quaternion.identity : Quaternion.Euler(0f, row * 7f, 0f);
                    pieces.Add(p);
                }
                row++;
                if (model == "LogPiece") inRow--;
            }
            var display = root.gameObject.AddComponent<StockPileDisplay>();
            display.Configure(resource, pieces.ToArray(), firstAt, growth);
            Block(pos.x, pos.z, 2f);
        }

        /// <summary>Pallets of finished goods (3 x 2 per layer, two layers) that fill up with the stock.</summary>
        static void GoodsPile(Transform parent, string name, string model, string resource, float x, float z, float yaw, Vector2 spacing, float layerHeight, float firstAt, float growth)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent);
            root.SetPositionAndRotation(WorldLayout.OnGround(x, z), Quaternion.Euler(0f, yaw, 0f));
            var pieces = new List<Transform>();
            for (int layer = 0; layer < 2; layer++)
                for (int row = 0; row < 2; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        var p = Model(model, root, Vector3.zero, Quaternion.identity, Vector3.one).transform;
                        p.localPosition = new Vector3((col - 1) * spacing.x, layer * layerHeight, (row - 0.5f) * spacing.y);
                        p.localRotation = Quaternion.Euler(0f, (col * 7 + row * 13 + layer * 5) % 9 - 4f, 0f);
                        pieces.Add(p);
                    }
            root.gameObject.AddComponent<StockPileDisplay>().Configure(resource, pieces.ToArray(), firstAt, growth);
            Block(x, z, 3f);
        }

        // ------------------------------------------------------------------ world

        static void GateAndFence(Transform parent, float z, ForestAreaDef area)
        {
            var gate = Place("Gate", parent, 0f, z, 180f);
            gate.name = "Gate_" + area.id;
            var barrierGo = new GameObject("Barrier");
            barrierGo.transform.SetParent(gate.transform, false);
            barrierGo.layer = GameLayers.Interactable;
            var barrier = barrierGo.AddComponent<BoxCollider>();
            barrier.center = new Vector3(0f, 2f, 0f);
            barrier.size = new Vector3(4.4f, 4f, 0.6f);
            var ag = gate.AddComponent<AreaGate>();
            Set(ag, "m_Area", area);
            Set(ag, "m_LeftDoor", Find(gate.transform, "Door_L"));
            Set(ag, "m_RightDoor", Find(gate.transform, "Door_R"));
            Set(ag, "m_Barrier", barrier);
            Set(ag, "m_Sign", SignText(Find(gate.transform, "SignAnchor"), gate.transform, area.displayName, 2.6f, 0.55f));
            Block(0f, z, 4f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (float x = 4.4f; x < WorldLayout.WallX + 2f; x += 4f)
                {
                    var f = Place("Fence_Segment", parent, side * x, z, 0f);
                    SetStatic(f);
                }
                // The fence itself is only 1.6 m tall: an invisible wall along each side stops the
                // player jumping over it (from flat ground or from a hill next to it).
                var wall = new GameObject("FenceWall").AddComponent<BoxCollider>();
                wall.transform.SetParent(parent);
                float x0 = 2.4f, x1 = WorldLayout.WallX + 1f;
                wall.transform.position = new Vector3(side * (x0 + x1) * 0.5f, 10f, z);
                wall.size = new Vector3(x1 - x0, 40f, 0.6f);
            }
            // Keep a corridor along the fence free of trees so it reads as a clear border.
            for (float x = -54f; x <= 54f; x += 6f) Block(x, z, 2.5f);
        }

        static void Bounds(Transform parent)
        {
            var root = new GameObject("WorldBounds").transform;
            root.SetParent(parent);
            void Wall(Vector3 c, Vector3 size)
            {
                var w = new GameObject("Wall").AddComponent<BoxCollider>();
                w.transform.SetParent(root);
                w.transform.position = c;
                w.size = size;
            }
            float len = WorldLayout.WallNorth - WorldLayout.WallSouth;
            float midZ = (WorldLayout.WallNorth + WorldLayout.WallSouth) / 2f;
            Wall(new Vector3(-WorldLayout.WallX - 0.5f, 10f, midZ), new Vector3(1f, 40f, len + 4f));
            Wall(new Vector3(WorldLayout.WallX + 0.5f, 10f, midZ), new Vector3(1f, 40f, len + 4f));
            Wall(new Vector3(0f, 10f, WorldLayout.WallSouth - 0.5f), new Vector3(WorldLayout.WallX * 2f + 4f, 40f, 1f));
            Wall(new Vector3(0f, 10f, WorldLayout.WallNorth + 0.5f), new Vector3(WorldLayout.WallX * 2f + 4f, 40f, 1f));
        }

        /// <summary>Scatters an area's trees with a minimum spacing (dart throwing), avoiding paths and structures.</summary>
        static int Forest(Transform parent, ForestAreaDef area, float x0, float x1, float z0, float z1, float spacing, int max, string guaranteeId = null, int guaranteeCount = 0)
        {
            var root = Find(parent, area.id) ?? new GameObject(area.id).transform;
            root.SetParent(parent);
            var placed = new List<Vector2>();
            int guaranteed = 0;
            for (int attempt = 0; attempt < max * 40 && placed.Count < max; attempt++)
            {
                float x = R(x0, x1), z = R(z0, z1);
                if (WorldLayout.DistToPath(x, z) < 3.2f || IsBlocked(x, z, 1.5f) || WorldLayout.DistToYard(x, z) < 1.5f && z < -6f && Mathf.Abs(x) < 33f) continue;
                var p = new Vector2(x, z);
                bool near = false;
                foreach (var q in placed) if ((q - p).sqrMagnitude < spacing * spacing) { near = true; break; }
                if (near) continue;
                TreeType type;
                if (guaranteeId != null && guaranteed < guaranteeCount && z > (z0 + z1) * 0.5f)
                {
                    type = area.trees.First(t => t.id == guaranteeId);
                    guaranteed++;
                }
                else type = Pick(area);
                // Big trees need more room.
                if (type.trunkRadius > 0.7f)
                {
                    bool crowded = false;
                    foreach (var q in placed) if ((q - p).sqrMagnitude < spacing * spacing * 1.7f) { crowded = true; break; }
                    if (crowded) continue;
                }
                placed.Add(p);
                Tree(root, type, x, z, placed.Count);
                Block(x, z, type.trunkRadius + 0.6f);
            }
            return placed.Count;
        }

        static TreeType Pick(ForestAreaDef area)
        {
            float total = area.weights.Sum();
            float r = R(0f, total);
            for (int i = 0; i < area.trees.Length; i++)
            {
                r -= area.weights[i];
                if (r <= 0f) return area.trees[i];
            }
            return area.trees[0];
        }

        static void Tree(Transform parent, TreeType type, float x, float z, int index)
        {
            float s = R(type.scaleRange.x, type.scaleRange.y);
            var root = new GameObject($"{type.displayName}_{index}");
            root.transform.SetParent(parent);
            root.transform.SetPositionAndRotation(WorldLayout.OnGround(x, z, 0.05f), Quaternion.Euler(0f, R(0f, 360f), 0f));
            float r = type.trunkRadius * s, notch = type.notchHeight * s, len = type.trunkHeight * s;

            var trunk = root.AddComponent<CapsuleCollider>();
            trunk.radius = r * 1.05f;
            trunk.height = notch + len * 0.8f;
            trunk.center = new Vector3(0f, trunk.height * 0.5f, 0f);

            var stump = Model(type.stumpModel, root.transform, root.transform.position, root.transform.rotation, new Vector3(r, notch, r), "Stump");
            var stumpCol = new GameObject("StumpCollider").AddComponent<BoxCollider>();
            stumpCol.transform.SetParent(root.transform, false);
            stumpCol.size = new Vector3(r * 1.7f, notch, r * 1.7f);
            stumpCol.center = new Vector3(0f, notch * 0.5f, 0f);

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(0f, notch, 0f);
            Model(type.modelName, hinge, hinge.position, hinge.rotation, Vector3.one * s, "Model");

            var notchGo = Model("CutNotch", root.transform, root.transform.position + Vector3.up * (notch + r * 0.25f), root.transform.rotation, Vector3.one, "Notch");
            NoShadows(notchGo);
            NoShadows(stump);
            foreach (var rend in stump.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = ShadowCastingMode.On;

            var tree = root.AddComponent<ChoppableTree>();
            tree.Setup(type, hinge, notchGo.transform, trunk, s);
            SetLayer(root, GameLayers.Tree);
        }

        /// <summary>Decorative (non-choppable) forest beyond the walls, plus hills and mountains on the horizon.</summary>
        static void Backdrop(Transform parent, GameDatabase db)
        {
            var root = new GameObject("Backdrop").transform;
            root.SetParent(parent);
            string[] models = { "Tree_Pine", "Tree_Pine", "Tree_Redwood", "Tree_Oak", "Tree_Birch", "Tree_Pine" };
            int n = 0;
            for (int attempt = 0; attempt < 3000 && n < 260; attempt++)
            {
                float x = R(WorldLayout.MinX + 5f, WorldLayout.MaxX - 5f), z = R(WorldLayout.MinZ + 5f, WorldLayout.MaxZ - 5f);
                bool outside = Mathf.Abs(x) > WorldLayout.WallX + 2f || z < WorldLayout.WallSouth - 2f || z > WorldLayout.WallNorth + 2f;
                if (!outside || Mathf.Abs(x) > 86f) continue;
                string m = models[s_Rng.Next(models.Length)];
                if (z > WorldLayout.Gate1Z && z < WorldLayout.Gate2Z && s_Rng.NextDouble() < 0.4) m = "Tree_Maple";
                var t = Place(m, root, x, z, R(0f, 360f), R(1.0f, 1.6f), 0.3f);
                SetStatic(t);
                n++;
            }
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                float x = Mathf.Sin(a) * 190f, z = 72f + Mathf.Cos(a) * 250f;
                var m = Model("Mountain_A", root, new Vector3(x, -6f, z), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(R(1.6f, 2.6f), R(1.0f, 1.8f), R(1.6f, 2.6f)));
                SetStatic(m);
            }
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2f;
                float x = Mathf.Sin(a) * 120f, z = 72f + Mathf.Cos(a) * 175f;
                var h = Model("Hill_A", root, new Vector3(x, -1f, z), Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * R(1.4f, 2.4f));
                SetStatic(h);
            }
        }

        static int Props(Transform parent)
        {
            var root = new GameObject("Props").transform;
            root.SetParent(parent);
            int n = 0;
            (string model, int count, float minScale, float maxScale, bool collider, float pad)[] table =
            {
                ("Grass_A", 1100, 0.8f, 1.6f, false, 0.2f),
                ("Flower_A", 160, 0.9f, 1.3f, false, 0.3f),
                ("Flower_B", 120, 0.9f, 1.3f, false, 0.3f),
                ("Bush_A", 70, 0.8f, 1.4f, false, 1.0f),
                ("Bush_Berry", 30, 0.8f, 1.2f, false, 1.0f),
                ("Rock_A", 60, 0.7f, 1.4f, false, 0.6f),
                ("Rock_B", 30, 0.8f, 1.3f, true, 1.2f),
                ("Rock_C", 10, 0.8f, 1.2f, true, 2.0f),
                ("Mushrooms", 50, 0.8f, 1.5f, false, 0.3f),
                ("Branch_Fallen", 35, 0.8f, 1.3f, false, 0.6f),
                ("Log_Fallen", 12, 0.9f, 1.2f, true, 2.5f),
            };
            foreach (var row in table)
            {
                int placed = 0;
                for (int attempt = 0; attempt < row.count * 20 && placed < row.count; attempt++)
                {
                    float x = R(-WorldLayout.WallX + 1f, WorldLayout.WallX - 1f), z = R(WorldLayout.WallSouth + 1f, WorldLayout.WallNorth - 1f);
                    bool small = !row.collider;
                    if (WorldLayout.DistToPath(x, z) < (small ? 1.6f : 3f)) continue;
                    if (small ? IsBlockedStructure(x, z, row.pad) : IsBlocked(x, z, row.pad)) continue;
                    if (!small && WorldLayout.DistToYard(x, z) < 2f) continue;
                    var go = Place(row.model, root, x, z, R(0f, 360f), R(row.minScale, row.maxScale), 0.03f);
                    SetStatic(go);
                    // Rocks and fallen logs get convex mesh colliders that follow their shape
                    // (a box from the world bounds didn't match rotated props, so you could walk into them).
                    bool solid = row.collider || row.model.StartsWith("Rock_");
                    if (solid)
                        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                        {
                            if (mf.sharedMesh == null) continue;
                            var mc = mf.gameObject.AddComponent<MeshCollider>();
                            mc.sharedMesh = mf.sharedMesh;
                            mc.convex = true;
                        }
                    if (row.collider)
                    {
                        var b = new Bounds(go.transform.position, Vector3.zero);
                        foreach (var rend in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(rend.bounds);
                        Block(x, z, Mathf.Max(b.extents.x, b.extents.z));
                    }
                    else NoShadows(go);
                    placed++;
                    n++;
                }
            }
            return n;
        }

        /// <summary>Small props may sit between trees but not on buildings, machine slots or gates.</summary>
        static bool IsBlockedStructure(float x, float z, float pad)
        {
            foreach (var b in s_Blocked)
            {
                if (b.z < 2f) continue; // tree trunks: grass around them is fine
                float dx = x - b.x, dz = z - b.y;
                if (dx * dx + dz * dz < (b.z + pad) * (b.z + pad)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ ground

        static void Ground(Transform parent)
        {
            // 1 m grid: smoother hills and paths than the old 1.5 m one.
            const float step = 1.0f;
            int nx = Mathf.CeilToInt((WorldLayout.MaxX - WorldLayout.MinX) / step) + 1;
            int nz = Mathf.CeilToInt((WorldLayout.MaxZ - WorldLayout.MinZ) / step) + 1;
            var verts = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = WorldLayout.MinX + i * step, z = WorldLayout.MinZ + j * step;
                verts[j * nx + i] = new Vector3(x, WorldLayout.Height(x, z), z);
                uvs[j * nx + i] = new Vector2((x - WorldLayout.MinX) / (WorldLayout.MaxX - WorldLayout.MinX), (z - WorldLayout.MinZ) / (WorldLayout.MaxZ - WorldLayout.MinZ));
            }
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            int k = 0;
            for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                tris[k++] = a; tris[k++] = c; tris[k++] = b;
                tris[k++] = b; tris[k++] = c; tris[k++] = d;
            }
            var mesh = new Mesh { name = "Ground", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string meshPath = $"{TexDir}/Ground.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.transform.SetParent(parent);
            go.layer = GameLayers.Ground;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshCollider>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = GroundMaterial();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>
        /// Ground material: a painted colour map over the whole world (grass, dry patches, needles,
        /// autumn leaves, moss, rock on hills, dirt paths and the yard) at about 17 px per metre,
        /// plus tiling detail maps (a fine grass-and-soil pattern and its normal map, repeating
        /// every 2.5 m) so the ground still has texture right under the player's feet.
        /// </summary>
        static Material GroundMaterial()
        {
            const int W = 2048, H = 3488;
            var px = new Color32[W * H];
            Color grassA = new Color(0.36f, 0.55f, 0.23f), grassB = new Color(0.27f, 0.46f, 0.20f), dry = new Color(0.55f, 0.56f, 0.28f);
            Color dirt = new Color(0.50f, 0.39f, 0.26f), dirtDark = new Color(0.38f, 0.29f, 0.19f), needles = new Color(0.42f, 0.33f, 0.20f);
            Color autumn = new Color(0.62f, 0.42f, 0.20f), moss = new Color(0.25f, 0.42f, 0.36f), rock = new Color(0.45f, 0.45f, 0.42f);
            var rnd = new System.Random(7);
            for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                float x = Mathf.Lerp(WorldLayout.MinX, WorldLayout.MaxX, (i + 0.5f) / W);
                float z = Mathf.Lerp(WorldLayout.MinZ, WorldLayout.MaxZ, (j + 0.5f) / H);
                float n1 = Mathf.PerlinNoise(x * 0.06f, z * 0.06f);
                float n2 = Mathf.PerlinNoise(x * 0.25f + 40f, z * 0.25f + 13f);
                float n3 = Mathf.PerlinNoise(x * 1.1f + 5f, z * 1.1f + 9f);
                Color c = Color.Lerp(grassA, grassB, n1);
                c = Color.Lerp(c, dry, Mathf.Clamp01((n2 - 0.6f) * 2.5f) * 0.6f);
                int area = WorldLayout.AreaAt(x, z);
                if (area == 0) c = Color.Lerp(c, needles, Mathf.Clamp01((n2 - 0.55f) * 3f) * 0.45f);
                if (area == 1 || (area == -1 && z > WorldLayout.Gate1Z && z < WorldLayout.Gate2Z)) c = Color.Lerp(c, autumn, Mathf.Clamp01(n2 * 1.3f - 0.35f) * 0.5f);
                if (area == 2 || (area == -1 && z >= WorldLayout.Gate2Z)) c = Color.Lerp(c, moss, 0.35f + n1 * 0.3f);
                float h = WorldLayout.Height(x, z);
                if (h > 6f) c = Color.Lerp(c, rock, Mathf.Clamp01((h - 6f) / 8f) * (0.4f + n2 * 0.6f));
                // Packed earth in the yard, worn dirt paths elsewhere.
                float yard = 1f - Mathf.Clamp01(WorldLayout.DistToYard(x, z) / 3f);
                float path = 1f - Mathf.Clamp01((WorldLayout.DistToPath(x, z) - 1.2f + (n3 - 0.5f) * 0.9f) / 1.0f);
                float d = Mathf.Max(yard * (0.55f + n2 * 0.35f), path * 0.92f);
                c = Color.Lerp(c, Color.Lerp(dirt, dirtDark, n3 * 0.7f), d);
                float speck = (float)rnd.NextDouble();
                c *= 0.94f + speck * 0.1f;
                c.a = 1f;
                px[j * W + i] = c;
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true);
            tex.SetPixels32(px);
            tex.Apply();
            string path0 = $"{TexDir}/Ground.png";
            File.WriteAllBytes(path0, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path0);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path0);
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 4096;
            imp.anisoLevel = 8;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            var (detail, detailNormal) = GroundDetailMaps();

            string matPath = $"{MaterialLibrary.Dir}/M_Ground.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, matPath);
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path0));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.05f);
            // URP Lit detail inputs: x2 multiply albedo (0.5 = unchanged) and a detail normal map.
            const float tile = 2.5f;
            var tiling = new Vector2((WorldLayout.MaxX - WorldLayout.MinX) / tile, (WorldLayout.MaxZ - WorldLayout.MinZ) / tile);
            m.SetTexture("_DetailAlbedoMap", detail);
            m.SetTexture("_DetailNormalMap", detailNormal);
            m.SetTextureScale("_DetailAlbedoMap", tiling);
            m.SetFloat("_DetailAlbedoMapScale", 1f);
            m.SetFloat("_DetailNormalMapScale", 0.9f);
            m.EnableKeyword("_DETAIL_MULX2");
            m.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Tileable 512 px detail maps for the ground: albedo around mid-grey (x2 multiply)
        /// with grass blades, soil grain and pebbles, and a normal map from the same height field.</summary>
        static (Texture2D albedo, Texture2D normal) GroundDetailMaps()
        {
            const int N = 512;
            var height = new float[N * N];
            var rng = new System.Random(11);
            // Tileable noise: blend the field with copies shifted by one tile.
            float Tile(float x, float y, float scale, float ox, float oy)
            {
                float T = N;
                float u = x / T, v = y / T;
                float a = Mathf.PerlinNoise((x) * scale + ox, (y) * scale + oy);
                float b = Mathf.PerlinNoise((x - T) * scale + ox, (y) * scale + oy);
                float c = Mathf.PerlinNoise((x) * scale + ox, (y - T) * scale + oy);
                float d = Mathf.PerlinNoise((x - T) * scale + ox, (y - T) * scale + oy);
                return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
            }
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float h = Tile(x, y, 0.012f, 3f, 7f) * 0.45f + Tile(x, y, 0.045f, 11f, 2f) * 0.35f + Tile(x, y, 0.16f, 5f, 19f) * 0.2f;
                height[y * N + x] = h;
            }
            // Grass blades: short bright strokes; pebbles: small dark bumps.
            for (int i = 0; i < 2600; i++)
            {
                int cx = rng.Next(N), cy = rng.Next(N), len = 4 + rng.Next(9);
                float dx = (float)(rng.NextDouble() - 0.5) * 0.7f;
                for (int s = 0; s < len; s++)
                {
                    int px = ((cx + Mathf.RoundToInt(dx * s)) % N + N) % N, py = ((cy + s) % N + N) % N;
                    height[py * N + px] += 0.08f * (1f - s / (float)len);
                }
            }
            for (int i = 0; i < 160; i++)
            {
                int cx = rng.Next(N), cy = rng.Next(N), r = 2 + rng.Next(4);
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    float d = Mathf.Sqrt(ox * ox + oy * oy) / r;
                    if (d > 1f) continue;
                    int px = ((cx + ox) % N + N) % N, py = ((cy + oy) % N + N) % N;
                    height[py * N + px] += 0.12f * Mathf.Sqrt(1f - d * d);
                }
            }
            var albedo = new Color32[N * N];
            var normal = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float h = height[y * N + x];
                float hx = height[y * N + (x + 1) % N] - height[y * N + (x - 1 + N) % N];
                float hy = height[((y + 1) % N) * N + x] - height[((y - 1 + N) % N) * N + x];
                var n = new Vector3(-hx * 6f, -hy * 6f, 1f).normalized;
                normal[y * N + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255);
                // Brighter on raised blades, darker in the hollows; centred on 0.5 (no tint change).
                byte g = (byte)(Mathf.Clamp01(0.5f + (h - 0.5f) * 0.55f + (float)(rng.NextDouble() - 0.5) * 0.04f) * 255);
                albedo[y * N + x] = new Color32(g, g, g, 255);
            }
            Texture2D Save(Color32[] px, string name, bool isNormal)
            {
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
                tex.SetPixels32(px);
                tex.Apply();
                string path = $"{TexDir}/{name}.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.anisoLevel = 8;
                imp.sRGBTexture = !isNormal;
                if (isNormal) imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return (Save(albedo, "GroundDetail", false), Save(normal, "GroundDetail_N", true));
        }

        // ------------------------------------------------------------------ lighting

        static void Lighting(Transform parent)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(parent);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.80f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(42f, -38f, 0f);
            RenderSettings.sun = sun;

            var sky = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialLibrary.Dir}/M_Sky.mat");
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, $"{MaterialLibrary.Dir}/M_Sky.mat");
            }
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", new Color(0.52f, 0.62f, 0.78f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.45f, 0.38f));
            sky.SetFloat("_Exposure", 1.2f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.70f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.27f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 330f;
            RenderSettings.fogColor = new Color(0.70f, 0.78f, 0.84f);

            var volGo = new GameObject("PostProcessing");
            volGo.transform.SetParent(parent);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            string profilePath = ProjectSetup.Root + "/Data/PostProfile.asset";
            AssetDatabase.DeleteAsset(profilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.6f);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.25f);
            ca.contrast.Override(8f);
            ca.saturation.Override(12f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.45f);
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            foreach (var c in profile.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            vol.sharedProfile = profile;
        }

        static LightingSettings LightingAsset()
        {
            string p = ProjectSetup.Root + "/Data/Lighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(p);
            if (ls == null)
            {
                ls = new LightingSettings { name = "Lighting" };
                AssetDatabase.CreateAsset(ls, p);
            }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            EditorUtility.SetDirty(ls);
            return ls;
        }

        // ------------------------------------------------------------------ player

        static GameObject PlayerRig(Transform spawn, GameDatabase db, out AxeController axe, out Interactor interactor, out GameObject cameraGo)
        {
            var player = new GameObject("Player");
            player.layer = GameLayers.Player;
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.38f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.05f;
            var motor = player.AddComponent<PlayerMotor>();

            var rig = new GameObject("CameraRig").transform;
            rig.SetParent(player.transform, false);
            rig.localPosition = new Vector3(0f, 1.62f, 0f);
            cameraGo = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(rig, false);
            var cam = cameraGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 700f;
            cam.fieldOfView = 72f;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            cameraGo.AddComponent<CameraGraphics>();
            var fx = cameraGo.AddComponent<CameraFx>();
            Set(fx, "m_Motor", motor);

            var holder = new GameObject("AxeHolder").transform;
            holder.SetParent(cameraGo.transform, false);
            holder.localPosition = new Vector3(0.36f, -0.38f, 0.56f);
            var tiers = new List<GameObject>();
            for (int i = 0; i < db.config.axeModels.Length; i++)
            {
                var m = Model(db.config.axeModels[i], holder, holder.position, holder.rotation, Vector3.one * 0.8f, "Axe_" + i);
                NoShadows(m);
                m.SetActive(i == 0);
                tiers.Add(m);
            }

            var look = player.AddComponent<PlayerLook>();
            Set(look, "m_Pitch", rig);
            interactor = player.AddComponent<Interactor>();
            Set(interactor, "m_Camera", cameraGo.transform);
            axe = player.AddComponent<AxeController>();
            Set(axe, "m_Camera", cameraGo.transform);
            Set(axe, "m_Holder", holder);
            Set(axe, "m_TierModels", tiers.Cast<Object>().ToArray());
            return player;
        }
    }
}
