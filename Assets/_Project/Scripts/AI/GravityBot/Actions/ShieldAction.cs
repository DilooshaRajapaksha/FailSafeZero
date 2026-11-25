using UnityEngine;
using static FailSafeZero.AI.Gravity.UtilityCurves;

namespace FailSafeZero.AI.Gravity
{
    
    public class ShieldAction : UtilityAction
    {
        public override string Name => "Shield";
        protected override float Cooldown => 6f;

        private const float DrainPerSecond = 0.08f;
        private float endsAt;

        public override float Score(GravityBotContext c)
        {
            int ammo = c.NearbyThrowables.Count + c.Hands.HeldCount;
            if (ammo == 0 || c.Energy01 < 0.15f) return 0f;
            float hurt = Inverse(c.Health01, 0.25f, 0.75f);
            float underFire = Inverse(c.TimeSinceDamaged, 0.5f, 2.5f);
            return Combine(hurt, underFire, Linear(ammo, 0f, 3f), Linear(c.Energy01, 0.15f, 0.5f));
        }

        public override void Enter(GravityBotContext c)
        {
            foreach (var t in c.NearbyThrowables)
            {
                if (c.Hands.HeldCount >= 3) break;
                c.Hands.Lift(t);
            }
            c.Hands.SetOrbit(true);
            endsAt = c.Now + 4f;
        }

        public override ActionStatus Tick(GravityBotContext c, float dt)
        {
            c.Bot.SpendEnergy(DrainPerSecond * dt);
            if (c.CanSeePlayer)
            {
                Vector3 away = (c.Bot.transform.position - c.Player.position).normalized * 3f;
                c.Agent.isStopped = false;
                c.Agent.SetDestination(c.Bot.ClampToHome(c.Bot.transform.position + away));
            }
            return c.Now > endsAt || c.Energy01 <= 0.05f ? ActionStatus.Done : ActionStatus.Running;
        }

        public override void Exit(GravityBotContext c, float now)
        {
            base.Exit(c, now);
            c.Hands.SetOrbit(false);
        }
    }
}
