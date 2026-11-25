using UnityEngine;
using UnityEngine.AI;
using static FailSafeZero.AI.Gravity.UtilityCurves;

namespace FailSafeZero.AI.Gravity
{
   
    public class RechargeAction : UtilityAction
    {
        public override string Name => "Recharge (weak!)";
        public override bool CanBeInterrupted => false;

        private const float RechargePerSecond = 0.16f;
        private enum Phase { Retreat, Charging }
        private Phase phase;
        private float phaseEnds;

        public override float Score(GravityBotContext c)
        {
            if (c.Energy01 >= 0.95f) return 0f;
            return Logistic(1f - c.Energy01, 0.75f, 14f);   // energy 25% -> 0.5, 10% -> ~0.9
        }

        public override void Enter(GravityBotContext c)
        {
            phase = Phase.Retreat;
            phaseEnds = c.Now + 2f;
            c.Hands.DropAll();
            c.Agent.isStopped = false;
            c.Agent.speed = 5f;
            c.Agent.SetDestination(RetreatPoint(c));
        }

        public override ActionStatus Tick(GravityBotContext c, float dt)
        {
            if (phase == Phase.Retreat)
            {
                bool arrived = !c.Agent.pathPending && c.Agent.remainingDistance <= c.Agent.stoppingDistance + 0.3f;
                if (!arrived && c.Now < phaseEnds) return ActionStatus.Running;
                phase = Phase.Charging;
                c.Agent.isStopped = true;
                c.Bot.SetCoreExposed(true);
            }

            c.Bot.AddEnergy(RechargePerSecond * dt);
            return c.Energy01 >= 0.9f ? ActionStatus.Done : ActionStatus.Running;
        }

        public override void Exit(GravityBotContext c, float now)
        {
            base.Exit(c, now);
            c.Bot.SetCoreExposed(false);
            c.Agent.isStopped = false;
        }

        private static Vector3 RetreatPoint(GravityBotContext c)
        {
            Vector3 self = c.Bot.transform.position;
            if (c.Player == null) return self;
            Vector3 away = (self - c.Player.position);
            away.y = 0f;
            Vector3 p = self + away.normalized * 6f;
            return c.Bot.ClampToHome(p);
        }
    }
}
