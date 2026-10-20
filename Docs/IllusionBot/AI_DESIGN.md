# Illusion Bot: AI Design Notes

**Owner:** Student 3 (Core Developer), IT24610798
**Module:** SE3062 Intelligent Systems (agent) + SE3032 GV (model)
**Code:** `Assets/_Project/Scripts/AI/IllusionBot/`

## 1. The idea in one paragraph

The Illusion Bot fights by **deception**. It projects light-copies of itself
(**mirages**) that move, glow and "shoot" exactly like it, then **swaps places**
with a mirage the player is not looking at. The story hint says: *"Light never
lies: only the REAL one casts a shadow."* Mirages cast no shadow and their bolts
do no damage. Hitting the real bot breaks the illusion and **stuns** it (2x damage).
The bot also **learns** how often the player is fooled and adapts: a sharp player
faces more mirages and more swaps.

## 2. Architecture: FSM + Utility AI + Player Model

```
             +---------------- PERCEPTION (10 Hz) ----------------+
             | vision cone + line-of-sight ray, close-sense (3 m), |
             | "a shot reveals the shooter", memory (4 s)          |
             +-----------------------------+------------------------+
                                           v
   FINITE STATE MACHINE (the big modes)
   Patrol --sees--> Combat --lost > 4 s--> Search --6 s--> Patrol
      any --hit (not immune)--> Stunned --1.5 s--> Combat / Search
      any --HP 0--> Dead
                                           |
                     inside Combat, every 0.25 s
                                           v
   UTILITY AI: score 5 tactics (0..1), pick the highest (+0.1 hysteresis)
   Attack | SpawnMirages | Swap | Reposition | Retreat
                                           ^
   PLAYER MODEL: FooledRate (EMA) -> Skill -> mirage count and swap eagerness
```

**Why this combination, and not the alternatives?**

| Option | Why not used alone |
|---|---|
| Plain FSM | Combat has 5 tactics that compete. An FSM would need transitions between every pair (20 of them) and becomes a "glorified IF/ELSE". |
| Behaviour Tree | Good for fixed priorities. Here priorities **change** with health, distance, cooldowns and the player's skill. A BT encodes priority in tree order, but utility encodes it in numbers that adapt. |
| GOAP | Plans long action sequences. Our tactics are short and reactive (under 1 s), so planning cost buys nothing. |
| ML-Agents | Needs training time and data, is hard to explain line by line in a viva, and is hard to tune for fairness. |

The **FSM** handles clear modes (patrol, search, stunned, dead), where states are
truly exclusive. **Utility AI** handles the fuzzy choice inside combat. The
**player model** makes the weights change during play.

## 3. Utility scores (with justification)

Considerations are **multiplied**, so any factor of 0 vetoes an action.

| Action | Formula | Reasoning |
|---|---|---|
| Attack | `0.8 x inRange x (0.5 + 0.5 x HP)` | 0 if the player is not visible or on cooldown. The 0.8 cap means an urgent swap or a missing illusion beats attacking. |
| SpawnMirages | `0.9 x sqrt(missing) x (seen ? 1 : 0.5)` | sqrt keeps even 1 missing mirage of 4 at 0.45. The illusion is the main defence. |
| Swap | `0.95 x cooldownReady x urgency x (0.4 + 0.6 x skill)` | urgency = 1 just after a hit, else 0.45. This is the highest cap, because escaping is best. |
| Reposition | `0.3 + 0.2 x tooClose` (0.2 if unseen) | The never-zero default, so the bot always does something sensible. |
| Retreat | `danger^2 x (0.5 + 0.5 x tooClose)` | Squared, so it only matters when badly hurt. |

**Tested scenarios** (run on the real `IllusionUtility` class):

| Situation | Chosen | Scores (Att / Spawn / Swap / Repo / Retreat) |
|---|---|---|
| First sight, no mirages | SpawnMirages | 0.80 / **0.90** / 0 / 0.30 / 0 |
| Mirages up, attack ready | Attack | **0.80** / 0 / 0.30 / 0.30 / 0 |
| Just fired | Reposition | 0 / 0 / 0.30 / **0.30+0.1** / 0 |
| Hit again during stun-immunity | Swap | 0 / 0 / **0.67** / 0.30 / 0 |
| Expert player, between shots | Swap | 0 / 0 / **0.43** / 0.30+0.1 / 0 |
| Average player, between shots | Reposition | 0 / 0 / 0.30 / **0.30+0.1** / 0 |
| 20% HP, player 3 m away | Retreat | 0 / 0 / 0.30 / 0.44 / **0.54** |
| 50% HP, player 3 m away | Reposition | 0 / 0 / 0.30 / **0.44** / 0.21 |

