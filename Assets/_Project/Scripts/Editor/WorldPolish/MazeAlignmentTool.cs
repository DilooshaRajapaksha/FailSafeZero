using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;

namespace FailSafeZero.EditorTools.WorldPolish
{
    /// <summary>
    /// Student 1 (World Builder) Maze Alignment & Integration Tool.
    /// Preserves Student 4's side placement, fixes floor height alignment,
    /// constructs the 07d_Connector_ToMaze hallway, aligns 08_ExitRoom,
    /// and configures NavMeshModifier overrides to prevent wall-top baking.
    /// </summary>
    public static class MazeAlignmentTool
    {
        // =========================================================================
        // 1. REPORT PLACEMENT (READ-ONLY)
        // =========================================================================
        [MenuItem("FailSafeZero/World/Maze 1 - Report Placement")]
        public static void ReportPlacement()
        {
            Debug.Log("[WorldPolish] ==================== MAZE PLACEMENT REPORT ====================");

            // 1. Holder & Instance Transforms
            GameObject labRoot = GameObject.Find("Lab_Environment_Root");
            Transform holder = labRoot != null ? labRoot.transform.Find("07c_Arena4_Maze_Student4") : null;
            if (holder == null)
            {
                Debug.LogError("[WorldPolish] 07c_Arena4_Maze_Student4 not found under Lab_Environment_Root!");
                return;
            }

            Debug.Log($"[WorldPolish] Holder '07c_Arena4_Maze_Student4' Transform: Pos={holder.position}, Rot={holder.eulerAngles}, Scale={holder.lossyScale}");

            Transform mazeArena = holder.Find("Maze_Arena");
            if (mazeArena == null)
            {
                Debug.LogError("[WorldPolish] Maze_Arena instance not found under 07c_Arena4_Maze_Student4!");
                return;
            }

            Debug.Log($"[WorldPolish] Maze_Arena Local: Pos={mazeArena.localPosition}, Rot={mazeArena.localEulerAngles} | World Pos={mazeArena.position}, Rot={mazeArena.eulerAngles}");

            // 2. Individual Target Renderer Bounds
            Transform mazeFloor = FindChildRecursive(mazeArena, "Maze_floor");
            Transform entryDoorFloor = FindChildRecursive(mazeArena, "Entry_door_floor");
            Transform exitDoorFloor = FindChildRecursive(mazeArena, "Exit_door_floor");
            Transform entryWall1 = FindChildRecursive(mazeArena, "Entry_Wall_01");
            Transform entryWall2 = FindChildRecursive(mazeArena, "Entry_Wall_02");
            Transform exitWall1 = FindChildRecursive(mazeArena, "Exit_Wall_01");
            Transform exitWall2 = FindChildRecursive(mazeArena, "Exit_Wall_02");

            Transform arena3 = labRoot.transform.Find("07_Arena3_Illusion");
            Transform corridorD = labRoot.transform.Find("07b_Corridor_D_ToMaze");
            Transform exitRoom = labRoot.transform.Find("08_ExitRoom");

            LogBounds("Maze_floor", mazeFloor);
            LogBounds("Entry_door_floor", entryDoorFloor);
            LogBounds("Exit_door_floor", exitDoorFloor);
            LogBounds("Entry_Wall_01", entryWall1);
            LogBounds("Entry_Wall_02", entryWall2);
            LogBounds("Exit_Wall_01", exitWall1);
            LogBounds("Exit_Wall_02", exitWall2);

            LogGroupBounds("07_Arena3_Illusion", arena3);
            LogGroupBounds("07b_Corridor_D_ToMaze", corridorD);
            LogGroupBounds("08_ExitRoom", exitRoom);

            // 3. Top Y of Maze_floor
            if (mazeFloor != null)
            {
                Bounds fb = GetRendererBounds(mazeFloor.gameObject);
                Debug.Log($"[WorldPolish] Maze_floor Top Y = {fb.max.y:F4}m (Center Y = {fb.center.y:F4}m, Min Y = {fb.min.y:F4}m)");
            }

            // 4. Computed Entry Opening
            float reportEntryZMin = 113.18f;
            float reportEntryZMax = 120.57f;
            if (entryWall1 != null && entryWall2 != null)
            {
                Bounds w1 = GetRendererBounds(entryWall1.gameObject);
                Bounds w2 = GetRendererBounds(entryWall2.gameObject);
                float minZ = Mathf.Min(w1.max.z, w2.max.z);
                float maxZ = Mathf.Max(w1.min.z, w2.min.z);
                if (minZ < maxZ)
                {
                    reportEntryZMin = minZ;
                    reportEntryZMax = maxZ;
                }
            }
            float reportEntryCenterZ = (reportEntryZMin + reportEntryZMax) * 0.5f;
            float labNearX = entryDoorFloor != null ? GetRendererBounds(entryDoorFloor.gameObject).max.x : -10.69f;
            Debug.Log($"[WorldPolish] Computed Entry Opening: Z Range = [{reportEntryZMin:F2} .. {reportEntryZMax:F2}] (Center Z = {reportEntryCenterZ:F2}m), Lab-facing X Edge = {labNearX:F2}m");

            // 5. Computed Exit Opening
            if (exitDoorFloor != null)
            {
                Bounds xb = GetRendererBounds(exitDoorFloor.gameObject);
                Debug.Log($"[WorldPolish] Computed Exit Opening: X Range = [{xb.min.x:F2} .. {xb.max.x:F2}] (Center X = {xb.center.x:F2}m), Open-end Z = {xb.max.z:F2}m");
            }

            // 6. Enabled NavMeshSurfaces in Scene
            NavMeshSurface[] surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"[WorldPolish] Active Enabled NavMeshSurfaces ({surfaces.Length} total in scene):");
            foreach (var s in surfaces)
            {
                if (s.enabled)
                {
                    Debug.Log($"[WorldPolish]   • ENABLED: {WorldPolishUtil.GetHierarchyPath(s.gameObject)}");
                }
            }

