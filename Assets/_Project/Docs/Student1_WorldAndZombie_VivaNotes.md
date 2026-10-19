# Student 1: World Builder & Zombie Bot - Technical Design Notes

## 1. Level Layout & Scale (1 Unity Unit = 1 Metre)
- **Linear Progression Flow:** Start Room (10x10m) -> Corridor A -> Arena 1: Zombie Cryo Lab (20x20m) -> Corridor B -> Arena 2: High-Bay Gravity Chamber (20x20m, 7m ceiling) -> Corridor C -> Arena 3: Illusion Containment Lab (20x20m) -> Exit Room (8x8m).
- **Tactical Cover:** Arena 1 includes 4 symmetrical pillars and containment crates so the player can break line-of-sight and time movement stops against the Zombie Bot.

## 2. Lighting Moods & Visual Cohesion
- **Corridors:** Dim ambient lighting paired with pulsing red emergency point lights (`EmergencyLightPulse.cs`) to create tension between arenas.
- **Arena 1 (Zombie):** Cold Cyan lighting (clinical cryo-containment atmosphere).
- **Arena 2 (Gravity):** Industrial Amber lighting with a 7m vertical ceiling to accommodate vertical physics traps.
- **Arena 3 (Illusion):** Deep Violet lighting to reinforce perceptual distortion.
- **Exit Room:** Bio-Green lighting to clearly signal the escape objective.

## 3. World & Rendering Optimizations
- **Static Batching:** All non-moving floors, walls, pillars, and props are flagged as `Static`, allowing Unity's Universal Render Pipeline (URP) to batch draw calls and reduce CPU overhead.
- **Texture Compression:** Project textures are capped at a maximum resolution of 1024px with Crunch Compression enabled to minimize VRAM usage.
- **NavMesh Baking:** Baked via `NavMeshSurface` (`CollectObjects.Children`), converting 3D collision geometry into simplified convex polygons for fast path queries.

## 4. Zombie Bot AI Architecture (`ZombieAgentAI.cs`)
- **4-State Finite State Machine (FSM):** Transitions cleanly between `Dormant`, `PursuingMotion`, `InvestigatingTremor`, and `Attacking`.
- **Motion Detection Math:** Computes instantaneous player speed each frame (`Vector3.Distance(currentPos, previousPos) / Time.deltaTime`). When speed drops below `motionVelocityThreshold` (0.12 m/s), the Zombie immediately halts (`navAgent.isStopped = true`, `ResetPath()`).
- **Weighted A* Pathfinding:** Evaluates paths over the baked NavMesh using `f(n) = g(n) + w * h(n)` (`heuristicWeight = 1.45`), prioritizing direct, obstacle-free pursuit while exposing `CurrentPathWaypoints` (`Vector3[]`) for Student 4's shared movement controller.
