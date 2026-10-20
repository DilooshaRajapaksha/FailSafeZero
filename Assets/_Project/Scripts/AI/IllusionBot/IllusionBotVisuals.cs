using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FailSafeZero.AI.Illusion
{
    /// <summary>How the bot "feels". The brain sets it, and the visuals show it with the eye colour.</summary>
    public enum BotMood { Calm, Alert, Charging, Stunned, Dead }

    /// <summary>
    /// ALL the looks of the bot live here and NOTHING else: spinning ring, bobbing shards,
    /// eye colour, flicker, appear, shatter and death effects.
    ///
    /// It finds the parts by the names given in Blender (Bot_Ring, Bot_Eye,
    /// Bot_Shard_1..3, Muzzle_Point). That is why the model has separate parts and no
    /// skeleton: simple transforms are cheaper than bones.
    ///
    /// The brain never touches renderers. It only raises events (MoodChanged, Swapped),
    /// and this script listens. That is the "logic decoupled from visuals" rule.
    /// </summary>
    public class IllusionBotVisuals : MonoBehaviour
    {
        [Header("Idle motion")]
        [SerializeField] float ringSpinDegreesPerSecond = 90f;
        [SerializeField] float shardBobAmplitude = 0.06f;
        [SerializeField] float shardBobFrequency = 1.5f;

        [Header("Eye colours (shown as emission)")]
        [SerializeField] Color calmColor = new Color(0.12f, 0.71f, 0.69f);     // teal #1FB5B0
        [SerializeField] Color alertColor = new Color(1f, 0.75f, 0.2f);        // amber
        [SerializeField] Color chargingColor = new Color(0.54f, 0.36f, 0.96f); // violet #8A5CF5
        [SerializeField] Color stunnedColor = new Color(1f, 0.2f, 0.2f);       // red
        [SerializeField] float emissionIntensity = 2.5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        readonly List<Transform> shards = new List<Transform>();
        readonly List<Vector3> shardBaseLocal = new List<Vector3>();

        Transform ring;
        Transform muzzle;
        Renderer eyeRenderer;
        Material eyeMaterial;
        Renderer[] renderers;
        Vector3 baseScale;
        BotMood mood = BotMood.Calm;
        float flickerTimer;
        IllusionBotBrain brain;
        Coroutine scaleRoutine;

        public Vector3 MuzzlePosition =>
            muzzle ? muzzle.position : transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;

        void Awake()
        {
            baseScale = transform.localScale;
            renderers = GetComponentsInChildren<Renderer>(true);

            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Bot_Ring") ring = t;
                else if (t.name == "Muzzle_Point") muzzle = t;
                else if (t.name == "Bot_Eye") eyeRenderer = t.GetComponent<Renderer>();
                else if (t.name.StartsWith("Bot_Shard")) shards.Add(t);
            }

            foreach (Transform shard in shards)
            {
                shardBaseLocal.Add(shard.localPosition);
            }

            if (eyeRenderer != null)
            {
                // One material copy for the eye only. The body keeps the shared material,
                // so the six parts still draw with one material (fewer SetPass calls).
                eyeMaterial = eyeRenderer.material;
                if (eyeMaterial.HasProperty(EmissionColorId))
                {
                    eyeMaterial.EnableKeyword("_EMISSION");
                }
            }

            ApplyMood();
        }

        void Start()
        {
            // Mirages have no brain, so they are driven directly by IllusionMirage.
            brain = GetComponent<IllusionBotBrain>();
            if (brain != null)
            {
                brain.MoodChanged += SetMood;
                brain.Swapped += PlaySwapFlicker;
            }
        }

        void OnDestroy()
        {
            if (brain != null)
            {
                brain.MoodChanged -= SetMood;
                brain.Swapped -= PlaySwapFlicker;
            }

            if (eyeMaterial != null)
            {
                Destroy(eyeMaterial); // avoid leaking the runtime material copy
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;

            if (ring != null && mood != BotMood.Stunned && mood != BotMood.Dead)
            {
                float speed = mood == BotMood.Charging ? ringSpinDegreesPerSecond * 3f : ringSpinDegreesPerSecond;
                ring.Rotate(Vector3.up, speed * dt, Space.World);
            }

            for (int i = 0; i < shards.Count; i++)
            {
                Transform shard = shards[i];
                float wave = Mathf.Sin(t * shardBobFrequency * Mathf.PI * 2f + i * 2.1f);
                Vector3 worldOffset = Vector3.up * (shardBobAmplitude * wave);
                Vector3 localOffset = shard.parent ? shard.parent.InverseTransformVector(worldOffset) : worldOffset;
                shard.localPosition = shardBaseLocal[i] + localOffset;
            }

            if (flickerTimer > 0f)
            {
                flickerTimer -= dt;
                bool visible = flickerTimer <= 0f || Mathf.Repeat(t * 20f, 1f) > 0.5f;
                SetRenderersEnabled(visible);
            }

            if (mood == BotMood.Stunned)
            {
                // Blink red/off while stunned, which tells the player "hit me NOW".
                SetEyeColor(Mathf.Repeat(t * 6f, 1f) > 0.5f ? stunnedColor : Color.black);
            }
        }

        // ------------------------------------------------------------ public API

        public void SetMood(BotMood newMood)
        {
            mood = newMood;
            ApplyMood();
        }

        /// <summary>The core of the weakness: mirages cast NO shadow, and the real bot does.</summary>
        public void SetShadows(bool castShadows)
        {
            foreach (Renderer r in renderers)
            {
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        public void PlaySwapFlicker()
        {
            flickerTimer = 0.3f;
        }

        public void PlayAppear()
        {
            StartScale(Vector3.zero, baseScale, 0.2f, 0f, null);
        }

        public void PlayShatter(Action onDone)
        {
            StartScale(transform.localScale, Vector3.zero, 0.25f, 0f, onDone);
        }

        public void PlayDeath(Action onDone)
        {
            SetMood(BotMood.Dead);
            StartScale(transform.localScale, Vector3.zero, 1.2f, -0.8f, onDone);
        }

        // ------------------------------------------------------------ helpers

        void ApplyMood()
        {
            switch (mood)
            {
                case BotMood.Calm: SetEyeColor(calmColor); break;
                case BotMood.Alert: SetEyeColor(alertColor); break;
                case BotMood.Charging: SetEyeColor(chargingColor); break;
                case BotMood.Stunned: SetEyeColor(stunnedColor); break;
                case BotMood.Dead: SetEyeColor(Color.black); break;
            }
        }

        void SetEyeColor(Color c)
        {
            if (eyeMaterial == null)
            {
                return;
            }

            if (eyeMaterial.HasProperty(EmissionColorId))
            {
                eyeMaterial.SetColor(EmissionColorId, c * emissionIntensity);
            }
            else if (eyeMaterial.HasProperty(BaseColorId))
            {
                eyeMaterial.SetColor(BaseColorId, c);
            }
        }

        void SetRenderersEnabled(bool on)
        {
            foreach (Renderer r in renderers)
            {
                r.enabled = on;
            }
        }

        void StartScale(Vector3 from, Vector3 to, float duration, float sink, Action onDone)
        {
            if (!isActiveAndEnabled)
            {
                onDone?.Invoke();
                return;
            }

            if (scaleRoutine != null)
            {
                StopCoroutine(scaleRoutine);
            }

            scaleRoutine = StartCoroutine(ScaleRoutine(from, to, duration, sink, onDone));
        }

        IEnumerator ScaleRoutine(Vector3 from, Vector3 to, float duration, float sink, Action onDone)
        {
            SetRenderersEnabled(true);
            Vector3 startPos = transform.position;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.localScale = Vector3.LerpUnclamped(from, to, k);
                if (sink != 0f)
                {
                    transform.position = startPos + Vector3.up * (sink * k);
                }
                yield return null;
            }

            transform.localScale = to;
            scaleRoutine = null;

            onDone?.Invoke();

            // Restore the scale so a pooled mirage looks right the next time it is reused.
            if (to == Vector3.zero)
            {
                transform.localScale = baseScale;
            }
        }
    }
}
