using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace FailSafeZero.Environment
{
    public class LabBlockoutBuilder : MonoBehaviour
    {
        [ContextMenu("1. Generate Phase 1 (Start Room & Corridor 1)")]
        public void GenerateStartAndCorridor1()
        {
            Transform root = GetOrCreateRoot();
            CreateRoomBlockout(root, "01_StartRoom", new Vector3(0f, 0f, 0f), new Vector3(10f, 4f, 10f), true, false);
            CreateRoomBlockout(root, "02_Corridor_A", new Vector3(0f, 0f, 10f), new Vector3(4f, 4f, 10f), false, false);
            SaveSceneChanges();
            Debug.Log("Phase 1 Blockout Generated and Saved!");
        }

        [ContextMenu("2. Generate Phase 2 (Arena 1 - Zombie Lab)")]
        public void GenerateArena1()
        {
            Transform root = GetOrCreateRoot();
            Transform arena1 = CreateRoomBlockout(root, "03_Arena1_Zombie", new Vector3(0f, 0f, 25f), new Vector3(20f, 5f, 20f), false, false);
            CreatePillar(arena1, "CoverPillar_NW", new Vector3(-5f, 2.5f, 5f));
            CreatePillar(arena1, "CoverPillar_NE", new Vector3(5f, 2.5f, 5f));
            CreatePillar(arena1, "CoverPillar_SW", new Vector3(-5f, 2.5f, -5f));
            CreatePillar(arena1, "CoverPillar_SE", new Vector3(5f, 2.5f, -5f));
            SaveSceneChanges();
            Debug.Log("Phase 2 Blockout Generated and Saved!");
        }

        [ContextMenu("3. Generate Phase 3 (Arena 2 - Gravity Lab)")]
        public void GenerateArena2()
        {
            Transform root = GetOrCreateRoot();
            CreateRoomBlockout(root, "04_Corridor_B", new Vector3(0f, 0f, 40f), new Vector3(4f, 4f, 10f), false, false);
            CreateRoomBlockout(root, "05_Arena2_Gravity", new Vector3(0f, 0f, 55f), new Vector3(20f, 7f, 20f), false, false);
            SaveSceneChanges();
            Debug.Log("Phase 3 Blockout Generated and Saved!");
        }

        [ContextMenu("4. Generate Phase 4 (Arena 3 & Exit Room)")]
        public void GenerateArena3AndExit()
        {
            Transform root = GetOrCreateRoot();
            CreateRoomBlockout(root, "06_Corridor_C", new Vector3(0f, 0f, 70f), new Vector3(4f, 4f, 10f), false, false);
            CreateRoomBlockout(root, "07_Arena3_Illusion", new Vector3(0f, 0f, 85f), new Vector3(20f, 5f, 20f), false, false);
            CreateRoomBlockout(root, "08_ExitRoom", new Vector3(0f, 0f, 99f), new Vector3(8f, 4f, 8f), false, true);
            SaveSceneChanges();
            Debug.Log("Phase 4 Blockout Generated and Saved!");
        }

        private void SaveSceneChanges()
        {
#if UNITY_EDITOR
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
#endif
        }

        private Transform GetOrCreateRoot()
        {
            GameObject existing = GameObject.Find("Lab_Environment_Root");
            if (existing != null) return existing.transform;
            return new GameObject("Lab_Environment_Root").transform;
        }

        private Transform CreateRoomBlockout(Transform parent, string roomName, Vector3 centerPos, Vector3 size, bool hasBackWall, bool hasFrontWall)
        {
            Transform existing = parent.Find(roomName);
            if (existing != null) DestroyImmediate(existing.gameObject);

            GameObject room = new GameObject(roomName);
            room.transform.SetParent(parent);
            room.transform.position = centerPos;

            CreateBox(room.transform, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(size.x, 0.5f, size.z));
            CreateBox(room.transform, "Wall_West", new Vector3(-size.x * 0.5f, size.y * 0.5f, 0f), new Vector3(0.5f, size.y, size.z));
            CreateBox(room.transform, "Wall_East", new Vector3(size.x * 0.5f, size.y * 0.5f, 0f), new Vector3(0.5f, size.y, size.z));

            if (hasBackWall)
                CreateBox(room.transform, "Wall_South", new Vector3(0f, size.y * 0.5f, -size.z * 0.5f), new Vector3(size.x, size.y, 0.5f));

            if (hasFrontWall)
                CreateBox(room.transform, "Wall_North", new Vector3(0f, size.y * 0.5f, size.z * 0.5f), new Vector3(size.x, size.y, 0.5f));

            if (size.x > 6f)
            {
                float sideWallWidth = (size.x - 4f) * 0.5f;
                float offset = 2f + (sideWallWidth * 0.5f);

                if (!hasBackWall)
                {
                    CreateBox(room.transform, "DoorFrame_South_L", new Vector3(-offset, size.y * 0.5f, -size.z * 0.5f), new Vector3(sideWallWidth, size.y, 0.5f));
                    CreateBox(room.transform, "DoorFrame_South_R", new Vector3(offset, size.y * 0.5f, -size.z * 0.5f), new Vector3(sideWallWidth, size.y, 0.5f));
                }

                if (!hasFrontWall)
                {
                    CreateBox(room.transform, "DoorFrame_North_L", new Vector3(-offset, size.y * 0.5f, size.z * 0.5f), new Vector3(sideWallWidth, size.y, 0.5f));
                    CreateBox(room.transform, "DoorFrame_North_R", new Vector3(offset, size.y * 0.5f, size.z * 0.5f), new Vector3(sideWallWidth, size.y, 0.5f));
                }
            }

            return room.transform;
        }

        private void CreatePillar(Transform parent, string name, Vector3 localPos)
        {
            CreateBox(parent, name, localPos, new Vector3(2f, 5f, 2f));
        }

        private void CreateBox(Transform parent, string name, Vector3 localPos, Vector3 scale)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent);
            box.transform.localPosition = localPos;
            box.transform.localScale = scale;
            box.isStatic = true;
        }
    }
}