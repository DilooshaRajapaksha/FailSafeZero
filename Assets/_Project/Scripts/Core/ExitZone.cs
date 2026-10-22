using UnityEngine;

namespace FailSafeZero.Core
{
    /// <summary>
    /// The finish line. An invisible box (trigger) behind the exit door.
    /// If the exit is unlocked, the player wins. If not, it reminds them how many bots are left.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ExitZone : MonoBehaviour
    {
        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (GameFlow.Instance == null || !ObjectiveZone.IsPlayer(other)) return;

            if (GameFlow.Instance.ExitUnlocked)
            {
                GameFlow.Instance.Win();
            }
            else
            {
                GameFlow.Instance.SetObjective($"The exit is sealed. Neutralise the remaining test subjects ({GameFlow.Instance.RemainingBots}).");
            }
        }
    }
}
