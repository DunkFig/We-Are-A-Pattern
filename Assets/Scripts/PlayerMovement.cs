using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5.0f;
    public float gravity = -9.81f;

    public float mouseSensitivity = 100.0f;
    
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private float xRotation = 0f;

    public Transform cameraTransform; // Reference to the camera's transform
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Lock the cursor to the center of the screen
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Check if the player is on the ground
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Reset velocity when grounded
        }

        // Movement input
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        // Apply movement
        controller.Move(move * speed * Time.deltaTime);


        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Mouse input for rotation
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Horizontal rotation (yaw)
        transform.Rotate(Vector3.up * mouseX);

        // Vertical rotation (pitch)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Limit the up/down angle to prevent over-rotation

        // Apply rotation to the camera
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
}
