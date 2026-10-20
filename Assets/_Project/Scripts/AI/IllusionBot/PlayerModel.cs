using UnityEngine;

namespace FailSafeZero.AI.Illusion
{
    /// <summary>
    /// The bot's "opinion" of the player. Plain C# (not a MonoBehaviour), so it holds
    /// no visuals and can be explained and tested on its own.
    ///
    /// It learns ONE thing: how often the player is fooled by the mirages.
    ///
    ///   FooledRate = estimated chance that the player's next hit lands on a mirage.
    ///
    /// It is updated with an exponential moving average (EMA):
    ///
    ///   FooledRate_new = FooledRate_old + a * (target - FooledRate_old)
    ///   target = 1 when the player hits a mirage (fooled)
    ///   target = 0 when the player hits the real bot (not fooled)
    ///
    /// Why an EMA and not a simple average?
    ///   Recent shots count more than old ones, so the bot adapts as the player LEARNS
    ///   the shadow trick. With a = 0.25, a hit from 4 shots ago only has
    ///   0.75^4 = about 32% of its original weight.
    ///
    /// The starting value is 0.5: before any evidence, we assume a 50/50 player.
    /// </summary>
    public class PlayerModel
    {
        readonly float learningRate;

        Vector3 lastPosition;
        bool hasLastPosition;

        public PlayerModel(float learningRate)
        {
            this.learningRate = Mathf.Clamp01(learningRate);
        }

        /// <summary>0 = never fooled, 1 = always fooled.</summary>
        public float FooledRate { get; private set; } = 0.5f;

        /// <summary>How good the player is at finding the real bot (0..1).</summary>
        public float Skill => 1f - FooledRate;

        public int MirageHits { get; private set; }
        public int RealHits { get; private set; }

        /// <summary>Smoothed player velocity, used to lead (aim ahead of) a moving player.</summary>
        public Vector3 EstimatedVelocity { get; private set; }

        public void RecordMirageHit()
        {
            MirageHits++;
            FooledRate += learningRate * (1f - FooledRate);
        }

        public void RecordRealHit()
        {
            RealHits++;
            FooledRate += learningRate * (0f - FooledRate);
        }

        /// <summary>Call while the player is visible. Estimates velocity from position change.</summary>
        public void ObservePosition(Vector3 position, float deltaTime)
        {
            if (hasLastPosition && deltaTime > 0f)
            {
                Vector3 rawVelocity = (position - lastPosition) / deltaTime;
                // Smooth it so one jittery frame does not ruin the aim.
                EstimatedVelocity = Vector3.Lerp(EstimatedVelocity, rawVelocity, 0.35f);
            }

            lastPosition = position;
            hasLastPosition = true;
        }

        /// <summary>Call when the player is lost, so old velocity is not used later.</summary>
        public void ForgetMotion()
        {
            hasLastPosition = false;
            EstimatedVelocity = Vector3.zero;
        }
    }
}
