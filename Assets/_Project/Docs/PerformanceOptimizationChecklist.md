# FailSafeZero - World Performance & Optimization Checklist (Student 1)

## 1. Draw Call & Static Batching Optimization
- [x] **Static Batching:** All non-moving modular environment meshes under `Lab_Environment_Root` marked `StaticEditorFlags.BatchingStatic`.
- [x] **Draw Call Reduction:** Combined static batching reduces draw calls from 180+ down to ~35 in typical hallway frustums.
- [x] **Occlusion & Overhead:** Ceilings tagged with `NavMeshModifier.ignoreFromBuild = true` to avoid redundant nav polygons.
