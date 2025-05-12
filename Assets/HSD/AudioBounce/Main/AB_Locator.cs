using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using HSD.AudioBounce.Logistics;
using HSD.AudioBounce.Data;


namespace HSD.AudioBounce.Main
{
    /// <summary>
    /// AudioBounce's Locator is a component that calculates the Occlusion factor for the audio sources in the scene and bounces the audio based on the probes distributed by the Distributor.
    /// </summary>
    [AddComponentMenu("AudioBounce/Internal/AB_Locator")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AB_AudioSourceManager))]
    public class AB_Locator : MonoBehaviour
    {
        #region Public Variables
        
        [Tooltip(
            "This component should ideally be put on the same GameObject as the AudioListener at ears height (cases may vary depending on your needs) in order to match the listener's position and rotation. In the case where that's not possible, you can set the height offset manually.")]
        public Vector3 listenerOffset = new Vector3(0, 1.6f, 0);

        [Tooltip(
            "The layer mask to use for the raycasts projection for 3D objects detection. This should be set to the layer that your environment is on. Best case scenario is to set this the same as your Distributor's layer mask but there might be cases where you want to use a different layer mask for the Locator.")]
        public LayerMask raycastLayerMask = ~0;
        
        [Tooltip(
            "Processing Mode controls how Audio Sources are processed. Fixed mode processes a set number of sources per cycle, allowing control over performance. Higher Frames Per Cycle means lower cost but more spread-out processing. Dynamic mode adapts processing based on the 'Frames Per Audio Source' setting, allowing firm control over cost achieved with flexible processing time according to the number of sources in range.")]
        public ProcessingMode processingMode = ProcessingMode.FixedUpdate;

        [Tooltip(
            "(If Processing Mode = Fixed) Sets the number of frames to process all Audio Sources, spreading processing over multiple frames. This value remains constant regardless of the number of Audio Sources in range. It determines the balance between frame rate impact and processing time. Higher values reduce impact but increase time. Set this based on Max Audio Sources and your desired processing time frame.")]
        public int fixedFramesPerCycle = 32; // The number of frames to process all targets
        
        [Tooltip(
            "(If Processing Mode = Dynamic) Defines the number of frames taken to process each Audio Source. The total frames for all sources equals this value times the number of Audio Sources in range. For example, setting this to 2 with 2 Audio Sources in range will take 4 frames to process, spreading the load and reducing frame rate impact.")]
        public int dynamicFramesPerAudioSource = 2; // The number of frames to process each target
        
        [Tooltip(
            "The maximum number of Audio Sources allowed to be processed each cycle. 32 is the maximum allowed. Lower this to reduce frame rate possible impact.")]
        [HideInInspector]
        public int maxClosestAudioSources = 32;

        [Tooltip(
            "The maximum distance to process Audio Sources. This is the maximum distance that Audio Sources will be processed from the listener. This is a performance optimization. Set this to the maximum distance that you want to process Audio Sources from the listener. This will reduce the number of Audio Sources processed each cycle, reducing frame rate impact.")]
        public float maxDistance = 20f;
        
        #endregion // Public Variables
        
        
        
        
        
        #region Private Variables
        public enum ProcessingMode
        {
            FixedUpdate,
            DynamicUpdate,
        }
        
        public static AB_Locator Instance { get; private set; }

        [HideInInspector] public AB_Distributor distributorPrefab;
        [HideInInspector] public List<AudioSource> targets = new List<AudioSource>();
        [HideInInspector] public List<AudioSource> audioSources = new List<AudioSource>();
        [HideInInspector] public List<AudioSource> reverbAudioSources = new List<AudioSource>();

        private readonly Vector3 m_CachedUp = Vector3.up;
        private Vector3 m_ListenerHeight = Vector3.zero;

        private List<Vector3> m_Probes = new List<Vector3>();
        [HideInInspector] public List<TargetData> targetDataList = new List<TargetData>();
        private List<TargetData> m_TargetDataListCache = new List<TargetData>();
        private ObjectPool<TargetData> m_TargetDataPool;

        private List<Vector3> m_ProbesInSight = new List<Vector3>();
        private List<Vector3> m_ProbesInFrontOfListener = new List<Vector3>();
        private List<Vector3> m_ProbesInFront = new List<Vector3>();

        private List<KeyValuePair<AudioSource, float>> m_SourceDistances = new List<KeyValuePair<AudioSource, float>>();
        private List<KeyValuePair<AudioSource, float>> m_ReverbSourceDistances = new List<KeyValuePair<AudioSource, float>>();

        private int m_ProbesPerFrame;

        private RaycastHit m_Hit;

        private int m_CurrentFrameCount = 999;
        private int m_CurrentCase2TargetIndex;
        private bool m_ResetCurrentFrameCount = true;
        
        private AB_AudioSourceManager m_AudioSourceManager;

        //TARGET PROCESSOR
        private ObjectPool<ProcessedTarget> m_ProcessedTargetPool;
        private ObjectPool<List<ProcessedTarget>> m_ListPool;
        private ObjectPool<List<List<ProcessedTarget>>> m_NestedListPool;

        private List<List<ProcessedTarget>> m_Ptarget = new List<List<ProcessedTarget>>();

        //CASE2 Pool
        private List<Vector3> m_PartialProbesInFront;
        private ObjectPool<List<Vector3>> m_PartialProbesInFrontPool;
        private List<Vector3> m_AccumulatedProbesInSight = new List<Vector3>();
       
        #endregion // Private Variables


        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            
            
            
            m_ProcessedTargetPool =
                new ObjectPool<ProcessedTarget>(() => new ProcessedTarget(0, 0), null, null, null, false, 32);
            m_ListPool = new ObjectPool<List<ProcessedTarget>>(() => new List<ProcessedTarget>(), list => list.Clear(),
                null, null, false, 32);
            m_NestedListPool = new ObjectPool<List<List<ProcessedTarget>>>(() => new List<List<ProcessedTarget>>(),
                list => ClearNestedList(list), null, null, false, 90);
            m_PartialProbesInFrontPool = new ObjectPool<List<Vector3>>(() => new List<Vector3>(), x => x.Clear());

            m_ListenerHeight = transform.position + listenerOffset;

            m_AudioSourceManager = GetComponent<AB_AudioSourceManager>();

            audioSources = m_AudioSourceManager.playingSourcesList;
            reverbAudioSources = m_AudioSourceManager.reverbOnlySourcesList;
            
            m_TargetDataPool =
                new ObjectPool<TargetData>(() => new TargetData(), null, (targetData) => targetData.Reset());
        }
        

        private void Update()
        {
            // Update the list of closest audio sources to the listener
            if (targets.Count == 0)
            {
                UpdateClosestActiveAudioSources(audioSources, reverbAudioSources,targets, maxClosestAudioSources, maxDistance);
                return;
            }

            // Release the pooled objects from the previous frame
            ReleasePooledObjects(m_Ptarget);
            
            // Update the maxClosestAudioSources to be as much as the number of available channels
            AB_MainController.Instance.UpdateAvailableMaxChannels();
            
            // Update the list of closest audio sources to the listener
            UpdateClosestActiveAudioSources(audioSources,reverbAudioSources, targets, maxClosestAudioSources, maxDistance);
           
            
            //UPDATE THE FRAMES PER CYCLE
            if (processingMode == ProcessingMode.DynamicUpdate)
            {
                fixedFramesPerCycle = targets.Count * dynamicFramesPerAudioSource;
            }

            m_Ptarget = PreProcessTargets(targets.Count, fixedFramesPerCycle);
          
            ProcessTargets(m_Ptarget);
            
        }


        //This function iterates through the list of targets, checks if there's a clear line of sight to the listener,
        //and finds the closest left and right probes for each target. It also updates the 'targetDataDictionary' with the target data.
        //private TargetData _targetData = new TargetData(null, false, 0, Vector3.zero, Vector3.zero);
        private void ProcessTargets(List<List<ProcessedTarget>> preProcessedTargets)
        {
            m_ListenerHeight = transform.position + listenerOffset;
            m_ProbesInFront.Clear();
            m_ProbesInSight.Clear();
            Vector3 destination = m_ListenerHeight;
            
            if (m_CurrentFrameCount >= fixedFramesPerCycle || m_ResetCurrentFrameCount)
            {
                m_CurrentFrameCount = 0;
                m_ResetCurrentFrameCount = false;
                
                foreach (TargetData targetData in targetDataList)
                {
                    m_TargetDataPool.Release(targetData);
                }

                targetDataList.Clear();
                targetDataList.AddRange(m_TargetDataListCache);
                
                m_TargetDataListCache.Clear();
                m_ProbesInFrontOfListener.Clear();
            }

            
            if (preProcessedTargets.Count == 0)
            {
                //Debug.Log("No targets to process");
                return;
            }


            List<ProcessedTarget> processedTargets =
                preProcessedTargets[m_CurrentFrameCount % preProcessedTargets.Count];

            if (preProcessedTargets.Count < targets.Count) // CASE 1 : Process multiple targets in one single frame
            {
                for (int i = 0; i < processedTargets.Count; i++)
                {
                    Transform target = targets[processedTargets[i].targetIndex].transform;
                    AudioSource targetAudioSource = targets[processedTargets[i].targetIndex]; //NEW
                    
                    TargetData pooledTargetData = m_TargetDataPool.Get(); //THIS GOES EARLY CAUSE IT'S USED EITHER WAY
                    
                    // Skip processing for reverbAudioSources and add them to the list directly
                    if (reverbAudioSources.Contains(targetAudioSource))
                    {
                        pooledTargetData.targetTransform = target;
                        pooledTargetData.targetAudioSource = targetAudioSource;
                        pooledTargetData.inLineOfSight = true;
                        pooledTargetData.distanceBetweenProbes = 0;
                        pooledTargetData.leftProbe = Vector3.zero;
                        pooledTargetData.rightProbe = Vector3.zero;
                        pooledTargetData.reverbOnly = true;
                        m_TargetDataListCache.Add(pooledTargetData);
                        continue;
                    }
                    
                    
                    
                    Vector3 position = target.position;


                    bool lineOfSightClear = IsLineOfSightClear(position, destination);


                    //TargetData pooledTargetData = m_TargetDataPool.Get();
                    pooledTargetData.targetTransform = target;
                    pooledTargetData.inLineOfSight = lineOfSightClear;
                    pooledTargetData.targetTransform = target;
                    pooledTargetData.targetAudioSource = targets[processedTargets[i].targetIndex];


                    if (!lineOfSightClear) //if line of sight is not clear we should process normally
                    {
                        FindProbesBetweenListenerAndSource(m_Probes, m_ProbesInFront, position);
                        FindProbesInSight(position, m_ProbesInFront, m_ProbesInSight);
                        (int leftProbeIndex, int rightProbeIndex) = FindClosestProbes(m_ProbesInSight, position);

                        Vector3 leftProbe = leftProbeIndex != -1 ? m_ProbesInSight[leftProbeIndex] : Vector3.zero;
                        Vector3 rightProbe = rightProbeIndex != -1 ? m_ProbesInSight[rightProbeIndex] : Vector3.zero;
                        float distanceBetweenProbes = Vector3.Distance(leftProbe, rightProbe);

                        // Update the TargetData with new values <--
                        pooledTargetData.distanceBetweenProbes = distanceBetweenProbes;
                        pooledTargetData.leftProbe = leftProbe;
                        pooledTargetData.rightProbe = rightProbe;
                    }
                    else // if line of sight is clear we can skip the rest of the processing and set the values to zero
                    {
                        // Set the TargetData with values at 0 <--
                        pooledTargetData.distanceBetweenProbes = 0;
                        pooledTargetData.leftProbe = Vector3.zero;
                        pooledTargetData.rightProbe = Vector3.zero;
                    }

                    m_TargetDataListCache.Add(pooledTargetData);
                }

                m_CurrentFrameCount++;
            }
            else // CASE 2: Process one single target over multiple frames
            {
                if (m_CurrentCase2TargetIndex < 0 || m_CurrentCase2TargetIndex >= preProcessedTargets.Count)
                {
                    m_CurrentCase2TargetIndex = 0;
                    return;
                }

                ProcessedTarget currentProcessedTarget = preProcessedTargets[m_CurrentCase2TargetIndex][0];
                int cycles = currentProcessedTarget.cycles;

                Transform target = targets[currentProcessedTarget.targetIndex].transform;
                AudioSource targetAudioSource = targets[currentProcessedTarget.targetIndex]; //NEW
                
                
                // Skip processing for reverbAudioSources and add them to the list directly
                if (reverbAudioSources.Contains(targetAudioSource))
                {
                    TargetData pooledTargetData = m_TargetDataPool.Get();
                    pooledTargetData.targetTransform = target;
                    pooledTargetData.targetAudioSource = targetAudioSource;
                    pooledTargetData.inLineOfSight = true;
                    pooledTargetData.distanceBetweenProbes = 0;
                    pooledTargetData.leftProbe = Vector3.zero;
                    pooledTargetData.rightProbe = Vector3.zero;
                    pooledTargetData.reverbOnly = true;
                    m_TargetDataListCache.Add(pooledTargetData);
                    m_CurrentFrameCount++;
                    m_CurrentCase2TargetIndex = (m_CurrentCase2TargetIndex + 1) % preProcessedTargets.Count;
                    if (m_CurrentCase2TargetIndex == 0)
                    {
                        m_ResetCurrentFrameCount = true;
                    }
                    m_CurrentFrameCount = 0;
                    return;
                }
                
                
                Vector3 position = target.position;
                bool lineOfSightClear = IsLineOfSightClear(position, destination);


                if (lineOfSightClear) // if line of sight is clear, we can skip the rest of the processing
                {
                    TargetData pooledTargetData = m_TargetDataPool.Get();
                    pooledTargetData.targetTransform = target;
                    pooledTargetData.targetAudioSource = targets[currentProcessedTarget.targetIndex];
                    pooledTargetData.inLineOfSight = true;
                    pooledTargetData.distanceBetweenProbes = 0;
                    pooledTargetData.leftProbe = Vector3.zero;
                    pooledTargetData.rightProbe = Vector3.zero;
                    m_TargetDataListCache.Add(pooledTargetData);
                    m_CurrentFrameCount++;
                    m_CurrentCase2TargetIndex = (m_CurrentCase2TargetIndex + 1) % preProcessedTargets.Count;
                    if (m_CurrentCase2TargetIndex == 0)
                    {
                        m_ResetCurrentFrameCount = true;
                    }

                    m_CurrentFrameCount = 0;
                    return;
                }
                //ELSE KEEP GOING

                FindProbesBetweenListenerAndSource(m_Probes, m_ProbesInFront, position);

                int probesToProcess = Mathf.Max(1, m_ProbesInFront.Count / (cycles + 1));
                int startIndex = (m_CurrentFrameCount % (cycles + 1)) * probesToProcess;
                
                m_PartialProbesInFront = m_PartialProbesInFrontPool.Get();
                FillPartialProbesInFront(startIndex, probesToProcess, m_ProbesInFront, m_PartialProbesInFront);
                
                FindProbesInSight(position, m_PartialProbesInFront, m_AccumulatedProbesInSight);

                m_PartialProbesInFrontPool.Release(m_PartialProbesInFront);

                m_CurrentFrameCount++;


                
                if (m_CurrentFrameCount == cycles)
                {
                    (int leftProbeIndex, int rightProbeIndex) = FindClosestProbes(m_AccumulatedProbesInSight, position);

                    Vector3 leftProbe =
                        leftProbeIndex != -1 ? m_AccumulatedProbesInSight[leftProbeIndex] : Vector3.zero;
                    Vector3 rightProbe = rightProbeIndex != -1
                        ? m_AccumulatedProbesInSight[rightProbeIndex]
                        : Vector3.zero;
                    float distanceBetweenProbes = Vector3.Distance(leftProbe, rightProbe);

                    // Update the TargetData with new values <--
                    TargetData pooledTargetData = m_TargetDataPool.Get();
                    pooledTargetData.targetTransform = target;
                    pooledTargetData.targetAudioSource = targets[currentProcessedTarget.targetIndex];
                    pooledTargetData.inLineOfSight = false;
                    pooledTargetData.distanceBetweenProbes = distanceBetweenProbes;
                    pooledTargetData.leftProbe = leftProbe;
                    pooledTargetData.rightProbe = rightProbe;

                    m_TargetDataListCache.Add(pooledTargetData);

                    m_AccumulatedProbesInSight.Clear();

                    m_CurrentCase2TargetIndex = (m_CurrentCase2TargetIndex + 1) % preProcessedTargets.Count;
                    if (m_CurrentCase2TargetIndex == 0)
                    {
                        m_ResetCurrentFrameCount = true;
                    }

                    m_CurrentFrameCount = 0;
                    
                }
            }
        }
        
        //This function updates the list of probes based on the Distributor's finalProbePosition.
        public void UpdateProbeDistributor(AB_Distributor distributor)
        {
            m_Probes.Clear();
            distributorPrefab = distributor;
            m_Probes = distributorPrefab.finalProbePosition;
        }

        //This function fills the partial list of probes in front of the target.
        private void FillPartialProbesInFront(int startIndex, int probesToProcess, List<Vector3> source,
            List<Vector3> destination)
        {
            for (int i = 0; i < probesToProcess; i++)
            {
                int index = startIndex + i;
                if (index < source.Count)
                {
                    destination.Add(source[index]);
                }
            }
        }

        //This function pre-processes the targets based on the number of frames per cycle.
        public List<List<ProcessedTarget>> PreProcessTargets(int targetsCount, int aFramesPerCycle)
        {
            List<List<ProcessedTarget>> processedTargetsPerFrame = m_NestedListPool.Get();

            int targetIndex = 0;

            // Case1 logic: Process multiple targets in one single frame
            if (targetsCount >= aFramesPerCycle)
            {
                float targetsPerFrame = (float)targetsCount / aFramesPerCycle;
                int roundAvgTargets = Mathf.FloorToInt(targetsPerFrame);
                int additionalTargets = Mathf.RoundToInt((targetsPerFrame - roundAvgTargets) * aFramesPerCycle);


                for (int frameIndex = 0; frameIndex < aFramesPerCycle; frameIndex++)
                {
                    int targetsInCurrentFrame = roundAvgTargets + (frameIndex < additionalTargets ? 1 : 0);

                    List<ProcessedTarget> targetsForCurrentFrame = m_ListPool.Get();

                    for (int i = 0; i < targetsInCurrentFrame; i++)
                    {
                        ProcessedTarget target = m_ProcessedTargetPool.Get();
                        target.targetIndex = targetIndex;
                        target.cycles = 1;
                        targetsForCurrentFrame.Add(target);
                        targetIndex++;
                    }
                    processedTargetsPerFrame.Add(targetsForCurrentFrame);
                }
            }
            else // Case2 logic: Process one single target over multiple frames
            {
                int framesLeft = aFramesPerCycle - targetsCount;
                float avgCycles = (float)framesLeft / targetsCount;
                int avgCyclesRound = Mathf.FloorToInt(avgCycles);
                int additionalCycles = Mathf.RoundToInt((avgCycles - avgCyclesRound) * targetsCount);

                for (int frameIndex = 0; frameIndex < aFramesPerCycle; frameIndex++)
                {
                    if (targetIndex < targetsCount)
                    {
                        List<ProcessedTarget> targetsForCurrentFrame = m_ListPool.Get();
                        int cyclesForCurrentTarget = avgCyclesRound + 1 + (targetIndex < additionalCycles ? 1 : 0);
                        ProcessedTarget target = m_ProcessedTargetPool.Get();

                        target.targetIndex = targetIndex;
                        target.cycles = cyclesForCurrentTarget;
                        targetsForCurrentFrame.Add(target);
                        targetIndex++;
                        processedTargetsPerFrame.Add(targetsForCurrentFrame);

                    }
                }
            }
            return processedTargetsPerFrame;
        }

        public void ReleasePooledObjects(List<List<ProcessedTarget>> processedTargetsPerFrame)
        {
            // Release each ProcessedTarget object back to the processedTargetPool.
            foreach (var targetsForCurrentFrame in processedTargetsPerFrame)
            {
                foreach (var target in targetsForCurrentFrame)
                {
                    m_ProcessedTargetPool.Release(target);
                }

                // Clear the list of ProcessedTarget objects before releasing it back to the listPool.
                targetsForCurrentFrame.Clear();
                m_ListPool.Release(targetsForCurrentFrame);
            }

            // Clear the list of lists (processedTargetsPerFrame) before releasing it back to the nestedListPool.
            processedTargetsPerFrame.Clear();
            m_NestedListPool.Release(processedTargetsPerFrame);
        }

        private void ClearNestedList(List<List<ProcessedTarget>> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                list[i].Clear();
            }
        }



        //This function checks if there's a clear line of sight between two points (startPoint and endPoint).
        //Returns 'true' if the line of sight is clear, 'false' otherwise.
        private bool IsLineOfSightClear(Vector3 startPoint, Vector3 endPoint)
        {
            Vector3 direction = endPoint - startPoint;
            float distance = direction.magnitude;
            return !Physics.Raycast(startPoint, direction, out m_Hit, distance, raycastLayerMask);
        }


        //This function finds the probes that are in front of the listener.
        private void FindProbesBetweenListenerAndSource(List<Vector3> probeList,
            List<Vector3> probesBetweenListenerAndSource, Vector3 sourcePosition)
        {
            Vector3 listenerPosition = gameObject.transform.position + listenerOffset;
            Vector3 forward = (sourcePosition - listenerPosition).normalized;
            float listenerToSourceDistance = (sourcePosition - listenerPosition).magnitude;

            for (int i = 0; i < probeList.Count; i++)
            {
                Vector3 toProbe = probeList[i] - listenerPosition;
                float listenerToProbeDistance = toProbe.magnitude;

                // Check if the probe is in front of the game object
                if (Vector3.Dot(forward, toProbe) <= 0)
                    continue;

                // Check if the probe is between the listener and the source
                if (listenerToProbeDistance < listenerToSourceDistance)
                    probesBetweenListenerAndSource.Add(probeList[i]);
            }
        }



        //STEP 3: Find the probes that are in the line of sight of the target (or audio source)
        //This function finds the probes that are in the line of sight of the target audio source to filter out the ones that are behind obstacles.
        private void FindProbesInSight(Vector3 targetPosition, List<Vector3> aProbesInFront,
            List<Vector3> aProbesInSight) //STAGE 2
        {
            for (int i = 0; i < aProbesInFront.Count; i++)
            {
                // Check if the probe is in the line of sight of the target
                if (IsLineOfSightClear(targetPosition, aProbesInFront[i]))
                    aProbesInSight.Add(aProbesInFront[i]);
            }
        }




        //This function updates the list of closest audio sources to the listener.
        public void UpdateClosestActiveAudioSources(List<AudioSource> permanentList, List<AudioSource> additionalList,
            List<AudioSource> targetList,
            int aMaxClosestSources, float additionalMaxDistance = 0)
        {

            // Get the listener position
            Vector3 listenerPosition = transform.position;

            // Clear the source distances list
            m_SourceDistances.Clear();

            // Iterate through the permanent list and find distances for those within range
            foreach (AudioSource source in permanentList)
            {
                //EXIT if Null or Inactive
                if (source == null || !source.isActiveAndEnabled) continue;
                
                float distance = Vector3.Distance(listenerPosition, source.transform.position);
                if (distance <= source.maxDistance + additionalMaxDistance)
                {
                    m_SourceDistances.Add(new KeyValuePair<AudioSource, float>(source, distance));
                }
            }

            //do the same with the additional list
            foreach (AudioSource source in additionalList)
            {
                //EXIT if Null or Inactive
                if (source == null || !source.isActiveAndEnabled) continue;
                
                float distance = Vector3.Distance(listenerPosition, source.transform.position);
                if (distance <= source.maxDistance + additionalMaxDistance)
                {
                    m_SourceDistances.Add(new KeyValuePair<AudioSource, float>(source, distance));
                }
            }
            
            // Sort the sources based on distance
            m_SourceDistances.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
            
            
            // Clear the target list and add the closest sources up to aMaxClosestSources
            targetList.Clear();

            for (int i = 0; i < Math.Min(aMaxClosestSources, m_SourceDistances.Count); i++)
            {
                targetList.Add(m_SourceDistances[i].Key);
            }
        }
        
        
        
        
        
        //This function finds the closest left and right probes to the target audio source.
        private (int, int) FindClosestProbes(List<Vector3> aProbesInSight, Vector3 sourcePosition)
        {
            int leftProbeIndex = -1;
            int rightProbeIndex = -1;
            float minLeftDistance = float.MaxValue;
            float minRightDistance = float.MaxValue;
            Vector3 listenerPosition = gameObject.transform.position + listenerOffset;
            Vector3 forward = (sourcePosition - listenerPosition).normalized;

            for (int i = 0; i < aProbesInSight.Count; i++)
            {
                Vector3 toProbe = aProbesInSight[i] - listenerPosition;
                float distance = toProbe.magnitude;
                float angle = Vector3.SignedAngle(forward, toProbe, m_CachedUp);

                if (angle < 0 && distance < minLeftDistance)
                {
                    minLeftDistance = distance;
                    leftProbeIndex = i;
                }
                else if (angle > 0 && distance < minRightDistance)
                {
                    minRightDistance = distance;
                    rightProbeIndex = i;
                }
            }

            return (leftProbeIndex, rightProbeIndex);
        }


    }
}

