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

    [NonSerialized] public Queue<int> availablePairIndices;

    public void InitPairQueue()
    {
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
    public List<CameraGroup> cameraGroups;

    [Header("Music System Reference")]
    public WebCamMusicSystem webCam;

    [Header("Threshold Timing")]
    public float thresholdCooldown = 1f;

    [Header("Group Visit Limits")]
    public int groupCycleLimit = 1;

    private int _currentGroup;
    private int _currentPair;
    private float _nextAllowedTime;
    private Dictionary<int,int> _groupVisitCount = new Dictionary<int,int>();

    void OnEnable()
    {
        for (int i = 0; i < cameraGroups.Count; i++)
        {
            cameraGroups[i]?.InitPairQueue();
            _groupVisitCount[i] = 0;
        }
        if (webCam != null)
            webCam.OnDarkestThreshold.AddListener(OnThresholdHit);

        _currentGroup = UnityEngine.Random.Range(0, cameraGroups.Count);
        _currentPair = GetValidPair(_currentGroup);
        _groupVisitCount[_currentGroup]++;
        ActivatePair(_currentGroup, _currentPair);
    }

    void Update()
    {
        // Debug: press Space to force the next camera/pair
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StepToNextPairOrGroup();
            // optional: reset the cooldown so you can spam Space
            _nextAllowedTime = 0f;
        }
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

        if (AllGroupsVisited())
            ResetGroupVisitCounts();

        int newGroup = PickNearestEligibleGroup();
        _currentGroup = newGroup >= 0 ? newGroup : _currentGroup;
        _currentPair = GetValidPair(_currentGroup);
        _groupVisitCount[_currentGroup]++;
        ActivatePair(_currentGroup, _currentPair);
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
        Vector3 currentPos;
        var activePair = GetPair(_currentGroup, _currentPair);
        if (activePair?.mainCamera != null)
            currentPos = activePair.mainCamera.transform.position;
        else
            return -1;

        bool anyBelow = false;
        foreach (var kv in _groupVisitCount)
            if (kv.Value < groupCycleLimit)
                anyBelow = true;

        float bestDist = float.MaxValue;
        int bestIdx = -1;
        for (int i = 0; i < cameraGroups.Count; i++)
        {
            if (i == _currentGroup) continue;
            if (anyBelow && _groupVisitCount[i] >= groupCycleLimit) continue;
            var group = cameraGroups[i];
            if (group?.anchor == null) continue;
            float dist = (group.anchor.position - currentPos).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    int GetValidPair(int groupIdx)
    {
        var group = cameraGroups[groupIdx];
        if (group == null || group.pairs == null || group.pairs.Count == 0)
            return 0;
        return group.DequeueNextPair();
    }

    CameraPair GetPair(int groupIdx, int pairIdx)
    {
        var group = cameraGroups[groupIdx];
        if (group == null || group.pairs == null) return null;
        if (pairIdx < 0 || pairIdx >= group.pairs.Count) return null;
        return group.pairs[pairIdx];
    }

    void ResetGroupVisitCounts()
    {
        var keys = new List<int>(_groupVisitCount.Keys);
        foreach (var k in keys)
            _groupVisitCount[k] = 0;
    }

    void ActivatePair(int groupIdx, int pairIdx)
    {
        foreach (var g in cameraGroups)
            if (g?.pairs != null)
                foreach (var p in g.pairs)
                {
                    if (p.mainCamera != null) { p.mainCamera.enabled = false; var ml = p.mainCamera.GetComponent<AudioListener>(); if (ml != null) ml.enabled = false; }
                    if (p.demonCamera != null) { p.demonCamera.enabled = false; var dl = p.demonCamera.GetComponent<AudioListener>(); if (dl != null) dl.enabled = false; }
                }

        var active = GetPair(groupIdx, pairIdx);
        if (active?.mainCamera != null) { active.mainCamera.enabled = true; var ml = active.mainCamera.GetComponent<AudioListener>(); if (ml != null) ml.enabled = true; }
        if (active?.demonCamera != null) { active.demonCamera.enabled = true; var dl = active.demonCamera.GetComponent<AudioListener>(); if (dl != null) dl.enabled = true; }
    }
}
