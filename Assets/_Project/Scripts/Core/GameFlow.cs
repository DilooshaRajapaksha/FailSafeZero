using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FailSafeZero.Core
{
    /// <summary>
    /// The game's "tour guide". Put ONE of these on an empty object called GameFlow.
    ///
    ///  - Shows the current OBJECTIVE at the top of the screen
    ///    (ObjectiveZone triggers in each room change it).
    ///  - Tracks the REQUIRED bots. A bot counts as defeated when its GameObject is
    ///    switched off, which every bot in this project does when it dies.
    ///  - When every required bot is defeated, it LOWERS the exit door.
    ///  - ExitZone calls Win(): shows "YOU ESCAPED!" with the time, plus a restart button.
    ///
    /// The Zombie can be left out of the required list: sneaking past it is a valid way to play.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        [Header("Objective shown at the start")]
        [TextArea(1, 3)]
        [SerializeField] string startObjective = "Escape the lab. Move carefully: something in the next room hunts by MOTION.";

        [Header("Bots that must be defeated to unlock the exit")]
        [SerializeField] List<GameObject> requiredBots = new List<GameObject>();

        [Header("Exit door (a wall that slides down into the floor)")]
        [SerializeField] Transform exitDoor;
        [SerializeField] float doorDropDistance = 5f;
        [SerializeField] float doorOpenSeconds = 1.5f;

        string objective;
        float objectiveFlashTimer;
        float startTime;
        bool exitUnlocked;
        bool won;
        float winTime;

        public bool ExitUnlocked => exitUnlocked;

        public int RemainingBots
        {
            get
            {
                int count = 0;
                foreach (GameObject bot in requiredBots)
                {
                    if (bot != null && bot.activeInHierarchy) count++;
                }
                return count;
            }
        }

        void Awake()
        {
            Instance = this;
            startTime = Time.time;
            SetObjective(startObjective);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (objectiveFlashTimer > 0f) objectiveFlashTimer -= Time.unscaledDeltaTime;

            if (!exitUnlocked && requiredBots.Count > 0 && RemainingBots == 0)
            {
                exitUnlocked = true;
                SetObjective("All test subjects neutralised. The EXIT is open. Get out!");
                if (exitDoor != null) StartCoroutine(OpenDoor());
            }
        }

        /// <summary>Change the text at the top of the screen (it flashes briefly so the player notices).</summary>
        public void SetObjective(string text)
        {
            if (string.IsNullOrEmpty(text) || text == objective) return;
            objective = text;
            objectiveFlashTimer = 1.5f;
        }

        public void Win()
        {
            if (won) return;
            won = true;
            winTime = Time.time - startTime;
            Time.timeScale = 0f; // freeze the world
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        IEnumerator OpenDoor()
        {
            Vector3 start = exitDoor.position;
            Vector3 end = start + Vector3.down * doorDropDistance;
            float t = 0f;
            while (t < doorOpenSeconds)
            {
                t += Time.deltaTime;
                exitDoor.position = Vector3.Lerp(start, end, t / doorOpenSeconds);
                yield return null;
            }
            exitDoor.gameObject.SetActive(false);
        }

        void OnGUI()
        {
            // Objective banner (top centre).
            if (!won && !string.IsNullOrEmpty(objective))
            {
                float w = Mathf.Min(760f, Screen.width - 40f);
                var style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = objectiveFlashTimer > 0f ? 20 : 17,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter
                };
                string extra = requiredBots.Count > 0 && !exitUnlocked ? $"\n(Test subjects remaining: {RemainingBots})" : "";
                GUI.Box(new Rect((Screen.width - w) * 0.5f, 12f, w, 62f), "OBJECTIVE: " + objective + extra, style);
            }

            if (!won) return;

            // Win screen.
            var big = new GUIStyle(GUI.skin.label) { fontSize = 46, alignment = TextAnchor.MiddleCenter };
            var small = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.Label(new Rect(0, Screen.height * 0.30f, Screen.width, 60f), "YOU ESCAPED!", big);
            int minutes = Mathf.FloorToInt(winTime / 60f);
            int seconds = Mathf.FloorToInt(winTime % 60f);
            GUI.Label(new Rect(0, Screen.height * 0.30f + 70f, Screen.width, 30f), $"Time: {minutes:00}:{seconds:00}", small);

            if (GUI.Button(new Rect(Screen.width * 0.5f - 90f, Screen.height * 0.30f + 130f, 180f, 44f), "Play again"))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }
}
