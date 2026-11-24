using UnityEngine;

namespace FailSafeZero.Core
{
    public class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField] PlayerHealth health;
        [SerializeField] float barWidth = 220f;
        [SerializeField] float marginLeft = 20f;
        [SerializeField] float marginBottom = 40f;

        static Texture2D whiteTexture;

        void Awake()
        {
            if (health == null)
                health = GetComponent<PlayerHealth>();

            if (whiteTexture == null)
                whiteTexture = Texture2D.whiteTexture;
        }

        void OnGUI()
        {
            if (health == null)
                return;

            if (whiteTexture == null)
                whiteTexture = Texture2D.whiteTexture;

            float ratio = Mathf.Clamp01(health.Current / Mathf.Max(1f, health.MaxHealth));
            var back = new Rect(marginLeft, Screen.height - marginBottom, barWidth, 18f);

            GUI.Box(back, GUIContent.none);

            Color previous = GUI.color;
            GUI.color = Color.Lerp(Color.red, Color.green, ratio);
            GUI.DrawTexture(new Rect(back.x + 2f, back.y + 2f, (barWidth - 4f) * ratio, 14f), whiteTexture);
            GUI.color = previous;

            GUI.Label(new Rect(back.x + 6f, back.y - 20f, barWidth, 20f),
                $"HEALTH {health.Current:0}/{health.MaxHealth:0}");
        }
    }
}
