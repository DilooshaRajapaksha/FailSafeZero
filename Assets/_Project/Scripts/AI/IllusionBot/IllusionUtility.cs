using System;
using UnityEngine;

namespace FailSafeZero.AI.Illusion
{
    /// <summary>The five tactics the bot can choose between while fighting.</summary>
    public enum IllusionAction
    {
        Attack,       // stop, charge, fire a bolt at the player
        SpawnMirages, // create fake copies around the player
        Swap,         // teleport-swap places with a mirage the player is not looking at
        Reposition,   // strafe sideways around the player (hard to hit)
        Retreat       // back away when hurt and too close
    }

    /// <summary>
    /// All tuning numbers in one place, editable in the Inspector.
    /// Each number is explained so it can be justified in the viva.
    /// </summary>
    [Serializable]
    public class UtilitySettings
    {
        [Tooltip("Distance (m) the bot likes to fight at. Far enough to dodge, close enough to hit.")]
        public float preferredRange = 10f;

        [Tooltip("Beyond this distance (m) the bot will not shoot.")]
        public float maxAttackRange = 22f;

        [Tooltip("Mirages used against a player who is always fooled (skill 0).")]
        public int minMirages = 1;

        [Tooltip("Mirages used against a player who always finds the real bot (skill 1).")]
        public int maxMirages = 4;

        [Tooltip("Seconds before another swap is allowed.")]
        public float swapCooldown = 4f;

        [Tooltip("Seconds before another wave of mirages is allowed.")]
        public float spawnCooldown = 3f;

        [Tooltip("Bonus added to the action already running, so the bot does not flicker between two near-equal choices.")]
        public float hysteresisBonus = 0.1f;

        [Tooltip("For this many seconds after being hit, the bot feels 'urgent' (wants to swap).")]
        public float hitUrgencyWindow = 1.5f;
    }

    /// <summary>A snapshot of everything the decision needs. Filled by the brain each tick.</summary>
    public struct UtilityContext
    {
        public bool CanSeePlayer;
        public float Distance;
        public float Health01;
        public int ActiveMirages;
        public int DesiredMirages;
        public bool AttackReady;
        public float TimeSinceSwap;
        public float TimeSinceSpawn;
        public float TimeSinceHit;
        public float PlayerSkill;
    }

    /// <summary>
    /// UTILITY AI. This is the decision-making core.
    ///
    /// Instead of a long IF/ELSE chain, every action gets a SCORE between 0 and 1
    /// that says "how useful is this action right now?". The highest score wins.
    ///
    /// Each score is built from "considerations" (small 0..1 curves) that are
    /// MULTIPLIED together. Multiplying means any consideration of 0 vetoes the action.
    /// For example, Attack is 0 when the player is not visible, however good the range is.
    ///
    /// Plain C#, no MonoBehaviour: it only does maths on the context, so it is
    /// fully decoupled from Unity objects and visuals.
    /// </summary>
    public class IllusionUtility
    {
        public static readonly int ActionCount = Enum.GetValues(typeof(IllusionAction)).Length;

        readonly UtilitySettings settings;

        public IllusionUtility(UtilitySettings settings)
        {
            this.settings = settings;
        }

        /// <summary>Raw scores from the last decision (shown in the debug panel).</summary>
        public float[] LastScores { get; } = new float[ActionCount];

        public IllusionAction Choose(in UtilityContext c, IllusionAction current)
        {
            LastScores[(int)IllusionAction.Attack] = ScoreAttack(c);
            LastScores[(int)IllusionAction.SpawnMirages] = ScoreSpawn(c);
            LastScores[(int)IllusionAction.Swap] = ScoreSwap(c);
            LastScores[(int)IllusionAction.Reposition] = ScoreReposition(c);
            LastScores[(int)IllusionAction.Retreat] = ScoreRetreat(c);

            IllusionAction best = current;
            float bestScore = float.MinValue;

            for (int i = 0; i < ActionCount; i++)
            {
                float score = LastScores[i];

                // Hysteresis: the running action gets a small bonus so the bot
                // commits to a plan instead of jittering between equal options.
                if (i == (int)current && score > 0f)
                {
                    score += settings.hysteresisBonus;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = (IllusionAction)i;
                }
            }

            return best;
        }

