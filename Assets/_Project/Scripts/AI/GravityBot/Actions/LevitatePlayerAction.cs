using UnityEngine;
using static FailSafeZero.AI.Gravity.UtilityCurves;

namespace FailSafeZero.AI.Gravity
{
    /// <summary>
    /// Flips the player's gravity for a moment: they float up and drift, unable to jump
    /// or dodge, then gravity snaps back hard. The brain values this most when there is
    /// ammo nearby, because it sets up an easy Fling: the combo emerges from the scores,
    /// it is not hard-coded as a sequence.
    /// </summary>
    public class LevitatePlayerAction : UtilityAction
    {
        public override string Name => "Levitate player";
        protected override float Cooldown => 9f;
        public override bool CanBeInterrupted => false;

        public const float EnergyCost = 0.3f;
        private float started;

        public override float Score(GravityBotContext c)
        {
            if (!c.CanSeePlayer || c.PlayerController == null || c.Energy01 < EnergyCost || c.PlayerIsFloating) return 0f;
            float close = Inverse(c.DistanceToPlayer, 4f, 15f);
            float energy = Linear(c.Energy01, EnergyCost, 1f);
            float ammo = Linear(c.NearbyThrowables.Count + c.Hands.HeldCount, 0f, 2f);
            return Combine(close, energy, Mathf.Max(0.2f, ammo)) * 0.95f;
        }

        public override void Enter(GravityBotContext c)
        {
            c.Agent.isStopped = true;
            c.Bot.SpendEnergy(EnergyCost);
            started = c.Now;
            c.Bot.HoldPlayerFloating();
        }

        public override ActionStatus Tick(GravityBotContext c, float dt)
        {
            float t = c.Now - started;
            if (c.PlayerController == null) return ActionStatus.Done;
            c.Bot.FaceTowards(c.Player.position);

            // 0-0.6s: gravity reversed, the player is lifted (~1-2 m).
            // After that: quarter gravity, a slow-motion fall where they can't dodge or jump.
            c.PlayerController.GravityScale = t < 0.6f ? -0.25f : 0.25f;
            return t < 1.0f ? ActionStatus.Running : ActionStatus.Done;
        }

        public override void Exit(GravityBotContext c, float now)
        {
            base.Exit(c, now);
            c.Agent.isStopped = false;
            // Float lasts a bit longer than the action, so the brain can follow up with Fling.
            c.Bot.ReleasePlayerAfter(1.3f);
        }
    }
}
