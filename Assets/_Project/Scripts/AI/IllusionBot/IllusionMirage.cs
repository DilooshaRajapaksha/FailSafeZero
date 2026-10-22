using UnityEngine;
using UnityEngine.AI;

namespace FailSafeZero.AI.Illusion
{
    /// <summary>
    /// One fake copy of the Illusion Bot.
    ///
    /// A mirage copies EVERYTHING the player normally looks at: the same model,
    /// strafing, eye colours and charge-then-fire attack. Two differences only:
    ///   1. It casts NO shadow (the weakness the hint tells the player about).
    ///   2. Its bolts do 0 damage.
    /// When hit, it shatters and tells the brain "the player was fooled", which
    /// feeds the PlayerModel.
    ///
    /// Mirages are created once by MiragePool and reused, never destroyed mid-fight.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class IllusionMirage : MonoBehaviour, IDamageable
    {
        IllusionBotBrain owner;
        NavMeshAgent agent;
        IllusionBotVisuals visuals;

        float lifeTimer;
        float retargetTimer;
        float fakeAttackTimer;
        float chargeTimer;
        bool charging;
        bool shattering;

        public bool IsActive => gameObject.activeSelf && !shattering;

        /// <summary>Called once by the pool, right after the mirage is built.</summary>
        public void Bind(IllusionBotBrain brain)
        {
            owner = brain;
            agent = GetComponent<NavMeshAgent>();
            visuals = GetComponent<IllusionBotVisuals>();
        }

        /// <summary>Place and activate the mirage. Returns false if the point is not on the NavMesh.</summary>
        public bool Spawn(Vector3 position, float lifetime)
        {
            Vector3 look = owner.PlayerPosition - position;
            look.y = 0f;
            Quaternion rotation = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;
            transform.SetPositionAndRotation(position, rotation);

            gameObject.SetActive(true);

            if (!agent.Warp(position))
            {
                gameObject.SetActive(false);
                return false;
            }

            agent.isStopped = false;
            agent.updateRotation = false; // we face the player ourselves, like the real bot

            shattering = false;
            charging = false;
            lifeTimer = lifetime;
            retargetTimer = Random.Range(0.2f, 1f);
            fakeAttackTimer = Random.Range(1.5f, 3.5f);

            if (visuals != null)
            {
                visuals.SetShadows(false);
                visuals.SetMood(BotMood.Alert);
                visuals.PlayAppear();
            }

            return true;
        }

        public void WarpTo(Vector3 position)
        {
            if (IsActive)
            {
                agent.Warp(position);
                agent.ResetPath();
                if (visuals != null) visuals.PlaySwapFlicker();
            }
        }

        public void Shatter()
        {
            if (shattering || !gameObject.activeSelf)
            {
                return;
            }

            shattering = true;
            charging = false;
            if (agent.isOnNavMesh) agent.isStopped = true;

            if (visuals != null)
            {
                visuals.PlayShatter(() => gameObject.SetActive(false));
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void ReceiveHit(float amount, Vector3 hitPoint, GameObject source)
        {
            if (shattering)
            {
                return;
            }

            if (owner != null)
            {
                owner.NotifyMirageHit(source);
            }

            Shatter();
        }

        /// <summary>Compatibility with SendMessage("TakeDamage", x).</summary>
        public void TakeDamage(float amount)
        {
            ReceiveHit(amount, transform.position, null);
        }

        void Update()
        {
            if (shattering || owner == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            lifeTimer -= dt;
            if (lifeTimer <= 0f)
            {
                Shatter();
                return;
            }

            FacePlayer(dt);

            if (charging)
            {
                chargeTimer -= dt;
                if (chargeTimer <= 0f)
                {
                    charging = false;
                    agent.isStopped = false;
                    if (visuals != null) visuals.SetMood(BotMood.Alert);

                    // Same bolt as the real bot, but 0 damage.
                    Vector3 origin = visuals != null ? visuals.MuzzlePosition : transform.position + Vector3.up;
                    owner.FireProjectile(origin, owner.PlayerAimPoint - origin, 0f, gameObject);
                }
                return;
            }

            // Strafe like the real bot, using the same position-search the brain uses.
            retargetTimer -= dt;
            if (retargetTimer <= 0f)
            {
                retargetTimer = Random.Range(1.8f, 3.2f);
                agent.SetDestination(owner.FindStrafePoint(transform.position, transform));
            }

            // Fake attack with the same telegraph (violet eye, stop, fire).
            fakeAttackTimer -= dt;
            if (fakeAttackTimer <= 0f)
            {
                fakeAttackTimer = Random.Range(owner.AttackCooldown * 1.2f, owner.AttackCooldown * 2.2f);
                if (owner.CanSeePlayer)
                {
                    charging = true;
                    chargeTimer = owner.ChargeTime;
                    agent.isStopped = true;
                    if (visuals != null) visuals.SetMood(BotMood.Charging);
                }
            }
        }

        void FacePlayer(float dt)
        {
            Vector3 d = owner.PlayerPosition - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f)
            {
                return;
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * dt);
        }
    }
}
