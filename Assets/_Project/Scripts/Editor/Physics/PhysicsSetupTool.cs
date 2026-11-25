using FailSafeZero.AI.Gravity;
using FailSafeZero.Core;
using FailSafeZero.Interactions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace FailSafeZero.EditorTools
{
    public static class Student2SetupTool
    {
        const string ToolsFolder = "Assets/_Project/Prefabs/Tools";
        const string BotsFolder = "Assets/_Project/Prefabs/Bots";
        const string MatFolder = "Assets/_Project/Prefabs/Tools/Materials";
        const string PlayerPrefab = "Assets/_Project/Prefabs/Player/FirstPersonPlayer.prefab";
        const string TestScenePath = "Assets/_Project/Scenes/Student2_PhysicsTest.unity";

    

        [MenuItem("FailSafeZero/Student 2/1. Build All Prefabs", priority = 1)]
        public static void BuildAllPrefabs()
        {
            EnsureFolder("Assets/_Project/Prefabs", "Tools");
            EnsureFolder(ToolsFolder, "Materials");
            EnsureFolder("Assets/_Project/Prefabs", "Bots");

            Save(BuildDoubleDoor(), ToolsFolder + "/Door_Double.prefab");
            Save(BuildBarricade(), ToolsFolder + "/Barricade_Heavy.prefab");
            Save(BuildCrate(), ToolsFolder + "/Crate_Throwable.prefab");
            Save(BuildCanister(), ToolsFolder + "/Canister_Throwable.prefab");
            Save(BuildFallingWeightTrap(), ToolsFolder + "/Trap_FallingWeight.prefab");
            Save(BuildPendulumTrap(), ToolsFolder + "/Trap_Pendulum.prefab");
            Save(BuildGravityBot(), BotsFolder + "/GravityBot.prefab");

            AssetDatabase.SaveAssets();
            Debug.Log("[Student 2] Built 7 prefabs in Prefabs/Tools and Prefabs/Bots.");
        }

        [MenuItem("FailSafeZero/Student 2/2. Upgrade Player Prefab (interact, pick up, push)", priority = 2)]
        public static void UpgradePlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                if (root.GetComponent<PlayerPusher>() == null) root.AddComponent<PlayerPusher>();
                Camera cam = root.GetComponentInChildren<Camera>(true);
                if (cam == null) { Debug.LogError("[Student 2] No Camera inside the player prefab."); return; }
                if (cam.GetComponent<PickupThrow>() == null) cam.gameObject.AddComponent<PickupThrow>();
                if (cam.GetComponent<PlayerInteractor>() == null) cam.gameObject.AddComponent<PlayerInteractor>();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                Debug.Log("[Student 2] Player prefab upgraded: PlayerPusher on the body, PickupThrow + PlayerInteractor on the camera.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("FailSafeZero/Student 2/3. Create Physics Test Scene", priority = 3)]
        public static void CreateTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ToolsFolder + "/Door_Double.prefab") == null) BuildAllPrefabs();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Object.DestroyImmediate(GameObject.Find("Main Camera"));   

            // Static level: floor, outer walls, a wall with a doorway, a cover block.
            var level = new GameObject("Level");
            Material floorMat = Mat("Test_Floor", new Color(0.32f, 0.33f, 0.35f), 0.4f);
            Material wallMat = Mat("Test_Wall", new Color(0.55f, 0.56f, 0.58f), 0.2f);
            Box(level.transform, "Floor", new Vector3(0, -0.25f, 0), new Vector3(40, 0.5f, 40), floorMat);
            Box(level.transform, "Wall_N", new Vector3(0, 3, 20), new Vector3(40, 6, 0.5f), wallMat);
            Box(level.transform, "Wall_S", new Vector3(0, 3, -20), new Vector3(40, 6, 0.5f), wallMat);
            Box(level.transform, "Wall_E", new Vector3(20, 3, 0), new Vector3(0.5f, 6, 40), wallMat);
            Box(level.transform, "Wall_W", new Vector3(-20, 3, 0), new Vector3(0.5f, 6, 40), wallMat);
            Box(level.transform, "DoorWall_L", new Vector3(-11, 3, -6), new Vector3(18, 6, 0.5f), wallMat);
            Box(level.transform, "DoorWall_R", new Vector3(11, 3, -6), new Vector3(18, 6, 0.5f), wallMat);
            Box(level.transform, "DoorWall_Top", new Vector3(0, 5, -6), new Vector3(4, 2, 0.5f), wallMat);
            Box(level.transform, "Cover_Block", new Vector3(-6, 1, 8), new Vector3(2, 2, 2), wallMat);
            foreach (Transform t in level.transform) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            string navPath = "Assets/_Project/Scenes/Student2_PhysicsTest_NavMesh.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);

            // Player in the south room, facing the door.
            var playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (playerAsset != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerAsset);
                player.transform.position = new Vector3(0, 1.1f, -14);
            }

            
            Place("Door_Double", new Vector3(0, 0, -6), 0);
            Place("Barricade_Heavy", new Vector3(4, 0, -11), 0);
            Place("Barricade_Heavy", new Vector3(6, 0, 4), 90);
            Place("Trap_FallingWeight", new Vector3(-6, 0, -12), 0);
            Place("Trap_Pendulum", new Vector3(8, 0, -14), 0);
            for (int i = 0; i < 4; i++) Place("Crate_Throwable", new Vector3(-3 + i * 1.6f, 0.5f, -10), 0);
            for (int i = 0; i < 6; i++) Place(i % 2 == 0 ? "Crate_Throwable" : "Canister_Throwable", new Vector3(-6 + i * 2.4f, 0.6f, 12 - (i % 3)), 0);
            Place("GravityBot", new Vector3(0, 0, 10), 180, BotsFolder);

            EditorSceneManager.SaveScene(scene, TestScenePath);
            AddSceneToBuild(TestScenePath);
            Debug.Log("[Student 2] Test scene saved at " + TestScenePath + ". Press Play!");
        }

        [MenuItem("FailSafeZero/Student 2/4. Place Pieces in Arena 2 (MainLabScene must be open)", priority = 20)]
        public static void PlaceInArena2()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("MainLabScene"))
            {
                EditorUtility.DisplayDialog("Student 2", "Open Assets/_Project/Scenes/MainLabScene first (and make sure no teammate is editing it right now).", "OK");
                return;
            }
            if (GameObject.Find("Student2_Arena2") != null)
            {
                EditorUtility.DisplayDialog("Student 2", "Student2_Arena2 is already in the scene. Delete it first if you want to place everything again.", "OK");
                return;
            }

            // Arena 2 
            var root = new GameObject("Student2_Arena2");
            Undo.RegisterCreatedObjectUndo(root, "Place Student 2 pieces");

            Place("Door_Double", new Vector3(0, 0, 45), 0, ToolsFolder, root.transform);
            Place("Barricade_Heavy", new Vector3(-7, 0, 48), 0, ToolsFolder, root.transform);
            Place("Barricade_Heavy", new Vector3(7, 0, 62), 90, ToolsFolder, root.transform);
            Place("Trap_Pendulum", new Vector3(0, 0, 49.5f), 0, ToolsFolder, root.transform);   
            Place("Trap_FallingWeight", new Vector3(6, 0, 51), 0, ToolsFolder, root.transform);

            Vector3[] props =
            {
                new Vector3(-3, 0.6f, 53), new Vector3(-7, 0.6f, 55), new Vector3(-2, 0.6f, 59),
                new Vector3(3, 0.6f, 55), new Vector3(7, 0.6f, 56), new Vector3(2, 0.6f, 61),
                new Vector3(-6, 0.6f, 60), new Vector3(4, 0.6f, 50),
            };
            for (int i = 0; i < props.Length; i++)
                Place(i % 3 == 2 ? "Canister_Throwable" : "Crate_Throwable", props[i], i * 37f, ToolsFolder, root.transform);

            Place("GravityBot", new Vector3(0, 0, 60), 180, BotsFolder, root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Student 2] Placed door, barricades, traps, 8 props and the Gravity bot in Arena 2. Save the scene (Ctrl+S).");
        }


        static GameObject BuildDoubleDoor()
        {
            Material leafMat = Mat("Door_Metal", new Color(0.36f, 0.4f, 0.45f), 0.65f);
            Material stripeMat = Mat("Hazard_Amber", new Color(1f, 0.55f, 0.05f), 0.3f, emissive: 0.6f);

            var root = new GameObject("Door_Double");
            Door left = BuildLeaf(root.transform, "Hinge_L", new Vector3(-2f, 0, 0), 0f, leafMat, stripeMat);
            Door right = BuildLeaf(root.transform, "Hinge_R", new Vector3(2f, 0, 0), 180f, leafMat, stripeMat);
            SetRef(left, "partner", right);
            SetRef(right, "partner", left);
            return root;
        }

        static Door BuildLeaf(Transform parent, string name, Vector3 pos, float yRot, Material mat, Material stripe)
        {
            var hinge = new GameObject(name);
            hinge.transform.SetParent(parent, false);
            hinge.transform.localPosition = pos;
            hinge.transform.localRotation = Quaternion.Euler(0, yRot, 0);
            var rb = hinge.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            // Leaf extends along the hinge's +X (the Door script relies on this).
            GameObject leaf = Box(hinge.transform, "Leaf", new Vector3(0.99f, 1.9f, 0), new Vector3(1.96f, 3.8f, 0.12f), mat);
            GameObject band = Box(hinge.transform, "Stripe", new Vector3(0.99f, 1.2f, 0), new Vector3(1.9f, 0.18f, 0.14f), stripe);
            Object.DestroyImmediate(band.GetComponent<Collider>());
            GameObject handle = Box(hinge.transform, "Handle", new Vector3(1.75f, 1.1f, 0), new Vector3(0.08f, 0.3f, 0.22f), stripe);
            Object.DestroyImmediate(handle.GetComponent<Collider>());

            // Carving obstacle: a closed door blocks bots' paths; an open one doesn't.
            var obstacle = leaf.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            return hinge.AddComponent<Door>();
        }

        static GameObject BuildBarricade()
        {
            Material mat = Mat("Barricade_Steel", new Color(0.25f, 0.27f, 0.25f), 0.5f);
            Material stripe = Mat("Hazard_Amber", new Color(1f, 0.55f, 0.05f), 0.3f, emissive: 0.6f);
            var root = new GameObject("Barricade_Heavy");
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.75f, 0);
            col.size = new Vector3(2.4f, 1.5f, 0.8f);
            Visual(root.transform, PrimitiveType.Cube, "Body", new Vector3(0, 0.75f, 0), new Vector3(2.4f, 1.5f, 0.8f), mat);
            Visual(root.transform, PrimitiveType.Cube, "Stripe", new Vector3(0, 1.2f, 0), new Vector3(2.42f, 0.2f, 0.82f), stripe);
            root.AddComponent<Rigidbody>();
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = col.center;
            obstacle.size = col.size;
            root.AddComponent<Barricade>();
            return root;
        }

        static GameObject BuildCrate()
        {
            Material mat = Mat("Crate_Lab", new Color(0.55f, 0.5f, 0.38f), 0.25f);
            Material edge = Mat("Crate_Edge", new Color(0.2f, 0.2f, 0.22f), 0.6f);
            var root = new GameObject("Crate_Throwable");
            var col = root.AddComponent<BoxCollider>();
            col.size = Vector3.one * 0.8f;
            Visual(root.transform, PrimitiveType.Cube, "Body", Vector3.zero, Vector3.one * 0.8f, mat);
            Visual(root.transform, PrimitiveType.Cube, "Band", Vector3.zero, new Vector3(0.82f, 0.12f, 0.82f), edge);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 8f;
            root.AddComponent<Throwable>();
            return root;
        }

        static GameObject BuildCanister()
        {
            Material mat = Mat("Canister_Blue", new Color(0.15f, 0.35f, 0.6f), 0.7f);
            Material glow = Mat("Canister_Glow", new Color(0.3f, 0.8f, 1f), 0.5f, emissive: 1.5f);
            var root = new GameObject("Canister_Throwable");
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.25f;
            col.height = 1f;
            Visual(root.transform, PrimitiveType.Cylinder, "Body", Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), mat);
            Visual(root.transform, PrimitiveType.Cylinder, "GlowBand", Vector3.zero, new Vector3(0.52f, 0.06f, 0.52f), glow);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 4f;
            root.AddComponent<Throwable>();
            return root;
        }

        static GameObject BuildFallingWeightTrap()
        {
            Material plateMat = Mat("Hazard_Amber", new Color(1f, 0.55f, 0.05f), 0.3f, emissive: 0.6f);
            Material weightMat = Mat("Weight_Iron", new Color(0.18f, 0.18f, 0.2f), 0.8f);
            var root = new GameObject("Trap_FallingWeight");

            GameObject plate = Box(root.transform, "PressurePlate", new Vector3(0, 0.03f, 0), new Vector3(2f, 0.06f, 2f), plateMat);
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            var trigger = plate.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, 10f, 0);            // tall trigger volume above the thin plate (local scale y = 0.06)
            trigger.size = new Vector3(1f, 20f, 1f);            // => 1.2 m high in world space
            var pressurePlate = plate.AddComponent<PressurePlate>();

            GameObject weight = Box(root.transform, "Weight", new Vector3(0, 4.5f, 0), new Vector3(1.6f, 0.8f, 1.6f), weightMat);
            var rb = weight.AddComponent<Rigidbody>();
            rb.mass = 200f;
            rb.isKinematic = true;
            var trap = weight.AddComponent<FallingWeightTrap>();

            UnityEventTools.AddPersistentListener(pressurePlate.OnPressed, new UnityAction(trap.Drop));
            return root;
        }

        static GameObject BuildPendulumTrap()
        {
            Material metal = Mat("Weight_Iron", new Color(0.18f, 0.18f, 0.2f), 0.8f);
            Material amber = Mat("Hazard_Amber", new Color(1f, 0.55f, 0.05f), 0.3f, emissive: 0.6f);
            var root = new GameObject("Trap_Pendulum");
            Box(root.transform, "Mount", new Vector3(0, 6.3f, 0), new Vector3(0.6f, 0.3f, 0.6f), metal);

            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0, 6.1f, 0);
            var rb = pivot.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            GameObject arm = Box(pivot.transform, "Arm", new Vector3(0, -2.3f, 0), new Vector3(0.12f, 4.6f, 0.12f), metal);
            Object.DestroyImmediate(arm.GetComponent<Collider>());
            GameObject head = Box(pivot.transform, "Head", new Vector3(0, -4.9f, 0), new Vector3(1.4f, 0.7f, 0.7f), amber);
            // Trigger slightly bigger than the solid head: CharacterControllers don't get
            // collision callbacks from kinematic bodies, but triggers always fire.
            var hitZone = head.AddComponent<BoxCollider>();
            hitZone.isTrigger = true;
            hitZone.size = new Vector3(1.25f, 1.4f, 1.6f);
            pivot.AddComponent<PendulumTrap>();
            return root;
        }

        static GameObject BuildGravityBot()
        {
            Material shell = Mat("GravityBot_Shell", new Color(0.16f, 0.17f, 0.2f), 0.75f);
            Material trim = Mat("Hazard_Amber", new Color(1f, 0.55f, 0.05f), 0.3f, emissive: 0.6f);
            Material core = Mat("GravityBot_Core", new Color(0.3f, 0.6f, 1f), 0.9f, emissive: 2.5f);

            var root = new GameObject("GravityBot");
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1.2f, 0);
            col.radius = 0.6f;
            col.height = 2.2f;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;     // moved by the NavMeshAgent; kinematic so plates/crates still detect it

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.6f;
            agent.height = 2.2f;
            agent.speed = 3.5f;
            agent.angularSpeed = 300f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.8f;

            // Visuals (no colliders: the capsule above is the only collider, which is cheap).
            Visual(root.transform, PrimitiveType.Sphere, "Shell", new Vector3(0, 1.4f, 0), new Vector3(1.2f, 1.0f, 1.2f), shell);
            Visual(root.transform, PrimitiveType.Cylinder, "Base", new Vector3(0, 0.35f, 0), new Vector3(0.5f, 0.35f, 0.5f), shell);
            GameObject ring = Visual(root.transform, PrimitiveType.Cylinder, "Ring", new Vector3(0, 1.4f, 0), new Vector3(1.9f, 0.03f, 1.9f), trim);
            GameObject coreObj = Visual(root.transform, PrimitiveType.Sphere, "Core", new Vector3(0, 1.45f, 0.42f), Vector3.one * 0.45f, core);

            var lightObj = new GameObject("CoreLight");
            lightObj.transform.SetParent(root.transform, false);
            lightObj.transform.localPosition = new Vector3(0, 1.45f, 0.8f);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 2.5f;
            light.color = new Color(0.3f, 0.6f, 1f);
            light.shadows = LightShadows.None;     // a moving shadow-casting light is expensive

            var eye = new GameObject("Eye");
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(0, 1.6f, 0.5f);

            var health = root.AddComponent<EnemyHealth>();
            SetFloat(health, "maxHealth", 150f);
            SetString(health, "healthBarLabel", "GRAVITY BOT");

            root.AddComponent<GravityHands>();
            var bot = root.AddComponent<GravityBot>();
            SetRef(bot, "eye", eye.transform);

            var visuals = root.AddComponent<GravityBotVisuals>();
            SetRef(visuals, "bot", bot);
            SetRef(visuals, "coreLight", light);
            SetRef(visuals, "coreRenderer", coreObj.GetComponent<Renderer>());
            SetRef(visuals, "ring", ring.transform);

            var hints = root.AddComponent<GravityBotHints>();
            SetRef(hints, "bot", bot);
            return root;
        }


        static void Place(string prefabName, Vector3 pos, float yRot, string folder = ToolsFolder, Transform parent = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + prefabName + ".prefab");
            if (asset == null) { Debug.LogError("[Student 2] Missing prefab " + prefabName + ". Run 'Build All Prefabs' first."); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yRot, 0));
            Undo.RegisterCreatedObjectUndo(go, "Place " + prefabName);
        }

        static void Save(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Visual(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Creates (or reuses) a URP Lit material asset so prefabs keep their look.</summary>
        static Material Mat(string name, Color color, float smoothness, float emissive = 0f)
        {
            EnsureFolder("Assets/_Project/Prefabs", "Tools");
            EnsureFolder(ToolsFolder, "Materials");
            string path = MatFolder + "/M_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", smoothness > 0.6f ? 0.8f : 0.1f);
            if (emissive > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emissive);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            mat.enableInstancing = true;   // many crates share one material: GPU instancing draws them together
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == path) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
