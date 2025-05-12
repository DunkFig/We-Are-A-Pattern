using HSD.AudioBounce.Main;
using UnityEngine;
using System.Collections.Generic;
using HSD.AudioBounce.Utilities;
using Color = System.Drawing.Color;


namespace HSD.AudioBounce.Logistics
{

    /// <summary>
    /// AudioBounce's AudioSourceVolumetric is a component that allows you to create audio areas that the audio source can travel through.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AB_AudioSourceVolumetric : MonoBehaviour
    {
        [System.Serializable]
        public struct AudioArea
        {
            public Vector3 size;
            public Vector3 offset;
        }

        private GameObject target; // Usually the player or the camera, in this case the locator

        [Tooltip(
            "Add one or more audio areas to the audio source. The audio source will be able to travel across the audio areas seamlessly.")]
        public List<AudioArea> audioAreas = new List<AudioArea>();

        [Tooltip("The speed at which the audio source travels through the audio areas.")]
        public float lerpSpeed = 2f;

        public bool drawGizmos = true; // Draw gizmos in the editor

        private AudioSource audioSource;
        private Vector3 initialPosition;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            initialPosition = transform.position;
        }

        private void Start()
        {
            target = AB_Locator.Instance.gameObject;
        }

        private void Update()
        {
            if(!target)
                if(AB_Locator.Instance)
                    target = AB_Locator.Instance.gameObject;
            if (target)
                MoveAudioSourceToClosestPoint();
        }

        private void MoveAudioSourceToClosestPoint()
        {
            Vector3 overallClosestPoint = Vector3.zero;
            float minDistance = float.MaxValue;

            var position = target.transform.position;

            foreach (AudioArea area in audioAreas)
            {
                Vector3 worldMin = initialPosition + area.offset - area.size * 0.5f;
                Vector3 worldMax = initialPosition + area.offset + area.size * 0.5f;
                Vector3 closestPoint = new Vector3(
                    Mathf.Clamp(position.x, worldMin.x, worldMax.x),
                    Mathf.Clamp(position.y, worldMin.y, worldMax.y),
                    Mathf.Clamp(position.z, worldMin.z, worldMax.z)
                );

                float distance = Vector3.Distance(position, closestPoint);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    overallClosestPoint = closestPoint;
                }
            }

            // Dynamic speed adjustment
            float dynamicLerpSpeed = lerpSpeed * 5.0f;

            audioSource.transform.position = Vector3.Lerp(audioSource.transform.position, overallClosestPoint,
                dynamicLerpSpeed * Time.deltaTime);
        }

        // Draw gizmos in the editor
        private void OnDrawGizmos()
        {
            if (!drawGizmos)
                return;

            if (Application.isPlaying && audioAreas.Count > 0)
            {
                foreach (AudioArea area in audioAreas)
                {
                    Gizmos.color = AB_Utilities.ToColor32(Color.PaleGreen, 0.5f);
                    Gizmos.DrawCube(initialPosition + area.offset, area.size);
                }
            }
            else if (audioAreas.Count > 0)
            {
                foreach (AudioArea area in audioAreas)
                {
                    Gizmos.color = AB_Utilities.ToColor32(Color.Aqua, 0.5f);
                    Gizmos.DrawCube(transform.position + area.offset, area.size);
                }
            }
        }
    }
}