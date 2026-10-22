using UnityEngine;

namespace FailSafeZero.Environment
{
    [RequireComponent(typeof(Light))]
    public class EmergencyLightPulse : MonoBehaviour
    {
        public float minIntensity = 0.5f;
        public float maxIntensity = 3.0f;
        public float pulseSpeed = 2.6f;

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
