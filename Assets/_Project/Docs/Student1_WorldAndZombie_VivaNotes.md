# Student 1: World Builder & Zombie Bot - Technical Design & Viva Defense Notes

## 1. Level Scale & Environmental Architecture
- **Metrics Standard:** 1 Unity Unit = 1 Metre across all modular assets.
- **Sequential Flow:** Start Room (10x10m) -> Corridor A -> Arena 1 (Zombie Cryo Lab) -> Corridor B -> Arena 2 (Gravity Chamber) -> Corridor C -> Arena 3 (Illusion Lab) -> Corridor D Extension -> Connector Stub -> Student 4 Maze Arena -> Exit Room (Extraction).
- **Tactical Geometry:** Symmetrical pillars and containment crates in Arena 1 provide cover for timing stops against the motion-tracking Zombie Bot.

## 2. Subterranean Lighting & Atmosphere (URP)
- **Directional Light Intensity = 0:** Emphasizes that the facility is deep underground.
- **Flat Ambient:** Replaced default skybox ambient with dark charcoal (`0.05, 0.06, 0.08`).
- **Volumetric Fog:** Exponential squared fog (`density = 0.02`) creates depth cueing down corridors.
- **Global Post-Processing:** ACES tonemapping, bloom on emissive trims, and vignette for helmet optics.

## 3. Student 4 Maze Integration Defense
- **Q: How did you connect Student 4's Maze without modifying their prefab?**
  - **A:** Preserved Student 4's side placement (`X = -36.1m, Z = 22.5m, Rot = -90°`). Leveled local Y to 0.00m so `Maze_floor` is flush at Y=0.00m. Built `07d_Connector_ToMaze` with Corridor D extension (Z 95 to 118.88m) and a 4m wide west stub corridor that slots flush into the 7.39m doorway gap between `Entry_Wall_01` and `Entry_Wall_02`. Moved Exit Room to maze exit (`-47.68m, 0, 148.87m`). All changes saved as scene-level overrides; `Maze_Arena.prefab` was untouched.

## 4. Teammate Safety & Isolation Architecture
- **Q: How did you guarantee that automated tools didn't alter teammates' code, bots, or prefabs?**
  - **A:** Implemented `WorldPolishUtil.IsTeammateObject(GameObject)` which traverses hierarchies up to `Lab_Environment_Root` and strictly identifies teammate prefabs (source in `Prefabs/Bots`, `Player`, `Tools`, `Maze_Arena`), components (`NavMeshAgent`, `CharacterController`, `Rigidbody`, `Animator`), and scripts (`FailSafeZero.AI`, `Player`, `Weapons`, `Interactions`, `Core`). Every editor tool skips these objects, and automated baseline tests verify zero unintended changes before committing.
