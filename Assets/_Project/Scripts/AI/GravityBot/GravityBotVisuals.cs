using UnityEngine;

namespace FailSafeZero.AI.Gravity
{
    /// <summary>
    /// Visual feedback only (kept apart from the AI logic on purpose).
    /// The core light flickers when energy is low (the hint), and blazes red while
    /// the core is exposed (the weak moment). Also spins the floating "ring" for flavour.
    /// </summary>
    public class GravityBotVisuals : MonoBehaviour
    {
        [SerializeField] private GravityBot bot;
        [SerializeField] private Light coreLight;
        [SerializeField] private Renderer coreRenderer;
        [SerializeField] private Transform ring;
        [SerializeField] private Color normalColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color exposedColor = new Color(1f, 0.15f, 0.1f);

        private MaterialPropertyBlock block;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            if (bot == null) bot = GetComponentInParent<GravityBot>();
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (bot == null) return;

            Color c = bot.CoreExposed ? exposedColor : normalColor;
            float intensity = bot.CoreExposed ? 6f : 2.5f;

            // HINT: below 30% energy the core flickers, warning the player a recharge is coming.
            if (!bot.CoreExposed && bot.Energy01 < 0.3f)
                intensity *= Mathf.PerlinNoise(Time.time * 12f, 0f) > 0.5f ? 1f : 0.15f;

            if (coreLight != null) { coreLight.color = c; coreLight.intensity = intensity; }
            if (coreRenderer != null)
            {
                coreRenderer.GetPropertyBlock(block);
                block.SetColor(EmissionId, c * intensity);
                block.SetColor(BaseColorId, c);
                coreRenderer.SetPropertyBlock(block);
            }
            if (ring != null) ring.Rotate(Vector3.up, (bot.CoreExposed ? 20f : 120f) * Time.deltaTime, Space.Self);
        }
    }
}
