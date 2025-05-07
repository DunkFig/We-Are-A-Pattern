using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class CameraPair
{
    public Camera stationary;
    public Camera moving;
}

[Serializable]
public class CameraGroup
{
    public string name;
    public Transform anchor;              // used for “nearest” calculation
    public List<CameraPair> pairs;        // all pairs in this group

    // runtime only:
    [NonSerialized] public Queue<int> availablePairIndices;

    public void InitPairQueue()
    {
        // shuffle indices once
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

    [Header("Music System")]
    [Tooltip("Drag in your WebCamMusicSystem here")]
    public WebCamMusicSystem webCam;

    [Header("Threshold Timing")]
    [Tooltip("Seconds after a change before we react again")]
    public float thresholdCooldown = 1f;

    [Header("Group Visit Limits")]
    [Tooltip("How many times a group may be re‑entered before we require visiting all others")]
    public int groupCycleLimit = 1;

    int _currentGroup, _currentPair;
    float _nextAllowedTime;
    bool  _prevThreshold;

    Dictionary<int,int> _groupVisitCount = new Dictionary<int,int>();

    void Awake()
    {
        // initialize groups & counts
        for (int i = 0; i < cameraGroups.Count; i++)
        {
            cameraGroups[i].InitPairQueue();
            _groupVisitCount[i] = 0;
        }

        // pick a random start
        _currentGroup = UnityEngine.Random.Range(0, cameraGroups.Count);
        _currentPair  = cameraGroups[_currentGroup].DequeueNextPair();
        _groupVisitCount[_currentGroup]++;
        ActivatePair(_currentGroup, _currentPair);
    }

    void Update()
    {
        bool thresholdHit = webCam.thresholdIndicator.enabled;
        if (thresholdHit && !_prevThreshold && Time.time >= _nextAllowedTime)
        {
            StepToNextPairOrGroup();
            _nextAllowedTime = Time.time + thresholdCooldown;
        }
        _prevThreshold = thresholdHit;
    }

    void StepToNextPairOrGroup()
    {
        // try same group first
        int nextPair = DequeueInCurrentGroup();
        if (nextPair >= 0)
        {
            _currentPair = nextPair;
            ActivatePair(_currentGroup, _currentPair);
            _groupVisitCount[_currentGroup]++;
            return;
        }

        // group exhausted → pick nearest eligible group
        int newGroup = PickNearestEligibleGroup();
        if (newGroup < 0) newGroup = _currentGroup; // fallback
        
        // if we just finished a full cycle of *all* groups, reset counts
        if (AllGroupsAtOrAboveLimit())
            ResetGroupVisitCounts();

        // move into the new group
        _currentGroup = newGroup;
        _currentPair  = cameraGroups[newGroup].DequeueNextPair();
        ActivatePair(_currentGroup, _currentPair);
        _groupVisitCount[_currentGroup]++;
    }

    int DequeueInCurrentGroup()
    {
        var g = cameraGroups[_currentGroup];
        if (g.availablePairIndices.Count > 0)
            return g.DequeueNextPair();
        return -1;
    }

    int PickNearestEligibleGroup()
    {
        Vector3 currentPos = cameraGroups[_currentGroup]
                             .pairs[_currentPair]
                             .stationary.transform.position;

        int bestIdx = -1;
        float bestSqr = float.MaxValue;
        bool anyBelowLimit = AnyGroupBelowLimit();

        for (int i = 0; i < cameraGroups.Count; i++)
        {
            if (i == _currentGroup) continue;
            int visits = _groupVisitCount[i];
            // skip over‑visited groups until we've visited all at least once
            if (anyBelowLimit && visits >= groupCycleLimit)
                continue;

            float sqr = (cameraGroups[i].anchor.position - currentPos).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    bool AnyGroupBelowLimit()
    {
        for (int i = 0; i < cameraGroups.Count; i++)
            if (_groupVisitCount[i] < groupCycleLimit)
                return true;
        return false;
    }

    bool AllGroupsAtOrAboveLimit()
    {
        for (int i = 0; i < cameraGroups.Count; i++)
            if (_groupVisitCount[i] < groupCycleLimit)
                return false;
        return true;
    }

    void ResetGroupVisitCounts()
    {
        var keys = new List<int>(_groupVisitCount.Keys);
        foreach (var k in keys) _groupVisitCount[k] = 0;
    }

    void ActivatePair(int groupIdx, int pairIdx)
    {
        // first, disable *all* cameras
        foreach (var g in cameraGroups)
            foreach (var p in g.pairs)
            {
                p.stationary.enabled = false;
                p.moving.enabled     = false;
            }

        // then enable the two we want
        var pair = cameraGroups[groupIdx].pairs[pairIdx];
        pair.stationary.enabled = true;
        pair.moving.enabled     = true;
    }
}
