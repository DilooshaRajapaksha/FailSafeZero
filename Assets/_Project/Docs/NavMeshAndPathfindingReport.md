# FailSafeZero - NavMesh Baking & Pathfinding Validation Report (Student 1)

## 1. External Asset Serialization Architecture
- **Persistent Asset Storage:** NavMesh surface data is baked and saved externally at `Assets/_Project/Scenes/MainLabScene/NavMesh-Lab_Environment_Root.asset`.
- **Git Merge Protection:** By storing binary triangulation in an external asset, `MainLabScene.unity` remains pure `%YAML 1.1` text, preventing 5MB+ merge conflicts across team branches.
- **Ceiling Isolation:** All modular ceiling slabs (rooms 01-08, 07d) include `NavMeshModifier` with `ignoreFromBuild = true` to prevent agents from baking onto roof surfaces.

## 2. Pathfinding Validation Metrics
- **Baking Output:** 904 vertices, 412 triangles (1,236 indices).
- **End-to-End Continuity Test:**
  - `Path Player -> 03_Arena1_Zombie`: **PathComplete** (Length: 25.00m)
  - `Path Player -> 05_Arena2_Gravity`: **PathComplete** (Length: 55.00m)
  - `Path Player -> 07_Arena3_Illusion`: **PathComplete** (Length: 85.00m)
  - `Path Player -> 07c Maze Entry`: **PathComplete** (Length: 118.88m)
  - `Path Player -> 08_ExitRoom`: **PathComplete** (Length: **241.63m**)
- **Obstacle Carving:** Student 4's `Maze_Walls` marked with `NavMeshModifier` (Area: Not Walkable) ensuring agents respect dynamic maze interior corridors.
