using UnityEngine;
using System.Collections;

namespace HSD.AudioBounce.Demo
{

    public class PointTweener : MonoBehaviour
    {
        public Vector3 startPoint;
        public Vector3 endPoint;
        public float duration = 1f;
        public bool loop = false;
        public bool pingPong = false;
        public AnimationCurve tweenCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public float delay = 0f;
        public bool autoStartOnPlay = false;
        public bool drawGizmos = false;

        private enum TweenState
        {
            Idle,
            TweeningToTarget,
            TweeningToStart,
            Paused
        }

        private TweenState currentState = TweenState.Idle;
        private float tweenProgress = 0f;
        private Vector3 initialPosition;

        private void Start()
        {
            initialPosition = transform.position;
            if (autoStartOnPlay)
            {
                StartTween();
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
                tweenProgress = 0f;
                StartCoroutine(DelayedStart());
            }
        }

        private IEnumerator DelayedStart()
        {
            yield return new WaitForSeconds(delay);
            currentState = TweenState.TweeningToTarget;
        }

        public void StopTween()
        {
            currentState = TweenState.Idle;
            tweenProgress = 0f;
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
            if (currentState == TweenState.TweeningToTarget || currentState == TweenState.TweeningToStart)
            {
                tweenProgress += Time.deltaTime / duration;
                float curveValue = tweenCurve.Evaluate(tweenProgress);

                Vector3 worldStartPoint = initialPosition + startPoint;
                Vector3 worldEndPoint = initialPosition + endPoint;

                if (currentState == TweenState.TweeningToTarget)
                {
                    transform.position = Vector3.Lerp(worldStartPoint, worldEndPoint, curveValue);
                }
                else
                {
                    transform.position = Vector3.Lerp(worldEndPoint, worldStartPoint, curveValue);
                }

                if (tweenProgress >= 1f)
                {
                    if (pingPong)
                    {
                        currentState = currentState == TweenState.TweeningToTarget
                            ? TweenState.TweeningToStart
                            : TweenState.TweeningToTarget;
                        tweenProgress = 0f;
                    }
                    else if (loop)
                    {
                        tweenProgress = 0f;
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
            if (Application.isPlaying)
            {
                Gizmos.DrawSphere(initialPosition + startPoint, 0.5f);
                Gizmos.DrawSphere(initialPosition + endPoint, 0.5f);
                Gizmos.DrawLine(initialPosition + startPoint, initialPosition + endPoint);

            }
            else
            {
                Gizmos.DrawSphere(transform.position + startPoint, 0.5f);
                Gizmos.DrawSphere(transform.position + endPoint, 0.5f);
                Gizmos.DrawLine(transform.position + startPoint, transform.position + endPoint);
            }
        }
    }
}
