using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class CameraPair
{
    [Tooltip("The main camera for this pair (renders normal scene)")]
    public Camera mainCamera;

    [Tooltip("The demon camera for this pair (renders only demon objects)")]
    public Camera demonCamera;
}

[Serializable]
public class CameraGroup
{
    [Tooltip("Name of this camera group")]
    public string name;

    [Tooltip("Anchor transform used for nearest-group calculations")]
    public Transform anchor;

    [Tooltip("All camera pairs in this group")]
    public List<CameraPair> pairs;

    // Runtime queue of unused pair indices
    [NonSerialized] public Queue<int> availablePairIndices;

    public void InitPairQueue()
    {
        // Shuffle indices for random cycling
        var idxs = new List<int>(pairs.Count);
        for (int i = 0; i < pairs.Count; i++) idxs.Add(i);
        for (int i = 0; i < idxs.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, idxs.Count);
            (idxs[i], idxs[j]) = (idxs[j], idxs[i]);
        }
        availablePairIndices = new Queue<int>(idxs);
    }

    public int DequeueNextPair()
    {
        if (availablePairIndices == null || availablePairIndices.Count == 0)
            InitPairQueue();
        return availablePairIndices.Dequeue();
    }
}

public class CameraSystemController : MonoBehaviour
{
    [Header("Groups & Pairs")]
    [Tooltip("Define all camera groups and their pairs here.")]
    public List<CameraGroup> cameraGroups;

    [Header("Music System Reference")]
    [Tooltip("Drag your WebCamMusicSystem instance here to receive threshold events.")]
    public WebCamMusicSystem webCam;

    [Header("Threshold Timing")]
    [Tooltip("Cooldown in seconds after a threshold event before switching again.")]
    public float thresholdCooldown = 1f;

    [Header("Group Visit Limits")]
    [Tooltip("How many times a group may be re-entered before requiring all others.")]
    public int groupCycleLimit = 1;

    private int _currentGroup;
    private int _currentPair;
    private float _nextAllowedTime;
    private Dictionary<int,int> _groupVisitCount = new Dictionary<int,int>();

    void OnEnable()
    {
        // Initialize visit counts and pair queues
        for (int i = 0; i < cameraGroups.Count; i++)
        {
            cameraGroups[i].InitPairQueue();
            _groupVisitCount[i] = 0;
        }

        // Subscribe to threshold event
        if (webCam != null)
            webCam.OnDarkestThreshold.AddListener(OnThresholdHit);

        // Pick a random start
        _currentGroup = UnityEngine.Random.Range(0, cameraGroups.Count);
        _currentPair = cameraGroups[_currentGroup].DequeueNextPair();
        _groupVisitCount[_currentGroup]++;
        ActivatePair(_currentGroup, _currentPair);
    }

    void OnDisable()
    {
        if (webCam != null)
            webCam.OnDarkestThreshold.RemoveListener(OnThresholdHit);
    }

    private void OnThresholdHit()
    {
        if (Time.time < _nextAllowedTime)
            return;

        StepToNextPairOrGroup();
        _nextAllowedTime = Time.time + thresholdCooldown;
    }

    void StepToNextPairOrGroup()
    {
        // Cycle within current group if possible
        var currentGroupData = cameraGroups[_currentGroup];
        int nextPair = currentGroupData.availablePairIndices.Count > 0
            ? currentGroupData.DequeueNextPair()
            : -1;

        if (nextPair >= 0)
        {
            _currentPair = nextPair;
            _groupVisitCount[_currentGroup]++;
            ActivatePair(_currentGroup, _currentPair);
            return;
        }

        // All pairs used: pick nearest eligible group
        if (AllGroupsVisited())
            ResetGroupVisitCounts();

        int newGroup = PickNearestEligibleGroup();
        if (newGroup < 0) newGroup = _currentGroup;

        _currentGroup = newGroup;
        _currentPair = cameraGroups[newGroup].DequeueNextPair();
        _groupVisitCount[newGroup]++;
        ActivatePair(newGroup, _currentPair);
    }

    bool AllGroupsVisited()
    {
        foreach (var kv in _groupVisitCount)
            if (kv.Value < groupCycleLimit)
                return false;
        return true;
    }

    int PickNearestEligibleGroup()
    {
        Vector3 currentPos = cameraGroups[_currentGroup]
            .pairs[_currentPair]
            .mainCamera.transform.position;

        bool anyBelowLimit = false;
        foreach (var kv in _groupVisitCount)
            if (kv.Value < groupCycleLimit)
                anyBelowLimit = true;

        float bestDist = float.MaxValue;
        int bestIdx = -1;
        for (int i = 0; i < cameraGroups.Count; i++)
        {
            if (i == _currentGroup) continue;
            if (anyBelowLimit && _groupVisitCount[i] >= groupCycleLimit) continue;
            float dist = (cameraGroups[i].anchor.position - currentPos).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    void ResetGroupVisitCounts()
    {
        var keys = new List<int>(_groupVisitCount.Keys);
        foreach (var k in keys)
            _groupVisitCount[k] = 0;
    }

    void ActivatePair(int groupIdx, int pairIdx)
    {
        // Disable all cameras in all groups
        foreach (var g in cameraGroups)
            foreach (var p in g.pairs)
            {
                if (p.mainCamera != null) p.mainCamera.enabled = false;
                if (p.demonCamera != null) p.demonCamera.enabled = false;
            }

        // Enable main + demon cameras for active pair
        var active = cameraGroups[groupIdx].pairs[pairIdx];
        if (active.mainCamera != null) active.mainCamera.enabled = true;
        if (active.demonCamera != null) active.demonCamera.enabled = true;
    }
}