        // ---------------------------------------------------------------- scores

        /// Attack = 0.8 x inRange x confidence
        /// inRange: 1 inside preferredRange, falls linearly to 0 at maxAttackRange.
        /// confidence: 0.5 to 1. A hurt bot still attacks, just a little less eagerly.
        /// 0.8 cap: attack is strong but loses to an urgent Swap (0.95) or a needed Spawn (0.9).
        float ScoreAttack(in UtilityContext c)
        {
            if (!c.CanSeePlayer || !c.AttackReady)
            {
                return 0f;
            }

            float span = Mathf.Max(0.01f, settings.maxAttackRange - settings.preferredRange);
            float inRange = 1f - Mathf.Clamp01((c.Distance - settings.preferredRange) / span);
            float confidence = 0.5f + 0.5f * c.Health01;
            return 0.8f * inRange * confidence;
        }

        /// Spawn = 0.9 x sqrt(missingFraction) x awareness
        /// sqrt: even ONE missing mirage out of four still scores well
        /// (sqrt(0.25) = 0.5), because the illusion is the bot's main defence.
        /// awareness: 1 if the player is visible, else 0.5 (prepare an ambush).
        float ScoreSpawn(in UtilityContext c)
        {
            if (c.DesiredMirages <= 0 || c.TimeSinceSpawn < settings.spawnCooldown)
            {
                return 0f;
            }

            float missing = Mathf.Clamp01(1f - (float)c.ActiveMirages / c.DesiredMirages);
            float awareness = c.CanSeePlayer ? 1f : 0.5f;
            return 0.9f * Mathf.Sqrt(missing) * awareness;
        }

        /// Swap = 0.95 x cooldownReady x urgency x skillFactor
        /// cooldownReady: ramps 0 to 1 over one extra cooldown length (no instant re-swaps).
        /// urgency: 1 just after being hit, else 0.45.
        /// Why 0.45: against an EXPERT (skill 1) an idle swap scores 0.95 x 0.45 = 0.43,
        /// which beats moving (0.30 + 0.10 hysteresis = 0.40). Against an average
        /// player (skill 0.5) it scores 0.30, so it loses. Swapping adapts to skill.
        /// skillFactor: 0.4 to 1. Swap more against players who find the real bot often.
        /// 0.95 cap: the highest of all. An urgent swap is the bot's best escape.
        float ScoreSwap(in UtilityContext c)
        {
            if (c.ActiveMirages == 0)
            {
                return 0f;
            }

            float cd = Mathf.Max(0.01f, settings.swapCooldown);
            float cooldownReady = Mathf.Clamp01((c.TimeSinceSwap - cd) / cd);
            float urgency = c.TimeSinceHit < settings.hitUrgencyWindow ? 1f : 0.45f;
            float skillFactor = 0.4f + 0.6f * c.PlayerSkill;
            return 0.95f * cooldownReady * urgency * skillFactor;
        }

        /// Reposition = 0.3 + 0.2 x tooClose. This is the "default" behaviour:
        /// it never drops to 0, so the bot always has something sensible to do.
        float ScoreReposition(in UtilityContext c)
        {
            if (!c.CanSeePlayer)
            {
                return 0.2f;
            }

            float tooClose = 1f - Mathf.Clamp01(c.Distance / Mathf.Max(0.01f, settings.preferredRange));
            return 0.3f + 0.2f * tooClose;
        }

        /// Retreat = danger^2 x (0.5 + 0.5 x tooClose)
        /// danger^2: barely matters at 70% HP (0.09) but a lot at 20% HP (0.64).
        /// Worked example at 20% HP with the player 3 m away (preferredRange 10):
        ///   Retreat    = 0.64 x (0.5 + 0.5 x 0.7) = 0.54
        ///   Reposition = 0.3 + 0.2 x 0.7          = 0.44   -> the bot runs.
        /// At 50% HP: Retreat = 0.25 x 0.85 = 0.21 -> it stands and fights.
        float ScoreRetreat(in UtilityContext c)
        {
            float danger = 1f - c.Health01;
            float tooClose = 1f - Mathf.Clamp01(c.Distance / Mathf.Max(0.01f, settings.preferredRange));
            return danger * danger * (0.5f + 0.5f * tooClose);
        }
    }
}
