using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Orbit Settings")]
    public float orbitSpeed = 10f; // degrees per second

    private Vector3 offset;
    private float radius;
    private float angle;
    private Quaternion zRotationFix;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("OrbitCamera: No target assigned!");
            enabled = false;
            return;
        }

        // Save the original Z-rotation to reapply after lookAt
        zRotationFix = Quaternion.Euler(0, 0, transform.eulerAngles.z);

        // Calculate offset and radius
        offset = transform.position - target.position;
        radius = new Vector2(offset.x, offset.z).magnitude;

        // Initial orbit angle
        angle = Mathf.Atan2(offset.z, offset.x);
    }

    void Update()
    {
        if (target == null) return;

        // Update angle
        angle += orbitSpeed * Mathf.Deg2Rad * Time.deltaTime;

        // Orbit position
        float x = Mathf.Cos(angle) * radius;
        float z = Mathf.Sin(angle) * radius;

        Vector3 newPos = new Vector3(x, offset.y, z) + target.position;

        transform.position = newPos;

        // Look at the target first
        transform.LookAt(target);

        // Then apply Z-axis fix
        transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, zRotationFix.eulerAngles.z);
    }
}
