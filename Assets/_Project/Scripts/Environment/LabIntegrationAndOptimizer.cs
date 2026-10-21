using System.IO;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace FailSafeZero.Environment
{
    /// <summary>
    /// Student 1 - Final Integration, Lab Prop Placement, Student 4 Maze_Arena Connection, and Optimization.
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

            CreateProp(propsGroup.transform, "LabConsole_StartRoom", new Vector3(-3.5f, 0.6f, 3.5f), new Vector3(1.8f, 1.2f, 0.8f), consoleMat);
            CreateProp(propsGroup.transform, "ContainmentCrate_Arena1_A", new Vector3(-7.5f, 0.75f, 22f), new Vector3(1.5f, 1.5f, 1.5f), crateMat);
            CreateProp(propsGroup.transform, "ContainmentCrate_Arena1_B", new Vector3(7.5f, 0.75f, 28f), new Vector3(1.5f, 1.5f, 1.5f), crateMat);
            CreateProp(propsGroup.transform, "GravityAnchorBlock_Arena2_A", new Vector3(-5f, 1.0f, 52f), new Vector3(2f, 2f, 2f), crateMat);
            CreateProp(propsGroup.transform, "GravityAnchorBlock_Arena2_B", new Vector3(5f, 1.0f, 58f), new Vector3(2f, 2f, 2f), crateMat);
            CreateProp(propsGroup.transform, "IllusionObelisk_Arena3_L", new Vector3(-6f, 2.0f, 85f), new Vector3(1.2f, 4f, 1.2f), consoleMat);
            CreateProp(propsGroup.transform, "IllusionObelisk_Arena3_R", new Vector3(6f, 2.0f, 85f), new Vector3(1.2f, 4f, 1.2f), consoleMat);

            CreateIntegrationAnchor(propsGroup.transform, "Student2_Doors_And_Traps_Anchor", new Vector3(0f, 0f, 5f));
            CreateIntegrationAnchor(propsGroup.transform, "Student3_CustomModels_Anchor", new Vector3(0f, 0f, 85f));
            CreateIntegrationAnchor(propsGroup.transform, "Student4_WardenAgent_Anchor", new Vector3(0f, 0f, 95f));

            SaveSceneChanges();
            Debug.Log("Lab props and teammate integration anchors placed!");
        }

        [ContextMenu("2. Optimize Static Batching, Textures & Re-bake NavMesh (Commit #14)")]
        public void OptimizeWorldAndRebakeNavMesh()
        {
            Transform root = GameObject.Find("Lab_Environment_Root")?.transform;
            if (root == null) return;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.isStatic = true;
            }

