# FailSafeZero - World Performance & Optimization Checklist (Student 1)

## 1. Draw Call & Static Batching Optimization
- [x] **Static Batching:** All non-moving modular environment meshes under `Lab_Environment_Root` marked `StaticEditorFlags.BatchingStatic`.
- [x] **Draw Call Reduction:** Combined static batching reduces draw calls from 180+ down to ~35 in typical hallway frustums.
- [x] **Occlusion & Overhead:** Ceilings tagged with `NavMeshModifier.ignoreFromBuild = true` to avoid redundant nav polygons and overhead raycast queries.

## 2. Texture & VRAM Budget
- [x] **Automated Texture Import Settings:** Enforced project VRAM budgets via automated importer configuration:
  - Floor Albedo & Normal: Capped at **2048px** for high near-camera floor clarity.
  - All Wall, Pillar, Ceiling, Trim maps: Capped at **1024px**.
  - Normal maps imported as `TextureImporterType.NormalMap` (linear color space).
  - PBR linear maps (Roughness, Metalness, Displacement, Ambient Occlusion) imported with `sRGB = false`.
  - Color / Albedo maps imported with `sRGB = true`.
  - Normal compression quality with Mipmaps enabled.
- [x] **PBR Material UV Tiling Standards:**
  - `M_Lab_Floor`: Tiling `4 x 4`
  - `M_Lab_Wall`: Tiling `4 x 2`
  - `M_Lab_Pillar`: Tiling `2 x 4`
  - `M_Lab_Ceiling`: Tiling `4 x 4`
  - `M_Lab_Trim`: Tiling `2 x 2`

## 3. Shadow Atlas & Runtime Lighting Management
- [x] **Baked Lightmaps:** 5 lightmaps (3x 1024px, 2x 256px, 8.3 MB total) baked via Progressive CPU lightmapper. Eliminates realtime shadow-casting overhead for 9 static room and arena lights.
- [x] **URP Punctual Light Atlas Budget:** 3 red pulsing corridor lights are configured as **Realtime** with **No Shadows** to avoid dynamic shadow atlas thrashing.
- [x] **Directional Light Culling:** Sun set to 0.00 intensity (Mode: `Baked`) so no global shadow map cascade overhead is incurred.
- [x] **Light Probe Illumination:** 1,194 light probes illuminate moving characters (`FirstPersonPlayer`, `ZombieBot_Agent`, `Maze_Ghost`, `IllusionBot`) at near-zero GPU cost.
- [x] **Reflection Probes:** 7 box-projected reflection probes provide high-fidelity specular reflections without expensive realtime screen-space reflections.
- [x] **Zero Garbage Collection Path Queries:** Agent path checks in `ZombieAgentAI` reuse cached `NavMeshPath` buffers without per-frame heap allocations.

## 4. Third-party assets and licences
- **PBR Texture Assets:** AmbientCG modular materials (`Lab_Floor`, `Lab_Wall`, `Lab_Pillar`, `Lab_Ceiling`, `Lab_Trim`).
- **Source:** [ambientCG.com](https://ambientCG.com) (created by Lennart Demes).
- **Licence:** Creative Commons CC0 1.0 Universal (Public Domain Dedication).
- **Terms:** Free for personal, educational, and commercial use with no attribution strictly required; cited here for academic integrity and project transparency.
