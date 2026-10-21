using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
#endif

namespace FailSafeZero.EditorTools.WorldPolish
{
    /// <summary>
    /// Utility methods for WorldPolish tools and team baseline tracking.
    /// Provides helper functions to distinguish Student 1's environment assets from teammates' objects,
    /// and shared methods for baking environment NavMesh assets.
    /// </summary>
    public static class WorldPolishUtil
    {
        /// <summary>
        /// Checks whether a GameObject belongs to a teammate (Student 2, 3, or 4).
        /// A teammate object is:
        /// - Any prefab instance whose source is in Prefabs/Bots, Prefabs/Player, Prefabs/Tools, or Arenas/Maze_Arena.prefab
        /// - Anything with a NavMeshAgent, CharacterController, Rigidbody, or Animator
        /// - Anything with a script from Scripts/AI, Scripts/Core, Scripts/Player, or Scripts/Weapons
        /// - Any teammate integration anchor
        /// - And all children of any teammate object.
        /// Excludes Student 1's environment rooms and props.
        /// </summary>
        public static bool IsTeammateObject(GameObject go)
        {
            if (go == null) return false;

            // Check if this object or any of its ancestors up to Lab_Environment_Root is a teammate object
            Transform current = go.transform;
            while (current != null)
            {
                // Stop traversal once we reach Lab_Environment_Root
                if (current.name == "Lab_Environment_Root") break;

                // Explicitly skip Student 1's holder containers and props from being considered teammate roots
                if (current.name == "07c_Arena4_Maze_Student4" ||
                    current.name == "09_Integration_And_Props" ||
                    current.name == "Maze_WalkableFloor_Base" ||
                    current.name == "MazeArena_MoodLight")
                {
                    current = current.parent;
                    continue;
                }

                if (IsDirectTeammateObject(current.gameObject))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsDirectTeammateObject(GameObject go)
        {
            if (go == null) return false;
            string name = go.name;

            // Teammate integration anchors
            if (name.StartsWith("Student", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("Anchor", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("_Anchor"))
            {
                return true;
            }

            // Student 4 Maze_Arena
            if (name == "Maze_Arena" || name.StartsWith("Maze_Arena"))
            {
                return true;
            }

            // Teammate bots / agents / player naming conventions
            if (name.Contains("Ghost") || name.Contains("Illusion") ||
                name.Contains("ZombieBot") || name.Contains("FirstPerson"))
            {
                return true;
            }

#if UNITY_EDITOR
            // Prefab source check
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                if (prefabPath.Contains("Prefabs/Bots") ||
                    prefabPath.Contains("Prefabs/Player") ||
                    prefabPath.Contains("Prefabs/Tools") ||
                    prefabPath.Contains("Arenas/Maze_Arena"))
                {
                    return true;
                }
            }
#endif

            // Component checks: NavMeshAgent, CharacterController, Rigidbody, Animator
            if (go.GetComponent<NavMeshAgent>() != null ||
                go.GetComponent<CharacterController>() != null ||
                go.GetComponent<Rigidbody>() != null ||
                go.GetComponent<Animator>() != null)
            {
                return true;
            }

            // Teammate scripts from Scripts/AI, Scripts/Core, Scripts/Player, Scripts/Weapons
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                string typeName = c.GetType().FullName ?? c.GetType().Name;
                if (typeName.Contains("FailSafeZero.AI") ||
                    typeName.Contains("FailSafeZero.Player") ||
                    typeName.Contains("FailSafeZero.Weapons") ||
                    typeName.Contains("FailSafeZero.Interactions") ||
                    typeName.Contains("FailSafeZero.Core") ||
                    typeName.Contains("AgentMover") ||
                    typeName.Contains("Agent4") ||
                    typeName.Contains("MazeGhost") ||
                    typeName.Contains("ZombieAgent") ||
                    typeName.Contains("IllusionBot") ||
                    typeName.Contains("BotChase") ||
                    typeName.Contains("EnemyHealth") ||
                    typeName.Contains("PlayerHealth") ||
                    typeName.Contains("GameFlow") ||
                    typeName.Contains("ObjectiveZone") ||
                    typeName.Contains("ExitZone"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Bakes the NavMesh for Lab_Environment_Root and saves the resulting NavMeshData
        /// as a persistent asset at Assets/_Project/Scenes/MainLabScene/NavMesh-Lab_Environment_Root.asset
        /// rather than embedding raw binary data inside the .unity scene file.
        /// Never modifies teammate NavMeshSurfaces.
        /// </summary>
        public static bool BakeLabNavMesh()
        {
#if UNITY_EDITOR
            GameObject labRoot = GameObject.Find("Lab_Environment_Root");
            if (labRoot == null)
            {
                Debug.LogError("[WorldPolish] BakeLabNavMesh FAIL: Lab_Environment_Root not found in active scene!");
                return false;
            }

            NavMeshSurface surface = labRoot.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = Undo.AddComponent<NavMeshSurface>(labRoot);
                surface.collectObjects = CollectObjects.Children;
            }

            // Build the NavMesh
            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                Debug.LogError("[WorldPolish] BakeLabNavMesh FAIL: surface.BuildNavMesh() resulted in null navMeshData!");
                return false;
            }

            // Ensure destination directory exists
            string assetDir = "Assets/_Project/Scenes/MainLabScene";
            if (!AssetDatabase.IsValidFolder(assetDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
                }
                AssetDatabase.CreateFolder("Assets/_Project/Scenes", "MainLabScene");
            }

            string assetPath = "Assets/_Project/Scenes/MainLabScene/NavMesh-Lab_Environment_Root.asset";

            // If an asset already exists, delete it so Unity replaces it cleanly
            NavMeshData existingAsset = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
            if (existingAsset != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            // Create new persistent asset
            AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
            AssetDatabase.SaveAssets();

            // Assign the saved asset back to the surface with Undo
            Undo.RecordObject(surface, "Bake Lab NavMesh");
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);

            // Mark the scene dirty
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);

            Debug.Log($"[WorldPolish] BakeLabNavMesh PASS: Saved NavMeshData to '{assetPath}' and assigned to Lab_Environment_Root.");
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Builds the full hierarchy path for a GameObject (e.g. "Lab_Environment_Root/03_Arena1_Zombie/Floor").
        /// </summary>
        public static string GetHierarchyPath(GameObject go)
        {
            if (go == null) return string.Empty;
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        /// <summary>
        /// Finds a GameObject by its hierarchy path, searching through root GameObjects (supports inactive objects).
        /// </summary>
        public static GameObject FindByHierarchyPath(string hierarchyPath)
        {
            if (string.IsNullOrEmpty(hierarchyPath)) return null;
            string[] parts = hierarchyPath.Split('/');
            if (parts.Length == 0) return null;

            GameObject current = null;
#if UNITY_EDITOR
            var scene = EditorSceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == parts[0])
                {
                    current = root;
                    break;
                }
            }
#else
            current = GameObject.Find(parts[0]);
#endif
            if (current == null) return null;

            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.transform.Find(parts[i]);
                if (child == null) return null;
                current = child.gameObject;
            }

            return current;
        }

        /// <summary>
        /// Gets relative path from a parent to a child Transform.
        /// </summary>
        public static string GetRelativePath(Transform child, Transform root)
        {
            if (child == null || root == null || child == root) return string.Empty;
            string path = child.name;
            Transform p = child.parent;
            while (p != null && p != root)
            {
                path = p.name + "/" + path;
                p = p.parent;
            }
            return path;
        }
    }
}

/// <summary>
/// Global alias for seamless access without requiring explicit using directives.
/// </summary>
public static class WorldPolishUtil
{
    public static bool IsTeammateObject(GameObject go) => FailSafeZero.EditorTools.WorldPolish.WorldPolishUtil.IsTeammateObject(go);
    public static bool BakeLabNavMesh() => FailSafeZero.EditorTools.WorldPolish.WorldPolishUtil.BakeLabNavMesh();
    public static string GetHierarchyPath(GameObject go) => FailSafeZero.EditorTools.WorldPolish.WorldPolishUtil.GetHierarchyPath(go);
    public static GameObject FindByHierarchyPath(string path) => FailSafeZero.EditorTools.WorldPolish.WorldPolishUtil.FindByHierarchyPath(path);
    public static string GetRelativePath(Transform child, Transform root) => FailSafeZero.EditorTools.WorldPolish.WorldPolishUtil.GetRelativePath(child, root);
}
