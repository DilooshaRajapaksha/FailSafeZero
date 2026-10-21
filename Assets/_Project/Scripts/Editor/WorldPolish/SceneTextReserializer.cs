using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;

namespace FailSafeZero.EditorTools.WorldPolish
{
    /// <summary>
    /// Student 1 - Scene Text Reserializer.
    /// Diagnoses and fixes MainLabScene.unity serialization, converting embedded binary NavMeshData
    /// into external .asset files so the scene can be saved as pure YAML text.
    /// </summary>
    public static class SceneTextReserializer
    {
        private const string ScenePath = "Assets/_Project/Scenes/MainLabScene.unity";

        // =========================================================================
        // A. SCENE FORMAT - REPORT (READ-ONLY)
        // =========================================================================
        [MenuItem("FailSafeZero/Tools/Scene Format - Report")]
        public static void ReportSceneFormat()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[WorldPolish] Scene file not found at '{ScenePath}'!");
                return;
            }

            // 1. Check first line for %YAML text header
            string firstLine = string.Empty;
            using (var reader = new StreamReader(ScenePath))
            {
                firstLine = reader.ReadLine() ?? string.Empty;
            }

            bool isText = firstLine.StartsWith("%YAML", StringComparison.Ordinal);
            Debug.Log($"[WorldPolish] MainLabScene Format: {(isText ? "TEXT (%YAML)" : "BINARY")} (First line: '{firstLine}')");
            Debug.Log($"[WorldPolish] EditorSettings.serializationMode: {EditorSettings.serializationMode} ({(int)EditorSettings.serializationMode})");

            // 2. Report every NavMeshSurface in the active scene
            NavMeshSurface[] surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"[WorldPolish] Found {surfaces.Length} NavMeshSurface(s) in active scene:");

            foreach (var surface in surfaces)
            {
                string path = WorldPolishUtil.GetHierarchyPath(surface.gameObject);
                bool isEnabled = surface.enabled;
                bool isNull = surface.navMeshData == null;

                string storageStatus;
                if (isNull)
                {
                    storageStatus = "navMeshData is NULL";
                }
                else
                {
                    bool isSavedAsset = AssetDatabase.Contains(surface.navMeshData);
                    string assetPath = isSavedAsset ? AssetDatabase.GetAssetPath(surface.navMeshData) : "none";
                    storageStatus = isSavedAsset ? $"SAVED ASSET ({assetPath})" : "EMBEDDED IN SCENE";
                }

                Debug.Log($"[WorldPolish]   • '{path}' | Enabled: {isEnabled} | Storage: {storageStatus}");
            }
        }

        // =========================================================================
        // B. SCENE FORMAT - FIX
        // =========================================================================
        [MenuItem("FailSafeZero/Tools/Scene Format - Fix")]
        public static void FixSceneFormat()
        {
            // 1. Warning dialog
            bool proceed = EditorUtility.DisplayDialog(
                "Re-save MainLabScene As Text",
                "This operation will:\n" +
                "1. Bake Lab_Environment_Root NavMesh to an external .asset file.\n" +
                "2. Save MainLabScene.unity.\n" +
                "3. Force-reserialize MainLabScene.unity to YAML text.\n\n" +
                "Please ensure teammates do not have unpushed MainLabScene changes.\n\n" +
                "Do you want to continue?",
                "Continue",
                "Cancel"
            );

            if (!proceed)
            {
                Debug.Log("[WorldPolish] Scene Format - Fix cancelled by user.");
                return;
            }

            // 2. Check EditorSettings.serializationMode
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                Debug.LogError("[WorldPolish] ABORTED: EditorSettings.serializationMode is not ForceText!\n" +
                               "Please set Edit > Project Settings > Editor > Asset Serialization > Mode = Force Text, then retry.");
                return;
            }

            // 3. Bake Lab_Environment_Root NavMesh so its NavMeshData is stored as an external asset
            bool baked = WorldPolishUtil.BakeLabNavMesh();
            if (!baked)
            {
                Debug.LogWarning("[WorldPolish] BakeLabNavMesh reported failure, proceeding with caution.");
            }

            // 4. Check for any OTHER NavMeshSurfaces with embedded data and LIST them (do NOT modify teammates' objects)
            List<string> teammateEmbedded = new List<string>();
            NavMeshSurface[] surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var surface in surfaces)
            {
                if (surface.gameObject.name == "Lab_Environment_Root") continue;

                if (surface.navMeshData != null && !AssetDatabase.Contains(surface.navMeshData))
                {
                    teammateEmbedded.Add(WorldPolishUtil.GetHierarchyPath(surface.gameObject));
                }
            }

            if (teammateEmbedded.Count > 0)
            {
                Debug.LogWarning($"[WorldPolish] NOTE: {teammateEmbedded.Count} teammate NavMeshSurface(s) have embedded navMeshData (leaving untouched): {string.Join(", ", teammateEmbedded)}");
            }

            // 5. Save the active scene
            var activeScene = EditorSceneManager.GetActiveScene();
            bool saved = EditorSceneManager.SaveScene(activeScene);
            if (!saved)
            {
                Debug.LogError("[WorldPolish] Failed to save active scene before reserialization!");
                return;
            }

            // 6. Force-reserialize ONLY MainLabScene.unity
            AssetDatabase.ForceReserializeAssets(new string[] { ScenePath });
            AssetDatabase.Refresh();

            // 7. Verify first line of file starts with %YAML
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[WorldPolish] Scene file not found at '{ScenePath}' after reserialization!");
                return;
            }

            string firstLine = string.Empty;
            using (var reader = new StreamReader(ScenePath))
            {
                firstLine = reader.ReadLine() ?? string.Empty;
            }

            bool isText = firstLine.StartsWith("%YAML", StringComparison.Ordinal);
            if (isText)
            {
                Debug.Log($"[WorldPolish] PASS: MainLabScene is now a YAML TEXT file! (First line: '{firstLine}')");
            }
            else
            {
                Debug.LogError($"[WorldPolish] FAIL: MainLabScene is still BINARY! (First line: '{firstLine}')");
            }

            if (teammateEmbedded.Count > 0)
            {
                Debug.LogWarning($"[WorldPolish] Objects still embedded in scene: {string.Join(", ", teammateEmbedded)}");
            }
        }
    }
}
