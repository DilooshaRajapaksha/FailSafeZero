using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FailSafeZero.Visuals
{
    /// <summary>
    /// A "costume" for any bot: glowing eyes, a heartbeat light, an aura of particles,
    /// a proper body material and (for ghosts) a flicker. Add it NEXT TO a bot's AI;
    /// it never touches the AI code.
    ///
    /// MENACE: everything reacts to how dangerous the bot is right now.
    ///   menace = max( speed / speedForFullMenace , 1 - distanceToPlayer / menaceRange )   (0..1)
    /// A still, far-away bot has dim eyes and a slow glow; a bot rushing at you has
    /// blazing eyes, a fast heartbeat and thick particles. For the Zombie (which freezes
    /// when you stand still) that means its eyes go dark when it freezes and ignite when it hunts.
    ///
    /// Pick a Preset in the Inspector (Zombie, Maze Ghost, Gravity) to fill in good values,
    /// then adjust Eye Height / Eye Forward so the eyes sit on the face.
    /// </summary>
    public class MonsterLook : MonoBehaviour
    {
        public enum Preset { Custom, Zombie, MazeGhost, Gravity }

        [SerializeField] Preset preset = Preset.Custom;

        [Header("Body")]
        [Tooltip("Replace the body's material with a new one in Body Color (fixes pink 'missing shader' bodies).")]
        [SerializeField] bool replaceBodyMaterial = true;
        [SerializeField] Color bodyColor = new Color(0.15f, 0.2f, 0.12f);
        [SerializeField, Range(0f, 1f)] float bodyMetallic = 0.1f;
        [SerializeField, Range(0f, 1f)] float bodySmoothness = 0.35f;

        [Header("Eyes")]
        [SerializeField] bool eyes = true;
        [SerializeField] Color eyeColor = new Color(0.55f, 1f, 0.2f);
        [SerializeField] float eyeHeight = 1.6f;
        [SerializeField] float eyeForward = 0.45f;
        [SerializeField] float eyeSpacing = 0.18f;
        [SerializeField] float eyeSize = 0.1f;
        [SerializeField] float eyeGlow = 6f;

        [Header("Heartbeat light")]
        [SerializeField] bool glowLight = true;
        [SerializeField] Color lightColor = new Color(0.4f, 1f, 0.2f);
        [SerializeField] float lightRange = 5f;
        [SerializeField] float lightIntensity = 2f;
        [SerializeField] float lightHeight = 1.2f;

        [Header("Aura particles")]
        [SerializeField] bool aura = true;
        [SerializeField] Color auraColor = new Color(0.4f, 0.9f, 0.2f, 0.6f);
        [SerializeField] float auraRadius = 0.6f;
        [SerializeField] float auraRate = 18f;
        [SerializeField] float auraSize = 0.08f;
        [SerializeField] float auraRiseSpeed = 0.4f;
        [Tooltip("Optional particle material. Empty = a URP Particles/Unlit material is made.")]
        [SerializeField] Material auraMaterial;

        [Header("Menace (reacts to speed and closeness)")]
        [SerializeField] float menaceRange = 12f;
        [SerializeField] float speedForFullMenace = 2.5f;
        [Tooltip("How bright the eyes are when the bot is calm (0..1).")]
        [SerializeField, Range(0f, 1f)] float idleBrightness = 0.2f;

        [Header("Ghost flicker")]
        [SerializeField] bool ghostFlicker = false;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        [SerializeField, HideInInspector] Preset lastPreset = Preset.Custom; // saved, so a reload never re-applies it
        readonly List<Renderer> bodyRenderers = new List<Renderer>();
        readonly List<Material> ownedMaterials = new List<Material>();
        Material eyeMaterial;
        Light heartLight;
        ParticleSystem auraSystem;
        Transform player;
        Vector3 lastPosition;
        float smoothedSpeed;
        float menace;
        float heartPhase;
        float flickerTimer;
        float nextFlickerAt;

        public float Menace => menace;

        // ------------------------------------------------------------------ presets

        void OnValidate()
        {
            // Choosing a preset in the Inspector fills in its values once.
            if (preset != lastPreset)
            {
                lastPreset = preset;
                ApplyPreset(preset);
            }
        }

        [ContextMenu("Apply Preset Again")]
        void ApplyPresetAgain() => ApplyPreset(preset);

        void ApplyPreset(Preset p)
        {
            switch (p)
            {
                case Preset.Zombie:
                    replaceBodyMaterial = true; // also fixes the pink 'missing material' capsule
                    bodyColor = new Color(0.16f, 0.22f, 0.12f); bodyMetallic = 0.05f; bodySmoothness = 0.2f;
                    eyeColor = new Color(0.6f, 1f, 0.15f); eyeHeight = 1.6f; eyeForward = 0.45f; eyeSpacing = 0.17f; eyeSize = 0.1f;
                    lightColor = new Color(0.45f, 1f, 0.15f); lightRange = 5f; lightIntensity = 2.2f;
                    auraColor = new Color(0.45f, 0.85f, 0.15f, 0.55f); auraRate = 16f; auraSize = 0.07f; auraRiseSpeed = 0.3f;
                    idleBrightness = 0.12f; ghostFlicker = false;
                    break;

                case Preset.MazeGhost:
                    replaceBodyMaterial = false; // keep Dilanka's ghost textures
                    bodyColor = new Color(0.75f, 0.85f, 1f); bodyMetallic = 0f; bodySmoothness = 0.8f;
                    eyeColor = new Color(0.5f, 0.85f, 1f); eyeHeight = 1.5f; eyeForward = 0.4f; eyeSpacing = 0.16f; eyeSize = 0.09f;
                    lightColor = new Color(0.45f, 0.75f, 1f); lightRange = 6f; lightIntensity = 2f;
                    auraColor = new Color(0.8f, 0.9f, 1f, 0.4f); auraRate = 25f; auraSize = 0.12f; auraRiseSpeed = 0.6f;
                    idleBrightness = 0.3f; ghostFlicker = true;
                    break;

                case Preset.Gravity:
                    bodyColor = new Color(0.12f, 0.08f, 0.2f); bodyMetallic = 0.6f; bodySmoothness = 0.7f;
                    eyeColor = new Color(0.75f, 0.4f, 1f); eyeHeight = 1.6f; eyeForward = 0.65f; eyeSpacing = 0.2f; eyeSize = 0.12f;
                    lightColor = new Color(0.6f, 0.3f, 1f); lightRange = 7f; lightIntensity = 2.5f;
                    auraColor = new Color(0.65f, 0.35f, 1f, 0.6f); auraRate = 22f; auraSize = 0.09f; auraRiseSpeed = 0.8f;
                    idleBrightness = 0.25f; ghostFlicker = false;
                    break;
            }
        }

        // ------------------------------------------------------------------ build

        void Awake()
        {
            // Remember the body BEFORE adding eyes/particles, so only the real body is recoloured.
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is MeshRenderer || r is SkinnedMeshRenderer) bodyRenderers.Add(r);
            }

            if (replaceBodyMaterial) BuildBodyMaterial();
            if (eyes) BuildEyes();
            if (glowLight) BuildLight();
            if (aura) BuildAura();

            lastPosition = transform.position;
            nextFlickerAt = Time.time + Random.Range(1f, 4f);
        }

        void Start()
        {
            GameObject tagged = GameObject.FindWithTag("Player");
            if (tagged != null) player = tagged.transform;
        }

        void OnDestroy()
        {
            foreach (Material m in ownedMaterials)
            {
                if (m != null) Destroy(m);
            }
        }

        void BuildBodyMaterial()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;

            var mat = new Material(lit) { name = name + "_MonsterBody" };
            mat.SetColor(BaseColorId, bodyColor);
            if (mat.HasProperty(MetallicId)) mat.SetFloat(MetallicId, bodyMetallic);
            if (mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, bodySmoothness);
            ownedMaterials.Add(mat);

            foreach (Renderer r in bodyRenderers)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        void BuildEyes()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            eyeMaterial = lit != null ? new Material(lit) { name = name + "_Eyes" } : null;
            if (eyeMaterial != null)
            {
                eyeMaterial.SetColor(BaseColorId, eyeColor);
                eyeMaterial.EnableKeyword("_EMISSION");
                ownedMaterials.Add(eyeMaterial);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = side < 0 ? "Eye_L" : "Eye_R";
                Destroy(eye.GetComponent<Collider>()); // eyes must not block bullets or the NavMesh
                eye.transform.SetParent(transform, false);
                eye.transform.localPosition = new Vector3(eyeSpacing * side, eyeHeight, eyeForward);
                eye.transform.localScale = Vector3.one * eyeSize;

                Renderer r = eye.GetComponent<Renderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                if (eyeMaterial != null) r.sharedMaterial = eyeMaterial;
            }
        }

        void BuildLight()
        {
            var go = new GameObject("HeartbeatLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * lightHeight;
            heartLight = go.AddComponent<Light>();
            heartLight.type = LightType.Point;
            heartLight.color = lightColor;
            heartLight.range = lightRange;
            heartLight.shadows = LightShadows.None; // cheap: no shadow maps for a decorative light
        }

        void BuildAura()
        {
            Material mat = auraMaterial;
            if (mat == null)
            {
                Shader particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (particles == null) return; // no shader = no aura, rather than pink squares
                mat = new Material(particles) { name = name + "_Aura" };
                ownedMaterials.Add(mat);
            }

            var go = new GameObject("Aura");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * (eyeHeight * 0.6f);

            auraSystem = go.AddComponent<ParticleSystem>();
            auraSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = auraSystem.main;
            main.loop = true;
            main.startLifetime = 1.6f;
            main.startSpeed = auraRiseSpeed;
            main.startSize = auraSize;
            main.startColor = auraColor;
            main.maxParticles = 60; // hard cap keeps the cost tiny
            main.simulationSpace = ParticleSystemSimulationSpace.World; // particles trail behind a moving bot
            main.gravityModifier = -0.05f; // drift upwards like spores/mist

            ParticleSystem.EmissionModule emission = auraSystem.emission;
            emission.rateOverTime = auraRate;

            ParticleSystem.ShapeModule shape = auraSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = auraRadius;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            auraSystem.Play();
        }

        // ------------------------------------------------------------------ live

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float t = Time.time;

            // 1. How dangerous is it right now? (speed from position change works for ANY mover:
            //    NavMeshAgent, Rigidbody or a script moving the transform)
            float rawSpeed = (transform.position - lastPosition).magnitude / dt;
            lastPosition = transform.position;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, rawSpeed, 1f - Mathf.Exp(-6f * dt));

            float speedThreat = Mathf.Clamp01(smoothedSpeed / speedForFullMenace);
            float closeThreat = 0f;
            if (player != null)
            {
                float d = Vector3.Distance(transform.position, player.position);
                closeThreat = 1f - Mathf.Clamp01(d / menaceRange);
            }
            float target = Mathf.Max(speedThreat, closeThreat);
            menace = Mathf.Lerp(menace, target, 1f - Mathf.Exp(-4f * dt));

            // 2. Eyes: dim when calm, blazing when hunting, with a nervous flicker (Perlin noise).
            if (eyeMaterial != null)
            {
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(t * 8f, 0.5f);
                float level = Mathf.Lerp(idleBrightness, 1f, menace) * flicker;
                eyeMaterial.SetColor(EmissionId, eyeColor * (eyeGlow * level));
            }

            // 3. Heartbeat: beats faster (0.8 -> 2.5 per second) and brighter as menace rises.
            if (heartLight != null)
            {
                heartPhase += dt * Mathf.Lerp(0.8f, 2.5f, menace);
                float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(heartPhase * Mathf.PI)), 6f); // sharp "thump"
                heartLight.intensity = lightIntensity * Mathf.Lerp(0.3f, 1f, menace) * (0.6f + 0.4f * beat);
            }

            // 4. Aura thickens when it hunts.
            if (auraSystem != null)
            {
                ParticleSystem.EmissionModule emission = auraSystem.emission;
                emission.rateOverTime = auraRate * Mathf.Lerp(0.3f, 1f, menace);
            }

            // 5. Ghost flicker: blink out for a split second, more often when it is close.
            if (ghostFlicker) UpdateFlicker(t, dt);
        }

        void UpdateFlicker(float t, float dt)
        {
            if (flickerTimer > 0f)
            {
                flickerTimer -= dt;
                if (flickerTimer <= 0f) SetBodyVisible(true);
                return;
            }

            if (t >= nextFlickerAt)
            {
                flickerTimer = Random.Range(0.05f, 0.14f);
                SetBodyVisible(false);
                float gap = Mathf.Lerp(4f, 0.8f, menace);
                nextFlickerAt = t + Random.Range(gap * 0.5f, gap);
            }
        }

        void SetBodyVisible(bool visible)
        {
            foreach (Renderer r in bodyRenderers)
            {
                if (r != null) r.enabled = visible;
            }
        }

        void OnDrawGizmosSelected()
        {
            // Shows where the eyes will appear, so they can be lined up with the face.
            Gizmos.color = eyeColor;
            for (int side = -1; side <= 1; side += 2)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(new Vector3(eyeSpacing * side, eyeHeight, eyeForward)), eyeSize * 0.6f);
            }
        }
    }
}
