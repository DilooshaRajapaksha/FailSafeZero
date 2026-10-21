# FailSafeZero - Level Scale & Architecture Metrics (Student 1)

## 1. Architectural Scale Standard
- **Unit Scale Agreement:** 1 Unity Unit = 1 Metre (1.00m) consistently applied across all modular walls, floors, ceilings, and prop meshes.
- **Player Clearance Standard:** Character Controller height = 2.0m, radius = 0.5m. All corridors provide 4.0m width and 4.0m height clearance; standard arena doorways provide 4.0m width and 4.0m-5.0m height.

## 2. Room & Corridor Metrics (Modular Environment)
- **01_StartRoom:** 10.0m (X) x 4.0m (Y) x 10.0m (Z). Center: `(0.00m, 0.00m, 0.00m)`. Initial player orientation chamber with armory console.
- **02_Corridor_A:** 4.0m x 4.0m x 10.0m. Center: `(0.00m, 0.00m, 10.00m)`. Connecting Start Room to Arena 1.
- **03_Arena1_Zombie:** 20.0m x 5.0m x 20.0m. Center: `(0.00m, 0.00m, 25.00m)`. Cryo containment chamber featuring 4 cover pillars (`2.0m x 5.0m x 2.0m`) at `(±5.0m, 2.5m, ±5.0m)` and 2 containment crates (`1.5m x 1.5m x 1.5m`) at `(-7.5m, 0.75m, 22.0m)` and `(7.5m, 0.75m, 28.0m)`.
- **04_Corridor_B:** 4.0m x 4.0m x 10.0m. Center: `(0.00m, 0.00m, 40.00m)`. Connecting Arena 1 to Arena 2.
- **05_Arena2_Gravity:** 20.0m x 7.0m x 20.0m. Center: `(0.00m, 0.00m, 55.00m)`. High-bay chamber with elevated 7m ceiling and 2 heavy gravity anchor blocks (`2.0m x 2.0m x 2.0m`) at `(-5.0m, 1.0m, 52.0m)` and `(5.0m, 1.0m, 58.0m)`.
- **06_Corridor_C:** 4.0m x 4.0m x 10.0m. Center: `(0.00m, 0.00m, 70.00m)`. Connecting Arena 2 to Arena 3.
- **07_Arena3_Illusion:** 20.0m x 5.0m x 20.0m. Center: `(0.00m, 0.00m, 85.00m)`. Perceptual evaluation arena with dual illusion obelisks (`1.2m x 4.0m x 1.2m`) at `(±6.0m, 2.0m, 85.0m)`.
- **Ceilings:** Enclosed across all rooms and corridors (4.0m, 5.0m, 7.0m heights) using modular ceiling slabs with `NavMeshModifier.ignoreFromBuild = true` to prevent overhead agent navigation.

## 3. Student 4 Maze Integration & Connector Architecture
- **07b_Corridor_D_ToMaze:** Extended straight from Arena 3 (Z=95.0m) to Z=118.88m (total length: 23.88m, width: 4.0m, height: 4.0m).
- **07d_Connector_ToMaze:** West-branching connector corridor linking Corridor D directly into Student 4's Maze entry opening.
  - Bounds: X = `[-38.10m .. -34.10m]`, Z = `[114.88m .. 118.88m]`, Y = `[0.00m .. 4.00m]` (width: 4.0m, height: 4.0m, length: 4.0m).
- **Student 4 Maze_Arena Placement:**
  - Preserved Student 4's exact side placement: `(-36.10m, 0.00m, 22.50m)`, rotation `(0.0°, -90.0°, 0.0°)`.
  - Leveled local Y from -2.118m to 0.00m so `Maze_floor` top surface sits at Y = 0.00m flush with lab flooring.
- **Doorway Clearance & Alignment:**
  - Student 4's `Entry_Wall_01` ends at Z = 113.18m.
  - Student 4's `Entry_Wall_02` starts at Z = 120.57m.
  - Entry gap width: 7.39m (centered at Z = 116.88m).
  - The 4.0m west connector stub slots flush into the 7.39m opening with zero geometry intersection or z-fighting.
- **08_ExitRoom (Extraction Chamber):**
  - Dimensions: 8.0m (X) x 4.0m (Y) x 8.0m (Z).
  - Position: `(-47.68m, 0.00m, 148.87m)`, positioned flush against Student 4's maze exit doorway.
  - Contains `ExitZone` trigger volume for seamless mission extraction.

## 4. Third-party assets and licences
- **PBR Texture Assets:** AmbientCG modular materials (`Lab_Floor`, `Lab_Wall`, `Lab_Pillar`, `Lab_Ceiling`, `Lab_Trim`).
- **Source:** [ambientCG.com](https://ambientCG.com) (created by Lennart Demes).
- **Licence:** Creative Commons CC0 1.0 Universal (Public Domain Dedication).
- **Terms:** Free for personal, educational, and commercial use with no attribution strictly required; cited here for academic integrity and project transparency.
