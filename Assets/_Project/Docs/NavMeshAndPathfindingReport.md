# FailSafeZero - NavMesh Baking & Pathfinding Validation Report (Student 1)

## 1. External Asset Serialization Architecture
- **Persistent Asset Storage:** NavMesh surface data is baked and saved externally at `Assets/_Project/Scenes/MainLabScene/NavMesh-Lab_Environment_Root.asset`.
- **Git Merge Protection:** By storing binary triangulation in an external asset rather than embedded scene YAML, `MainLabScene.unity` remains pure `%YAML 1.1` text, preventing 5MB+ merge conflicts across team branches.
- **Ceiling Isolation:** All modular ceiling slabs (rooms 01-08, 07b, 07d) include `NavMeshModifier` with `ignoreFromBuild = true` to prevent agents from spawning or calculating paths onto roof geometry.

## 2. Pathfinding Validation Metrics
- **NavMesh Geometry:** 904 vertices, 412 triangles (1,236 indices).
- **Agent Parameters:**
  - Agent Height: `2.0m` | Agent Radius: `0.5m` | Max Slope: `45°` | Step Height: `0.4m`
- **End-to-End Continuity Test:**
  - `Path Player -> 03_Arena1_Zombie`: **PathComplete** (Length: 25.00m)
  - `Path Player -> 05_Arena2_Gravity`: **PathComplete** (Length: 55.00m)
  - `Path Player -> 07_Arena3_Illusion`: **PathComplete** (Length: 85.00m)
  - `Path Player -> 07c Maze Entry`: **PathComplete** (Length: 118.88m)
  - `Path Player -> 08_ExitRoom`: **PathComplete** (Length: **241.63m**)
- **Obstacle Carving & Traversal:** Student 4's `Maze_Walls` are marked with `NavMeshModifier` (Area: Not Walkable) ensuring agents strictly navigate corridor channels without cutting corners.

## 3. Dynamic Agents Utilizing Baked NavMesh
- **ZombieBot_Agent (Student 1):**
  - Uses `NavMeshAgent` with speed `4.2m/s`, acceleration `22.0m/s²`, angular speed `360°/s`, stopping distance `1.5m`.
  - Driven by `ZombieAgentAI` executing 4-state FSM (Dormant, PursuingMotion, InvestigatingTremor, Attacking).
- **Maze_Ghost (Student 4):** Patrols and pursues within the connected `Maze_Arena`.
- **IllusionBot (Student 3):** Deploys holographic decoys in `07_Arena3_Illusion`.

## 4. Third-party assets and licences
- **PBR Texture Assets:** AmbientCG modular materials (`Lab_Floor`, `Lab_Wall`, `Lab_Pillar`, `Lab_Ceiling`, `Lab_Trim`).
- **Source:** [ambientCG.com](https://ambientCG.com) (created by Lennart Demes).
- **Licence:** Creative Commons CC0 1.0 Universal (Public Domain Dedication).
- **Terms:** Free for personal, educational, and commercial use with no attribution strictly required; cited here for academic integrity and project transparency.
