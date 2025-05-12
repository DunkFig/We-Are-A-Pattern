using UnityEngine;

namespace HSD.AudioBounce.Demo
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public Camera playerCamera;
        public float speed = 5.0f;
        public float mouseSensitivity = 100.0f;
        public float yRotationLimit = 80.0f; // Limit for looking up and down

        private CharacterController characterController;
        private float xRotation = 0f;

        public float gravity = -9.81f;
        private bool grounded;
        private float verticalVelocity = 0f;
        public float groundCheckDistance = 0.1f;
        public float deceleration = 10.0f; 

        private void Start()
        {
            characterController = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            GroundCheck();
            ApplyGravity();
            MoveCharacter();
            RotateCamera();
        }

        private void GroundCheck()
        {
            grounded = characterController.isGrounded;
            if (grounded && verticalVelocity < 0)
            {
                verticalVelocity = -groundCheckDistance;
            }
        }

        private void ApplyGravity()
        {
            if (!grounded)
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
        }

        private void RotateCamera()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -yRotationLimit, yRotationLimit);

            playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

        private void MoveCharacter()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            Vector3 inputDirection = new Vector3(horizontal, 0, vertical);
            Vector3 direction;

            if (inputDirection.magnitude >= 0.1f)
            {
                direction = transform.forward * vertical + transform.right * horizontal;
                direction.y = verticalVelocity;
            }
            else
            {
                // Immediate stop
                direction = Vector3.zero;
                direction.y = verticalVelocity;
            }

            characterController.Move(direction.normalized * speed * Time.deltaTime);
        }


    }
}
