using FailSafeZero.AI;
using FailSafeZero.AI.Illusion;
using UnityEngine;

namespace FailSafeZero.Core
{
    /// <summary>
    /// Gives the Zombie a HEAVY melee hit. Put it on ZombieBot_Agent, next to ZombieAgentAI.
    ///
    /// It does not change the zombie's AI. It only WATCHES it: when ZombieAgentAI says
    /// it is in the Attacking state and the player is within reach, it deals damage
    /// every strikeInterval seconds.
    ///
    /// 40 damage vs 100 player HP = 3 hits to die, so fighting it up close is a bad idea.
    /// The smart play is still Akini's design: stand still and it freezes.
    /// </summary>
    [RequireComponent(typeof(ZombieAgentAI))]
    public class ZombieStrike : MonoBehaviour
    {
        [SerializeField] float damagePerHit = 40f;
        [SerializeField] float strikeInterval = 1.2f;
        [Tooltip("Extra reach on top of the zombie's own attack range.")]
        [SerializeField] float reachBonus = 0.4f;

        ZombieAgentAI zombie;
        float strikeTimer;

        void Awake()
        {
            zombie = GetComponent<ZombieAgentAI>();
        }

        void Update()
        {
            strikeTimer -= Time.deltaTime;

            if (zombie.playerTarget == null || zombie.CurrentState != ZombieAgentAI.AgentState.Attacking)
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, zombie.playerTarget.position);
            if (distance > zombie.attackRange + reachBonus || strikeTimer > 0f)
            {
                return;
            }

            strikeTimer = strikeInterval;

            IDamageable target = zombie.playerTarget.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.ReceiveHit(damagePerHit, zombie.playerTarget.position, gameObject);
            }
            else
            {
                zombie.playerTarget.SendMessageUpwards("TakeDamage", damagePerHit, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
