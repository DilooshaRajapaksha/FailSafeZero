using System.IO;
using UnityEngine;
using Unity.AI.Navigation;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace FailSafeZero.Environment
{
    /// <summary>
    /// Student 1 - Final Integration, Lab Prop Placement, Texture/Static Optimization, and NavMesh Re-baking.
    /// Covers Oct 17 to Oct 20 milestones in the FailSafeZero Team Roadmap.
    /// </summary>
    public class LabIntegrationAndOptimizer : MonoBehaviour
    {
        [ContextMenu("1. Place Lab Props & Integration Anchors (Commit #13)")]
        public void PlaceLabPropsAndIntegrationSlots()
        {
            Transform root = GameObject.Find("Lab_Environment_Root")?.transform;
            if (root == null) return;

            Transform existing = root.Find("09_Integration_And_Props");
            if (existing != null) DestroyImmediate(existing.gameObject);

            GameObject propsGroup = new GameObject("09_Integration_And_Props");
            propsGroup.transform.SetParent(root);

            Shader urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material crateMat = new Material(urp) { color = new Color(0.2f, 0.32f, 0.4f) };
            Material consoleMat = new Material(urp) { color = new Color(0.1f, 0.12f, 0.15f) };

            // Add sci-fi containment crates & consoles in Arenas 1, 2, 3 (grounded with zero clipping)
            CreateProp(propsGroup.transform, "LabConsole_StartRoom", new Vector3(-3.5f, 0.6f, 3.5f), new Vector3(1.8f, 1.2f, 0.8f), consoleMat);
            CreateProp(propsGroup.transform, "ContainmentCrate_Arena1_A", new Vector3(-7.5f, 0.75f, 22f), new Vector3(1.5f, 1.5f, 1.5f), crateMat);
            CreateProp(propsGroup.transform, "ContainmentCrate_Arena1_B", new Vector3(7.5f, 0.75f, 28f), new Vector3(1.5f, 1.5f, 1.5f), crateMat);
            CreateProp(propsGroup.transform, "GravityAnchorBlock_Arena2_A", new Vector3(-5f, 1.0f, 52f), new Vector3(2f, 2f, 2f), crateMat);
            CreateProp(propsGroup.transform, "GravityAnchorBlock_Arena2_B", new Vector3(5f, 1.0f, 58f), new Vector3(2f, 2f, 2f), crateMat);
            CreateProp(propsGroup.transform, "IllusionObelisk_Arena3_L", new Vector3(-6f, 2.0f, 85f), new Vector3(1.2f, 4f, 1.2f), consoleMat);
            CreateProp(propsGroup.transform, "IllusionObelisk_Arena3_R", new Vector3(6f, 2.0f, 85f), new Vector3(1.2f, 4f, 1.2f), consoleMat);

            // Create clean organization anchors for Student 2 (Doors/Traps) & Student 3 (Blender Models)
            CreateIntegrationAnchor(propsGroup.transform, "Student2_Doors_And_Traps_Anchor", new Vector3(0f, 0f, 5f));
            CreateIntegrationAnchor(propsGroup.transform, "Student3_CustomModels_Anchor", new Vector3(0f, 0f, 85f));
            CreateIntegrationAnchor(propsGroup.transform, "Student4_WardenAgent_Anchor", new Vector3(0f, 0f, 95f));

            SaveSceneChanges();
            Debug.Log("Commit #13 Ready: Lab props and teammate integration anchors placed with zero clipping!");
        }

        [ContextMenu("2. Optimize Static Batching, Textures & Re-bake NavMesh (Commit #14)")]
        public void OptimizeWorldAndRebakeNavMesh()
        {
            Transform root = GameObject.Find("Lab_Environment_Root")?.transform;
            if (root == null) return;

            // 1. Mark all non-moving geometry as Static (Batching, Occlusion, Navigation, Lightmap)
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.isStatic = true;
            }

#if UNITY_EDITOR
            // 2. Compress textures in _Project to max 1024px & Crunch Compression to save GPU memory
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project" });
            foreach (string guid in textureGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.maxTextureSize = 1024;
                    importer.crunchedCompression = true;
                    importer.compressionQuality = 75;
                    importer.SaveAndReimport();
                }
            }
#endif

            // 3. Re-bake NavMesh so bots navigate around all new props and obstacles
            NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
            if (surface == null) surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();

            SaveSceneChanges();
            Debug.Log("Commit #14 Ready: Static batching enabled, textures compressed, and NavMesh re-baked!");
        }

        [ContextMenu("3. Generate Student 1 Design & Viva Documentation (Commit #15)")]
        public void GenerateVivaDocumentation()
        {
            string docFolder = "Assets/_Project/Docs";
#if UNITY_EDITOR
            if (!AssetDatabase.IsValidFolder(docFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Docs");
            }
#endif
            string mdContent = @"# Student 1: World Builder & Zombie Bot - Technical Design Notes

## 1. Level Layout & Scale (1 Unity Unit = 1 Metre)
- **Linear Progression Flow:** Start Room (10x10m) -> Corridor A -> Arena 1: Zombie Cryo Lab (20x20m) -> Corridor B -> Arena 2: High-Bay Gravity Chamber (20x20m, 7m ceiling) -> Corridor C -> Arena 3: Illusion Containment Lab (20x20m) -> Exit Room (8x8m).
- **Tactical Cover:** Arena 1 includes 4 symmetrical pillars and containment crates so the player can break line-of-sight and time movement stops against the Zombie Bot.

## 2. Lighting Moods & Visual Cohesion
- **Corridors:** Dim ambient lighting paired with pulsing red emergency point lights (`EmergencyLightPulse.cs`) to create tension between arenas.
- **Arena 1 (Zombie):** Cold Cyan lighting (clinical cryo-containment atmosphere).
- **Arena 2 (Gravity):** Industrial Amber lighting with a 7m vertical ceiling to accommodate vertical physics traps.
- **Arena 3 (Illusion):** Deep Violet lighting to reinforce perceptual distortion.
- **Exit Room:** Bio-Green lighting to clearly signal the escape objective.

## 3. World & Rendering Optimizations
- **Static Batching:** All non-moving floors, walls, pillars, and props are flagged as `Static`, allowing Unity's Universal Render Pipeline (URP) to batch draw calls and reduce CPU overhead.
- **Texture Compression:** Project textures are capped at a maximum resolution of 1024px with Crunch Compression enabled to minimize VRAM usage.
- **NavMesh Baking:** Baked via `NavMeshSurface` (`CollectObjects.Children`), converting 3D collision geometry into simplified convex polygons for fast path queries.

## 4. Zombie Bot AI Architecture (`ZombieAgentAI.cs`)
- **4-State Finite State Machine (FSM):** Transitions cleanly between `Dormant`, `PursuingMotion`, `InvestigatingTremor`, and `Attacking`.
- **Motion Detection Math:** Computes instantaneous player speed each frame (`Vector3.Distance(currentPos, previousPos) / Time.deltaTime`). When speed drops below `motionVelocityThreshold` (0.12 m/s), the Zombie immediately halts (`navAgent.isStopped = true`, `ResetPath()`).
- **Weighted A* Pathfinding:** Evaluates paths over the baked NavMesh using `f(n) = g(n) + w * h(n)` (`heuristicWeight = 1.45`), prioritizing direct, obstacle-free pursuit while exposing `CurrentPathWaypoints` (`Vector3[]`) for Student 4's shared movement controller.
";
            File.WriteAllText(Path.Combine(docFolder, "Student1_WorldAndZombie_VivaNotes.md"), mdContent);
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            SaveSceneChanges();
            Debug.Log("Commit #15 Ready: Student 1 technical justification markdown generated!");
        }

        private void CreateProp(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = name;
            prop.transform.SetParent(parent);
            prop.transform.position = pos;
            prop.transform.localScale = scale;
            prop.GetComponent<Renderer>().sharedMaterial = mat;
            prop.isStatic = true;
        }

        private void CreateIntegrationAnchor(Transform parent, string name, Vector3 pos)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent);
            anchor.transform.position = pos;
        }

        private void SaveSceneChanges()
        {
#if UNITY_EDITOR
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
#endif
        }
    }
}