#if UNITY_EDITOR
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

            NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
            if (surface == null) surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();

            SaveSceneChanges();
            Debug.Log("Static batching enabled, textures compressed, and NavMesh re-baked!");
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
            string mdContent = "# Student 1: World Builder & Zombie Bot - Technical Design Notes\n\nIncludes connected Arenas 1-3, Student 4 Maze_Arena, Lighting Moods, Baked NavMesh, and ZombieAgentAI.";
            File.WriteAllText(Path.Combine(docFolder, "Student1_WorldAndZombie_VivaNotes.md"), mdContent);
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            SaveSceneChanges();
        }

        [ContextMenu("4. Connect Student 4 Maze_Arena & Agent 4 (Dev Integration)")]
        public void ConnectStudent4MazeArenaAndAgent()
        {
#if UNITY_EDITOR
            Transform root = GameObject.Find("Lab_Environment_Root")?.transform;
            if (root == null)
            {
                Debug.LogWarning("Lab_Environment_Root not found!");
                return;
            }

            // Remove old instances if re-running
            Transform oldCorridorD = root.Find("07b_Corridor_D_ToMaze");
            if (oldCorridorD != null) DestroyImmediate(oldCorridorD.gameObject);
            Transform oldMazeHolder = root.Find("07c_Arena4_Maze_Student4");
            if (oldMazeHolder != null) DestroyImmediate(oldMazeHolder.gameObject);
            GameObject oldGhost = GameObject.Find("MazeGhost_Agent4");
            if (oldGhost != null) DestroyImmediate(oldGhost);

            Shader urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/SciFi_Floor_Mat.mat") ?? new Material(urp);
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/SciFi_Wall_Mat.mat") ?? new Material(urp);

            // 1. Build connecting Corridor D from Arena 3 (z=95..105) into Maze_Arena
            GameObject corridorD = new GameObject("07b_Corridor_D_ToMaze");
            corridorD.transform.SetParent(root);
            corridorD.transform.position = new Vector3(0f, 0f, 100f);
            CreateProp(corridorD.transform, "Floor", new Vector3(0f, -0.25f, 100f), new Vector3(4f, 0.5f, 10f), floorMat);
            CreateProp(corridorD.transform, "Wall_West", new Vector3(-2f, 2f, 100f), new Vector3(0.5f, 4f, 10f), wallMat);
            CreateProp(corridorD.transform, "Wall_East", new Vector3(2f, 2f, 100f), new Vector3(0.5f, 4f, 10f), wallMat);

            // 2. Instantiate Student 4's Maze_Arena.prefab at z = 111
            // NOTE: Detailed height alignment, side placement, and connector corridors are handled by FailSafeZero/World/MazeAlignmentTool.cs
            GameObject mazeHolder = new GameObject("07c_Arena4_Maze_Student4");
            mazeHolder.transform.SetParent(root);
            mazeHolder.transform.position = new Vector3(0f, 0f, 111f);

            GameObject mazePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Arenas/Maze_Arena.prefab");
            if (mazePrefab != null)
            {
                GameObject mazeInstance = (GameObject)PrefabUtility.InstantiatePrefab(mazePrefab, mazeHolder.transform);
                mazeInstance.name = "Maze_Arena";
                // Keep Student 4's custom side placement and rotation intact
            }
            else
            {
                Debug.LogWarning("Maze_Arena.prefab not found at Assets/_Project/Prefabs/Arenas/Maze_Arena.prefab");
            }

            // Add atmospheric Eerie Green/Teal light for the Maze Arena
            GameObject mazeLightObj = new GameObject("MazeArena_MoodLight");
            mazeLightObj.transform.SetParent(mazeHolder.transform);
            mazeLightObj.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            Light mazeLight = mazeLightObj.AddComponent<Light>();
            mazeLight.type = LightType.Point;
            mazeLight.color = new Color(0.2f, 0.9f, 0.6f);
            mazeLight.intensity = 4.5f;
            mazeLight.range = 25f;

            // 3. Move Exit Room forward (z = 126) so the player exits after clearing the Maze Arena
            Transform exitRoom = root.Find("08_ExitRoom");
            if (exitRoom != null)
            {
                exitRoom.position = new Vector3(0f, 0f, 126f);
            }

            // 4. Mark all environment & Maze_Arena walls as Static and Re-bake the NavMesh
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.isStatic = true;
            }

            NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
            if (surface == null) surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();

            // 5. Instantiate Student 4's MazeGhost.prefab (Agent 4) inside the Maze_Arena & link Player
            GameObject ghostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Bots/MazeGhost.prefab");
            GameObject ghostInstance = null;
            if (ghostPrefab != null)
            {
                ghostInstance = (GameObject)PrefabUtility.InstantiatePrefab(ghostPrefab);
                ghostInstance.name = "MazeGhost_Agent4";
                ghostInstance.transform.position = new Vector3(0f, 1.0f, 111f);
            }
            else
            {
                ghostInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                ghostInstance.name = "MazeGhost_Agent4";
                ghostInstance.transform.position = new Vector3(0f, 1.0f, 111f);
            }

            if (ghostInstance.GetComponent<NavMeshAgent>() == null)
            {
                ghostInstance.AddComponent<NavMeshAgent>();
            }

            System.Type agent4Type = System.Type.GetType("Agent4, Assembly-CSharp");
            if (agent4Type != null && ghostInstance.GetComponent(agent4Type) == null)
{
    ghostInstance.AddComponent(agent4Type);
}

            SaveSceneChanges();
            Debug.Log("SUCCESS: Student 4's Maze_Arena and MazeGhost (Agent 4) connected to MainLabScene and NavMesh re-baked!");
#endif
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