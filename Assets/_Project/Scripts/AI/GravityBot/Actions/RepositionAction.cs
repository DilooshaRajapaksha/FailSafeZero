using UnityEngine;
using UnityEngine.AI;
using static FailSafeZero.AI.Gravity.UtilityCurves;

namespace FailSafeZero.AI.Gravity
{
    
    public class RepositionAction : UtilityAction
    {
        public override string Name => "Reposition";
        protected override float Cooldown => 1.5f;

        private const float PreferredRange = 10f;
        private const int Candidates = 12;
        private float giveUpAt;

        public override float Score(GravityBotContext c)
        {
            if (!c.CanSeePlayer && c.TimeSinceSeen > 1f) return 0f;
            float wrongRange = 1f - Bell(c.DistanceToPlayer, PreferredRange, 4f);   // too close OR too far
            float needAmmo = c.NearbyThrowables.Count == 0 && c.Hands.HeldCount == 0 ? 1f : 0.3f;
            return Combine(Mathf.Max(wrongRange, needAmmo * 0.8f), Linear(c.Energy01, 0.05f, 0.3f));
        }

        public override void Enter(GravityBotContext c)
        {
            c.Agent.isStopped = false;
            c.Agent.speed = 4.5f;
            c.Agent.SetDestination(PickBestPoint(c));
            giveUpAt = c.Now + 4f;
        }

        public override ActionStatus Tick(GravityBotContext c, float dt)
        {
            bool arrived = !c.Agent.pathPending && c.Agent.remainingDistance <= c.Agent.stoppingDistance + 0.3f;
            return arrived || c.Now > giveUpAt ? ActionStatus.Done : ActionStatus.Running;
        }

        private Vector3 PickBestPoint(GravityBotContext c)
        {
            Vector3 playerPos = c.HasLastKnownPosition ? c.LastKnownPlayerPosition : c.Player.position;
            Vector3 best = c.Bot.ClampToHome(c.Bot.transform.position);
            float bestScore = float.MinValue;

            for (int i = 0; i < Candidates; i++)
            {
                float angle = i * Mathf.PI * 2f / Candidates;
                Vector3 ring = playerPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * PreferredRange;
                Vector3 p = c.Bot.ClampToHome(ring);   // pulled back inside the arena and onto the NavMesh

                Vector3 eye = p + Vector3.up * 1.6f;
                bool los = c.Bot.HasLineOfSight(eye, playerPos + Vector3.up * 0.5f);
                int ammo = c.Bot.CountThrowablesNear(p);
                float travel = Vector3.Distance(c.Bot.transform.position, p);
                float rangeError = Mathf.Abs(Vector3.Distance(p, playerPos) - PreferredRange);

                
                float score = (los ? 3f : 0f) + Mathf.Min(ammo, 3) * 1f - rangeError * 0.15f - travel * 0.08f;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }
    }
}