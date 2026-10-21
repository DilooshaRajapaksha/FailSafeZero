# FailSafeZero - NavMesh Baking & Pathfinding Validation Report (Student 1)

## 1. External Asset Serialization Architecture
- **Persistent Asset Storage:** NavMesh surface data is baked and saved externally at `Assets/_Project/Scenes/MainLabScene/NavMesh-Lab_Environment_Root.asset`.
- **Git Merge Protection:** By storing binary triangulation in an external asset, `MainLabScene.unity` remains pure `%YAML 1.1` text, preventing 5MB+ merge conflicts across team branches.
- **Ceiling Isolation:** All modular ceiling slabs (rooms 01-08, 07d) include `NavMeshModifier` with `ignoreFromBuild = true` to prevent agents from baking onto roof surfaces.
