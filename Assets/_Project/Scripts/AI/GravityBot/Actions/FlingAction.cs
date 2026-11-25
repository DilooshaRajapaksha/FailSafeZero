using UnityEngine;
using FailSafeZero.Interactions;
using static FailSafeZero.AI.Gravity.UtilityCurves;

namespace FailSafeZero.AI.Gravity
{
    /// <summary>
    /// The main attack. Pick the best nearby object, lift it above its head (this pause
    /// is the player's warning to take cover), then hurl it where the player WILL be,
    /// using BallisticSolver to lead the target. Much stronger if the player is floating
    /// (from LevitatePlayer), because a floating player cannot dodge: a planned combo.
    /// </summary>
    public class FlingAction : UtilityAction
    {
        public override string Name => "Fling";
        protected override float Cooldown => 1.2f;
        public override bool CanBeInterrupted => phase == Phase.None;

        public const float EnergyCost = 0.2f;
        private const float LiftTime = 0.9f;
        private const float ThrowSpeed = 16f;

        private enum Phase { None, Lifting, Done }
        private Phase phase;
        private Throwable projectile;
        private float phaseEnds;

        public override float Score(GravityBotContext c)
        {
            if (!c.CanSeePlayer || c.Energy01 < EnergyCost) return 0f;
            bool hasAmmo = c.Hands.HeldCount > 0 || c.NearbyThrowables.Count > 0;
            if (!hasAmmo) return 0f;

            float range = Bell(c.DistanceToPlayer, 10f, 7f);
            float energy = Linear(c.Energy01, EnergyCost, 0.8f);
            float combo = c.PlayerIsFloating ? 1f : 0.75f;
            return Combine(range, Mathf.Max(0.3f, energy), combo);
        }

        public override void Enter(GravityBotContext c)
        {
            c.Agent.isStopped = true;
            projectile = c.Hands.HeldCount > 0 ? c.Hands.Held[0] : ChooseBestObject(c);
            if (projectile == null || !(c.Hands.HeldCount > 0 || c.Hands.Lift(projectile))) { phase = Phase.Done; return; }
            c.Bot.SpendEnergy(EnergyCost);
            phase = Phase.Lifting;
            phaseEnds = c.Now + LiftTime;
        }

        public override ActionStatus Tick(GravityBotContext c, float dt)
        {
            c.Bot.FaceTowards(c.Player.position);
            if (phase == Phase.Done || projectile == null) return ActionStatus.Done;
            if (c.Now < phaseEnds) return ActionStatus.Running;

            Vector3 origin = projectile.Body.position;
            Vector3 target = c.Player.position + Vector3.up * 0.3f;
            Vector3 vel = c.PlayerController != null ? c.PlayerController.Velocity : Vector3.zero;
            Vector3 dir = BallisticSolver.InterceptDirection(origin, target, vel, ThrowSpeed, out _);

            c.Hands.Launch(projectile, dir * ThrowSpeed);
            phase = Phase.Done;
            return ActionStatus.Done;
        }

        public override void Exit(GravityBotContext c, float now)
        {
            base.Exit(c, now);
            phase = Phase.None;
            projectile = null;
            c.Agent.isStopped = false;
        }

        /// <summary>
        /// Object choice: prefer close objects (cheaper to lift), medium mass (heavy hurts more,
        /// but too heavy flies badly), and ones with a clear line to the player.
        /// </summary>
        private static Throwable ChooseBestObject(GravityBotContext c)
        {
            Throwable best = null;
            float bestScore = 0f;
            foreach (var t in c.NearbyThrowables)
            {
                if (t == null || t.IsHeld) continue;
                float near = Inverse(Vector3.Distance(t.transform.position, c.Bot.transform.position), 0f, c.Bot.LiftRadius);
                float mass = Bell(t.Body.mass, 12f, 10f);
                bool clear = c.Bot.HasLineOfSight(t.transform.position, c.Player.position + Vector3.up * 0.5f);
                float s = Combine(near, mass, clear ? 1f : 0.4f);
                if (s > bestScore) { bestScore = s; best = t; }
            }
            return best;
        }
    }
}