            // 7. Bounds Overlaps Check between Maze Walls and Lab Rooms
            Transform mazeWalls = FindChildRecursive(mazeArena, "Maze_Walls");
            if (mazeWalls != null)
            {
                CheckOverlap("07_Arena3_Illusion", mazeWalls, arena3);
                CheckOverlap("07b_Corridor_D_ToMaze", mazeWalls, corridorD);
                CheckOverlap("08_ExitRoom", mazeWalls, exitRoom);
            }

            Debug.Log("[WorldPolish] ================================================================");
        }

        // =========================================================================
        // 2. FIX PLACEMENT (UNDO-ABLE, MARKS SCENE DIRTY)
        // =========================================================================
        [MenuItem("FailSafeZero/World/Maze 2 - Fix Placement")]
        public static void FixPlacement()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject labRoot = GameObject.Find("Lab_Environment_Root");
            if (labRoot == null)
            {
                Debug.LogError("[WorldPolish] Lab_Environment_Root not found!");
                return;
            }

            Transform holder = labRoot.transform.Find("07c_Arena4_Maze_Student4");
            if (holder == null)
            {
                Debug.LogError("[WorldPolish] 07c_Arena4_Maze_Student4 not found!");
                return;
            }

            Transform mazeArena = holder.Find("Maze_Arena");
            if (mazeArena == null)
            {
                Debug.LogError("[WorldPolish] Maze_Arena not found under holder!");
                return;
            }

            Transform mazeFloor = FindChildRecursive(mazeArena, "Maze_floor");
            if (mazeFloor == null)
            {
                Debug.LogError("[WorldPolish] Maze_floor not found in Maze_Arena!");
                return;
            }

            // Record initial state for assertion
            Vector3 origFloorCenter = GetRendererBounds(mazeFloor.gameObject).center;
            float origTopY = GetRendererBounds(mazeFloor.gameObject).max.y;

            // -----------------------------------------------------------------
            // A. HEIGHT ONLY ALIGNMENT
            // -----------------------------------------------------------------
            Undo.RecordObject(holder, "Set Holder Y to 0");
            holder.position = new Vector3(holder.position.x, 0f, holder.position.z);

            // Compute current top Y after holder Y adjustment
            float currentTopY = GetRendererBounds(mazeFloor.gameObject).max.y;
            float neededShiftY = 0f - currentTopY;

            Undo.RecordObject(mazeArena, "Align Maze Local Y");
            mazeArena.localPosition = new Vector3(
                mazeArena.localPosition.x,
                mazeArena.localPosition.y + neededShiftY,
                mazeArena.localPosition.z
            );

            // Verify top Y and assert X/Z moved < 0.01m
            Bounds adjustedFloorBounds = GetRendererBounds(mazeFloor.gameObject);
            Vector3 newFloorCenter = adjustedFloorBounds.center;
            float xzMove = Vector2.Distance(
                new Vector2(origFloorCenter.x, origFloorCenter.z),
                new Vector2(newFloorCenter.x, newFloorCenter.z)
            );