**Hysteresis (+0.1):** without it, two near-equal scores make the bot flip
direction every 0.25 s (jitter). The bonus makes it commit to the running action.

## 4. Player model (learning)

`FooledRate += a x (target - FooledRate)`, with a = 0.25, a target of 1 for a mirage hit and 0 for a real hit.

- Exponential moving average: recent behaviour matters most, so the bot notices
  when the player **learns** the shadow trick. Weight of a hit k shots ago = 0.75^k.
- Starts at 0.5 (no evidence yet: a 50/50 prior).
- Example: 4 real hits in a row: 0.50 -> 0.375 -> 0.281 -> 0.211 -> 0.158.
- `Skill = 1 - FooledRate` drives `desiredMirages = round(lerp(1, 4, skill))`
  (+1 below half HP), and the swap skill factor.
- **Lead targeting:** bolts aim at `position + velocity x (distance / boltSpeed)`,
  with velocity smoothed from observed positions and time capped at 1 s.

## 5. Position search (heuristic)

When placing a mirage, the bot tests 12 random points around the player, snaps
them to the NavMesh, and scores each one:

`score = (0.45 x spread + 0.35 x rangeFit + 0.20 x visible) x reachable`

- **spread**: distance to the nearest other bot, capped at 6 m. Copies must not stack.
- **rangeFit**: closeness to the preferred fighting range (10 m).
- **visible**: the player's eyes can see it. A hidden decoy fools nobody.
- **reachable**: NavMesh path is complete (1.0) or not (0.2). This multiplies, as a veto.

A weighted **sum** is used for the "nice to haves" (one weak factor lowers the
score but does not reject the point). Reachability **multiplies**, because an
unreachable point is useless. Movement itself uses Unity's NavMesh (A*).

**Swap target:** the mirage with the **largest angle** from the player's aim
direction, meaning the one the player is least likely to be watching.

## 6. Edge cases handled

| Case | Handling |
|---|---|
| No player in the scene | Searches by tag `Player`, then by the name `Player_TestTarget`, once per second, with one warning. No null errors. |
| Bot placed off the NavMesh | Snaps to the nearest NavMesh point within 3 m. Otherwise it gives a clear error and disables itself. |
| Stuck on geometry | Wants to move but has a speed of about 0 for 1.5 s: picks a new nearby point. |
| Shot from behind | The shot sets the last known position to the shooter, so the bot turns and engages. |
| Stun-lock (spamming hits) | 3 s stun immunity after each stun. Hits in that window make the bot **swap** instead. |
| Pool exhausted | Never creates objects mid-fight. It just spawns fewer mirages. |
| Candidate point on a NavMesh island | The reachability veto scores it 20%. |
| Player lost | Memory (4 s), then Search (go to the last known point and look around), then Patrol. |
| Bot defeated | All mirages shatter, the collider and agent turn off, and the `onDefeated` UnityEvent fires (doors and next arena). |

## 7. Clean architecture (rubric: "AI logic decoupled from visuals")

| File | Responsibility | Knows about visuals? |
|---|---|---|
| `IllusionUtility.cs` | Scoring maths (plain C#) | No |
| `PlayerModel.cs` | Learning maths (plain C#) | No |
| `IllusionBotBrain.cs` | Perception, FSM, actions | No. It raises `MoodChanged` / `Swapped` events |
| `IllusionBotVisuals.cs` | Ring spin, shard bob, eye colour, effects | Listens to events only |
| `IllusionBotHealth.cs` | HP numbers and events | No |
| `MiragePool.cs` / `IllusionMirage.cs` | Object pool and fake copies | Calls visuals for its own body |
| `IllusionProjectile.cs` | Raycast bolt | No |
| `IDamageable.cs` | Shared "can be shot" contract | No |

## 8. Optimisation (GV rubric)

- Perception at **10 Hz**, decisions at **4 Hz**, not every frame.
- **Object pools** for mirages and bolts: no Instantiate/Destroy mid-fight, so no garbage-collection spikes.
- Bolts use a **raycast per frame** (no Rigidbody or collider), which is cheaper and cannot tunnel through walls.
- `RaycastNonAlloc` with a reused buffer: no allocations.
- One shared bolt material (batchable). Only the eye gets its own material copy; the body keeps the shared one.
- Bolts and mirages cast **no shadows** (fewer shadow draws, and it is the gameplay weakness).
- Model: 1,554 triangles, one 1024x1024 texture, no bones (transform animation instead of skinning).

## 9. Tuning

Every number is exposed in the Inspector on `IllusionBotBrain`, under **Utility AI weights**,
**Perception**, **Combat**, **Weakness** and **Mirages**. Turn on **Show Debug Panel** to
see live scores, the current state and the player model while playing.
