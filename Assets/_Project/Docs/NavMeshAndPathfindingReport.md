# FailSafeZero - NavMesh Baking & Weighted A* Pathfinding (Student 1)
- **NavMesh Surface Configuration:** Baked over `Lab_Environment_Root` children using `CollectObjects.Children`.
- **Obstacle Carve & Static Flags:** All structural walls, cover pillars, containment crates, and Maze_Arena walls marked Static.
- **ZombieAgentAI FSM:** 4 states (`Dormant`, `PursuingMotion`, `InvestigatingTremor`, `Attacking`).
- **Weighted A* Formula:** `f(n) = g(n) + w * h(n)` with `w = 1.45` heuristic weight for responsive pursuit when player velocity exceeds `0.12 m/s`.
