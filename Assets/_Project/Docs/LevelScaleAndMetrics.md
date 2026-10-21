# FailSafeZero - Level Scale & Architecture Metrics (Student 1)

## 1. Architectural Scale Standard
- **Unit Scale Agreement:** 1 Unity Unit = 1 Metre (1.00m) consistently applied across all modular walls, floors, ceilings, and prop meshes.
- **Player Clearance Standard:** Character Controller height = 2.0m, radius = 0.5m. All doorways and corridors provide a minimum clearance of 3.0m height and 2.5m width.

## 2. Room & Corridor Metrics
- **01_StartRoom:** 10.0m x 10.0m x 4.0m (Initial player spawn, armory, orientation chamber).
- **02_Corridor_A:** 4.0m width x 4.0m height x 10.0m length (Z=5m to 15m).
- **03_Arena1_Zombie:** 20.0m x 20.0m x 5.0m (Cryo containment facility with 4 symmetrical cover pillars).
- **04_Corridor_B:** 4.0m width x 4.0m height x 10.0m length (Z=35m to 45m).
- **05_Arena2_Gravity:** 20.0m x 20.0m x 7.0m (High-bay test chamber for vertical physics and gravity hazards).
- **06_Corridor_C:** 4.0m width x 4.0m height x 10.0m length (Z=65m to 75m).
- **07_Arena3_Illusion:** 20.0m x 20.0m x 5.0m (Perceptual decoy evaluation arena with dual obelisks).

## 3. Student 4 Maze Integration & Corridor D Extension
- **07b_Corridor_D_ToMaze:** Extended from Z=95.0m to Z=118.88m (total length: 23.88m, width: 4.0m).
- **07d_Connector_ToMaze:** West stub corridor connecting Corridor D to Student 4's Maze_Arena entry opening.
  - Connector bounds: X = [-38.10m .. -34.10m], Z = [114.88m .. 118.88m] (4.0m wide x 4.0m high).
- **Doorway Clearance Alignment:**
  - Student 4's `Entry_Wall_01` ends at Z = 113.18m.
  - Student 4's `Entry_Wall_02` starts at Z = 120.57m.
  - Doorway gap width: 7.39m (centered at Z = 116.88m).
  - The 4.0m stub corridor slots flush inside this 7.39m opening with zero geometry intersection or z-fighting.
- **Vertical Leveling:** Maze_Arena local Y elevated from -2.118m to 0.000m so `Maze_floor` top surface sits at Y = 0.00m flush with lab flooring.

## 4. Extraction & Exit Room Placement
- **08_ExitRoom:** 8.0m x 8.0m x 4.0m final extraction chamber.
- **Relocation Coordinates:** Positioned at `(-47.68m, 0.00m, 148.87m)` flush with Student 4's maze exit doorway.
- **Extraction Flow:** Player enters maze from west stub at Z=116.88m, navigates through maze corridors, and emerges directly into 08_ExitRoom to trigger `ExitZone`.
