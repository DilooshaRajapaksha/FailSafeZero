using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FailSafeZero.EditorTools.WorldPolish
{
    /// <summary>
    /// Student 1 - Environment Material and Texture Import Application Tool.
    /// Manages texture import settings for Assets/_Project/Art/Textures/Environment
    /// and assigns M_Lab_* environment materials across the lab and Maze_Arena instance.
    /// Safe, undo-able, and strictly excludes teammate objects.
    /// </summary>
    public static class MaterialApplyTool
    {
        private const string TextureFolder = "Assets/_Project/Art/Textures/Environment";
        private const string MaterialFolder = "Assets/_Project/Materials/Environment";

        private struct TextureReportRecord
        {
            public string filename;
            public string type;
            public bool sRGB;
            public int maxSize;
            public bool mipmaps;
        }

        // ====================================================================================
        // MENU 0: GENERATE M_LAB MATERIALS FROM TEXTURES
        // ====================================================================================
        [MenuItem("FailSafeZero/World/Materials 0 - Generate M_Lab Materials From Textures", priority = 20)]
        public static void GenerateLabMaterialsFromTextures()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "Environment");
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // 1. M_Lab_Floor
            Texture2D floorColor = LoadTexture("MetalPlates006_2K-JPG_Color") ?? LoadTexture("Metal027_1K-JPG_Color");
            Texture2D floorNormal = LoadTexture("MetalPlates006_2K-JPG_NormalDX") ?? LoadTexture("Metal027_1K-JPG_NormalDX");
            CreateOrUpdateMaterial("M_Lab_Floor", urpLit, floorColor, floorNormal, Color.white, 0.6f, 0.5f, new Vector2(4f, 4f));

            // 2. M_Lab_Wall
            Texture2D wallColor = LoadTexture("Metal033_1K-JPG_Color");
            Texture2D wallNormal = LoadTexture("Metal033_1K-JPG_NormalDX");
            CreateOrUpdateMaterial("M_Lab_Wall", urpLit, wallColor, wallNormal, Color.white, 0.5f, 0.4f, new Vector2(4f, 2f));

            // 3. M_Lab_Pillar
            Texture2D concreteColor = LoadTexture("Concrete015_1K-JPG_Color");
            Texture2D concreteNormal = LoadTexture("Concrete015_1K-JPG_NormalDX");
            CreateOrUpdateMaterial("M_Lab_Pillar", urpLit, concreteColor, concreteNormal, Color.white, 0.1f, 0.3f, new Vector2(2f, 4f));

            // 4. M_Lab_Ceiling
            CreateOrUpdateMaterial("M_Lab_Ceiling", urpLit, concreteColor, concreteNormal, new Color(0.25f, 0.28f, 0.32f), 0.2f, 0.3f, new Vector2(4f, 4f));

            // 5. M_Lab_Trim
            CreateOrUpdateMaterial("M_Lab_Trim", urpLit, wallColor, wallNormal, new Color(0.35f, 0.38f, 0.45f), 0.7f, 0.6f, new Vector2(2f, 2f));

            AssetDatabase.SaveAssets();
            Debug.Log("[WorldPolish] Materials 0 - Successfully created and configured M_Lab_Floor, M_Lab_Wall, M_Lab_Pillar, M_Lab_Ceiling, M_Lab_Trim in Assets/_Project/Materials/Environment/!");

            // Automatically apply the new materials across the lab!
            ApplyMaterialsToLab();
        }

        private static Texture2D LoadTexture(string searchName)
        {
            string[] guids = AssetDatabase.FindAssets($"{searchName} t:Texture2D", new[] { TextureFolder });
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        private static Material CreateOrUpdateMaterial(string matName, Shader shader, Texture2D baseMap, Texture2D normalMap, Color tint, float metallic, float smoothness, Vector2 tiling)
        {
            string path = $"{MaterialFolder}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = false;
            if (mat == null)
            {
                mat = new Material(shader);
                isNew = true;
            }
            else
            {
                mat.shader = shader;
            }

            Undo.RecordObject(mat, "Configure Material");
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            if (baseMap != null)
            {
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetTextureScale("_BaseMap", tiling);
            }

            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        // ====================================================================================
        // MENU 1: FIX TEXTURE IMPORT SETTINGS
        // ====================================================================================
        [MenuItem("FailSafeZero/World/Materials 1 - Fix Texture Import Settings", priority = 21)]
        public static void FixTextureImportSettings()
        {
            // Force asset database refresh to index newly dragged files
            AssetDatabase.Refresh();

            if (!AssetDatabase.IsValidFolder(TextureFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Textures"))
                {
                    AssetDatabase.CreateFolder("Assets/_Project/Art", "Textures");
                }
                AssetDatabase.CreateFolder("Assets/_Project/Art/Textures", "Environment");
                AssetDatabase.Refresh();
                Debug.LogWarning($"[WorldPolish] Materials 1: Created folder '{TextureFolder}'. Please place your environment textures there and run this tool again.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder });
            if (guids == null || guids.Length == 0)
            {
                Debug.LogWarning($"[WorldPolish] Materials 1: No Texture2D assets found inside '{TextureFolder}'. If you just dragged files in, please wait a moment for Unity to finish importing them or run this menu item again.");
                return;
            }

            List<TextureReportRecord> records = new List<TextureReportRecord>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    // Strictly enforce path boundary
                    if (!path.StartsWith(TextureFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    string filename = Path.GetFileNameWithoutExtension(path);

                    // 1. Texture Type & sRGB
                    bool isNormal = filename.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isLinearMap = filename.IndexOf("Roughness", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       filename.IndexOf("Metalness", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       filename.IndexOf("Metallic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       filename.IndexOf("AmbientOcclusion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       filename.IndexOf("AO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       filename.IndexOf("Displacement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       filename.IndexOf("Height", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isNormal)
                    {
                        importer.textureType = TextureImporterType.NormalMap;
                        importer.sRGBTexture = false;
                    }
                    else if (isLinearMap)
                    {
                        importer.textureType = TextureImporterType.Default;
                        importer.sRGBTexture = false;
                    }
                    else
                    {
                        // Color / Albedo
                        importer.textureType = TextureImporterType.Default;
                        importer.sRGBTexture = true;
                    }

                    // 2. Max Texture Size: 2048 ONLY for Floor color/normal, 1024 for all others
                    bool isFloor = filename.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   path.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isFloorColorOrNormal = isFloor && (isNormal ||
                        filename.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        filename.IndexOf("Albedo", StringComparison.OrdinalIgnoreCase) >= 0);

                    importer.maxTextureSize = isFloorColorOrNormal ? 2048 : 1024;

                    // 3. Compression: Normal Quality
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.compressionQuality = 50;

                    // 4. Mipmaps: ON
                    importer.mipmapEnabled = true;

                    importer.SaveAndReimport();

                    string relPath = path.Substring(TextureFolder.Length).TrimStart('/');
                    records.Add(new TextureReportRecord
                    {
                        filename = relPath,
                        type = importer.textureType.ToString(),
                        sRGB = importer.sRGBTexture,
                        maxSize = importer.maxTextureSize,
                        mipmaps = importer.mipmapEnabled
                    });
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Print formatted report table
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[WorldPolish] Materials 1 - Texture Import Settings Report ({records.Count} textures):");
            sb.AppendLine("-----------------------------------------------------------------------------------------------------------------");
            sb.AppendLine(string.Format("{0,-45} | {1,-12} | {2,-6} | {3,-10} | {4,-14} | {5,-7}", "Texture Path", "Type", "sRGB", "Max Size", "Compression", "Mipmaps"));
            sb.AppendLine("-----------------------------------------------------------------------------------------------------------------");
            foreach (var r in records)
            {
                sb.AppendLine(string.Format("{0,-45} | {1,-12} | {2,-6} | {3,-10} | {4,-14} | {5,-7}",
                    r.filename, r.type, r.sRGB ? "ON" : "OFF", r.maxSize, "Compressed(50)", r.mipmaps ? "ON" : "OFF"));
            }
            sb.AppendLine("-----------------------------------------------------------------------------------------------------------------");
            Debug.Log(sb.ToString());
        }

        // ====================================================================================
        // MENU 2: APPLY MATERIALS TO LAB (EXCLUDING MAZE & TEAMMATES)
        // ====================================================================================
        [MenuItem("FailSafeZero/World/Materials 2 - Apply To Lab", priority = 22)]
        public static void ApplyMaterialsToLab()
        {
            GameObject labRoot = GameObject.Find("Lab_Environment_Root");
            if (labRoot == null)
            {
                Debug.LogError("[WorldPolish] Materials 2 FAIL: 'Lab_Environment_Root' not found in active scene!");
                return;
            }

            Material matFloor = LoadMaterial("M_Lab_Floor", "SciFi_Floor_Mat");
            Material matCeiling = LoadMaterial("M_Lab_Ceiling", "SciFi_Ceiling_Mat");
            Material matPillar = LoadMaterial("M_Lab_Pillar", "SciFi_Pillar_Mat");
            Material matTrim = LoadMaterial("M_Lab_Trim", "SciFi_Pillar_Mat");
            Material matWall = LoadMaterial("M_Lab_Wall", "SciFi_Wall_Mat");

            Renderer[] renderers = labRoot.GetComponentsInChildren<Renderer>(true);

            int countFloor = 0;
            int countCeiling = 0;
            int countPillar = 0;
            int countTrim = 0;
            int countWall = 0;
            int countSkippedTeammate = 0;
            int countSkippedMaze = 0;

            Undo.SetCurrentGroupName("Apply Environment Materials To Lab");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (Renderer rend in renderers)
            {
                if (rend == null) continue;

                // 1. Skip teammate objects
                if (WorldPolishUtil.IsTeammateObject(rend.gameObject))
                {
                    countSkippedTeammate++;
                    continue;
                }

                // 2. Skip objects inside Maze_Arena instance
                if (IsInsideMazeArena(rend.transform, labRoot.transform))
                {
                    countSkippedMaze++;
                    continue;
                }

                // 3. Determine target material based on object name
                string objName = rend.gameObject.name;
                Material targetMat;

                if (objName.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetMat = matFloor;
                    countFloor++;
                }
                else if (objName.IndexOf("Ceiling", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetMat = matCeiling;
                    countCeiling++;
                }
                else if (objName.IndexOf("Pillar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         objName.IndexOf("Obelisk", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetMat = matPillar;
                    countPillar++;
                }
                else if (objName.IndexOf("DoorFrame", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         objName.IndexOf("Crate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         objName.IndexOf("Block", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         objName.IndexOf("Console", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetMat = matTrim;
                    countTrim++;
                }
                else
                {
                    targetMat = matWall;
                    countWall++;
                }

                Undo.RecordObject(rend, "Apply Environment Material");
                rend.sharedMaterial = targetMat;
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(labRoot.scene);

            int totalApplied = countFloor + countCeiling + countPillar + countTrim + countWall;
            Debug.Log($"[WorldPolish] Materials 2 - Apply To Lab Report:\n" +
                      $"  - Floor ({matFloor.name}): {countFloor} renderers\n" +
                      $"  - Ceiling ({matCeiling.name}): {countCeiling} renderers\n" +
                      $"  - Pillar/Obelisk ({matPillar.name}): {countPillar} renderers\n" +
                      $"  - Trim ({matTrim.name}): {countTrim} renderers\n" +
                      $"  - Wall ({matWall.name}): {countWall} renderers\n" +
                      $"  -----------------------------------------\n" +
                      $"  Total Renderers Updated: {totalApplied}\n" +
                      $"  Skipped Teammate Renderers: {countSkippedTeammate}\n" +
                      $"  Skipped Maze Renderers: {countSkippedMaze}");
        }

        // ====================================================================================
        // MENU 3: APPLY MATERIALS TO MAZE INSTANCE (OPTIONAL, SCENE OVERRIDES ONLY)
        // ====================================================================================
        [MenuItem("FailSafeZero/World/Materials 3 - Apply To Maze Instance (optional)", priority = 23)]
        public static void ApplyMaterialsToMazeInstance()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Apply To Maze Instance",
                "Ask the maze owner first.\n\nThis will apply environment material overrides to the Maze_Arena instance in this scene only. The Maze_Arena prefab asset and teammate materials will not be modified.\n\nDo you want to proceed?",
                "Apply Overrides",
                "Cancel");

            if (!confirmed)
            {
                Debug.Log("[WorldPolish] Materials 3: Cancelled by user.");
                return;
            }

            GameObject labRoot = GameObject.Find("Lab_Environment_Root");
            if (labRoot == null)
            {
                Debug.LogError("[WorldPolish] Materials 3 FAIL: 'Lab_Environment_Root' not found in active scene!");
                return;
            }

            Transform mazeHolder = labRoot.transform.Find("07c_Arena4_Maze_Student4");
            Transform mazeInstance = mazeHolder != null ? mazeHolder.Find("Maze_Arena") : null;

            if (mazeInstance == null)
            {
                // Fallback: search anywhere under Lab_Environment_Root
                foreach (Transform child in labRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Maze_Arena")
                    {
                        mazeInstance = child;
                        break;
                    }
                }
            }

            if (mazeInstance == null)
            {
                Debug.LogError("[WorldPolish] Materials 3 FAIL: 'Maze_Arena' instance not found under 'Lab_Environment_Root'!");
                return;
            }

            Material matFloor = LoadMaterial("M_Lab_Floor", "SciFi_Floor_Mat");
            Material matCeiling = LoadMaterial("M_Lab_Ceiling", "SciFi_Ceiling_Mat");
            Material matPillar = LoadMaterial("M_Lab_Pillar", "SciFi_Pillar_Mat");
            Material matTrim = LoadMaterial("M_Lab_Trim", "SciFi_Pillar_Mat");
            Material matWall = LoadMaterial("M_Lab_Wall", "SciFi_Wall_Mat");

            Renderer[] renderers = mazeInstance.GetComponentsInChildren<Renderer>(true);

            int countFloor = 0;
            int countCeiling = 0;
            int countPillar = 0;
            int countTrim = 0;
            int countWall = 0;
            int countSkippedTeammate = 0;

            Undo.SetCurrentGroupName("Apply Maze Instance Material Overrides");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (Renderer rend in renderers)
            {
                if (rend == null) continue;

                // Skip teammate bots, agents, player, or tools placed inside Maze
                if (rend.GetComponent<UnityEngine.AI.NavMeshAgent>() != null ||
                    rend.GetComponent<CharacterController>() != null ||
                    rend.GetComponent<Rigidbody>() != null ||
                    WorldPolishUtil.HasTeammateScript(rend.gameObject) ||
                    rend.gameObject.name.Contains("Ghost") ||
                    rend.gameObject.name.Contains("Agent") ||
                    rend.gameObject.name.Contains("Player") ||
                    rend.gameObject.name.Contains("Bot"))
                {
                    countSkippedTeammate++;
                    continue;
                }

                string objName = rend.gameObject.name;
                Material targetMat;

                if (objName.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetMat = matFloor;
                    countFloor++;
                }
                else if (objName.IndexOf("Ceiling", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetMat = matCeiling;
                    countCeiling++;
                }
                else if (objName.IndexOf("Pillar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         objName.IndexOf("Obelisk", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetMat = matPillar;
                    countPillar++;
                }
                else if (objName.IndexOf("DoorFrame", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         objName.IndexOf("Crate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         objName.IndexOf("Block", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         objName.IndexOf("Console", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetMat = matTrim;
                    countTrim++;
                }
                else
                {
                    targetMat = matWall;
                    countWall++;
                }

                // Modifying sharedMaterial on an instantiated prefab in the scene applies as a scene override
                Undo.RecordObject(rend, "Apply Maze Instance Material Override");
                rend.sharedMaterial = targetMat;
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(mazeInstance.gameObject.scene);

            int totalApplied = countFloor + countCeiling + countPillar + countTrim + countWall;
            Debug.Log($"[WorldPolish] Materials 3 - Apply To Maze Instance Report (Scene Overrides):\n" +
                      $"  - Floor ({matFloor.name}): {countFloor} renderers\n" +
                      $"  - Ceiling ({matCeiling.name}): {countCeiling} renderers\n" +
                      $"  - Pillar/Obelisk ({matPillar.name}): {countPillar} renderers\n" +
                      $"  - Trim ({matTrim.name}): {countTrim} renderers\n" +
                      $"  - Wall ({matWall.name}): {countWall} renderers\n" +
                      $"  -----------------------------------------\n" +
                      $"  Total Maze Renderers Updated: {totalApplied}\n" +
                      $"  Skipped Teammate Renderers: {countSkippedTeammate}");
        }

        // ====================================================================================
        // HELPER METHODS
        // ====================================================================================
        private static bool IsInsideMazeArena(Transform t, Transform labRoot)
        {
            Transform curr = t;
            while (curr != null && curr != labRoot)
            {
                if (curr.name == "Maze_Arena" || curr.name.StartsWith("Maze_Arena"))
                {
                    return true;
                }
                curr = curr.parent;
            }
            return false;
        }

        private static Material LoadMaterial(string matName, string fallbackName)
        {
            // 1. Try target path in Environment folder
            string primaryPath = $"{MaterialFolder}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(primaryPath);
            if (mat != null) return mat;

            // 2. Try fallback in Materials folder
            string fallbackPath = $"Assets/_Project/Materials/{fallbackName}.mat";
            mat = AssetDatabase.LoadAssetAtPath<Material>(fallbackPath);
            if (mat != null)
            {
                Debug.LogWarning($"[WorldPolish] Material '{primaryPath}' not found; using fallback '{fallbackPath}'.");
                return mat;
            }

            // 3. Search anywhere under Assets/_Project/Materials
            string[] guids = AssetDatabase.FindAssets($"{matName} t:Material", new[] { "Assets/_Project/Materials" });
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null) return mat;
            }

            Debug.LogError($"[WorldPolish] Missing material '{matName}' and fallback '{fallbackName}'!");
            return null;
        }
    }
}
#endif
