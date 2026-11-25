namespace FailSafeZero.AI.Gravity
{
    public enum ActionStatus { Running, Done }

    /// <summary>
    /// One thing the Gravity bot can do. Each action answers two questions:
    /// "how good would I be right now?" (Score) and "do one step of me" (Tick).
    /// The brain asks every action for a score and runs the winner.
    /// New behaviours are added by writing a new action, without touching the others.
    /// </summary>
    public abstract class UtilityAction
    {
        public abstract string Name { get; }

        /// <summary>Seconds after finishing before this action may score above 0 again.</summary>
        protected virtual float Cooldown => 0f;

        /// <summary>False while an action must finish (e.g. mid-throw), so the brain won't cut it off.</summary>
        public virtual bool CanBeInterrupted => true;

        public float LastScore { get; set; }
        private float lastFinishedTime = -999f;

        public bool OnCooldown(float now) => now - lastFinishedTime < Cooldown;

        /// <summary>Returns 0..1. 0 means "impossible right now".</summary>
        public abstract float Score(GravityBotContext ctx);

        public virtual void Enter(GravityBotContext ctx) { }
        public abstract ActionStatus Tick(GravityBotContext ctx, float deltaTime);
        public virtual void Exit(GravityBotContext ctx, float now) => lastFinishedTime = now;
    }
}
