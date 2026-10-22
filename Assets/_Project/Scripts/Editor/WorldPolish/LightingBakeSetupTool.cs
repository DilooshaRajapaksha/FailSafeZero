using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FailSafeZero.EditorTools.WorldPolish
{
    /// <summary>
    /// Student 1 - Baked Lighting Setup Tool.
    /// Configures light baking types, mesh ContributeGI flags, LightProbes, ReflectionProbes,
    /// and project LightingSettings for the FailSafeZero underground environment.
    /// Strictly excludes teammate objects and does not auto-bake.
    /// </summary>
    public static class LightingBakeSetupTool
    {
        private const string LightingSettingsFolder = "Assets/_Project/Settings/Environment";
        private const string LightingSettingsPath = "Assets/_Project/Settings/Environment/LabLightingSettings.lighting";

        // ====================================================================================
        // MENU 1: LIGHTING REPORT (READ-ONLY)
        // ====================================================================================
        [MenuItem("FailSafeZero/Lighting/1 - Report", priority = 31)]
        public static void ReportLighting()
        {
            var scene = EditorSceneManager.GetActiveScene();
            List<Light> sceneLights = GetSceneLights(scene);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[WorldPolish] ==================== LIGHTING REPORT ====================");
            sb.AppendLine($"Active Scene: {scene.name} (Path: {scene.path})");

            // 1. Current LightingSettings
            LightingSettings currentSettings = null;
            try
            {
                currentSettings = Lightmapping.lightingSettings;
            }
            catch (Exception)
            {
                currentSettings = null;
            }

            string settingsInfo = currentSettings != null 
                ? $"{currentSettings.name} ({AssetDatabase.GetAssetPath(currentSettings)})" 
                : "None (Not assigned yet / Default)";
            sb.AppendLine($"Current LightingSettings Asset: {settingsInfo}");
            sb.AppendLine();

            // 2. Lights Table
            sb.AppendLine($"Lights in Scene ({sceneLights.Count} lights found):");
            sb.AppendLine("------------------------------------------------------------------------------------------------------------------");
            sb.AppendLine(string.Format("{0,-45} | {1,-11} | {2,-9} | {3,-10} | {4,-7} | {5,-9}", "Hierarchy Path", "Type", "Mode", "Shadows", "Range", "Intensity"));
            sb.AppendLine("------------------------------------------------------------------------------------------------------------------");
            foreach (var l in sceneLights)
            {
                string path = WorldPolishUtil.GetHierarchyPath(l.gameObject);
                string modeStr = l.lightmapBakeType.ToString();
                string shadowStr = l.shadows.ToString();
                string rangeStr = l.type == LightType.Directional ? "N/A" : l.range.ToString("F1") + "m";
                string intensityStr = l.intensity.ToString("F2");

                sb.AppendLine(string.Format("{0,-45} | {1,-11} | {2,-9} | {3,-10} | {4,-7} | {5,-9}",
                    path, l.type.ToString(), modeStr, shadowStr, rangeStr, intensityStr));
            }
            sb.AppendLine("------------------------------------------------------------------------------------------------------------------");
            sb.AppendLine();

            // 3. ContributeGI on Renderers under Lab_Environment_Root
            GameObject labRoot = GameObject.Find("Lab_Environment_Root");
            if (labRoot != null)
            {
                MeshRenderer[] renderers = labRoot.GetComponentsInChildren<MeshRenderer>(true);
                int contributeGICount = 0;
                int noGICount = 0;
                int teammateCount = 0;

                foreach (var mr in renderers)
                {
                    if (mr == null) continue;
                    if (WorldPolishUtil.IsTeammateObject(mr.gameObject))
                    {
                        teammateCount++;
                        continue;
                    }

                    if (GameObjectUtility.AreStaticEditorFlagsSet(mr.gameObject, StaticEditorFlags.ContributeGI))
                    {
                        contributeGICount++;
                    }
                    else
                    {
                        noGICount++;
                    }
                }

                sb.AppendLine("Lab_Environment_Root MeshRenderer GI Status:");
                sb.AppendLine($"  - ContributeGI Enabled: {contributeGICount} renderers");
                sb.AppendLine($"  - ContributeGI Disabled: {noGICount} renderers");
                sb.AppendLine($"  - Teammate Renderers Skipped: {teammateCount} renderers");
            }
            else
            {
                sb.AppendLine("Lab_Environment_Root NOT FOUND in active scene!");
            }

            sb.AppendLine("======================================================================");
            Debug.Log(sb.ToString());
        }

        // ====================================================================================
        // MENU 2: PREPARE FOR BAKE (UNDO-ABLE)
        // ====================================================================================
        [MenuItem("FailSafeZero/Lighting/2 - Prepare For Bake", priority = 32)]
        public static void PrepareForBake()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject labRoot = GameObject.Find("Lab_Environment_Root");
            if (labRoot == null)
            {
                Debug.LogError("[WorldPolish] Prepare For Bake FAIL: 'Lab_Environment_Root' not found in active scene!");
                return;
            }

            Undo.SetCurrentGroupName("Prepare Environment For Baked Lighting");
            int undoGroup = Undo.GetCurrentGroup();

            // --------------------------------------------------------------------------------
            // 1. Configure Light Components
            // --------------------------------------------------------------------------------
            List<Light> sceneLights = GetSceneLights(scene);
            int bakedLightsCount = 0;
            int realtimeLightsCount = 0;
            int dirLightCount = 0;

            foreach (var l in sceneLights)
            {
                if (l == null) continue;
                string name = l.gameObject.name;

                if (name == "ArenaMoodLight" || name == "MazeArena_MoodLight" || name.StartsWith("MazeArena_FillLight_"))
                {
                    Undo.RecordObject(l, "Configure Baked Light");
                    l.lightmapBakeType = LightmapBakeType.Baked;
                    l.shadows = LightShadows.Soft;
                    bakedLightsCount++;
                }
                else if (name == "EmergencyLight_Red")
                {
                    // Must remain Realtime with no shadows because it pulses at runtime
                    Undo.RecordObject(l, "Configure Realtime Emergency Light");
                    l.lightmapBakeType = LightmapBakeType.Realtime;
                    l.shadows = LightShadows.None;
                    realtimeLightsCount++;
                }
                else if (name == "Directional Light" || l.type == LightType.Directional)
                {
                    Undo.RecordObject(l, "Configure Directional Light");
                    l.intensity = 0f;
                    l.lightmapBakeType = LightmapBakeType.Baked;
                    l.shadows = LightShadows.None;
                    dirLightCount++;
                }
            }

            // --------------------------------------------------------------------------------
            // 2. Enable ContributeGI on static environment MeshRenderers
            // --------------------------------------------------------------------------------
            MeshRenderer[] allRenderers = labRoot.GetComponentsInChildren<MeshRenderer>(true);
            int updatedRenderers = 0;
            int skippedTeammates = 0;

            foreach (var mr in allRenderers)
            {
                if (mr == null) continue;

                // Handle Maze_Arena instance vs teammate objects
                bool insideMaze = IsInsideMaze(mr.transform, labRoot.transform);
                if (insideMaze)
                {
                    // Only skip bots/agents inside maze
                    if (mr.GetComponent<UnityEngine.AI.NavMeshAgent>() != null ||
                        mr.GetComponent<CharacterController>() != null ||
                        WorldPolishUtil.HasTeammateScript(mr.gameObject) ||
                        mr.gameObject.name.Contains("Ghost") ||
                        mr.gameObject.name.Contains("Bot"))
                    {
                        skippedTeammates++;
                        continue;
                    }
                }
                else if (WorldPolishUtil.IsTeammateObject(mr.gameObject))
                {
                    skippedTeammates++;
                    continue;
                }

                // Apply ContributeGI and receiveGI on static objects
                Undo.RecordObject(mr.gameObject, "Set ContributeGI Static Flag");
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(mr.gameObject);
                flags |= StaticEditorFlags.ContributeGI;
                GameObjectUtility.SetStaticEditorFlags(mr.gameObject, flags);

                Undo.RecordObject(mr, "Set ReceiveGI Lightmaps");
                mr.receiveGI = ReceiveGI.Lightmaps;
                updatedRenderers++;
            }

            // --------------------------------------------------------------------------------
            // 3. Create & Assign LabLightingSettings.lighting
            // --------------------------------------------------------------------------------
            LightingSettings settings = CreateOrUpdateLightingSettings();
            Lightmapping.lightingSettings = settings;

            // --------------------------------------------------------------------------------
            // 4. Create/Update LightProbeGroups in each Room/Arena and Maze
            // --------------------------------------------------------------------------------
            int lightProbeGroupsUpdated = SetupLightProbes(labRoot);

            // --------------------------------------------------------------------------------
            // 5. Create/Update ReflectionProbes in Arenas, Maze, Start Room, and Exit Room
            // --------------------------------------------------------------------------------
            int reflectionProbesUpdated = SetupReflectionProbes(labRoot);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[WorldPolish] Prepare For Bake COMPLETE:\n" +
                      $"  - Lights Configured: {bakedLightsCount} Baked, {realtimeLightsCount} Realtime, {dirLightCount} Directional (intensity 0)\n" +
                      $"  - Environment MeshRenderers Updated to ContributeGI: {updatedRenderers} (Skipped {skippedTeammates} teammate renderers)\n" +
                      $"  - LightingSettings Asset: '{LightingSettingsPath}' assigned to scene\n" +
                      $"  - LightProbeGroups Configured: {lightProbeGroupsUpdated} rooms/arenas\n" +
                      $"  - ReflectionProbes Configured: {reflectionProbesUpdated} probes (Start, Arenas 1-3, Maze, Exit)\n" +
                      $"  ---------------------------------------------------------\n" +
                      $"  Ready to bake! Open Window > Rendering > Lighting and click 'Generate Lighting' when ready.");
        }

        // ====================================================================================
        // MENU 3: OPEN LIGHTING WINDOW CONVENIENCE SHORTCUT
        // ====================================================================================
        [MenuItem("FailSafeZero/Lighting/3 - Open Lighting Window", priority = 33)]
        public static void OpenLightingWindow()
        {
            EditorApplication.ExecuteMenuItem("Window/Rendering/Lighting");
        }

        // ====================================================================================
        // HELPER METHODS: LIGHTING SETTINGS CREATION
        // ====================================================================================
        private static LightingSettings CreateOrUpdateLightingSettings()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");
            }
            if (!AssetDatabase.IsValidFolder(LightingSettingsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Settings", "Environment");
            }

            LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings();
                AssetDatabase.CreateAsset(settings, LightingSettingsPath);
            }

            SerializedObject so = new SerializedObject(settings);
            so.Update();

            // Baked GI ON, Realtime GI OFF
            SetSerializedProperty(so, "m_EnableBakedLightmaps", true);
            SetSerializedProperty(so, "m_EnableRealtimeLightmaps", false);

            // Lightmapper: Progressive GPU (1), CPU fallback (0)
            int backend = SystemInfo.supportsComputeShaders ? 1 : 0;
            SetSerializedProperty(so, "m_BakeBackend", backend);

            // Lightmap Resolution: 10 texels/unit, Max Size: 1024
            SetSerializedProperty(so, "m_BakeResolution", 10.0f);
            SetSerializedProperty(so, "m_LightmapMaxSize", 1024);

            // Ambient Occlusion ON, Max Distance 1.0m
            SetSerializedProperty(so, "m_AO", true);
            SetSerializedProperty(so, "m_AOMaxDistance", 1.0f);

            // Texture Compression ON
            SetSerializedProperty(so, "m_TextureCompression", true);

            // Sampling: Direct 32, Indirect 256, Bounces 2
            SetSerializedProperty(so, "m_PVRDirectSampleCount", 32);
            SetSerializedProperty(so, "m_PVRSampleCount", 256);
            SetSerializedProperty(so, "m_PVRBounces", 2);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            return settings;
        }

        private static void SetSerializedProperty(SerializedObject so, string propName, object val)
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop == null) return;

            if (prop.propertyType == SerializedPropertyType.Boolean)
            {
                if (val is bool b) prop.boolValue = b;
                else if (val is int i) prop.boolValue = (i != 0);
            }
            else if (prop.propertyType == SerializedPropertyType.Integer)
            {
                if (val is int i) prop.intValue = i;
                else if (val is bool b) prop.intValue = b ? 1 : 0;
            }
            else if (prop.propertyType == SerializedPropertyType.Float)
            {
                if (val is float f) prop.floatValue = f;
                else if (val is int i) prop.floatValue = (float)i;
                else if (val is double d) prop.floatValue = (float)d;
            }
            else if (prop.propertyType == SerializedPropertyType.Enum)
            {
                if (val is int i) prop.enumValueIndex = i;
            }
        }

        // ====================================================================================
        // HELPER METHODS: LIGHT PROBE GRID SETUP
        // ====================================================================================
        private static int SetupLightProbes(GameObject labRoot)
        {
            int count = 0;
            string[] targetRooms = new[]
            {
                "01_StartRoom",
                "02_Corridor_A",
                "03_Arena1_Zombie",
                "04_Corridor_B",
                "05_Arena2_Gravity",
                "06_Corridor_C",
                "07_Arena3_Illusion",
                "07b_Corridor_D_ToMaze",
                "07d_Connector_ToMaze",
                "07c_Arena4_Maze_Student4",
                "08_ExitRoom"
            };

            foreach (string roomName in targetRooms)
            {
                Transform roomT = labRoot.transform.Find(roomName);
                if (roomT == null) continue;

                // Find floor bounds
                Bounds floorBounds = GetRoomFloorBounds(roomT);
                if (floorBounds.size.sqrMagnitude < 0.1f)
                {
                    floorBounds = GetCompositeBounds(roomT);
                }

                // Generate probe grid inside bounds: 0.6m from walls, stepped every 3.0m, heights 0.5m & 2.0m
                float minX = floorBounds.min.x + 0.6f;
                float maxX = floorBounds.max.x - 0.6f;
                float minZ = floorBounds.min.z + 0.6f;
                float maxZ = floorBounds.max.z - 0.6f;
                float floorY = floorBounds.max.y;

                if (minX > maxX) { minX = floorBounds.center.x; maxX = floorBounds.center.x; }
                if (minZ > maxZ) { minZ = floorBounds.center.z; maxZ = floorBounds.center.z; }

                List<Vector3> worldPoints = new List<Vector3>();
                for (float x = minX; x <= maxX + 0.05f; x += 3.0f)
                {
                    for (float z = minZ; z <= maxZ + 0.05f; z += 3.0f)
                    {
                        worldPoints.Add(new Vector3(x, floorY + 0.5f, z));
                        worldPoints.Add(new Vector3(x, floorY + 2.0f, z));
                    }
                }

                if (worldPoints.Count == 0) continue;

                // Find or create child LightProbes
                Transform probeChild = roomT.Find("LightProbes");
                GameObject probeObj;
                if (probeChild == null)
                {
                    probeObj = new GameObject("LightProbes");
                    probeObj.transform.SetParent(roomT, false);
                    probeObj.transform.localPosition = Vector3.zero;
                    probeObj.transform.localRotation = Quaternion.identity;
                    probeObj.transform.localScale = Vector3.one;
                    Undo.RegisterCreatedObjectUndo(probeObj, "Create LightProbes");
                }
                else
                {
                    probeObj = probeChild.gameObject;
                    Undo.RecordObject(probeObj, "Update LightProbes");
                }

                LightProbeGroup group = probeObj.GetComponent<LightProbeGroup>();
                if (group == null) group = Undo.AddComponent<LightProbeGroup>(probeObj);

                Vector3[] localPoints = new Vector3[worldPoints.Count];
                for (int i = 0; i < worldPoints.Count; i++)
                {
                    localPoints[i] = probeObj.transform.InverseTransformPoint(worldPoints[i]);
                }

                Undo.RecordObject(group, "Set Probe Positions");
                group.probePositions = localPoints;
                count++;
            }

            return count;
        }

        // ====================================================================================
        // HELPER METHODS: REFLECTION PROBE SETUP
        // ====================================================================================
        private static int SetupReflectionProbes(GameObject labRoot)
        {
            int count = 0;
            string[] reflectionRooms = new[]
            {
                "01_StartRoom",
                "03_Arena1_Zombie",
                "05_Arena2_Gravity",
                "07_Arena3_Illusion",
                "07c_Arena4_Maze_Student4",
                "08_ExitRoom"
            };

            foreach (string roomName in reflectionRooms)
            {
                Transform roomT = labRoot.transform.Find(roomName);
                if (roomT == null) continue;

                Bounds bounds = GetCompositeBounds(roomT);
                if (bounds.size.sqrMagnitude < 0.1f) continue;

                Transform probeChild = roomT.Find("ReflectionProbe");
                GameObject probeObj;
                if (probeChild == null)
                {
                    probeObj = new GameObject("ReflectionProbe");
                    probeObj.transform.SetParent(roomT, false);
                    Undo.RegisterCreatedObjectUndo(probeObj, "Create ReflectionProbe");
                }
                else
                {
                    probeObj = probeChild.gameObject;
                    Undo.RecordObject(probeObj, "Update ReflectionProbe");
                }

                ReflectionProbe probe = probeObj.GetComponent<ReflectionProbe>();
                if (probe == null) probe = Undo.AddComponent<ReflectionProbe>(probeObj);

                Undo.RecordObject(probeObj.transform, "Position ReflectionProbe");
                probeObj.transform.position = bounds.center;

                Undo.RecordObject(probe, "Configure ReflectionProbe");
                probe.mode = ReflectionProbeMode.Baked;
                probe.boxProjection = true;
                probe.resolution = 128;
                probe.size = bounds.size;
                probe.center = Vector3.zero;

                count++;
            }

            return count;
        }

        // ====================================================================================
        // UTILITIES
        // ====================================================================================
        private static List<Light> GetSceneLights(UnityEngine.SceneManagement.Scene scene)
        {
            List<Light> result = new List<Light>();
            Light[] all = Resources.FindObjectsOfTypeAll<Light>();
            foreach (var l in all)
            {
                if (l != null && l.gameObject.scene == scene && !EditorUtility.IsPersistent(l))
                {
                    result.Add(l);
                }
            }
            return result;
        }

        private static bool IsInsideMaze(Transform t, Transform labRoot)
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

        private static Bounds GetRoomFloorBounds(Transform room)
        {
            Bounds b = new Bounds();
            bool hasFloor = false;

            foreach (Renderer r in room.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null && r.gameObject.name.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!hasFloor)
                    {
                        b = r.bounds;
                        hasFloor = true;
                    }
                    else
                    {
                        b.Encapsulate(r.bounds);
                    }
                }
            }

            return b;
        }

        private static Bounds GetCompositeBounds(Transform room)
        {
            Bounds b = new Bounds();
            bool init = false;

            foreach (Renderer r in room.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!init)
                {
                    b = r.bounds;
                    init = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            if (!init)
            {
                b = new Bounds(room.position, new Vector3(10f, 5f, 10f));
            }

            return b;
        }
    }
}
#endif
