using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace FailSafeZero.AI
{
    public class ZombieSetupBuilder : MonoBehaviour
    {
        [ContextMenu("1. Spawn Zombie Bot & Create Prefab (Commit #11)")]
        public void SpawnZombieAndPrefab()
        {
            GameObject existing = GameObject.Find("ZombieBot_Agent");
            if (existing != null) DestroyImmediate(existing);

            // Create Zombie Capsule in Arena 1
            GameObject zombie = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            zombie.name = "ZombieBot_Agent";
            zombie.transform.position = new Vector3(0f, 1.0f, 28f); // Inside Arena 1

            // Visual Visor to show facing direction
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Zombie_Visor";
            visor.transform.SetParent(zombie.transform);
            visor.transform.localPosition = new Vector3(0f, 0.5f, 0.4f);
            visor.transform.localScale = new Vector3(0.6f, 0.25f, 0.3f);

            Shader urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material zMat = new Material(urp) { color = new Color(0.15f, 0.45f, 0.2f) };
            Material vMat = new Material(urp) { color = Color.red };
            zombie.GetComponent<Renderer>().sharedMaterial = zMat;
            visor.GetComponent<Renderer>().sharedMaterial = vMat;

            // Add NavMeshAgent & ZombieAgentAI
            NavMeshAgent agent = zombie.AddComponent<NavMeshAgent>();
            agent.speed = 3.8f;
            agent.angularSpeed = 360f;
            agent.acceleration = 16f;
            agent.stoppingDistance = 1.5f;

            ZombieAgentAI ai = zombie.AddComponent<ZombieAgentAI>();

            // Link Player if present, or create a tagged Test_Player in Arena 1
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                player.name = "Player_TestTarget";
                player.tag = "Player";
                player.transform.position = new Vector3(0f, 1.0f, 16f); // Entrance of Arena 1
            }
            ai.playerTarget = player.transform;

#if UNITY_EDITOR
            string prefabFolder = "Assets/_Project/Prefabs/Bots";
            if (!AssetDatabase.IsValidFolder(prefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Bots");
            }
            PrefabUtility.SaveAsPrefabAssetAndConnect(zombie, prefabFolder + "/ZombieBot_Agent.prefab", InteractionMode.AutomatedAction);
#endif
            SaveSceneChanges();
            Debug.Log("Commit #11 Ready: ZombieBot spawned in Arena 1 and saved as Prefab!");
        }

        [ContextMenu("2. Tune Zombie Speed & Detection Balance (Commit #12)")]
        public void TuneZombieParameters()
        {
            ZombieAgentAI ai = Object.FindFirstObjectByType<ZombieAgentAI>();
            if (ai == null) return;

            ai.detectionRadius = 24.0f;
            ai.motionVelocityThreshold = 0.12f;
            ai.heuristicWeight = 1.45f;

            NavMeshAgent agent = ai.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.speed = 4.2f;
                agent.acceleration = 22.0f;
            }

            SaveSceneChanges();
            Debug.Log("Commit #12 Ready: Zombie parameters tuned for scary but fair gameplay!");
        }

        private void SaveSceneChanges()
        {
#if UNITY_EDITOR
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
#endif
        }
    }
}