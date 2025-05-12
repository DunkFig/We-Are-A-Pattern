using UnityEngine;

namespace HSD.AudioBounce.Demo
{
    public class DEMOThirdPersonCameraToAudioListener : MonoBehaviour
    {
        public Camera mainCamera;

        void Start()
        {
            if (!GetComponent<Camera>())
                mainCamera = Camera.main;
            // This fix is for third person camera. PLEASE REVISE LATER
            if (!mainCamera)
                mainCamera = Camera.main;
        }

        void Update()
        {
            RotateAudioListener();
        }

        // Rotate the Audio Listener to match the camera's rotation
        private void RotateAudioListener()
        {
            transform.rotation = mainCamera.transform.rotation;
        }

    }
}
