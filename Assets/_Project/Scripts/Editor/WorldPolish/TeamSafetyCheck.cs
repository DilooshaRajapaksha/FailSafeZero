using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FailSafeZero.EditorTools.WorldPolish
{
    /// <summary>
    /// Student 1 (World Builder) Team Safety Check Tool.
    /// Provides non-destructive scene safety baseline capture, baseline comparison,
    /// and NavMesh health verification.
    /// Does NOT modify the active scene or any asset.
    /// </summary>
    public static class TeamSafetyCheck
    {
        private const string BaselineDirectory = "Library/FailSafeZero";
        private const string BaselineFilePath = "Library/FailSafeZero/safety_baseline.json";

        [Serializable]
        public class Vector3Data
        {
            public float x;
            public float y;
            public float z;

            public Vector3Data() { }
            public Vector3Data(Vector3 v)
            {
                x = v.x;
                y = v.y;
                z = v.z;
            }

            public Vector3 ToVector3() => new Vector3(x, y, z);
            public override string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
        }

        [Serializable]
        public class ComponentRecord
        {
            public string childPath;
            public string typeName;
        }

        [Serializable]
        public class RendererMaterialRecord
        {
            public string childPath;
            public List<string> materialNames = new List<string>();
        }

        [Serializable]
        public class RecordedObject
        {
            public string hierarchyPath;
            public string name;
            public bool activeSelf;
            public Vector3Data position;
            public Vector3Data rotationEuler;
            public Vector3Data lossyScale;
            public List<ComponentRecord> components = new List<ComponentRecord>();
            public string prefabAssetPath;
            public int prefabOverrideCount;
            public bool isStatic;
            public List<RendererMaterialRecord> rendererMaterials = new List<RendererMaterialRecord>();
        }

        [Serializable]
        public class NavMeshAgentRecord
        {
            public string hierarchyPath;
            public string name;
            public Vector3Data position;
        }

        [Serializable]
        public class SafetyBaselineData
        {
            public string sceneName;
            public string savedTimestamp;
            public List<RecordedObject> rootObjects = new List<RecordedObject>();
            public List<RecordedObject> teammateObjects = new List<RecordedObject>();
            public List<NavMeshAgentRecord> navMeshAgents = new List<NavMeshAgentRecord>();
        }

        // =========================================================================
        // 1. SAVE BASELINE
        // =========================================================================
        [MenuItem("FailSafeZero/Safety/1. Save Baseline")]
        public static void SaveBaseline()
        {
            var scene = EditorSceneManager.GetActiveScene();
            SafetyBaselineData baseline = new SafetyBaselineData
            {
                sceneName = scene.name,
                savedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // 1. Record every ROOT GameObject except Lab_Environment_Root, LevelBuilder, and Directional Light
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Lab_Environment_Root" ||
                    root.name == "LevelBuilder" ||
                    root.name == "Directional Light")
                {
                    continue;
                }

                baseline.rootObjects.Add(CaptureObjectRecord(root, false));
            }

            // 2. Record every GameObject in the scene that has a NavMeshAgent
            NavMeshAgent[] agents = UnityEngine.Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var agent in agents)
            {
                baseline.navMeshAgents.Add(new NavMeshAgentRecord
                {
                    hierarchyPath = WorldPolishUtil.GetHierarchyPath(agent.gameObject),
                    name = agent.gameObject.name,
                    position = new Vector3Data(agent.transform.position)
                });
            }

            // 3. Record every teammate object inside Lab_Environment_Root
            GameObject labRoot = GameObject.Find("Lab_Environment_Root");
            if (labRoot != null)
            {
                foreach (Transform t in labRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (t.gameObject == labRoot) continue;

                    if (WorldPolishUtil.IsTeammateObject(t.gameObject))
                    {
                        baseline.teammateObjects.Add(CaptureObjectRecord(t.gameObject, true));
                    }
                }
            }

            // Save JSON to Library/FailSafeZero/safety_baseline.json
            if (!Directory.Exists(BaselineDirectory))
            {
                Directory.CreateDirectory(BaselineDirectory);
            }

            string json = JsonUtility.ToJson(baseline, true);
            File.WriteAllText(BaselineFilePath, json);

            int totalObjects = baseline.rootObjects.Count + baseline.teammateObjects.Count + baseline.navMeshAgents.Count;
            Debug.Log($"[WorldPolish] Saved safety baseline to {BaselineFilePath}: " +
                      $"{baseline.rootObjects.Count} root objects, " +
                      $"{baseline.teammateObjects.Count} teammate objects inside Lab_Environment_Root, " +
                      $"{baseline.navMeshAgents.Count} NavMeshAgents recorded ({totalObjects} total).");
        }

        // =========================================================================
        // 2. COMPARE WITH BASELINE
        // =========================================================================
        [MenuItem("FailSafeZero/Safety/2. Compare With Baseline")]
        public static void CompareWithBaseline()
        {
            if (!File.Exists(BaselineFilePath))
            {
                Debug.LogError($"[WorldPolish][SAFETY] No baseline file found at {BaselineFilePath}. Run 'FailSafeZero/Safety/1. Save Baseline' first.");
                return;
            }

            string json = File.ReadAllText(BaselineFilePath);
            SafetyBaselineData baseline = JsonUtility.FromJson<SafetyBaselineData>(json);
            if (baseline == null)
            {
                Debug.LogError($"[WorldPolish][SAFETY] Failed to parse baseline JSON at {BaselineFilePath}.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            int problems = 0;

            // A. Compare recorded root objects
            foreach (var rec in baseline.rootObjects)
            {
                GameObject cur = WorldPolishUtil.FindByHierarchyPath(rec.hierarchyPath);
                if (cur == null)
                {
                    Debug.LogError($"[WorldPolish][SAFETY] FAIL {rec.name}: missing from scene (expected root at '{rec.hierarchyPath}')");
                    problems++;
                    continue;
                }

                CompareObject(rec, cur, isTeammate: false, ref problems);
            }

            // B. Detect new root objects
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Lab_Environment_Root" ||
                    root.name == "LevelBuilder" ||
                    root.name == "Directional Light")
                {
                    continue;
                }

                bool existsInBaseline = baseline.rootObjects.Any(r => r.name == root.name && r.hierarchyPath == root.name);
                if (!existsInBaseline)
                {
                    Debug.LogError($"[WorldPolish][SAFETY] FAIL New unexpected root GameObject appeared: '{root.name}'");
                    problems++;
                }
            }

            // C. Compare teammate objects inside Lab_Environment_Root
            foreach (var rec in baseline.teammateObjects)
            {
                GameObject cur = WorldPolishUtil.FindByHierarchyPath(rec.hierarchyPath);
                if (cur == null)
                {
                    Debug.LogError($"[WorldPolish][SAFETY] FAIL {rec.name}: missing from scene (expected at '{rec.hierarchyPath}')");
                    problems++;
                    continue;
                }

                CompareObject(rec, cur, isTeammate: true, ref problems);
            }

            // D. Compare NavMeshAgent positions
            foreach (var agentRec in baseline.navMeshAgents)
            {
                GameObject cur = WorldPolishUtil.FindByHierarchyPath(agentRec.hierarchyPath);
                if (cur == null)
                {
                    Debug.LogError($"[WorldPolish][SAFETY] FAIL NavMeshAgent '{agentRec.name}': missing from scene (path: '{agentRec.hierarchyPath}')");
                    problems++;
                    continue;
                }

                if (cur.GetComponent<NavMeshAgent>() == null)
                {
                    Debug.LogError($"[WorldPolish][SAFETY] FAIL NavMeshAgent '{agentRec.name}': NavMeshAgent component missing on GameObject");
                    problems++;
                    continue;
                }

                float dist = Vector3.Distance(agentRec.position.ToVector3(), cur.transform.position);
                if (dist > 0.01f)
                {
                    Debug.LogError($"[WorldPolish][SAFETY] FAIL NavMeshAgent '{agentRec.name}': position was {agentRec.position} now ({cur.transform.position.x:F3}, {cur.transform.position.y:F3}, {cur.transform.position.z:F3}) (dist: {dist:F3}m)");
                    problems++;
                }
            }

            // Final Summary
            if (problems == 0)
            {
                Debug.Log("[WorldPolish] SAFETY RESULT: ALL PASS");
            }
            else
            {
                Debug.LogError($"[WorldPolish] SAFETY RESULT: {problems} problems");
            }
        }

        // =========================================================================
        // 3. NAVMESH HEALTH CHECK
        // =========================================================================
        [MenuItem("FailSafeZero/Safety/3. NavMesh Health Check")]
        public static void NavMeshHealthCheck()
        {
            // 1. Check all NavMeshAgents within 1.5m
            NavMeshAgent[] agents = UnityEngine.Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            Debug.Log($"[WorldPolish][NavMesh] Checking {agents.Length} NavMeshAgent(s)...");
            foreach (var agent in agents)
            {
                if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                {
                    float offset = Vector3.Distance(agent.transform.position, hit.position);
                    Debug.Log($"[WorldPolish][NavMesh] PASS {agent.gameObject.name} on NavMesh (offset: {offset:F2}m)");
                }
                else
                {
                    Debug.LogError($"[WorldPolish][NavMesh] FAIL {agent.gameObject.name} not within 1.5m of NavMesh (position: {agent.transform.position})");
                }
            }

            // 2. Find player
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("FirstPersonPlayer");

            if (player == null)
            {
                Debug.LogError("[WorldPolish][NavMesh] FAIL: Player object ('FirstPersonPlayer' or tagged 'Player') not found in scene!");
            }
            else
            {
                Vector3 playerPos = player.transform.position;
                if (NavMesh.SamplePosition(playerPos, out NavMeshHit playerHit, 2.0f, NavMesh.AllAreas))
                {
                    playerPos = playerHit.position;
                }

                // Rooms to test
                string[] targetRooms = new string[]
                {
                    "03_Arena1_Zombie",
                    "05_Arena2_Gravity",
                    "07_Arena3_Illusion",
                    "the Maze_floor inside Maze_Arena",
                    "08_ExitRoom"
                };

                foreach (string roomKey in targetRooms)
                {
                    Vector3? targetCenter = FindRoomFloorCenter(roomKey);
                    if (!targetCenter.HasValue)
                    {
                        Debug.LogWarning($"[WorldPolish][NavMesh] Room or floor '{roomKey}' not found in scene, skipping path check.");
                        continue;
                    }

                    Vector3 targetPos = targetCenter.Value;
                    if (NavMesh.SamplePosition(targetPos, out NavMeshHit targetHit, 2.5f, NavMesh.AllAreas))
                    {
                        targetPos = targetHit.position;
                    }

                    NavMeshPath path = new NavMeshPath();
                    NavMesh.CalculatePath(playerPos, targetPos, NavMesh.AllAreas, path);

                    float length = 0f;
                    if (path.corners != null && path.corners.Length > 1)
                    {
                        for (int i = 0; i < path.corners.Length - 1; i++)
                        {
                            length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                        }
                    }

                    Debug.Log($"[WorldPolish][NavMesh] Path Player -> {roomKey}: {path.status} (Length: {length:F2}m)");
                }
            }

            // 3. Print total NavMesh vertices and triangles
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            int vertCount = tri.vertices != null ? tri.vertices.Length : 0;
            int triCount = tri.indices != null ? tri.indices.Length / 3 : 0;
            Debug.Log($"[WorldPolish][NavMesh] NavMesh Triangulation: {vertCount} vertices, {triCount} triangles (indices count: {(tri.indices != null ? tri.indices.Length : 0)}).");
        }

        // =========================================================================
        // PRIVATE HELPERS
        // =========================================================================
        private static RecordedObject CaptureObjectRecord(GameObject go, bool isTeammate)
        {
            RecordedObject rec = new RecordedObject
            {
                hierarchyPath = WorldPolishUtil.GetHierarchyPath(go),
                name = go.name,
                activeSelf = go.activeSelf,
                position = new Vector3Data(go.transform.position),
                rotationEuler = new Vector3Data(go.transform.eulerAngles),
                lossyScale = new Vector3Data(go.transform.lossyScale),
                components = RecordComponents(go),
                prefabAssetPath = GetPrefabAssetPath(go),
                prefabOverrideCount = GetPrefabOverrideCount(go),
                isStatic = go.isStatic
            };

            if (isTeammate)
            {
                rec.rendererMaterials = RecordMaterials(go);
            }

            return rec;
        }

        private static List<ComponentRecord> RecordComponents(GameObject root)
        {
            List<ComponentRecord> list = new List<ComponentRecord>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string relPath = WorldPolishUtil.GetRelativePath(t, root.transform);
                foreach (Component c in t.GetComponents<Component>())
                {
                    if (c == null)
                    {
                        list.Add(new ComponentRecord { childPath = relPath, typeName = "(Missing Script)" });
                    }
                    else
                    {
                        list.Add(new ComponentRecord { childPath = relPath, typeName = c.GetType().FullName ?? c.GetType().Name });
                    }
                }
            }
            return list;
        }

        private static List<RendererMaterialRecord> RecordMaterials(GameObject root)
        {
            List<RendererMaterialRecord> list = new List<RendererMaterialRecord>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                string relPath = WorldPolishUtil.GetRelativePath(r.transform, root.transform);
                RendererMaterialRecord rec = new RendererMaterialRecord { childPath = relPath };
                Material[] sharedMats = r.sharedMaterials;
                if (sharedMats != null)
                {
                    foreach (var m in sharedMats)
                    {
                        rec.materialNames.Add(m != null ? m.name : "None");
                    }
                }
                list.Add(rec);
            }
            return list;
        }

        private static string GetPrefabAssetPath(GameObject go)
        {
            if (!PrefabUtility.IsPartOfAnyPrefab(go)) return string.Empty;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (string.IsNullOrEmpty(path))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (source != null)
                {
                    path = AssetDatabase.GetAssetPath(source);
                }
            }
            return path ?? string.Empty;
        }

        private static int GetPrefabOverrideCount(GameObject go)
        {
            if (!PrefabUtility.IsPartOfAnyPrefab(go)) return 0;
            try
            {
                var objOverrides = PrefabUtility.GetObjectOverrides(go, false);
                var added = PrefabUtility.GetAddedComponents(go);
                var removed = PrefabUtility.GetRemovedComponents(go);
                return (objOverrides != null ? objOverrides.Count : 0) +
                       (added != null ? added.Count : 0) +
                       (removed != null ? removed.Count : 0);
            }
            catch
            {
                return 0;
            }
        }

        private static void CompareObject(RecordedObject rec, GameObject cur, bool isTeammate, ref int problems)
        {
            bool hasFail = false;

            // activeSelf
            if (rec.activeSelf != cur.activeSelf)
            {
                Debug.LogError($"[WorldPolish][SAFETY] FAIL {rec.name}: activeSelf was {rec.activeSelf} now {cur.activeSelf}");
                hasFail = true;
            }

            // position (tolerance 0.01)
            float posDist = Vector3.Distance(rec.position.ToVector3(), cur.transform.position);
            if (posDist > 0.01f)
            {
                Debug.LogError($"[WorldPolish][SAFETY] FAIL {rec.name}: position was {rec.position} now ({cur.transform.position.x:F3}, {cur.transform.position.y:F3}, {cur.transform.position.z:F3}) (dist: {posDist:F3}m)");
                hasFail = true;
            }

            // rotation
            float rotDist = Vector3.Distance(rec.rotationEuler.ToVector3(), cur.transform.eulerAngles);
            if (rotDist > 0.05f)
            {
                Debug.LogError($"[WorldPolish][SAFETY] FAIL {rec.name}: rotation was {rec.rotationEuler} now ({cur.transform.eulerAngles.x:F3}, {cur.transform.eulerAngles.y:F3}, {cur.transform.eulerAngles.z:F3})");
                hasFail = true;
            }

            // lossy scale
            float scaleDist = Vector3.Distance(rec.lossyScale.ToVector3(), cur.transform.lossyScale);
            if (scaleDist > 0.01f)
            {
                Debug.LogError($"[WorldPolish][SAFETY] FAIL {rec.name}: lossy scale was {rec.lossyScale} now ({cur.transform.lossyScale.x:F3}, {cur.transform.lossyScale.y:F3}, {cur.transform.lossyScale.z:F3})");
                hasFail = true;
            }

            // Prefab source
            string curPrefabPath = GetPrefabAssetPath(cur);
            if (rec.prefabAssetPath != curPrefabPath)
            {
                Debug.LogError($"[WorldPolish][SAFETY] FAIL {rec.name}: prefab source was '{rec.prefabAssetPath}' now '{curPrefabPath}'");
                hasFail = true;
            }

            // Prefab overrides
            int curOverrides = GetPrefabOverrideCount(cur);
            if (rec.prefabOverrideCount != curOverrides)
            {
                Debug.LogError($"[WorldPolish][SAFETY] FAIL {rec.name}: prefab overrides was {rec.prefabOverrideCount} now {curOverrides}");
                hasFail = true;
            }

            // Components
            List<ComponentRecord> curComps = RecordComponents(cur);
            if (!AreComponentListsEqual(rec.components, curComps, out string compDiff))
            {
                Debug.LogError($"[WorldPolish][SAFETY] FAIL {rec.name}: components was [{string.Join(", ", rec.components.Select(c => c.typeName))}] now [{string.Join(", ", curComps.Select(c => c.typeName))}] ({compDiff})");
                hasFail = true;
            }

            // Teammate-specific checks: static flags and materials
            if (isTeammate)
            {
                if (rec.isStatic != cur.isStatic)
                {
                    Debug.LogError($"[WorldPolish][SAFETY] FAIL {rec.name}: isStatic was {rec.isStatic} now {cur.isStatic}");
                    hasFail = true;
                }

                List<RendererMaterialRecord> curMats = RecordMaterials(cur);
                if (!AreMaterialListsEqual(rec.rendererMaterials, curMats, out string matDiff))
                {
                    Debug.LogError($"[WorldPolish][SAFETY] FAIL {rec.name}: materials changed ({matDiff})");
                    hasFail = true;
                }
            }

            if (hasFail)
            {
                problems++;
            }
            else
            {
                Debug.Log($"[WorldPolish][SAFETY] PASS {rec.name}");
            }
        }

        private static bool AreComponentListsEqual(List<ComponentRecord> a, List<ComponentRecord> b, out string diff)
        {
            if (a.Count != b.Count)
            {
                diff = $"count was {a.Count} now {b.Count}";
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].childPath != b[i].childPath || a[i].typeName != b[i].typeName)
                {
                    diff = $"at index {i}: was [{a[i].childPath}: {a[i].typeName}] now [{b[i].childPath}: {b[i].typeName}]";
                    return false;
                }
            }
            diff = string.Empty;
            return true;
        }

        private static bool AreMaterialListsEqual(List<RendererMaterialRecord> a, List<RendererMaterialRecord> b, out string diff)
        {
            if (a.Count != b.Count)
            {
                diff = $"renderer count was {a.Count} now {b.Count}";
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].childPath != b[i].childPath)
                {
                    diff = $"renderer path mismatch was {a[i].childPath} now {b[i].childPath}";
                    return false;
                }
                if (a[i].materialNames.Count != b[i].materialNames.Count)
                {
                    diff = $"{a[i].childPath} material count was {a[i].materialNames.Count} now {b[i].materialNames.Count}";
                    return false;
                }
                for (int j = 0; j < a[i].materialNames.Count; j++)
                {
                    if (a[i].materialNames[j] != b[i].materialNames[j])
                    {
                        diff = $"{a[i].childPath} material was '{a[i].materialNames[j]}' now '{b[i].materialNames[j]}'";
                        return false;
                    }
                }
            }
            diff = string.Empty;
            return true;
        }

        private static Vector3? FindRoomFloorCenter(string roomIdentifier)
        {
            GameObject labRoot = GameObject.Find("Lab_Environment_Root");
            Transform rootSearch = labRoot != null ? labRoot.transform : null;

            if (roomIdentifier == "the Maze_floor inside Maze_Arena")
            {
                GameObject mazeArena = GameObject.Find("Maze_Arena");
                if (mazeArena != null)
                {
                    Transform mazeFloor = mazeArena.transform.Find("Maze_floor");
                    if (mazeFloor == null)
                    {
                        foreach (Transform child in mazeArena.GetComponentsInChildren<Transform>(true))
                        {
                            if (child.name.Equals("Maze_floor", StringComparison.OrdinalIgnoreCase))
                            {
                                mazeFloor = child;
                                break;
                            }
                        }
                    }

                    if (mazeFloor != null)
                    {
                        return GetCenterOfObject(mazeFloor.gameObject);
                    }
                }

                if (rootSearch != null)
                {
                    Transform holder = rootSearch.Find("07c_Arena4_Maze_Student4");
                    if (holder != null)
                    {
                        Transform floorBase = holder.Find("Maze_WalkableFloor_Base");
                        if (floorBase != null) return GetCenterOfObject(floorBase.gameObject);
                    }
                }
                return null;
            }

            Transform roomTransform = null;
            if (rootSearch != null)
            {
                roomTransform = rootSearch.Find(roomIdentifier);
            }
            if (roomTransform == null)
            {
                GameObject roomObj = GameObject.Find(roomIdentifier);
                if (roomObj != null) roomTransform = roomObj.transform;
            }

            if (roomTransform == null) return null;

            Transform floor = roomTransform.Find("Floor");
            if (floor == null)
            {
                foreach (Transform child in roomTransform.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        floor = child;
                        break;
                    }
                }
            }

            if (floor != null)
            {
                return GetCenterOfObject(floor.gameObject);
            }

            return roomTransform.position;
        }

        private static Vector3 GetCenterOfObject(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null) return col.bounds.center;
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null) return rend.bounds.center;
            return go.transform.position;
        }
    }
}
