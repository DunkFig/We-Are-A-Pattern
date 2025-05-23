using UnityEngine;

public class RotateY : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 20f; // Degrees per second

    void Update()
    {
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }
}