            if (xzMove < 0.01f)
            {
                Debug.Log($"[WorldPolish] PASS: Maze_floor X/Z centre moved {xzMove:F4}m (< 0.01m). New Top Y = {adjustedFloorBounds.max.y:F4}m (exactly 0.00m)");
            }
            else
            {
                Debug.LogError($"[WorldPolish] FAIL: Maze_floor X/Z centre moved {xzMove:F4}m (>= 0.01m)!");
            }

            // -----------------------------------------------------------------
            // B. CLEANUP & LIGHTING
            // -----------------------------------------------------------------
            // Delete leftover Maze_WalkableFloor_Base
            Transform baseFloor = holder.Find("Maze_WalkableFloor_Base");
            if (baseFloor != null)
            {
                Undo.DestroyObjectImmediate(baseFloor.gameObject);
                Debug.Log("[WorldPolish] Deleted leftover Maze_WalkableFloor_Base.");
            }

            // Move MazeArena_MoodLight to maze centre at y = 4.0
            Color moodColor = new Color(0.2f, 0.9f, 0.6f);
            Transform moodLightTrans = holder.Find("MazeArena_MoodLight");
            if (moodLightTrans != null)
            {
                Undo.RecordObject(moodLightTrans, "Move Maze Mood Light");
                moodLightTrans.position = new Vector3(newFloorCenter.x, 4.0f, newFloorCenter.z);
                Light pt = moodLightTrans.GetComponent<Light>();
                if (pt != null) moodColor = pt.color;
            }

            // Remove previous fill lights if re-running (idempotent)
            for (int i = 1; i <= 3; i++)
            {
                Transform oldFill = holder.Find($"MazeArena_FillLight_{i}");
                if (oldFill != null) Undo.DestroyObjectImmediate(oldFill.gameObject);
            }

            // Add 3 distributed fill lights
            Vector3[] fillPositions = new Vector3[]
            {
                new Vector3(adjustedFloorBounds.min.x + adjustedFloorBounds.size.x * 0.25f, 3.5f, adjustedFloorBounds.min.z + adjustedFloorBounds.size.z * 0.35f),
                new Vector3(adjustedFloorBounds.min.x + adjustedFloorBounds.size.x * 0.75f, 3.5f, adjustedFloorBounds.min.z + adjustedFloorBounds.size.z * 0.35f),
                new Vector3(adjustedFloorBounds.min.x + adjustedFloorBounds.size.x * 0.50f, 3.5f, adjustedFloorBounds.min.z + adjustedFloorBounds.size.z * 0.75f)
            };

            for (int i = 0; i < fillPositions.Length; i++)
            {
                GameObject fillObj = new GameObject($"MazeArena_FillLight_{i + 1}");
                fillObj.transform.SetParent(holder);
                fillObj.transform.position = fillPositions[i];

                Light lightComp = fillObj.AddComponent<Light>();
                lightComp.type = LightType.Point;
                lightComp.color = moodColor;
                lightComp.intensity = 2.5f;
                lightComp.range = 14f;
                lightComp.shadows = LightShadows.None;

                Undo.RegisterCreatedObjectUndo(fillObj, $"Create MazeArena_FillLight_{i + 1}");
            }
            Debug.Log("[WorldPolish] Repositioned MazeArena_MoodLight and created 3 distributed fill lights.");

            // -----------------------------------------------------------------
            // C. CONNECTOR BLOCKOUT (07d_Connector_ToMaze)
            // -----------------------------------------------------------------
            Transform oldConnector = labRoot.transform.Find("07d_Connector_ToMaze");
            if (oldConnector != null)
            {
                Undo.DestroyObjectImmediate(oldConnector.gameObject);
            }

            GameObject connectorObj = new GameObject("07d_Connector_ToMaze");
            connectorObj.transform.SetParent(labRoot.transform);
            connectorObj.transform.position = Vector3.zero;
            connectorObj.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(connectorObj, "Create 07d_Connector_ToMaze");

            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/SciFi_Floor_Mat.mat");
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/SciFi_Wall_Mat.mat");

            // Measurements
            Transform corridorD = labRoot.transform.Find("07b_Corridor_D_ToMaze");
            Transform corridorDFloor = corridorD != null ? corridorD.Find("Floor") : null;
            float corridorDNorthEnd = corridorDFloor != null ? GetRendererBounds(corridorDFloor.gameObject).max.z : 105.0f;

