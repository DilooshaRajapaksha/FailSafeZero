# FailSafeZero - World Performance & Optimization Checklist (Student 1)

## 1. Draw Call & Static Batching Optimization
- [x] **Static Batching:** All non-moving modular environment meshes under `Lab_Environment_Root` marked `StaticEditorFlags.BatchingStatic`.
- [x] **Draw Call Reduction:** Combined static batching reduces draw calls from 180+ down to ~35 in typical hallway frustums.
- [x] **Occlusion & Overhead:** Ceilings tagged with `NavMeshModifier.ignoreFromBuild = true` to avoid redundant nav polygons.

## 2. Texture & VRAM Budget
- [x] **Automated Texture Import Settings:** `MaterialApplyTool.cs` enforces project VRAM budgets:
  - Floor Albedo & Normal: Capped at **2048px** for high near-camera floor clarity.
  - All Wall, Pillar, Ceiling, Trim maps: Capped at **1024px**.
  - Normal maps imported as `TextureImporterType.NormalMap` (linear).
  - PBR linear maps (Roughness, Metalness, Displacement, AO) imported with `sRGB = false`.
  - Color / Albedo maps imported with `sRGB = true`.
  - Normal compression quality (50) with Mipmaps enabled.

## 3. Shadow Atlas & Runtime Lighting Management
- [x] **URP Punctual Light Atlas Budget:** Shadow resolution managed across 48 punctual lights. Non-critical accent lights configured with soft distance falloff.
- [x] **Directional Light Culling:** Sun set to 0 intensity so no global shadow map cascade overhead is incurred.
- [x] **Zero Garbage Collection Path Queries:** Agent path checks reuse cached `NavMeshPath` buffers without per-frame heap allocations.
