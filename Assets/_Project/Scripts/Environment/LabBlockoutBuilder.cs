using UnityEngine;

namespace FailSafeZero.Environment
{
    /// <summary>
    /// Student 1 - Greybox Level Blockout Generator (1 Unity Unit = 1 Metre).
    /// Constructs modular room hierarchy for Start Room, Corridors, 3 Arenas, and Exit.
    /// </summary>
    public class LabBlockoutBuilder : MonoBehaviour
    {
        [ContextMenu("1. Generate Phase 1 (Start Room & Corridor 1)")]
        public void GenerateStartAndCorridor1()
        {
            Transform root = GetOrCreateRoot();
            CreateRoomBlockout(root, "01_StartRoom", new Vector3(0f, 0f, 0f), new Vector3(10f, 4f, 10f), true, false);
            CreateRoomBlockout(root, "02_Corridor_A", new Vector3(0f, 0f, 10f), new Vector3(4f, 4f, 10f), false, false);
            Debug.Log("Phase 1 Blockout Generated: Start Room and Corridor A.");
        }

        [ContextMenu("2. Generate Phase 2 (Arena 1 - Zombie Lab)")]
        public void GenerateArena1()
        {
            Transform root = GetOrCreateRoot();
            Transform arena1 = CreateRoomBlockout(root, "03_Arena1_Zombie", new Vector3(0f, 0f, 25f), new Vector3(20f, 5f, 20f), false, false);
            
            // Add tactical cover pillars inside Arena 1 for Zombie line-of-sight breaking
            CreatePillar(arena1, "CoverPillar_NW", new Vector3(-5f, 2.5f, 5f));
            CreatePillar(arena1, "CoverPillar_NE", new Vector3(5f, 2.5f, 5f));
            CreatePillar(arena1, "CoverPillar_SW", new Vector3(-5f, 2.5f, -5f));
            CreatePillar(arena1, "CoverPillar_SE", new Vector3(5f, 2.5f, -5f));
            Debug.Log("Phase 2 Blockout Generated: Arena 1 with cover pillars.");
        }

        [ContextMenu("3. Generate Phase 3 (Arena 2 - Gravity Lab)")]
        public void GenerateArena2()
        {
            Transform root = GetOrCreateRoot();
            CreateRoomBlockout(root, "04_Corridor_B", new Vector3(0f, 0f, 40f), new Vector3(4f, 4f, 10f), false, false);
            CreateRoomBlockout(root, "05_Arena2_Gravity", new Vector3(0f, 0f, 55f), new Vector3(20f, 7f, 20f), false, false);
            Debug.Log("Phase 3 Blockout Generated: Corridor B and Arena 2.");
        }

        [ContextMenu("4. Generate Phase 4 (Arena 3 & Exit Room)")]
        public void GenerateArena3AndExit()
        {
            Transform root = GetOrCreateRoot();
            CreateRoomBlockout(root, "06_Corridor_C", new Vector3(0f, 0f, 70f), new Vector3(4f, 4f, 10f), false, false);
            CreateRoomBlockout(root, "07_Arena3_Illusion", new Vector3(0f, 0f, 85f), new Vector3(20f, 5f, 20f), false, false);
            CreateRoomBlockout(root, "08_ExitRoom", new Vector3(0f, 0f, 99f), new Vector3(8f, 4f, 8f), false, true);
            Debug.Log("Phase 4 Blockout Generated: Arena 3 and Exit Room.");
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

            // Floor
            CreateBox(room.transform, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(size.x, 0.5f, size.z));

            // Left and Right Walls
            CreateBox(room.transform, "Wall_West", new Vector3(-size.x * 0.5f, size.y * 0.5f, 0f), new Vector3(0.5f, size.y, size.z));
            CreateBox(room.transform, "Wall_East", new Vector3(size.x * 0.5f, size.y * 0.5f, 0f), new Vector3(0.5f, size.y, size.z));

            if (hasBackWall)
            {
                CreateBox(room.transform, "Wall_South", new Vector3(0f, size.y * 0.5f, -size.z * 0.5f), new Vector3(size.x, size.y, 0.5f));
            }

            if (hasFrontWall)
            {
                CreateBox(room.transform, "Wall_North", new Vector3(0f, size.y * 0.5f, size.z * 0.5f), new Vector3(size.x, size.y, 0.5f));
            }

            // Doorway framing walls for large rooms (Arenas / Start / Exit)
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