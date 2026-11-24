using UnityEngine;

namespace FailSafeZero.Environment
{
    public class FinishDoorController : MonoBehaviour
    {
        public Transform leftPanel;
        public Transform rightPanel;
        public Vector3 leftOpenPosition;
        public Vector3 rightOpenPosition;
        public Vector3 leftClosedPosition;
        public Vector3 rightClosedPosition;
        public float closeSpeed = 3f;
        public GameObject finishSign;

        private bool closing;

        private void Awake()
        {
            if (leftPanel != null) leftPanel.position = leftOpenPosition;
            if (rightPanel != null) rightPanel.position = rightOpenPosition;
            if (finishSign != null) finishSign.SetActive(false);
        }

        private void Update()
        {
            if (!closing) return;

            bool leftDone = true;
            bool rightDone = true;

            if (leftPanel != null)
            {
                leftPanel.position = Vector3.MoveTowards(
                    leftPanel.position, leftClosedPosition, closeSpeed * Time.deltaTime);
                leftDone = Vector3.Distance(leftPanel.position, leftClosedPosition) < 0.01f;
            }

            if (rightPanel != null)
            {
                rightPanel.position = Vector3.MoveTowards(
                    rightPanel.position, rightClosedPosition, closeSpeed * Time.deltaTime);
                rightDone = Vector3.Distance(rightPanel.position, rightClosedPosition) < 0.01f;
            }

            if (!leftDone || !rightDone) return;

            closing = false;
            if (finishSign != null) finishSign.SetActive(true);
        }

        public void Close()
        {
            closing = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) Close();
        }
    }
}