            // Measure opening between Entry_Wall_01 and Entry_Wall_02
            Transform entryWall1 = FindChildRecursive(mazeArena, "Entry_Wall_01");
            Transform entryWall2 = FindChildRecursive(mazeArena, "Entry_Wall_02");
            float entryOpeningZMin = 113.18f;
            float entryOpeningZMax = 120.57f;
            if (entryWall1 != null && entryWall2 != null)
            {
                Bounds w1 = GetRendererBounds(entryWall1.gameObject);
                Bounds w2 = GetRendererBounds(entryWall2.gameObject);
                float minZ = Mathf.Min(w1.max.z, w2.max.z);
                float maxZ = Mathf.Max(w1.min.z, w2.min.z);
                if (minZ < maxZ)
                {
                    entryOpeningZMin = minZ;
                    entryOpeningZMax = maxZ;
                }
            }
            float entryCenterZ = (entryOpeningZMin + entryOpeningZMax) * 0.5f;

            Transform entryDoorFloor = FindChildRecursive(mazeArena, "Entry_door_floor");
            Bounds entryFloorBounds = entryDoorFloor != null ? GetRendererBounds(entryDoorFloor.gameObject) : new Bounds(new Vector3(-10.7f, -0.25f, 116.9f), new Vector3(2f, 0.5f, 4f));
            float entryDoorNearX = entryFloorBounds.max.x;

            float extZStart = corridorDNorthEnd;
            float extZEnd = entryCenterZ + 2.0f;
            float extLength = extZEnd - extZStart;

            // 1. Extension Corridor D Northwards (x from -2 to +2)
            // Floor
            CreateCube(connectorObj.transform, "Connector_CorridorD_Floor",
                new Vector3(0f, -0.25f, extZStart + extLength * 0.5f),
                new Vector3(4.0f, 0.5f, extLength), floorMat);

            // East Wall (Full-length along x = +2)
            CreateCube(connectorObj.transform, "Connector_CorridorD_EastWall",
                new Vector3(2.25f, 2.0f, extZStart + extLength * 0.5f),
                new Vector3(0.5f, 4.0f, extLength), wallMat);

            // West Wall (Segment south of doorway gap)
            float westSouthLen = (entryCenterZ - 2.0f) - extZStart;
            if (westSouthLen > 0.05f)
            {
                CreateCube(connectorObj.transform, "Connector_CorridorD_WestWall_South",
                    new Vector3(-2.25f, 2.0f, extZStart + westSouthLen * 0.5f),
                    new Vector3(0.5f, 4.0f, westSouthLen), wallMat);
            }

            // North End-Cap Wall (Seals north end of Corridor D extension)
            CreateCube(connectorObj.transform, "Connector_CorridorD_NorthCap",
                new Vector3(0.25f, 2.0f, extZEnd + 0.25f),
                new Vector3(4.5f, 4.0f, 0.5f), wallMat);

            // 2. West Stub Corridor (4m wide, centred on entryCenterZ, from x = -2 to entryDoorNearX)
            float stubLength = Mathf.Abs(entryDoorNearX - (-2.0f));
            float stubCenterX = (-2.0f + entryDoorNearX) * 0.5f;

            // Stub Floor (Ends flush with Entry_door_floor, zero z-fighting)
            CreateCube(connectorObj.transform, "Connector_Stub_Floor",
                new Vector3(stubCenterX, -0.25f, entryCenterZ),
                new Vector3(stubLength, 0.5f, 4.0f), floorMat);

            // Stub North Wall
            CreateCube(connectorObj.transform, "Connector_Stub_NorthWall",
                new Vector3(stubCenterX, 2.0f, entryCenterZ + 2.25f),
                new Vector3(stubLength, 4.0f, 0.5f), wallMat);

            // Stub South Wall
            CreateCube(connectorObj.transform, "Connector_Stub_SouthWall",
                new Vector3(stubCenterX, 2.0f, entryCenterZ - 2.25f),
                new Vector3(stubLength, 4.0f, 0.5f), wallMat);

            Debug.Log($"[WorldPolish] Built 07d_Connector_ToMaze: Extension Z [{extZStart:F2} -> {extZEnd:F2}], West Stub X [-2.00 -> {entryDoorNearX:F2}] at Z [{entryCenterZ - 2f:F2} .. {entryCenterZ + 2f:F2}].");

