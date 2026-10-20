using UnityEngine;

namespace FailSafeZero.AI.Illusion
{
    /// <summary>
    /// The story hint for the Illusion arena. Put it on an invisible box (trigger)
    /// at the arena entrance. When the player walks in, a lab note appears.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class IllusionHintZone : MonoBehaviour
    {
        [TextArea(2, 4)]
        [SerializeField] string hint =
            "LAB NOTE #3 - Subject ILLUSION projects light-copies of itself.\n" +
            "Light never lies: only the REAL one casts a shadow.";

        [SerializeField] float showSeconds = 7f;
        [SerializeField] bool showOnlyOnce = true;
        [SerializeField] string playerTag = "Player";

        float timer;
        bool shown;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (showOnlyOnce && shown)
            {
                return;
            }

            bool isPlayer = other.CompareTag(playerTag) ||
                            (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag)) ||
                            other.transform.root.CompareTag(playerTag);
            if (!isPlayer)
            {
                return;
            }

            shown = true;
            timer = showSeconds;
        }

        void Update()
        {
            if (timer > 0f)
            {
                timer -= Time.deltaTime;
            }
        }

        void OnGUI()
        {
            if (timer <= 0f)
            {
                return;
            }

            float width = Mathf.Min(620f, Screen.width - 40f);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 130f, width, 90f);
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Box(rect, hint, style);
        }
    }
}
