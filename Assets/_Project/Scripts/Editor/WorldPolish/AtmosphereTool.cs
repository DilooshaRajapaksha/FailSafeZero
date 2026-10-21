using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FailSafeZero.EditorTools.WorldPolish
{
    /// <summary>
    /// Student 1 (World Builder) Atmosphere, Lighting & Post-Processing Tool.
    /// Builds ceilings with NavMeshModifier.ignoreFromBuild, adjusts light clearances,
    /// sets dark underground ambient lighting and fog, establishes URP Post-Processing Volume,
    /// and checks player camera configuration.
    /// </summary>
    public static class AtmosphereTool
    {
        private const string CeilingMatPath = "Assets/_Project/Materials/SciFi_Ceiling_Mat.mat";
        private const string SettingsDirPath = "Assets/_Project/Settings/Environment";
        private const string ProfilePath = "Assets/_Project/Settings/Environment/LabPostFX.asset";

        [MenuItem("FailSafeZero/World/Atmosphere - Apply")]
        public static void ApplyAtmosphere()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject labRoot = GameObject.Find("Lab_Environment_Root");
            if (labRoot == null)
            {
                Debug.LogError("[WorldPolish] Lab_Environment_Root not found in scene!");
                return;
            }

            Debug.Log("[WorldPolish] ==================== APPLYING ATMOSPHERE ====================");

            // -----------------------------------------------------------------
            // A. CEILINGS
            // -----------------------------------------------------------------
            Material ceilingMat = GetOrCreateCeilingMaterial();

            string[] roomNames = new string[]
            {
                "01_StartRoom",
                "02_Corridor_A",
                "03_Arena1_Zombie",
                "04_Corridor_B",
                "05_Arena2_Gravity",
                "06_Corridor_C",
                "07_Arena3_Illusion",
                "07b_Corridor_D_ToMaze",
                "08_ExitRoom"
            };

            int ceilingsBuilt = 0;

            // 1. Standard Rooms
            foreach (string rName in roomNames)
            {
                Transform room = labRoot.transform.Find(rName);
                if (room == null)
                {
                    Debug.LogWarning($"[WorldPolish] Room '{rName}' not found under Lab_Environment_Root, skipping.");
                    continue;
                }

                Transform floor = room.Find("Floor");
                if (floor == null)
                {
                    floor = room.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0);
                }

                if (floor == null)
                {
                    Debug.LogWarning($"[WorldPolish] Floor not found in '{rName}', skipping ceiling.");
                    continue;
                }

                Renderer floorRend = floor.GetComponent<Renderer>();
                Vector3 floorCenter = floorRend != null ? floorRend.bounds.center : floor.position;
                Vector3 floorSize = floorRend != null ? floorRend.bounds.size : floor.lossyScale;

                // Wall height from Wall_West or any wall
                float wallTopY = floorCenter.y + 4.0f;
                Transform wallWest = room.Find("Wall_West");
                if (wallWest != null && wallWest.GetComponent<Renderer>() != null)
                {
                    wallTopY = wallWest.GetComponent<Renderer>().bounds.max.y;
                }
                else
                {
                    var wallRends = room.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("Wall")).ToArray();
                    if (wallRends.Length > 0)
                    {
                        wallTopY = wallRends.Max(r => r.bounds.max.y);
                    }
                }

                // Remove old ceiling if re-running (idempotent)
                Transform oldCeiling = room.Find("Ceiling");
                if (oldCeiling != null)
                {
                    Undo.DestroyObjectImmediate(oldCeiling.gameObject);
                }

                // Ceiling cube: thickness 0.5m, bottom face flush with wall top
                Vector3 ceilingPos = new Vector3(floorCenter.x, wallTopY + 0.25f, floorCenter.z);
                Vector3 ceilingScale = new Vector3(floorSize.x, 0.5f, floorSize.z);

                GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ceiling.name = "Ceiling";
                ceiling.transform.SetParent(room);
                ceiling.transform.position = ceilingPos;
                ceiling.transform.localScale = ceilingScale;
                ceiling.isStatic = true;
                ceiling.GetComponent<Renderer>().sharedMaterial = ceilingMat;

                NavMeshModifier mod = Undo.AddComponent<NavMeshModifier>(ceiling);
                mod.ignoreFromBuild = true;

                Undo.RegisterCreatedObjectUndo(ceiling, $"Create Ceiling in {rName}");
                ceilingsBuilt++;
            }

            // 2. Connector Hallway (07d_Connector_ToMaze)
            Transform connector = labRoot.transform.Find("07d_Connector_ToMaze");
            if (connector != null)
            {
                Transform oldConnCeiling = connector.Find("Ceiling_Group");
                if (oldConnCeiling != null) Undo.DestroyObjectImmediate(oldConnCeiling.gameObject);

                GameObject connCeilingGroup = new GameObject("Ceiling_Group");
                connCeilingGroup.transform.SetParent(connector);
                connCeilingGroup.transform.position = Vector3.zero;

                foreach (Transform child in connector)
                {
                    if (child.name.Contains("Floor"))
                    {
                        Renderer fRend = child.GetComponent<Renderer>();
                        if (fRend != null)
                        {
                            Bounds fb = fRend.bounds;
                            GameObject cCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            cCube.name = $"Ceiling_{child.name}";
                            cCube.transform.SetParent(connCeilingGroup.transform);
                            cCube.transform.position = new Vector3(fb.center.x, 4.0f + 0.25f, fb.center.z);
                            cCube.transform.localScale = new Vector3(fb.size.x, 0.5f, fb.size.z);
                            cCube.isStatic = true;
                            cCube.GetComponent<Renderer>().sharedMaterial = ceilingMat;

                            NavMeshModifier cMod = Undo.AddComponent<NavMeshModifier>(cCube);
                            cMod.ignoreFromBuild = true;
                        }
                    }
                }
                Undo.RegisterCreatedObjectUndo(connCeilingGroup, "Create Connector Ceilings");
                ceilingsBuilt++;
            }

            // 3. Maze Ceiling (under 07c_Arena4_Maze_Student4)
            Transform holder = labRoot.transform.Find("07c_Arena4_Maze_Student4");
            if (holder != null)
            {
                Transform mazeFloor = FindChildRecursive(holder, "Maze_floor");
                if (mazeFloor != null)
                {
                    Bounds floorBounds = mazeFloor.GetComponent<Renderer>().bounds;

                    float mazeWallTopY = 5.0f;
                    Transform mazeWalls = FindChildRecursive(holder, "Maze_Walls");
                    if (mazeWalls != null)
                    {
                        var wRends = mazeWalls.GetComponentsInChildren<Renderer>();
                        if (wRends.Length > 0) mazeWallTopY = wRends.Max(r => r.bounds.max.y);
                    }

                    Transform oldMazeCeiling = holder.Find("Ceiling");
                    if (oldMazeCeiling != null) Undo.DestroyObjectImmediate(oldMazeCeiling.gameObject);

                    GameObject mazeCeiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    mazeCeiling.name = "Ceiling";
                    mazeCeiling.transform.SetParent(holder);
                    mazeCeiling.transform.position = new Vector3(floorBounds.center.x, mazeWallTopY + 0.25f, floorBounds.center.z);
                    mazeCeiling.transform.localScale = new Vector3(floorBounds.size.x, 0.5f, floorBounds.size.z);
                    mazeCeiling.isStatic = true;
                    mazeCeiling.GetComponent<Renderer>().sharedMaterial = ceilingMat;

                    NavMeshModifier mod = Undo.AddComponent<NavMeshModifier>(mazeCeiling);
                    mod.ignoreFromBuild = true;

                    Undo.RegisterCreatedObjectUndo(mazeCeiling, "Create Maze Ceiling");
                    ceilingsBuilt++;
                }
            }

            Debug.Log($"[WorldPolish] Built {ceilingsBuilt} ceiling sections with SciFi_Ceiling_Mat and NavMeshModifier.ignoreFromBuild = true.");

            // -----------------------------------------------------------------
            // B. LIGHTS CLEARANCE ADJUSTMENT
            // -----------------------------------------------------------------
            int lightsMoved = 0;
            foreach (Transform room in labRoot.GetComponentsInChildren<Transform>(true))
            {
                if (room.parent != labRoot.transform && room != holder) continue;
                if (WorldPolishUtil.IsTeammateObject(room.gameObject)) continue;

                Transform ceiling = room.Find("Ceiling");
                if (ceiling == null) continue;

                float ceilingBottom = ceiling.position.y - 0.25f;
                float upperThreshold = ceilingBottom - 0.3f;
                float targetY = ceilingBottom - 0.5f;

                Light[] lights = room.GetComponentsInChildren<Light>(true);
                foreach (Light lt in lights)
                {
                    if (lt.type == LightType.Point && lt.transform.position.y >= upperThreshold)
                    {
                        float oldY = lt.transform.position.y;
                        Undo.RecordObject(lt.transform, "Lower Light below Ceiling");
                        lt.transform.position = new Vector3(lt.transform.position.x, targetY, lt.transform.position.z);
                        Debug.Log($"[WorldPolish] Lowered light '{lt.gameObject.name}' in '{room.name}' from y={oldY:F2}m to y={targetY:F2}m.");
                        lightsMoved++;
                    }
                }
            }
            Debug.Log($"[WorldPolish] Adjusted heights of {lightsMoved} point light(s) for ceiling clearance.");

            // -----------------------------------------------------------------
            // C. RENDER SETTINGS & DIRECTIONAL LIGHT
            // -----------------------------------------------------------------
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.08f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.06f);
            RenderSettings.fogDensity = 0.025f;

            Debug.Log("[WorldPolish] RenderSettings configured: Ambient Flat (0.05, 0.06, 0.08), Fog ExponentialSquared density=0.025 color=(0.03, 0.04, 0.06).");

            // Turn off Directional Light (underground facility)
            Light dirLight = GameObject.Find("Directional Light")?.GetComponent<Light>();
            if (dirLight == null)
            {
                dirLight = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            }

            if (dirLight != null)
            {
                Undo.RecordObject(dirLight, "Turn off Directional Light");
                dirLight.intensity = 0f;
                Debug.Log("[WorldPolish] Directional Light intensity set to 0.00 (underground facility).");
            }
            else
            {
                Debug.LogWarning("[WorldPolish] Directional Light not found in scene.");
            }

            // -----------------------------------------------------------------
            // D. POST-PROCESSING VOLUME & PROFILE
            // -----------------------------------------------------------------
            VolumeProfile profile = GetOrCreatePostFXProfile();

            Transform postFxObj = labRoot.transform.Find("PostFX_Volume");
            if (postFxObj == null)
            {
                GameObject go = new GameObject("PostFX_Volume");
                go.transform.SetParent(labRoot.transform);
                Undo.RegisterCreatedObjectUndo(go, "Create PostFX_Volume");
                postFxObj = go.transform;
            }

            Volume volume = postFxObj.GetComponent<Volume>();
            if (volume == null)
            {
                volume = Undo.AddComponent<Volume>(postFxObj.gameObject);
            }
            else
            {
                Undo.RecordObject(volume, "Configure Volume");
            }

            volume.isGlobal = true;
            volume.profile = profile;
            Debug.Log($"[WorldPolish] Global Volume assigned at 'Lab_Environment_Root/PostFX_Volume' with profile '{ProfilePath}'.");

            // -----------------------------------------------------------------
            // E. CHECK FIRSTPERSONPLAYER CAMERA
            // -----------------------------------------------------------------
            GameObject player = GameObject.Find("FirstPersonPlayer") ?? GameObject.FindWithTag("Player");
            if (player != null)
            {
                Camera cam = player.GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    UniversalAdditionalCameraData camData = cam.GetComponent<UniversalAdditionalCameraData>();
                    if (camData != null)
                    {
                        if (!camData.renderPostProcessing)
                        {
                            Debug.LogWarning("[WorldPolish] [TEAMMATE NOTICE] FirstPersonPlayer camera has 'Post Processing' disabled. Please ask Student 2/teammate to enable 'Post Processing' on FirstPersonPlayer's Camera inspector (UniversalAdditionalCameraData.renderPostProcessing = true).");
                        }
                        else
                        {
                            Debug.Log("[WorldPolish] PASS: FirstPersonPlayer camera has Post Processing enabled.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[WorldPolish] FirstPersonPlayer camera does not have UniversalAdditionalCameraData component.");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[WorldPolish] FirstPersonPlayer not found for camera check.");
            }

            // -----------------------------------------------------------------
            // F. REBUILD NAVMESH
            // -----------------------------------------------------------------
            WorldPolishUtil.BakeLabNavMesh();

            // Mark scene dirty (no auto-save; user saves)
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[WorldPolish] Atmosphere applied successfully. Scene marked dirty (remember to save with Cmd+S).");
            Debug.Log("[WorldPolish] ================================================================");
        }

        // =========================================================================
        // PRIVATE HELPERS
        // =========================================================================
        private static Material GetOrCreateCeilingMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(CeilingMatPath);
            if (mat == null)
            {
                string dir = "Assets/_Project/Materials";
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "Materials");
                }

                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(urpLit);
                mat.color = new Color(0.10f, 0.11f, 0.13f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.2f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);

                AssetDatabase.CreateAsset(mat, CeilingMatPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[WorldPolish] Created SciFi_Ceiling_Mat at '{CeilingMatPath}'.");
            }
            return mat;
        }

        private static VolumeProfile GetOrCreatePostFXProfile()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");
            }
            if (!AssetDatabase.IsValidFolder(SettingsDirPath))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Settings", "Environment");
            }

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            // 1. Bloom
            Bloom bloom;
            if (!profile.TryGet(out bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.8f);

            // 2. Vignette
            Vignette vignette;
            if (!profile.TryGet(out vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }
            vignette.intensity.Override(0.3f);

            // 3. Tonemapping
            Tonemapping tonemapping;
            if (!profile.TryGet(out tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
            }
            tonemapping.mode.Override(TonemappingMode.ACES);

            // 4. Color Adjustments
            ColorAdjustments colorAdj;
            if (!profile.TryGet(out colorAdj))
            {
                colorAdj = profile.Add<ColorAdjustments>(true);
            }
            colorAdj.contrast.Override(10.0f);
            colorAdj.saturation.Override(-5.0f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            return profile;
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null) return null;
            if (parent.name.Equals(childName, StringComparison.OrdinalIgnoreCase)) return parent;

            foreach (Transform child in parent)
            {
                Transform found = FindChildRecursive(child, childName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
