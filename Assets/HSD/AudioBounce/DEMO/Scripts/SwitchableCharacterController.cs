using System.Collections;
using UnityEngine;

namespace HSD.AudioBounce.Demo
{

    [RequireComponent(typeof(CharacterController))]
    public class SwitchableCharacterController : MonoBehaviour
    {
        public Transform thirdPersonTarget;
        public Transform firstPersonTarget;
        public Camera playerCamera;
        public float speed = 5.0f;
        public float mouseSensitivity = 100.0f;
        public float yRotationLimit = 80.0f; // Limit for looking up and down

        private CharacterController characterController;
        private bool isFirstPerson = false;
        private bool isSwitching = false;
        private float xRotation = 0f;
        private float distanceToPlayer;
        private Vector3 initialCameraPosition;
        private Quaternion initialCameraRotation;

        public float gravity = -9.81f; // Earth's gravity in m/s^2
        private bool grounded;
        private float verticalVelocity = 0f;
        public float groundCheckDistance = 0.1f; // Small value to check if player is on the ground

        private void Start()
        {
            characterController = GetComponent<CharacterController>();
            SetCameraPosition(isFirstPerson ? firstPersonTarget : thirdPersonTarget);
            Cursor.lockState = CursorLockMode.Locked; // Lock the cursor to the center of the screen
            distanceToPlayer = Vector3.Distance(thirdPersonTarget.position, transform.position);
        }

        private void Update()
        {
            GroundCheck();
            ApplyGravity();
            MoveCharacter();

            if (isFirstPerson)
            {
                playerCamera.transform.position = transform.position + firstPersonTarget.localPosition;
            }

            if (Input.GetKeyDown(KeyCode.V) && !isSwitching) // Press 'V' to switch views
            {
                isFirstPerson = !isFirstPerson;
                SwitchView();
            }
        }

        private void LateUpdate()
        {
            RotateCamera();
            if (isSwitching)
            {
                SwitchView();
            }
        }

        private void GroundCheck()
        {
            grounded = characterController.isGrounded;
            if (grounded && verticalVelocity < 0)
            {
                verticalVelocity = -groundCheckDistance; // Small negative value to keep the player grounded
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

            if (isFirstPerson)
            {
                xRotation -= mouseY;
                xRotation = Mathf.Clamp(xRotation, -yRotationLimit, yRotationLimit);

                playerCamera.transform.localRotation =
                    Quaternion.Euler(xRotation, playerCamera.transform.localEulerAngles.y + mouseX, 0f);
            }
            else
            {
                transform.Rotate(Vector3.up * mouseX);
                xRotation -= mouseY;
                xRotation = Mathf.Clamp(xRotation, -yRotationLimit, yRotationLimit);

                Vector3 direction = new Vector3(0, 0, -distanceToPlayer);
                Quaternion rotation = Quaternion.Euler(xRotation, transform.eulerAngles.y, 0);

                // Adjust this line to make the camera look at the firstPersonTarget
                Vector3 lookAtPoint = transform.position + firstPersonTarget.localPosition;

                playerCamera.transform.position = lookAtPoint + rotation * direction;
                playerCamera.transform.LookAt(lookAtPoint);
            }
        }


        private Vector3 targetPosition; // Store the target position at the start of the transition
        private Quaternion targetRotation; // Store the target rotation at the start of the transition

        private void SwitchView()
        {
            Transform target = isFirstPerson ? firstPersonTarget : thirdPersonTarget;
            SetCameraPosition(target);
        }


        private void MoveCharacter()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            Vector3 direction = new Vector3(horizontal, 0, vertical).normalized;

            if (direction.magnitude >= 0.1f)
            {
                Vector3 flatForward = new Vector3(playerCamera.transform.forward.x, 0, playerCamera.transform.forward.z)
                    .normalized;
                Vector3 moveDirection = flatForward * vertical + playerCamera.transform.right * horizontal;
                moveDirection.y = verticalVelocity; // Apply vertical movement (gravity)
                characterController.Move(moveDirection.normalized * speed * Time.deltaTime);
            }

            characterController.transform.rotation = Quaternion.Euler(0, playerCamera.transform.eulerAngles.y, 0);
        }


        private void SetCameraPosition(Transform target)
        {
            playerCamera.transform.position = transform.position + target.localPosition;
            playerCamera.transform.rotation = target.rotation;
        }
    }
}
