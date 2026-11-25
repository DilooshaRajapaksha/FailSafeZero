using UnityEngine;

namespace FailSafeZero.AI.Gravity
{
    /// <summary>
    /// Teaches the player the bot's weakness through short on-screen hints:
    /// first sighting, first low-energy flicker, and first exposed core.
    /// Each hint shows once for a few seconds.
    /// </summary>
    public class GravityBotHints : MonoBehaviour
    {
        [SerializeField] private GravityBot bot;
        [SerializeField] private float showSeconds = 5f;

        private string message;
        private float hideAt;
        private bool saidLow, saidExposed;

        private void Awake()
        {
            if (bot == null) bot = GetComponent<GravityBot>();
        }

        private void OnEnable()
        {
            bot.OnPlayerFirstSeen.AddListener(OnFirstSeen);
            bot.OnCoreExposedChanged.AddListener(OnExposed);
        }

        private void OnDisable()
        {
            bot.OnPlayerFirstSeen.RemoveListener(OnFirstSeen);
            bot.OnCoreExposedChanged.RemoveListener(OnExposed);
        }

        private void Update()
        {
            if (!saidLow && bot.Energy01 < 0.3f) { saidLow = true; Show("Its core is flickering... it is running out of power."); }
        }

        private void OnFirstSeen() => Show("The air bends around it. Every trick it uses drains its core.");

        private void OnExposed(bool exposed)
        {
            if (exposed && !saidExposed) { saidExposed = true; Show("Its core is open! Shoot it NOW, or hit it with something heavy."); }
        }

        private void Show(string text) { message = text; hideAt = Time.time + showSeconds; }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(message) || Time.time > hideAt) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, wordWrap = true };
            style.normal.textColor = new Color(0.6f, 0.85f, 1f);
            GUI.Label(new Rect(Screen.width / 2f - 300f, Screen.height - 120f, 600f, 60f), message, style);
        }
    }
}
