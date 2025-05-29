using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SimpleFlyCam : MonoBehaviour
{
    [Header("Speed Settings")]
    [Tooltip("Movement speed in units/sec")]
    public float moveSpeed = 5f;
    [Tooltip("Rotation speed in degrees/sec")]
    public float lookSpeed = 180f;

    [Header("Controller Settings")]
    [Range(0f, 1f), Tooltip("Joystick dead-zone (no input below this)")]
    public float deadZone = 0.2f;

    void Update()
    {
        // --- MOVEMENT (Left Stick) ---
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 dir = transform.right * moveX + transform.forward * moveZ;
        transform.position += dir * moveSpeed * Time.deltaTime;

        // --- LOOK (Right Stick) ---
        float rawYaw   = Input.GetAxis("RightStickX");
        float rawPitch = Input.GetAxis("RightStickY");

        // apply dead-zone
        float yaw   = Mathf.Abs(rawYaw)   > deadZone ? rawYaw   : 0f;
        float pitch = Mathf.Abs(rawPitch) > deadZone ? rawPitch : 0f;

        yaw   *= lookSpeed * Time.deltaTime;
        pitch *= lookSpeed * Time.deltaTime;

        // yaw around world Y, pitch around local X
        transform.Rotate(0f, yaw, 0f, Space.World);
        transform.Rotate(pitch, 0f, 0f, Space.Self);
    }
}
