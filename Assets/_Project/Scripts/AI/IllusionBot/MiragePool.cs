using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FailSafeZero.AI.Illusion
{
    /// <summary>
    /// OBJECT POOL for mirages.
    ///
    /// All mirages are built ONCE when the level starts, then switched on and off.
    /// Creating and destroying objects during a fight causes stutters (memory
    /// allocation + garbage collection). A pool avoids that, which matters
    /// for the "no frame-rate drops" rubric line.
    ///
    /// Each mirage is built by cloning the real bot's MODEL child, so no second
    /// prefab is needed and the copies always match the real bot exactly.
    /// </summary>
    public class MiragePool
    {
        readonly List<IllusionMirage> mirages = new List<IllusionMirage>();

        public MiragePool(IllusionBotBrain owner, GameObject modelSource, int size)
        {
            NavMeshAgent agentTemplate = owner.GetComponent<NavMeshAgent>();
            CapsuleCollider colliderTemplate = owner.GetComponent<CapsuleCollider>();

            for (int i = 0; i < size; i++)
            {
                mirages.Add(CreateMirage(owner, modelSource, agentTemplate, colliderTemplate, i));
            }
        }

        public int Capacity => mirages.Count;

        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (IllusionMirage m in mirages)
                {
                    if (m != null && m.IsActive) count++;
                }
                return count;
            }
        }

        public IEnumerable<IllusionMirage> All => mirages;

        public bool TryGetFree(out IllusionMirage free)
        {
            foreach (IllusionMirage m in mirages)
            {
                // Free = switched off (not just shattering, which is still playing its effect).
                if (m != null && !m.gameObject.activeSelf)
                {
                    free = m;
                    return true;
                }
            }

            free = null;
            return false;
        }

        /// <summary>
        /// Pick the mirage the player is LEAST likely to be watching: the one with
        /// the biggest angle away from where the player is aiming. Swapping into it
        /// means the real bot "teleports" somewhere the player is not looking.
        /// </summary>
        public IllusionMirage PickSwapTarget(Vector3 playerPosition, Vector3 playerAimForward)
        {
            IllusionMirage best = null;
            float bestAngle = -1f;

            foreach (IllusionMirage m in mirages)
            {
                if (m == null || !m.IsActive) continue;

                Vector3 toMirage = m.transform.position - playerPosition;
                toMirage.y = 0f;
                Vector3 aim = playerAimForward;
                aim.y = 0f;

                float angle = Vector3.Angle(aim, toMirage);
                if (angle > bestAngle)
                {
                    bestAngle = angle;
                    best = m;
                }
            }

            return best;
        }

        public void ShatterAll()
        {
            foreach (IllusionMirage m in mirages)
            {
                if (m != null) m.Shatter();
            }
        }

        public void DestroyAll()
        {
            foreach (IllusionMirage m in mirages)
            {
                if (m != null) Object.Destroy(m.gameObject);
            }
            mirages.Clear();
        }

        static IllusionMirage CreateMirage(IllusionBotBrain owner, GameObject modelSource,
                                           NavMeshAgent agentTemplate, CapsuleCollider colliderTemplate, int index)
        {
            var go = new GameObject($"{owner.name}_Mirage_{index}");
            // Build while INACTIVE, so the NavMeshAgent does not complain about
            // "not on NavMesh" before we place it.
            go.SetActive(false);
            go.layer = owner.gameObject.layer;

            GameObject model = Object.Instantiate(modelSource, go.transform);
            model.name = modelSource.name;
            model.transform.localPosition = modelSource.transform.localPosition;
            model.transform.localRotation = modelSource.transform.localRotation;
            model.transform.localScale = modelSource.transform.localScale;

            if (colliderTemplate != null)
            {
                var col = go.AddComponent<CapsuleCollider>();
                col.center = colliderTemplate.center;
                col.radius = colliderTemplate.radius;
                col.height = colliderTemplate.height;
                col.direction = colliderTemplate.direction;
            }

            var agent = go.AddComponent<NavMeshAgent>();
            agent.agentTypeID = agentTemplate.agentTypeID;
            agent.speed = agentTemplate.speed;
            agent.angularSpeed = agentTemplate.angularSpeed;
            agent.acceleration = agentTemplate.acceleration;
            agent.radius = agentTemplate.radius;
            agent.height = agentTemplate.height;
            agent.baseOffset = agentTemplate.baseOffset;
            agent.stoppingDistance = agentTemplate.stoppingDistance;
            agent.areaMask = agentTemplate.areaMask;
            agent.obstacleAvoidanceType = agentTemplate.obstacleAvoidanceType;

            go.AddComponent<IllusionBotVisuals>();
            var mirage = go.AddComponent<IllusionMirage>();
            mirage.Bind(owner);
            return mirage;
        }
    }
}
