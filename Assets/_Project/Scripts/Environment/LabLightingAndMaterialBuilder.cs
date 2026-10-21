using UnityEngine;
using Unity.AI.Navigation;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace FailSafeZero.Environment
{
    public class LabLightingAndMaterialBuilder : MonoBehaviour
    {
        [ContextMenu("1. Apply Dark Sci-Fi Materials")]
        public void ApplyMaterials()
        {
            Transform root = GameObject.Find("Lab_Environment_Root")?.transform;
            if (root == null)
            {
                Debug.LogWarning("Lab_Environment_Root not found!");
                return;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Material floorMat = CreateOrLoadMaterial("SciFi_Floor_Mat", urpLit, new Color(0.12f, 0.14f, 0.18f), 0.7f, 0.6f);
            Material wallMat = CreateOrLoadMaterial("SciFi_Wall_Mat", urpLit, new Color(0.22f, 0.25f, 0.30f), 0.5f, 0.4f);
            Material pillarMat = CreateOrLoadMaterial("SciFi_Pillar_Mat", urpLit, new Color(0.18f, 0.20f, 0.22f), 0.8f, 0.5f);

            foreach (Renderer rend in root.GetComponentsInChildren<Renderer>())
            {
                if (rend.gameObject.name.Contains("Floor"))
                    rend.sharedMaterial = floorMat;
                else if (rend.gameObject.name.Contains("Pillar"))
                    rend.sharedMaterial = pillarMat;
                else
                    rend.sharedMaterial = wallMat;

                rend.gameObject.isStatic = true;
            }

            SaveSceneChanges();
            Debug.Log("Commit #6 Ready: Dark Sci-Fi Materials applied and saved!");
        }

        [ContextMenu("2. Add Dim Lab & Red Emergency Corridor Lights")]
        public void AddCorridorLights()
        {
            Transform root = GameObject.Find("Lab_Environment_Root")?.transform;
            if (root == null) return;

            // Dim global directional light for dark lab atmosphere
            Light dirLight = Object.FindFirstObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                dirLight.intensity = 0.2f;
                dirLight.color = new Color(0.4f, 0.5f, 0.65f);
            }

            CreateRoomLight(root, "01_StartRoom", new Vector3(0f, 3.2f, 0f), new Color(0.6f, 0.75f, 1f), 2.5f, 12f, false);
            CreateRoomLight(root, "02_Corridor_A", new Vector3(0f, 3.2f, 0f), Color.red, 2.5f, 10f, true);
            CreateRoomLight(root, "04_Corridor_B", new Vector3(0f, 3.2f, 0f), Color.red, 2.5f, 10f, true);
            CreateRoomLight(root, "06_Corridor_C", new Vector3(0f, 3.2f, 0f), Color.red, 2.5f, 10f, true);

            SaveSceneChanges();
            Debug.Log("Commit #7 Ready: Dim lab lights and red emergency lights created!");
        }

        [ContextMenu("3. Add 3 Distinct Arena Mood Lights")]
        public void AddArenaMoodLights()
        {
            Transform root = GameObject.Find("Lab_Environment_Root")?.transform;
            if (root == null) return;

            // Arena 1: Cold Cryo Cyan Mood (Zombie Arena)
            CreateRoomLight(root, "03_Arena1_Zombie", new Vector3(0f, 4.2f, 0f), new Color(0.0f, 0.85f, 0.95f), 5.0f, 25f, false);

            // Arena 2: High-Voltage Industrial Amber Mood (Gravity Arena)
            CreateRoomLight(root, "05_Arena2_Gravity", new Vector3(0f, 6.0f, 0f), new Color(1.0f, 0.55f, 0.05f), 5.5f, 25f, false);

            // Arena 3: Eerie Violet Containment Mood (Illusion Arena)
            CreateRoomLight(root, "07_Arena3_Illusion", new Vector3(0f, 4.2f, 0f), new Color(0.75f, 0.15f, 1.0f), 5.0f, 25f, false);

            // Exit Room: Safe Bio-Green Light
            CreateRoomLight(root, "08_ExitRoom", new Vector3(0f, 3.2f, 0f), new Color(0.1f, 1.0f, 0.3f), 3.0f, 10f, false);

            SaveSceneChanges();
            Debug.Log("Commit #8 Ready: 3 Distinct Arena Moods applied and saved!");
        }

        [ContextMenu("4. Bake Walkable NavMesh Surface")]
        public void BakeLabNavMesh()
        {
            Transform root = GameObject.Find("Lab_Environment_Root")?.transform;
            if (root == null) return;

            NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = root.gameObject.AddComponent<NavMeshSurface>();
            }

            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();

            SaveSceneChanges();
            Debug.Log("Commit #9 Ready: NavMesh baked across all rooms and arenas!");
        }

        private void CreateRoomLight(Transform root, string roomName, Vector3 localPos, Color color, float intensity, float range, bool isEmergency)
        {
            Transform room = root.Find(roomName);
            if (room == null) return;

            string lightName = isEmergency ? "EmergencyLight_Red" : "ArenaMoodLight";
            Transform existing = room.Find(lightName);
            if (existing != null) DestroyImmediate(existing.gameObject);

            GameObject lightObj = new GameObject(lightName);
            lightObj.transform.SetParent(room);
            lightObj.transform.localPosition = localPos;

            Light ptLight = lightObj.AddComponent<Light>();
            ptLight.type = LightType.Point;
            ptLight.color = color;
            ptLight.intensity = intensity;
            ptLight.range = range;
            ptLight.shadows = LightShadows.Soft;

            if (isEmergency)
            {
                lightObj.AddComponent<EmergencyLightPulse>();
            }
        }

        private Material CreateOrLoadMaterial(string matName, Shader shader, Color color, float metallic, float smoothness)
        {
#if UNITY_EDITOR
            string folderPath = "Assets/_Project/Materials";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }

            string fullPath = folderPath + "/" + matName + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(fullPath);
            if (mat == null)
            {
                mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                AssetDatabase.CreateAsset(mat, fullPath);
                AssetDatabase.SaveAssets();
            }
            return mat;
#else
            return new Material(shader);
#endif
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
