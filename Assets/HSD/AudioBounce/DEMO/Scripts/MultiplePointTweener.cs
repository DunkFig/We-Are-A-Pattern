using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace HSD.AudioBounce.Demo
{

    public class MultiplePointTweener : MonoBehaviour
    {
        public List<Vector3> tweenPoints = new List<Vector3>();
        public float duration = 1f;
        public bool loop = false;
        public AnimationCurve tweenCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public float delay = 0f;
        public bool autoStartOnPlay = false;
        public bool drawGizmos = false;

        private enum TweenState
        {
            Idle,
            Tweening,
            Paused
        }

        private TweenState currentState = TweenState.Idle;
        private int currentPointIndex = 0;
        private Vector3 initialPosition;

        private float totalDistance;
        //private int currentPointIndex = 0;

        private void Start()
        {
            initialPosition = transform.position;
            if (autoStartOnPlay)
            {
                StartTween();
            }

            // Calculate total distance
            totalDistance = 0f;
            for (int i = 0; i < tweenPoints.Count - 1; i++)
            {
                totalDistance += Vector3.Distance(tweenPoints[i], tweenPoints[i + 1]);
            }
        }

        private void Update()
        {
            HandleTweening();
        }

        public void StartTween()
        {
            if (currentState == TweenState.Idle)
            {
                initialPosition = transform.position;
                currentPointIndex = 0;
                StartCoroutine(DelayedStart());
            }
        }

        private IEnumerator DelayedStart()
        {
            yield return new WaitForSeconds(delay);
            currentState = TweenState.Tweening;
        }

        public void StopTween()
        {
            currentState = TweenState.Idle;
            currentPointIndex = 0;
        }

        public void PauseTween()
        {
            if (currentState != TweenState.Idle)
            {
                currentState = TweenState.Paused;
            }
        }

        private void HandleTweening()
        {
            if (currentState == TweenState.Tweening)
            {
                float speed = totalDistance / duration;
                float moveDistance = speed * Time.deltaTime;

                while (moveDistance > 0 && currentPointIndex < tweenPoints.Count - 1)
                {
                    Vector3 currentPoint = initialPosition + tweenPoints[currentPointIndex];
                    Vector3 nextPoint = initialPosition + tweenPoints[currentPointIndex + 1];
                    float distanceToNextPoint = Vector3.Distance(transform.position, nextPoint);

                    if (moveDistance >= distanceToNextPoint)
                    {
                        currentPointIndex++;
                        moveDistance -= distanceToNextPoint;
                        transform.position = nextPoint;
                    }
                    else
                    {
                        transform.position = Vector3.MoveTowards(transform.position, nextPoint, moveDistance);
                        moveDistance = 0;
                    }
                }

                // Check if reached the last point
                if (currentPointIndex >= tweenPoints.Count - 1)
                {
                    if (loop)
                    {
                        currentPointIndex = 0;
                    }
                    else
                    {
                        currentState = TweenState.Idle;
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos)
                return;

            Gizmos.color = Color.blue;

            for (int i = 0; i < tweenPoints.Count; i++)
            {
                Vector3 worldPoint = transform.position + tweenPoints[i];
                Gizmos.DrawSphere(worldPoint, 0.5f);

                // Draw lines connecting the points
                if (i < tweenPoints.Count - 1)
                {
                    Vector3 nextWorldPoint = transform.position + tweenPoints[i + 1];
                    Gizmos.DrawLine(worldPoint, nextWorldPoint);
                }
            }
        }
    }
}