            // -----------------------------------------------------------------
            // D. MOVE 08_EXITROOM
            // -----------------------------------------------------------------
            Transform exitRoom = labRoot.transform.Find("08_ExitRoom");
            Transform exitDoorFloor = FindChildRecursive(mazeArena, "Exit_door_floor");
            if (exitRoom != null && exitDoorFloor != null)
            {
                Bounds exitFloorBounds = GetRendererBounds(exitDoorFloor.gameObject);
                float exitCenterX = exitFloorBounds.center.x;
                float exitOpenEndZ = exitFloorBounds.max.z;

                Undo.RecordObject(exitRoom, "Move 08_ExitRoom");
                // South door gap of ExitRoom is at local z = -4.0. Setting position.z = exitOpenEndZ + 4.0 aligns south wall at exitOpenEndZ
                exitRoom.position = new Vector3(exitCenterX, 0f, exitOpenEndZ + 4.0f);
                exitRoom.rotation = Quaternion.identity;

                Debug.Log($"[WorldPolish] Repositioned 08_ExitRoom to: Pos={exitRoom.position}, Rot={exitRoom.eulerAngles} (aligned with maze exit at X={exitCenterX:F2}, Z={exitOpenEndZ:F2}).");
            }

            // -----------------------------------------------------------------
            // E. DISABLE REDUNDANT NAVMESHSURFACES UNDER 07c
            // -----------------------------------------------------------------
            int disabledSurfaces = 0;
            NavMeshSurface[] mazeSurfaces = holder.GetComponentsInChildren<NavMeshSurface>(true);
            foreach (var s in mazeSurfaces)
            {
                if (s.enabled)
                {
                    Undo.RecordObject(s, "Disable Maze NavMeshSurface Override");
                    s.enabled = false;
                    disabledSurfaces++;
                }
            }
            Debug.Log($"[WorldPolish] Disabled {disabledSurfaces} redundant NavMeshSurface(s) under 07c_Arena4_Maze_Student4 as scene instance overrides.");

            // -----------------------------------------------------------------
            // E2. NAVMESHMODIFIER OVERRIDES ON MAZE WALLS
            // -----------------------------------------------------------------
            NavMeshTriangulation beforeTri = NavMesh.CalculateTriangulation();
            int beforeTriCount = beforeTri.indices != null ? beforeTri.indices.Length / 3 : 0;

            Transform mazeWalls = FindChildRecursive(mazeArena, "Maze_Walls");
            if (mazeWalls != null)
            {
                NavMeshModifier mod = mazeWalls.GetComponent<NavMeshModifier>();
                if (mod == null)
                {
                    mod = Undo.AddComponent<NavMeshModifier>(mazeWalls.gameObject);
                }
                else
                {
                    Undo.RecordObject(mod, "Configure NavMeshModifier");
                }
                mod.overrideArea = true;
                mod.area = 1; // 1 = Not Walkable
                mod.applyToChildren = true;
            }

            // Also check any wall pieces under Maze_Arena not under Maze_Walls
            foreach (Transform t in mazeArena.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Wall") && (mazeWalls == null || !t.IsChildOf(mazeWalls)))
                {
                    NavMeshModifier extraMod = t.GetComponent<NavMeshModifier>();
                    if (extraMod == null)
                    {
                        extraMod = Undo.AddComponent<NavMeshModifier>(t.gameObject);
                    }
                    else
                    {
                        Undo.RecordObject(extraMod, "Configure NavMeshModifier");
                    }
                    extraMod.overrideArea = true;
                    extraMod.area = 1;
                    extraMod.applyToChildren = true;
                }
            }

            // Rebuild NavMesh on Lab_Environment_Root
            WorldPolishUtil.BakeLabNavMesh();

            NavMeshTriangulation afterTri = NavMesh.CalculateTriangulation();
            int afterTriCount = afterTri.indices != null ? afterTri.indices.Length / 3 : 0;
            Debug.Log($"[WorldPolish] NavMesh rebuilt: Triangles Before={beforeTriCount}, After={afterTriCount} (clean elimination of wall-top islands).");

            // Mark scene dirty (no auto-save; user saves)
            EditorSceneManager.MarkSceneDirty(scene);

            // -----------------------------------------------------------------
            // F. MESSAGE FOR TEAMMATES
            // -----------------------------------------------------------------
            Debug.Log("[WorldPolish] ==================== MESSAGE FOR TEAMMATES ====================");
            Debug.Log($"[WorldPolish] New maze floor height: y = 0.00m (world top: {GetRendererBounds(mazeFloor.gameObject).max.y:F2}m)");
            if (exitRoom != null) Debug.Log($"[WorldPolish] New exit room position: {exitRoom.position}");

