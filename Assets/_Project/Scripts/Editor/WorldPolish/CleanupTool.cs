using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using FailSafeZero.EditorTools.WorldPolish;

namespace FailSafeZero.EditorTools.WorldPolish
{
    /// <summary>
    /// Student 1 - Environment Clean-up & Hygiene Tool.
    /// Provides idempotent menu items to safely remove unused test objects, verify references,
    /// and reassign persistent PBR materials to static props under 09_Integration_And_Props.
    /// </summary>
    public static class CleanupTool
    {
        private const string MenuCategory = "FailSafeZero/World/";

        [MenuItem(MenuCategory + "Cleanup - Remove Test Objects", false, 60)]
        public static void RemoveTestObjects()
        {
            var scene = EditorSceneManager.GetActiveScene();
            Debug.Log("[WorldPolish] ========================================================");
            Debug.Log("[WorldPolish] STARTING CLEANUP: Checking Test Objects & Empty Anchors...");
            Debug.Log("[WorldPolish] ========================================================");

            int removedCount = 0;
            int keptCount = 0;

            // 1. Root Test Objects to check: "Player_TestTarget" and "Main Camera"
            string[] rootTargets = new string[] { "Player_TestTarget", "Main Camera" };
            foreach (string rootName in rootTargets)
            {
                GameObject rootObj = FindRootObject(rootName);
                if (rootObj == null)
                {
                    Debug.Log($"[WorldPolish] Root target '{rootName}' is not present in active scene (already cleaned up).");
                    continue;
                }

                // Rule c: Remove ONLY if inactive AND no other component in the scene references them
                if (rootObj.activeSelf)
                {
                    Debug.LogWarning($"[WorldPolish] KEPT '{rootName}': GameObject is currently ACTIVE. Root test objects must be inactive to remove.");
                    keptCount++;
                    continue;
                }

                if (IsReferencedByAnyComponent(rootObj, out string refInfo))
                {
                    Debug.LogWarning($"[WorldPolish] KEPT '{rootName}': Referenced by scene component -> {refInfo}");
                    keptCount++;
                    continue;
                }

                // Safe to remove
                Undo.DestroyObjectImmediate(rootObj);
                Debug.Log($"[WorldPolish] REMOVED root test object '{rootName}' (inactive and unreferenced).");
                removedCount++;
            }

            // 2. Empty Teammate Integration Anchors to check under 09_Integration_And_Props:
            // "Student2_Doors_And_Traps_Anchor", "Student3_CustomModels_Anchor", "Student4_WardenAgent_Anchor"
            string[] anchorTargets = new string[]
            {
                "Student2_Doors_And_Traps_Anchor",
                "Student3_CustomModels_Anchor",
                "Student4_WardenAgent_Anchor"
            };

            foreach (string anchorName in anchorTargets)
            {
                GameObject anchorObj = GameObject.Find(anchorName);
                if (anchorObj == null)
                {
                    Debug.Log($"[WorldPolish] Anchor '{anchorName}' not present in scene (already cleaned up).");
                    continue;
                }

                // Rule e: Remove ONLY if empty (0 children) AND nothing references them
                if (anchorObj.transform.childCount > 0)
                {
                    Debug.LogWarning($"[WorldPolish] KEPT anchor '{anchorName}': Contains {anchorObj.transform.childCount} child objects.");
                    keptCount++;
                    continue;
                }

                if (IsReferencedByAnyComponent(anchorObj, out string refInfo))
                {
                    Debug.LogWarning($"[WorldPolish] KEPT anchor '{anchorName}': Referenced by scene component -> {refInfo}");
                    keptCount++;
                    continue;
                }

                Undo.DestroyObjectImmediate(anchorObj);
                Debug.Log($"[WorldPolish] REMOVED empty anchor '{anchorName}' (0 children, unreferenced).");
                removedCount++;
            }

            if (removedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Debug.Log($"[WorldPolish] CLEANUP COMPLETE: {removedCount} object(s) removed, {keptCount} object(s) kept.");
        }

        [MenuItem(MenuCategory + "Cleanup - Reassign Prop Materials", false, 61)]
        public static void ReassignPropMaterials()
        {
            var scene = EditorSceneManager.GetActiveScene();
            Debug.Log("[WorldPolish] ========================================================");
            Debug.Log("[WorldPolish] REASSIGNING PROP MATERIALS TO SAVED ASSETS...");
            Debug.Log("[WorldPolish] ========================================================");

            Material trimMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Environment/M_Lab_Trim.mat");
            Material pillarMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Environment/M_Lab_Pillar.mat");

            if (trimMat == null || pillarMat == null)
            {
                Debug.LogError("[WorldPolish] ERROR: Could not find M_Lab_Trim.mat or M_Lab_Pillar.mat in Assets/_Project/Materials/Environment/!");
                return;
            }

            Transform propsGroup = GameObject.Find("Lab_Environment_Root/09_Integration_And_Props")?.transform;
            if (propsGroup == null)
            {
                Debug.LogWarning("[WorldPolish] 'Lab_Environment_Root/09_Integration_And_Props' not found in active scene.");
                return;
            }

            int reassignedCount = 0;
            foreach (Transform child in propsGroup)
            {
                // Strict teammate protection: skip any teammate object
                if (WorldPolishUtil.IsTeammateObject(child.gameObject))
                {
                    Debug.Log($"[WorldPolish] Skipping teammate object: '{child.name}'");
                    continue;
                }

                Renderer rend = child.GetComponent<Renderer>();
                if (rend == null) continue;

                Material targetMat;
                if (child.name.Contains("Console") || child.name.Contains("Obelisk"))
                {
                    targetMat = trimMat;
                }
                else if (child.name.Contains("Crate") || child.name.Contains("AnchorBlock") || child.name.Contains("Pillar"))
                {
                    targetMat = pillarMat;
                }
                else
                {
                    targetMat = trimMat;
                }

                if (rend.sharedMaterial != targetMat)
                {
                    Undo.RecordObject(rend, "Reassign Prop Material");
                    rend.sharedMaterial = targetMat;
                    Debug.Log($"[WorldPolish] Assigned '{targetMat.name}' to '{child.name}'");
                    reassignedCount++;
                }
                else
                {
                    Debug.Log($"[WorldPolish] Prop '{child.name}' already has '{targetMat.name}' assigned.");
                }
            }

            if (reassignedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Debug.Log($"[WorldPolish] REASSIGN PROP MATERIALS COMPLETE: {reassignedCount} prop(s) updated with persistent PBR assets.");
        }

        private static GameObject FindRootObject(string name)
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }

        /// <summary>
        /// Checks whether the target GameObject or any of its components is referenced
        /// by any external component in the active scene using SerializedProperty iteration.
        /// </summary>
        private static bool IsReferencedByAnyComponent(GameObject target, out string referencingInfo)
        {
            referencingInfo = string.Empty;
            if (target == null) return false;

            // Collect all Object targets belonging to this GameObject hierarchy
            HashSet<UnityEngine.Object> targetObjects = new HashSet<UnityEngine.Object>();
            targetObjects.Add(target);

            foreach (Component comp in target.GetComponentsInChildren<Component>(true))
            {
                if (comp != null)
                {
                    targetObjects.Add(comp);
                }
            }

            var scene = EditorSceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component comp in root.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;

                    // Skip components that are part of the target object itself
                    if (targetObjects.Contains(comp)) continue;

                    // If checking an anchor child, skip the parent Transform's internal hierarchy references
                    if (comp is Transform parentTransform && target.transform.parent == parentTransform)
                    {
                        continue;
                    }

                    try
                    {
                        SerializedObject so = new SerializedObject(comp);
                        SerializedProperty prop = so.GetIterator();
                        while (prop.Next(true))
                        {
                            if (prop.propertyType == SerializedPropertyType.ObjectReference)
                            {
                                if (prop.objectReferenceValue != null && targetObjects.Contains(prop.objectReferenceValue))
                                {
                                    referencingInfo = $"{comp.gameObject.name} ({comp.GetType().Name}.{prop.propertyPath})";
                                    return true;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Some internal Unity components cannot create SerializedObjects; ignore gracefully
                    }
                }
            }

            return false;
        }
    }
}
