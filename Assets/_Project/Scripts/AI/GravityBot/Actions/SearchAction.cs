using UnityEngine;
using UnityEngine.AI;
using static FailSafeZero.AI.Gravity.UtilityCurves;

namespace FailSafeZero.AI.Gravity
{
    
    public class SearchAction : UtilityAction
    {
        public override string Name => "Search";
        private float sweepUntil;
        private Vector3 lastClue;

        public override float Score(GravityBotContext c)
        {
            if (c.CanSeePlayer) return 0.05f;
            return Combine(0.35f + 0.55f * Linear(c.TimeSinceSeen, 0f, 4f));
        }

        public override void Enter(GravityBotContext c)
        {
            c.Agent.isStopped = false;
            c.Agent.speed = 3.5f;
            lastClue = c.LastKnownPlayerPosition;
            Vector3 goal = c.HasLastKnownPosition ? c.LastKnownPlayerPosition : RandomPointNear(c.Bot.Home, 7f);
            c.Agent.SetDestination(c.Bot.ClampToHome(goal));
            sweepUntil = 0f;
        }

        public override ActionStatus Tick(GravityBotContext c, float dt)
        {
            if (c.HasLastKnownPosition && (c.LastKnownPlayerPosition - lastClue).sqrMagnitude > 4f)
            {
                lastClue = c.LastKnownPlayerPosition;
                sweepUntil = 0f;
                c.Agent.SetDestination(c.Bot.ClampToHome(lastClue));
                return ActionStatus.Running;
            }

            if (c.Agent.pathPending || c.Agent.remainingDistance > c.Agent.stoppingDistance + 0.3f) return ActionStatus.Running;

            
            if (sweepUntil <= 0f) sweepUntil = c.Now + 6f;
            if (c.Now > sweepUntil) { c.HasLastKnownPosition = false; return ActionStatus.Done; }
            c.Agent.SetDestination(c.Bot.ClampToHome(RandomPointNear(c.Bot.transform.position, 5f)));
            return ActionStatus.Running;
        }

        private static Vector3 RandomPointNear(Vector3 centre, float radius)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 p = centre + new Vector3(r.x, 0f, r.y);
            return NavMesh.SamplePosition(p, out NavMeshHit hit, radius, NavMesh.AllAreas) ? hit.position : centre;
        }
    }
}