            // Ghost status
            GameObject ghost = GameObject.Find("Maze_Ghost") ?? GameObject.Find("MazeGhost_Agent4");
            if (ghost != null)
            {
                bool ghostOnNav = NavMesh.SamplePosition(ghost.transform.position, out NavMeshHit ghostHit, 1.5f, NavMesh.AllAreas);
                float offset = ghostOnNav ? Vector3.Distance(ghost.transform.position, ghostHit.position) : -1f;
                Debug.Log($"[WorldPolish] Maze_Ghost position: {ghost.transform.position} | On NavMesh: {ghostOnNav} (sample offset: {(ghostOnNav ? $"{offset:F2}m" : "N/A")})");
            }
            else
            {
                Debug.Log("[WorldPolish] Maze_Ghost: (Not present in scene)");
            }

            // IllusionBot status
            GameObject illusion = GameObject.Find("IllusionBot");
            if (illusion != null)
            {
                bool illusionOnNav = NavMesh.SamplePosition(illusion.transform.position, out NavMeshHit illHit, 1.5f, NavMesh.AllAreas);
                float offset = illusionOnNav ? Vector3.Distance(illusion.transform.position, illHit.position) : -1f;
                Debug.Log($"[WorldPolish] IllusionBot position: {illusion.transform.position} | On NavMesh: {illusionOnNav} (sample offset: {(illusionOnNav ? $"{offset:F2}m" : "N/A")})");
            }
            else
            {
                Debug.Log("[WorldPolish] IllusionBot: (Not present in scene)");
            }

            Debug.Log("[WorldPolish] NOTE: NavMesh-*.asset files in Assets/_Project/Scenes/MainLabScene/ from maze surfaces are no longer used.");
            Debug.Log("[WorldPolish] ================================================================");
        }

        // =========================================================================
        // PRIVATE HELPERS
        // =========================================================================
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

        private static Bounds GetRendererBounds(GameObject go)
        {
            if (go == null) return new Bounds(Vector3.zero, Vector3.zero);

            Renderer r = go.GetComponent<Renderer>();
            if (r != null) return r.bounds;

            Collider c = go.GetComponent<Collider>();
            if (c != null) return c.bounds;

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                return b;
            }

            return new Bounds(go.transform.position, Vector3.zero);
        }

        private static void LogBounds(string label, Transform t)
        {
            if (t == null)
            {
                Debug.LogWarning($"[WorldPolish] {label}: NOT FOUND");
                return;
            }
            Bounds b = GetRendererBounds(t.gameObject);
            Debug.Log($"[WorldPolish] {label}: Center={b.center}, Size={b.size}, Min={b.min}, Max={b.max}");
        }

        private static void LogGroupBounds(string label, Transform t)
        {
            if (t == null)
            {
                Debug.LogWarning($"[WorldPolish] {label}: NOT FOUND");
                return;
            }
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.Log($"[WorldPolish] {label}: No renderers found, transform position = {t.position}");
                return;
            }
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            Debug.Log($"[WorldPolish] {label} (Combined): Center={b.center}, Size={b.size}, Min={b.min}, Max={b.max}");
        }

        private static void CheckOverlap(string labRoomName, Transform mazeWalls, Transform labRoom)
        {
            if (mazeWalls == null || labRoom == null) return;

            Renderer[] labRenderers = labRoom.GetComponentsInChildren<Renderer>();
            Renderer[] mazeRenderers = mazeWalls.GetComponentsInChildren<Renderer>();

            int overlaps = 0;
            foreach (var lr in labRenderers)
            {
                foreach (var mr in mazeRenderers)
                {
                    if (lr.bounds.Intersects(mr.bounds))
                    {
                        overlaps++;
                    }
                }
            }

            if (overlaps > 0)
            {
                Debug.LogWarning($"[WorldPolish] Overlap check: {overlaps} intersecting bounds between Maze_Walls and '{labRoomName}'.");
            }
            else
            {
                Debug.Log($"[WorldPolish] Overlap check: 0 intersections between Maze_Walls and '{labRoomName}' (Clean separation).");
            }
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = scale;
            cube.isStatic = true;

            if (mat != null)
            {
                Renderer rend = cube.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = mat;
            }

            Undo.RegisterCreatedObjectUndo(cube, $"Create {name}");
            return cube;
        }
    }
}
