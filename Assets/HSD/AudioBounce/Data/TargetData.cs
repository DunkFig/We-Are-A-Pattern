    using System;
    using UnityEngine;
    using UnityEngine.Serialization;

    namespace HSD.AudioBounce.Data
    {

        [Serializable]
        public class TargetData
        {
            public Transform targetTransform;
            public AudioSource targetAudioSource;

            [FormerlySerializedAs("InLineOfSight")]
            public bool inLineOfSight;

            [FormerlySerializedAs("DistanceBetweenProbes")]
            public float distanceBetweenProbes;
            
            public bool reverbOnly;

            [FormerlySerializedAs("LeftProbe")] public Vector3 leftProbe;
            [FormerlySerializedAs("RightProbe")] public Vector3 rightProbe;


            public void Reset()
            {
                targetTransform = null;
                targetAudioSource = null;
                inLineOfSight = false;
                reverbOnly = false;
                distanceBetweenProbes = 0;
                leftProbe = Vector3.zero;
                rightProbe = Vector3.zero;
            }

            public float CalculateAngles(Vector3 listenerPosition)
            {
                if (!targetTransform)
                    return 180;
                if (inLineOfSight)
                    return 0;

                Vector3 targetPosition = targetTransform.position;
                Vector3 targetForward = (listenerPosition - targetPosition).normalized;
                Vector3 toLeftProbe = leftProbe - targetPosition;
                Vector3 toRightProbe = rightProbe - targetPosition;

                // Calculate angles
                float leftAngle = leftProbe == Vector3.zero ? 90 : Vector3.Angle(targetForward, toLeftProbe);

                if (leftProbe == rightProbe)
                    rightProbe = Vector3.zero;

                float rightAngle = rightProbe == Vector3.zero ? 90 : Vector3.Angle(targetForward, toRightProbe);

                return leftAngle + rightAngle;
            }

            public Vector3 CalculatePerceivedPosition()
            {
                if (leftProbe == Vector3.zero)
                    return rightProbe;

                if (rightProbe == Vector3.zero)
                    return leftProbe;

                // If both probes are defined, return their midpoint
                return (leftProbe + rightProbe) / 2;
            }

            public Vector3 CalculatePerceivedPosition(Vector3 listenerPosition)
            {
                Vector3 perceivedPosition;

                if (leftProbe == Vector3.zero)
                    perceivedPosition = rightProbe;
                else if (rightProbe == Vector3.zero)
                    perceivedPosition = leftProbe;
                else
                    perceivedPosition = (leftProbe + rightProbe) / 2;

                if (targetAudioSource == null || targetAudioSource.clip == null)
                    return perceivedPosition;

                // Calculate distance between the original source position and the perceived position
                float distanceAmount = Vector3.Distance(targetAudioSource.transform.position, perceivedPosition);

                // Calculate the direction from the listener to the perceived position
                Vector3 direction = (perceivedPosition - listenerPosition).normalized;

                // Move the perceived position away from the listener by the distanceAmount
                Vector3 adjustedPosition = perceivedPosition + direction * distanceAmount;

                return adjustedPosition;
            }

            public Vector3 CalculatePerceivedPosition(Vector3 listenerPosition, float blendFactor)
            {
                Vector3 originalPosition = targetAudioSource.transform.position;
                Vector3 perceivedPosition;

                if (leftProbe == Vector3.zero)
                    perceivedPosition = rightProbe;
                else if (rightProbe == Vector3.zero)
                    perceivedPosition = leftProbe;
                else
                    perceivedPosition = (leftProbe + rightProbe) / 2;

                if (targetAudioSource == null || targetAudioSource.clip == null)
                    return originalPosition;

                // Lerp between the original and the perceived position based on blendFactor
                Vector3 blendedPosition = Vector3.Lerp(originalPosition, perceivedPosition, blendFactor);

                // Calculate distance between the original source position and the perceived position
                float distanceAmount = Vector3.Distance(originalPosition, blendedPosition);

                // Calculate the direction from the listener to the perceived position
                Vector3 direction = (blendedPosition - listenerPosition).normalized;

                // Move the perceived position away from the listener by the distanceAmount
                Vector3 adjustedPosition = blendedPosition + direction * distanceAmount;

                return adjustedPosition;
            }
        }
    }