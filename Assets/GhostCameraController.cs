using System.Collections.Generic;
using UnityEngine;

public class GhostCameraController : MonoBehaviour
{
    [Header("Target Setup")]
    public List<Transform> targets;
    public float approachSpeed = 2f;
    public float proximityThreshold = 1f;
    public float stayDuration = 3f;

    [Header("Camera Settings")]
    public float zRotationFixed = -90f;

    private Transform currentTarget;
    private HashSet<Transform> visitedTargets = new HashSet<Transform>();
    private float stayTimer = 0f;

    void Start()
    {
        if (targets.Count > 0)
        {
            currentTarget = GetFarthestTarget(transform.position, targets);
            visitedTargets.Add(currentTarget);
        }
    }

    void Update()
    {
        if (currentTarget == null || targets.Count == 0) return;

        // LERP toward the target's position
        transform.position = Vector3.Lerp(transform.position, currentTarget.position, Time.deltaTime * approachSpeed);

        // Always look directly at the target's current position
        transform.LookAt(currentTarget.position);

        // Re-apply sideways camera correction (lock Z rotation)
        Vector3 euler = transform.eulerAngles;
        euler.z = zRotationFixed;
        transform.eulerAngles = euler;

        // Check proximity and advance when stayDuration is reached
        float dist = Vector3.Distance(transform.position, currentTarget.position);

        if (dist < proximityThreshold)
        {
            stayTimer += Time.deltaTime;

            if (stayTimer >= stayDuration)
            {
                currentTarget = GetFarthestUnvisitedTarget(transform.position);
                if (currentTarget == null)
                {
                    visitedTargets.Clear();
                    currentTarget = GetFarthestTarget(transform.position, targets);
                }

                visitedTargets.Add(currentTarget);
                stayTimer = 0f;
            }
        }
        else
        {
            stayTimer = 0f;
        }
    }

    Transform GetFarthestTarget(Vector3 from, List<Transform> list)
    {
        Transform farthest = null;
        float maxDist = -1f;
        foreach (var t in list)
        {
            float dist = Vector3.Distance(from, t.position);
            if (dist > maxDist)
            {
                maxDist = dist;
                farthest = t;
            }
        }
        return farthest;
    }

    Transform GetFarthestUnvisitedTarget(Vector3 from)
    {
        Transform farthest = null;
        float maxDist = -1f;
        foreach (var t in targets)
        {
            if (visitedTargets.Contains(t)) continue;
            float dist = Vector3.Distance(from, t.position);
            if (dist > maxDist)
            {
                maxDist = dist;
                farthest = t;
            }
        }
        return farthest;
    }
}
