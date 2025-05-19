using UnityEngine;

namespace HSD.AudioBounce.Demo
{
    public class OpenDoorProximity : MonoBehaviour
    {
        public GameObject player;
        public float maxOpenDistance = 5f;
        public Animator animator;
        public AnimationCurve doorSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f); // Default linear curve

        private void Awake()
        {
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private void Update()
        {
            float distanceToPlayer = Vector3.Distance(player.transform.position, transform.position);
            float normalizedDistance = 1f - Mathf.Clamp01(distanceToPlayer / maxOpenDistance);

            float modulatedValue = doorSpeedCurve.Evaluate(normalizedDistance);
            animator.SetFloat("DoorOpen", modulatedValue);
        }
    }
}