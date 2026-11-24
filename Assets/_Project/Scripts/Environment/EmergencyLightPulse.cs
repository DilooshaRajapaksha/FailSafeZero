using UnityEngine;

namespace FailSafeZero.Environment
{
    [RequireComponent(typeof(Light))]
    public class EmergencyLightPulse : MonoBehaviour
    {
        public float minIntensity = 2.5f;
        public float maxIntensity = 2.5f;
        public float pulseSpeed = 0f;

        private Light pointLight;

        private void Awake()
        {
            pointLight = GetComponent<Light>();
        }

        private void Update()
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) * 0.5f;
            pointLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
        }
    }
}
