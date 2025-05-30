using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SidewaysFlyCam : MonoBehaviour
{
    [Header("Speeds")]
    public float moveSpeed = 5f;      // units/sec
    public float lookSpeed = 180f;    // deg/sec
    [Range(0f, 1f)] public float deadZone = 0.2f;

    // keep yaw and pitch ourselves
    float yaw;    // rotation around world Y
    float pitch;  // rotation around local X

    // fixed roll because our display is sideways
    const float roll = -90f;

    void Start()
    {
        // initialize yaw/pitch from current rotation (minus the roll)
        Vector3 e = transform.localEulerAngles;
        yaw   = e.y;
        pitch = e.x;
    }

    void Update()
    {
        HandleLook();
        HandleMove();
    }

    void HandleLook()
    {
        // read your right‐stick axes (rename to match your Input settings)
        float rawYaw   = Input.GetAxis("RightStickX");
        float rawPitch = Input.GetAxis("RightStickY");

        // deadzone
        float dx = Mathf.Abs(rawYaw)   > deadZone ? rawYaw   : 0f;
        float dy = Mathf.Abs(rawPitch) > deadZone ? rawPitch : 0f;

        // accumulate
        yaw   += dx * lookSpeed * Time.deltaTime;
        pitch -= dy * lookSpeed * Time.deltaTime;  // invert if you like

        // clamp pitch so you don’t flip all the way over
        pitch = Mathf.Clamp(pitch, -89f, +89f);

        // rebuild rotation: note we re-inject the fixed roll
        transform.localRotation = Quaternion.Euler(pitch, yaw, roll);
    }

    void HandleMove()
    {
        // read your left‐stick axes
        float mx = Input.GetAxis("DPadX");
        float my = Input.GetAxis("DPadY");

        // deadzone
        mx = Mathf.Abs(mx) > deadZone ? mx : 0f;
        my = Mathf.Abs(my) > deadZone ? my : 0f;

        // build a purely yaw+pitch orientation (ignore roll)
        Quaternion flatOrient = Quaternion.Euler(pitch, yaw, 0f);

        // local movement vector before speed
        Vector3 localDir = new Vector3(mx, 0f, my);

        // rotate into world
        Vector3 worldDir = flatOrient * localDir;

        // apply
        transform.position += worldDir * moveSpeed * Time.deltaTime;
    }
}
