using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FailSafeZero.EditorTools.WorldPolish
{
    /// <summary>
    /// Utility methods for WorldPolish tools and team baseline tracking.
    /// Provides helper functions to distinguish Student 1's environment assets from teammates' objects.
    /// </summary>
    public static class WorldPolishUtil
    {
        /// <summary>
        /// Checks whether a GameObject belongs to a teammate (Student 2, 3, or 4).
        /// Identifies teammate anchors, Student 4's Maze_Arena prefab instance and its contents,
        /// teammate bots/agents, weapons, and teammate-specific components.
        /// Excludes Student 1's environment rooms and props.
        /// </summary>
        public static bool IsTeammateObject(GameObject go)
        {
            if (go == null) return false;

            string name = go.name;

            // Student 1's own containers and props inside Lab_Environment_Root
            if (name == "Lab_Environment_Root" ||
                name == "07c_Arena4_Maze_Student4" ||
                name == "09_Integration_And_Props" ||
                name == "Maze_WalkableFloor_Base" ||
                name == "MazeArena_MoodLight")
            {
                return false;
            }

            // Exclude Student 1's standard room names
            if (name.StartsWith("01_") || name.StartsWith("02_") || name.StartsWith("03_") ||
                name.StartsWith("04_") || name.StartsWith("05_") || name.StartsWith("06_") ||
                name.StartsWith("07_") || name.StartsWith("07b_") || name.StartsWith("08_"))
            {
                return false;
            }

            // Teammate integration anchors
            if (name.StartsWith("Student", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("Anchor", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("_Anchor"))
            {
                return true;
            }

            // Student 4 Maze_Arena or any child within it
            if (name == "Maze_Arena" || name.StartsWith("Maze_Arena"))
            {
                return true;
            }

            Transform t = go.transform;
            while (t != null)
            {
                if (t.name == "Maze_Arena") return true;
                t = t.parent;
            }

            // Teammate bot or player naming conventions
            if (name.Contains("Ghost") || name.Contains("Illusion") ||
                name.Contains("ZombieBot") || name.Contains("FirstPerson"))
            {
                return true;
            }

#if UNITY_EDITOR
            // Prefab source tracking
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                if (prefabPath.Contains("Arenas/Maze_Arena") ||
                    prefabPath.Contains("Prefabs/Bots") ||
                    prefabPath.Contains("Prefabs/Player") ||
                    prefabPath.Contains("Prefabs/Tools"))
                {
                    return true;
                }
            }
#endif

            // Teammate component types
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
                    typeName.Contains("BotChase"))
                {
                    return true;
                }
            }

            return false;
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
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
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
    public static string GetHierarchyPath(GameObject go) => FailSafeZero.EditorTools.WorldPolish.WorldPolishUtil.GetHierarchyPath(go);
    public static GameObject FindByHierarchyPath(string path) => FailSafeZero.EditorTools.WorldPolish.WorldPolishUtil.FindByHierarchyPath(path);
    public static string GetRelativePath(Transform child, Transform root) => FailSafeZero.EditorTools.WorldPolish.WorldPolishUtil.GetRelativePath(child, root);
}
