# Student 1: World Builder & Zombie Bot - Technical Design & Viva Defense Notes

## 1. Level Scale & Environmental Architecture
- **Metrics Standard:** 1 Unity Unit = 1 Metre across all modular walls, floors, ceilings, and prop meshes.
- **Sequential Flow:** Start Room (10x10m) -> Corridor A (4x10m) -> Arena 1 (Zombie Cryo Lab, 20x20m) -> Corridor B (4x10m) -> Arena 2 (Gravity High-Bay, 20x20x7m) -> Corridor C (4x10m) -> Arena 3 (Illusion Lab, 20x20m) -> Corridor D Extension (4x23.88m) -> Connector Stub (4x4m) -> Student 4 Maze Arena -> Exit Room (Extraction, 8x8m).
- **Tactical Geometry:** Symmetrical cover pillars (`2x5x2m`) and containment crates (`1.5x1.5x1.5m`) in Arena 1 provide cover for timing stops against the motion-tracking Zombie Bot.
- **Ceiling Isolation:** Modular ceiling slabs enclose all rooms at 4m, 5m, and 7m heights with `NavMeshModifier.ignoreFromBuild = true` to prevent overhead agent navigation.

## 2. PBR Materials & Automated Texture Pipeline
- **Modular PBR Materials:** Configured in `Assets/_Project/Materials/Environment/`:
  - `M_Lab_Floor`: Tiling `4 x 4`
  - `M_Lab_Wall`: Tiling `4 x 2`
  - `M_Lab_Pillar`: Tiling `2 x 4`
  - `M_Lab_Ceiling`: Tiling `4 x 4`
  - `M_Lab_Trim`: Tiling `2 x 2`
- **Automated Importer Enforcements:**
  - Floor Albedo & Normal maps capped at 2048px; all other maps capped at 1024px.
  - Normal maps imported in linear space (`TextureImporterType.NormalMap`).
  - Roughness, Metalness, Displacement, AO imported as linear data (`sRGB = false`).
  - Color / Albedo maps imported as sRGB data (`sRGB = true`).

## 3. Subterranean Lighting & Baked Lightmaps (URP)
- **Subterranean Atmosphere:** Directional sunlight intensity set to `0.00` (Mode: `Baked`) and global ambient set to flat dark charcoal (`0.05, 0.06, 0.08`).
- **Volumetric Fog:** Exponential squared fog (`density = 0.02`, `RGB: 0.08, 0.09, 0.12`) creates depth cueing down corridors.
- **Progressive CPU Lightmapper:**
  - Asset: `LabLightingSettings.lighting` (10 texels/unit, 1024 max size, AO ON 1.0m, 32 direct / 256 indirect samples, 2 bounces).
  - Generated 5 baked lightmaps (3x 1024px, 2x 256px, 8.3 MB total) in `Assets/_Project/Scenes/MainLabScene/LightingData.asset`.
  - 9 room/arena lights baked with soft shadows.
  - 3 corridor emergency lights kept **Realtime** with **No Shadows** (sinusoidal pulse via `EmergencyLightPulse.cs`), protecting dynamic shadow atlas budgets.
- **Light Probes & Reflection Probes:**
  - 1,194 light probes distributed across all 11 zones for high-fidelity dynamic illumination of moving player and AI agents.
  - 7 box-projected reflection probes in Start Room, Arenas 1-3, Maze, and Exit Room for PBR specular reflections.

## 4. Baked NavMesh & Pathfinding Continuity
- **External Triangulation:** Baked NavMeshData saved externally at `Assets/_Project/Scenes/MainLabScene/NavMesh-Lab_Environment_Root.asset`. Keeps `MainLabScene.unity` as lightweight YAML text, avoiding binary merge conflicts.
- **Geometry Output:** 904 vertices, 412 triangles (1,236 indices).
- **Verified Pathfinding:** End-to-end player path to Exit Room verified as **PathComplete** (total length: **241.63m**).

## 5. Autonomous Zombie Bot Agent (SE3062 Intelligent Systems)
- **Architecture:** 4-State Finite State Machine (FSM) + Weighted A* NavMesh Pathfinding.
- **FSM States:**
  1. `Dormant`: Complete freeze when player is motionless.
  2. `PursuingMotion`: Active pursuit when player velocity exceeds threshold.
  3. `InvestigatingTremor`: Brief check of last detected player position before freezing.
  4. `Attacking`: Strike behavior when within close range.
- **Verified Parameters (from Code & Scene):**
  - **NavMeshAgent Speed:** `4.2 m/s`
  - **NavMeshAgent Acceleration:** `22.0 m/s²`
  - **Stopping Distance:** `1.5 m` | **Angular Speed:** `360°/s`
  - **Detection Radius:** `22.0m` (code default) / `24.0m` (tuned)
  - **Motion Velocity Threshold:** `0.15 m/s` (code default) / `0.12 m/s` (tuned)
  - **Attack Range:** `1.75 m`
  - **Weighted A* Heuristic Weight:** `1.35` (code default) / `1.45` (tuned)
  - **Freeze Reaction Delay:** `0.06 s`
  - **Target:** `FirstPersonPlayer` transform linked
- **Mechanic Defense:** The Zombie Bot acts as a "Red Light, Green Light" predator. Players can only safely move when behind cover pillars or while the zombie is dormant; running in the open immediately alerts the bot.

## 6. Student 4 Maze Integration Defense
- **Q: How did you connect Student 4's Maze without modifying their prefab?**
  - **A:** Preserved Student 4's exact side placement (`X = -36.1m, Z = 22.5m, Rot = -90°`). Leveled local Y to 0.00m so `Maze_floor` is flush at Y=0.00m. Built `07b_Corridor_D_ToMaze` extension (Z 95 to 118.88m) and `07d_Connector_ToMaze` (4m wide west stub corridor) that slots flush into the 7.39m doorway gap between `Entry_Wall_01` and `Entry_Wall_02`. Moved Exit Room to maze exit (`-47.68m, 0, 148.87m`). All modifications are scene-level overrides; `Maze_Arena.prefab` was untouched.

## 7. Teammate Safety & Isolation Architecture
- **Q: How did you guarantee that automated tools didn't alter teammates' code, bots, or prefabs?**
  - **A:** Implemented `WorldPolishUtil.IsTeammateObject(GameObject)` which traverses hierarchies up to `Lab_Environment_Root` and strictly identifies teammate prefabs (source in `Prefabs/Bots`, `Player`, `Tools`, `Maze_Arena`), components (`NavMeshAgent`, `CharacterController`, `Rigidbody`, `Animator`), and scripts (`FailSafeZero.AI`, `Player`, `Weapons`, `Interactions`, `Core`). Every editor tool skips these objects, and automated baseline tests verify zero unintended changes before committing.

## 8. Third-party assets and licences
- **PBR Texture Assets:** AmbientCG modular materials (`Lab_Floor`, `Lab_Wall`, `Lab_Pillar`, `Lab_Ceiling`, `Lab_Trim`).
- **Source:** [ambientCG.com](https://ambientCG.com) (created by Lennart Demes).
- **Licence:** Creative Commons CC0 1.0 Universal (Public Domain Dedication).
- **Terms:** Free for personal, educational, and commercial use with no attribution strictly required; cited here for academic integrity and project transparency.
