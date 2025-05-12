using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using UnityEngine.Pool;
using Color = System.Drawing.Color;
using HSD.AudioBounce.Utilities;
using HSD.AudioBounce.Data;
using HSD.AudioBounce.Logistics;


namespace HSD.AudioBounce.Main
{
    /// <summary>
    /// AudioBounce's MainController is a singleton that controls the audio effects and the audio sources in the scene. It also contains the main audio adjusting logic of the tool.
    /// </summary>
    [AddComponentMenu("AudioBounce/AB_MainController")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AB_Locator))]
    public class AB_MainController : MonoBehaviour
    {

        [System.Serializable]
        public struct ChannelData
        {
            public int index;
            public string highPassParam;
            public string lowPassParam;
            public string reverbParam;
            public AudioSource audioSource;
            public float currentHighPass;
            public float currentLowPass;
            public float currentReverb;
        }

        [HideInInspector] public bool disable;

        public static AB_MainController Instance { get; private set; }

        private AB_Locator m_AbLocator;
        private AB_AudioSourceManager m_AudisourceManager;

        //This is a public value in case you want to change it in runtime, but it's hidden in the inspector because it's not meant to be changed there.
        private AB_Distributor m_AbProbeDistributor;

        public AB_Distributor AbProbeDistributor
        {
            get { return m_AbProbeDistributor; }
            set
            {
                m_AbProbeDistributor = value;
                m_AbLocator.UpdateProbeDistributor(value);
            }
        } //This is the script that will distribute the probes in the scene

        [Tooltip("The speed at which the occlusion and reverb effects will blend in and out in seconds, the lower the fastest.")]
        public float sFxBlendSpeed = 0.5f;

        [Tooltip("The in depth description of what the curves are used for is in the ModulationCurves scriptable object. In short this allows you to change the curves used to modulate the occlusion and reverb effects in and out at a faster or slower rate depending on the amount of reverb and occlusion factor the tool calculates.")]
        public ModulationCurves modulationCurves;

        [Tooltip("This is your Main Audio Mixer, it will be used to apply the effects. We do provide a preconfigured one in the package so please use that one! If you really want to use your own, make sure it has the same effects and exposed parameters with the same names as the one provided. Also make sure you add each audio group of your mixer to the list below.")]
        public AudioMixer audioMixer;

        [Tooltip("The Audio Mixer Groups that you want to use for your audio sources. The Audio Mixer we provide comes with preconfigured Audio Mixer Groups called 'Channel_nr' that you can simply add down here. If you really want to use your own, make sure it has the same effects and exposed parameters with the same names as the one provided. AudioBounce won't calculate more 32 AudioSources in your scene but you can configure it to use less if you want to.")]
        public AudioMixerGroup[] audioMixerGroups;

        [Tooltip("The maximum number of AudioSources that AudioBounce will handle. If you have more AudioSources playing at the same time, the ones that are not calculated will be ignored and play as regular Unity Audio. This is to prevent performance loss. This value can be changed in runtime but it will never go above the number of Audio Mixer Channels you have set up or 32 in total.")]
        [Range(1, 32)]
        public int maxChannels = 32;

        [Tooltip("The speed at which the calculated 3D AudioSource will move to it's target position. This prevents audio souces teleportation which in game doesn't sound natural.")]
        public float bounceRepositionSpeed = 12f;
        
        [Tooltip("The amount of bias to apply to the bounce repositioning. 0 = no reposition (off), 1 = full bias (the audio will be positioned at bounce point). A value of 0.7 will give more bias to the bounce position but still factor in the original audio starting position which sounds more natural.")]
        [Range(0, 1)]
        public float bounceBiasFactor = 0.7f;
        
        [Tooltip("If true, the doppler effect will be disabled for the instanced audio sources. This is to prevent the doppler effect from being applied when the managed Audio Source repositions.")]
        public bool disableDopplerForBouncedAudio = true;
        
        [Tooltip("The speed at which the volume of the instanced audio sources will interpolate to the original audio source. This is to prevent the audio from popping when it's instanced and repositioned.")]
        [Min(1)]
        public float volumeInterpolationSpeed = 1f;

        
        //These are the max and min value for the reverb effect, they can be safely changed in here in case you need to tweak them but they are usually controlled by the "reverbModulationCurve", when the curve is at 1 reverb is at 0 so fully audible, when the curve is at 0 reverb is at -80 so fully muted.
        private const float MIN_REVERB = -80.0f;
        private const float MAX_REVERB = 0f;

        private const float MAXIMUM_DISTANCE_FACTOR = 180.0f;

        private float
            m_CloseEnoughRange =
                0.01f; // This determines the range at which the instanced audio sources are despawned from the originals when put back, adjust as per your needs

        private float m_TargetHighPass, m_TargetLowPass, m_TargetReverb;
        private float m_CurrentHighPass, m_CurrentLowPass, m_CurrentReverb;
        private ObjectPool<AudioSource> m_AudioSourcePool;
        private List<ChannelData> m_Channels = new List<ChannelData>();
        private ChannelData m_UpdatedChannel;

        private Dictionary<AudioSource, AudioSource> m_InstancedAudioSources =
            new Dictionary<AudioSource, AudioSource>();
        private Dictionary<AudioSource, float> m_InstancedAudioSourcesVolumes =
            new Dictionary<AudioSource, float>();

        private AudioSource targetAudioSource = new AudioSource();
        private AudioSource instance = new AudioSource();
        private ChannelData currentChannel = new ChannelData();
        private bool m_GoingBack;
        private int m_ChannelIndex;
        private List<AudioSource> keysToRemove = new List<AudioSource>();
        private GUIStyle baseLabelStyle;
        private GUIStyle labelStyle;
        private List<TargetData> m_TargetDataList;
        private TargetData m_TargetData; //This is for combining the two lists of bounced and unbounced sounds

        //GIzmos
        [Tooltip("The number of AudioSources to display gizmos for.")]
        public int gizmosDataDisplay = 1;

        public bool showGizmos = true;
        
        [Tooltip("Draw gizmos on build. Useful for debugging during playtests.")]
        public bool showGizmosOnBuild;


        void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            AudioListener.volume = 0f;

            m_AudioSourcePool = new ObjectPool<AudioSource>(() =>
                {
                    var prefab = new GameObject("3D_AudioSource").AddComponent<AudioSource>();
                    return prefab.GetComponent<AudioSource>();
                },
                get => get.gameObject.SetActive(true),
                release =>
                {
                    release.Stop();
                    release.gameObject.SetActive(false);
                    release.clip = null;
                    release.volume = 0f;
                },
                //destroy => Destroy(destroy.gameObject), true, 32);

                null, true, 32);
            if (maxChannels > audioMixerGroups.Length)
            {
                Debug.LogWarning(
                    "You have more channels than Audio Mixer Groups, please add more groups to your Audio Mixer or reduce the number of channels in the Audio Controller. Currently using the maximum number of groups available: " +
                    audioMixerGroups.Length);
            }
            
            

        }

        private void Start()
        {

            for (int i = 1; i <= maxChannels; i++)
            {
                m_Channels.Add(new ChannelData
                {
                    index = i,
                    highPassParam = $"CH_{i:D2}_HighPass",
                    lowPassParam = $"CH_{i:D2}_LowPass",
                    reverbParam = $"CH_{i:D2}_Reverb",
                    audioSource = null,
                });
            }

            //Get the other components from the scene
            m_AbLocator = AB_Locator.Instance;
            AbProbeDistributor = AB_ProbeDistributor.Instance;
            m_AudisourceManager = AB_AudioSourceManager.Instance;

            //Update the maxChannels value
            UpdateAvailableMaxChannels();

            //Start the coroutine that will increase the volume of the audio listener
            StartCoroutine(WakeUpVolumeRise());

            Debug.Log("AudioBounce Initialized");
        }

        private void Update()
        {
            if (disable)
            {
                ResetAllChannels();
                ReleaseAllInstances();
                return;
            }

            EvaluateSfx();
            
            CleanUpNonPlayingAudioSources();
            
            if(showGizmos && showGizmosOnBuild)
                DrawGizmosOnBuild();
        }

        private void CleanUpNonPlayingAudioSources()
        {
            keysToRemove.Clear();
            foreach (var instance in m_InstancedAudioSources)
            {
                if (!instance.Key.isPlaying && !instance.Value.isPlaying)
                {
                    keysToRemove.Add(instance.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                // Check if the original audio source is in the m_InstancedAudioSourcesVolumes dictionary
                if (m_InstancedAudioSourcesVolumes.ContainsKey(key))
                {
                    // Reset the volume of the original audio source
                    key.volume = m_InstancedAudioSourcesVolumes[key];
                    // Remove the original audio source from the m_InstancedAudioSourcesVolumes dictionary
                    m_InstancedAudioSourcesVolumes.Remove(key);
                }

                m_AudioSourcePool.Release(m_InstancedAudioSources[key]);
                m_InstancedAudioSources.Remove(key);
            }
        }
        
        
        //This function will set maxChannels to the minimum number between the value of maxChannels and the currentMixersGroup count, it will also update the maxClosestAudioSources value in the Locator
        public void UpdateAvailableMaxChannels()
        {
            maxChannels = Mathf.Min(maxChannels, audioMixerGroups.Length);
            m_AbLocator.maxClosestAudioSources = maxChannels;
        }

        private IEnumerator
            WakeUpVolumeRise() //This coroutine has the duration of 3 seconds and will increase the volume of the AudioListener from 0 to 1 at the start of the scene
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 3f;
                AudioListener.volume = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }
        }

        private void EvaluateSfx()
        {
            //If both Locator bounced sounds and Manager unbounced sounds lists are empty, there's no need to continue
            if (m_AbLocator.targetDataList.Count == 0)
                return;
           
            for (int i = 0; i < m_AbLocator.targetDataList.Count; i++)
            {
                //Exit if the audio source is null
                if (m_AbLocator.targetDataList[i].targetAudioSource == null)
                    continue;
                
                targetAudioSource = m_AbLocator.targetDataList[i].targetAudioSource;

                // Assign a channel to the audio source
                m_ChannelIndex = GetChannelIndexForAudioSource(targetAudioSource);

                if (m_ChannelIndex == -1)
                {
                    m_ChannelIndex = AssignAudioSourceToFreeChannel(targetAudioSource);
                    if (m_ChannelIndex == -1)
                        continue;
                }
                
                m_GoingBack = false;
                
                // If the audio source is reverb only, skip the instancing and repositioning logic
                if (m_AbLocator.targetDataList[i].reverbOnly)
                {
                    UpdateChannelEffects(m_ChannelIndex, i);
                    targetAudioSource.dopplerLevel = disableDopplerForBouncedAudio ? 0f : targetAudioSource.dopplerLevel;
                    continue;
                }

                bool shouldInstance = ShouldInstanceAudio(i);
                
                HandleInstancing(targetAudioSource, shouldInstance);

                UpdateInstancedAudioPosition(targetAudioSource, i);
               
                UpdateChannelEffects(m_ChannelIndex, i);
            }
        }
        
        
        private bool ShouldInstanceAudio(int index)
        {
            return m_AbLocator.targetDataList[index].leftProbe != Vector3.zero ||
                   m_AbLocator.targetDataList[index].rightProbe != Vector3.zero;
        }

        private int GetChannelIndexForAudioSource(AudioSource targetAudioSource)
        {
            for (int i = 0; i < m_Channels.Count; i++)
            {
                if (m_Channels[i].audioSource == targetAudioSource)
                    return i;
            }

            return -1; // Not found
        }

        public AudioSource GetInstanceForOriginal(AudioSource original)
        {
            m_InstancedAudioSources.TryGetValue(original, out AudioSource instance);
            return instance;
        }

        private void HandleInstancing(AudioSource targetAudioSource, bool shouldInstance, bool isReverbOnly = false)
        {
            
            // Check if the original audio source is playing
            bool originalPlaying = targetAudioSource.isPlaying;

            // Check if the original audio source has finished playing
            bool originAudioFinished = targetAudioSource.clip != null && targetAudioSource.time >= targetAudioSource.clip.length;

            // Check if there's an instanced version of this audio
            bool instanceExists = m_InstancedAudioSources.TryGetValue(targetAudioSource, out AudioSource instance);

            // Check if the instanced audio source has finished playing
            bool instanceAudioFinished = instance != null && instance.clip != null && instance.time >= instance.clip.length;

            // Check if both the original and instanced audio sources have finished playing
            bool audioFinished = originAudioFinished && instanceAudioFinished;
            
            
            // Code for instancing 
            if (shouldInstance && originalPlaying && !instanceExists)
            {
                // Instance the audio if conditions are met
                instance = m_AudioSourcePool.Get();
                CopyAllAudioSourceParameters(targetAudioSource, instance);
                m_InstancedAudioSources[targetAudioSource] = instance;
                m_InstancedAudioSourcesVolumes[targetAudioSource] = targetAudioSource.volume;
                instance.volume = 0f;
                instance.mute = false;
                m_GoingBack = false;
            }
            else if ((!shouldInstance || !originalPlaying || audioFinished) && instanceExists)
            {
                
                // Check if the instance has returned close enough to the original position
                float distanceToOriginal = Vector3.Distance(instance.transform.position, targetAudioSource.transform.position);

                if (distanceToOriginal <= m_CloseEnoughRange && instance.volume == 0f || !originalPlaying && instance.isPlaying)
                {
                    // Release the instance if it's close enough and the original is not playing but also if the original audio is not playing, and the instanced version is playing
                    m_AudioSourcePool.Release(instance);
                    m_InstancedAudioSources.Remove(targetAudioSource);
                    targetAudioSource.volume = m_InstancedAudioSourcesVolumes[targetAudioSource];
                    m_InstancedAudioSourcesVolumes.Remove(targetAudioSource);
                    m_GoingBack = false;
                }
                else
                {
                    m_GoingBack = true;
                }
            }
        }

        private void UpdateInstancedAudioPosition(AudioSource targetAudioSource, int index)
        {
            if (m_InstancedAudioSources.ContainsKey(targetAudioSource))
            {

                if (m_GoingBack) //Return the instanced audio source to the original position and lower it's volume while raising the original audio source volume
                {
                    Vector3 targetPosition = targetAudioSource.transform.position;
                    Vector3 currentPosition = m_InstancedAudioSources[targetAudioSource].transform.position;
                    Vector3 newPosition = Vector3.Lerp(currentPosition, targetPosition, Time.deltaTime * bounceRepositionSpeed);
                    m_InstancedAudioSources[targetAudioSource].transform.position = newPosition;

                    float distance = Vector3.Distance(newPosition, targetPosition);
                    float normalizedDistance = Mathf.Clamp01(distance / m_CloseEnoughRange);
                    float originalVolume = m_InstancedAudioSourcesVolumes[targetAudioSource];

                    float scaledDistance = normalizedDistance * volumeInterpolationSpeed;
                    float newVolume = (1 - scaledDistance) * originalVolume;
                    
                    // Inverse volume adjustment when going back
                    m_InstancedAudioSources[targetAudioSource].volume = originalVolume - newVolume;
                    
                    if (targetAudioSource.volume == 0) //Unmute the original source to slowly rise it's volume
                        targetAudioSource.mute = false;
                    
                    targetAudioSource.volume = newVolume; //Rise the volume of the original audio source
                    
                    if (newVolume >= originalVolume - 0.05f)
                    {
                        m_InstancedAudioSources[targetAudioSource].volume = 0f;
                        m_InstancedAudioSources[targetAudioSource].mute = true;
                    }
                }
                else //Move the instanced audio source to the perceived position and rise it's volume while lowering the original audio source volume
                {
                    Vector3 targetPosition = m_AbLocator.targetDataList[index].CalculatePerceivedPosition(transform.position, bounceBiasFactor);
                    Vector3 currentPosition = m_InstancedAudioSources[targetAudioSource].transform.position;
                    Vector3 newPosition = Vector3.Lerp(currentPosition, targetPosition, Time.deltaTime * bounceRepositionSpeed);
                    m_InstancedAudioSources[targetAudioSource].transform.position = newPosition;

                    float distance = Vector3.Distance(newPosition, targetPosition);
                    float normalizedDistance = Mathf.Clamp01(distance / m_CloseEnoughRange);
                    float originalVolume = m_InstancedAudioSourcesVolumes[targetAudioSource];

                    float scaledDistance = normalizedDistance * volumeInterpolationSpeed;
                    float newVolume = (1 - scaledDistance) * originalVolume;
                    
                    // Original volume adjustment
                    if (newVolume > m_InstancedAudioSources[targetAudioSource].volume)
                    {
                        targetAudioSource.volume = originalVolume - newVolume;
                        
                        if (m_InstancedAudioSources[targetAudioSource].volume == 0) //Unmute the instanced source to slowly rise it's volume
                            m_InstancedAudioSources[targetAudioSource].mute = false; 
                        
                        m_InstancedAudioSources[targetAudioSource].volume = newVolume; //Rise the volume of the instanced audio source
                        
                        if (newVolume >= originalVolume - 0.05f)
                        {
                            targetAudioSource.volume = 0f;
                            targetAudioSource.mute = true; 
                        }
                    }
                }
            }
        }


        private void UpdateChannelEffects(int channelIndex, int targetDataIndex)
        {
            currentChannel = m_Channels[channelIndex];
            var (targetHighPass, targetLowPass, targetReverb) = CalculateEffects(targetDataIndex);

            // Smoothly lerp the values over smoothSeconds
            float t = Time.deltaTime / sFxBlendSpeed;
            currentChannel.currentHighPass = Mathf.Lerp(currentChannel.currentHighPass, targetHighPass, t);
            currentChannel.currentLowPass = Mathf.Lerp(currentChannel.currentLowPass, targetLowPass, t);
            currentChannel.currentReverb = Mathf.Lerp(currentChannel.currentReverb, targetReverb, t);

            // Set the parameter values in the Audio Mixer
            audioMixer.SetFloat(currentChannel.highPassParam, currentChannel.currentHighPass);
            audioMixer.SetFloat(currentChannel.lowPassParam, currentChannel.currentLowPass);
            audioMixer.SetFloat(currentChannel.reverbParam, currentChannel.currentReverb);

            // Update the channels list with the modified ChannelData
            m_Channels[channelIndex] = currentChannel;
        }

        
        

        private void CopyAllAudioSourceParameters(AudioSource source, AudioSource target)
        {
            if (source == null || target == null)
            {
                Debug.LogWarning(
                    "Your source audio has probably been destroyed while audio was playing. If you see this message often, please check your audio sources and make sure they are not being destroyed while playing.");
                return;
            }

            var clip = source.clip;
            target.name = "3DAudio_" + clip.name;
            target.transform.position = source.transform.position;
            target.clip = clip;
            target.time = source.time;
            target.volume = source.volume;
            target.pitch = source.pitch;
            target.loop = source.loop;
            target.playOnAwake = source.playOnAwake;
            target.ignoreListenerPause = source.ignoreListenerPause;
            target.ignoreListenerVolume = source.ignoreListenerVolume;
            target.spatialBlend = source.spatialBlend;
            target.reverbZoneMix = source.reverbZoneMix;
            target.bypassEffects = source.bypassEffects;
            target.bypassListenerEffects = source.bypassListenerEffects;
            target.bypassReverbZones = source.bypassReverbZones;
            target.spread = source.spread;
            target.rolloffMode = source.rolloffMode;
            if(target.rolloffMode == AudioRolloffMode.Custom)
                target.SetCustomCurve(AudioSourceCurveType.CustomRolloff, source.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            target.minDistance = source.minDistance;
            target.maxDistance = source.maxDistance;
            target.panStereo = source.panStereo;
            target.spatialize = source.spatialize;
            target.spatializePostEffects = source.spatializePostEffects;
            target.priority = source.priority;
            target.outputAudioMixerGroup = source.outputAudioMixerGroup;
            target.mute = source.mute;
            target.playOnAwake = source.playOnAwake;
            if (disableDopplerForBouncedAudio)
            {
                target.dopplerLevel = 0f;
            }
            else
            {
                target.dopplerLevel = source.dopplerLevel;
            }
            
            if (source.isPlaying)
                target.Play();
            else
                target.Stop();
        }

        private int AssignAudioSourceToFreeChannel(AudioSource source)
        {
            for (int i = 0; i < m_Channels.Count; i++)
            {
                if (m_Channels[i].audioSource == null)
                {
                    m_UpdatedChannel = m_Channels[i];
                    m_UpdatedChannel.audioSource = source;
                    m_Channels[i] = m_UpdatedChannel;
                    source.outputAudioMixerGroup = audioMixerGroups[i];
                    return i;
                }
            }

            return -1;
        }





        private (float highPass, float lowPass, float reverb) CalculateEffects(int index)
        {
            // Get the position of the listener.
            Vector3 thisPosition = transform.position;

            // Calculate the angle between the listener and the sound source.
            float angles = m_AbLocator.targetDataList[index].CalculateAngles(thisPosition);
            angles = Mathf.Clamp(angles, 0, MAXIMUM_DISTANCE_FACTOR);

            // Linear interpolation value based on angle, adjusted with occlusion modulation curve.
            float lerpValue = angles / MAXIMUM_DISTANCE_FACTOR;
            float modulatedLerpValue = modulationCurves.occlusionModulationCurve.Evaluate(lerpValue);

            // Calculate the distance from the listener to the sound source.
            float distanceToAudioSource = Vector3.Distance(thisPosition,
                m_AbLocator.targetDataList[index].targetAudioSource.transform.position);

            // Normalize the distance for occlusion effect (within defined min and max range).
            float normalizedOcclusionDistance = 1.0f - Mathf.Clamp01(
                (distanceToAudioSource - modulationCurves.oDF_MIN) /
                (modulationCurves.oDF_MAX - modulationCurves.oDF_MIN));
            float occlusionDistanceMultiplier =
                modulationCurves.occlusionDistanceFalloff.Evaluate(normalizedOcclusionDistance);

            // Normalize the distance for reverb effect (within new defined min and max range).
            float normalizedReverbDistance = 1.0f - Mathf.Clamp01((distanceToAudioSource - modulationCurves.rDF_MIN) /
                                                                  (modulationCurves.rDF_MAX -
                                                                   modulationCurves.rDF_MIN));
            float reverbDistanceMultiplier = modulationCurves.reverbDistanceFalloff.Evaluate(normalizedReverbDistance);

            // Calculate the high-pass filter frequency.
            float highPassDifference = 300 - Mathf.Lerp(10, 300, modulatedLerpValue);
            float targetHighPass = Mathf.Lerp(10, 300, modulatedLerpValue) +
                                   highPassDifference * occlusionDistanceMultiplier;

            // Calculate the low-pass filter frequency.
            float lowPassDifference = 20000 - Mathf.Lerp(20000, 300, modulatedLerpValue);
            float targetLowPass = Mathf.Lerp(20000, 300, modulatedLerpValue) +
                                  lowPassDifference * occlusionDistanceMultiplier;

            // Determine the reverb level based on the reverb factor and modulation curve.
            float allowedPointsRatio = m_AbProbeDistributor.reverbFactor;
            float modulatedAllowedPointsRatio = modulationCurves.reverbModulationCurve.Evaluate(1 - allowedPointsRatio);
            float targetReverb = Mathf.Lerp(MIN_REVERB, MAX_REVERB, modulatedAllowedPointsRatio) *
                                 reverbDistanceMultiplier;

            // Return the calculated high-pass, low-pass, and reverb values.
            return (targetHighPass, targetLowPass, targetReverb);
        }



        private float GetOcclusionValueFromHighLowPass(int index)
        {
            var (targetHighPass, targetLowPass, _) = CalculateEffects(index);

            float normalizedHighPass = (targetHighPass - 10) / (300 - 10);
            float normalizedLowPass = (20000 - targetLowPass) / (20000 - 300);

            float occlusionValue = (normalizedHighPass + normalizedLowPass) / 2.0f;

            return occlusionValue;
        }


        private void ResetAllChannels()
        {
            ChannelData channel;
            ChannelData updatedChannel;
            for (int i = 0; i < m_Channels.Count; i++)
            {
                channel = m_Channels[i];
                if (channel.audioSource == null) continue;

                // Reset the parameter values in the Audio Mixer
                audioMixer.SetFloat(channel.highPassParam, 10); // Reset HighPass to original value
                audioMixer.SetFloat(channel.lowPassParam, 20000); // Reset LowPass to original value
                audioMixer.SetFloat(channel.reverbParam, MIN_REVERB); // Reset Reverb to original value

                // Update the ChannelData with the reset values
                updatedChannel = channel;
                updatedChannel.currentHighPass = 10;
                updatedChannel.currentLowPass = 20000;
                updatedChannel.currentReverb = MIN_REVERB;
                m_Channels[i] = updatedChannel;
            }
        }

        private void ReleaseAllInstances()
        {
            foreach (var instance in m_InstancedAudioSources)
            {
                m_AudioSourcePool.Release(instance.Value);
                instance.Key.mute = false;
            }

            m_InstancedAudioSources.Clear();
        }

        //Purge Instances with this audio source
        public void PurgeThisSound(AudioSource source)
        {
            if (m_InstancedAudioSources.ContainsKey(source))
            {
                m_AudioSourcePool.Release(m_InstancedAudioSources[source]);
                m_InstancedAudioSources.Remove(source);
            }
        }



        
        private void OnDrawGizmos()
        {
            if (!showGizmos || showGizmosOnBuild)
                return;
            
            if (m_AbLocator == null || m_AbLocator.targetDataList == null)
                return;
            

            Color32 orange = AB_Utilities.ToColor32(Color.Orange);

            // Create GUIStyle with the desired color
            
            labelStyle.normal.textColor = orange;

            // Main Gizmo Function
            for (int i = 0; i < gizmosDataDisplay; i++)
            {
                if (m_AbLocator.targetDataList.Count <= i)
                    continue;

                TargetData targetData = m_AbLocator.targetDataList[i];
                Transform target = targetData.targetTransform;

                if (target != null)
                {
                    Vector3 targetPosition = target.position;
                    Vector3 thisPosition = gameObject.transform.position;
                    bool lineOfSightClear = targetData.inLineOfSight;

                    Gizmos.color = lineOfSightClear
                        ? AB_Utilities.ToColor32(Color.DarkGreen)
                        : AB_Utilities.ToColor32(Color.DarkRed);
                    Gizmos.DrawLine(target.position, thisPosition);

                    if (!targetData.inLineOfSight)
                    {
                        Vector3 midPoint = (targetPosition + thisPosition) * 0.5f;
                        Vector3 direction = (thisPosition - targetPosition).normalized;
                        Vector3 perpendicularDirection = new Vector3(-direction.z, 0, direction.x);

                        Vector3 leftProbe = targetData.leftProbe;
                        Vector3 rightProbe = targetData.rightProbe;

                        if (leftProbe == Vector3.zero)
                        {
                            leftProbe = midPoint - perpendicularDirection * 2f;
                        }

                        if (rightProbe == Vector3.zero)
                        {
                            rightProbe = midPoint + perpendicularDirection * 2f;
                        }

                        // Draw left and right probe lines
                        Gizmos.color = targetData.leftProbe == Vector3.zero
                            ? AB_Utilities.ToColor32(Color.Maroon)
                            : AB_Utilities.ToColor32(Color.Teal);
                        Gizmos.DrawLine(targetPosition, leftProbe);
                        Gizmos.DrawLine(thisPosition, leftProbe);
                        Gizmos.DrawSphere(leftProbe, 0.2f);
                        Gizmos.color = targetData.rightProbe == Vector3.zero
                            ? AB_Utilities.ToColor32(Color.Maroon)
                            : AB_Utilities.ToColor32(Color.Salmon);
                        Gizmos.DrawLine(targetPosition, rightProbe);
                        Gizmos.DrawLine(thisPosition, rightProbe);
                        Gizmos.DrawSphere(rightProbe, 0.2f);
                    }
                }
            }
        }
        
        private void DrawGizmosOnBuild()
        {
            if (m_AbLocator == null || m_AbLocator.targetDataList == null)
                return;
            
            // Main Gizmo Function
            for (int i = 0; i < gizmosDataDisplay; i++)
            {
                if (m_AbLocator.targetDataList.Count <= i)
                    continue;

                TargetData targetData = m_AbLocator.targetDataList[i];
                Transform target = targetData.targetTransform;

                if (target != null)
                {
                    Vector3 targetPosition = target.position;
                    Vector3 thisPosition = gameObject.transform.position;
                    bool lineOfSightClear = targetData.inLineOfSight;

                   
                    Transform targetTransform = GetInstanceForOriginal(targetData.targetAudioSource) ? GetInstanceForOriginal(targetData.targetAudioSource).transform : target;
                    nDebug.DrawSphere(targetTransform.position, 0.25f,
                        Color.Orange, nDebug.SphereRes.four, 0.7f, true);
                    nDebug.DrawSphere(targetPosition, 0.15f,
                        Color.AntiqueWhite, nDebug.SphereRes.four, 1f, true);
                    
                    nDebug.DrawLine(targetPosition, thisPosition, lineOfSightClear ? Color.DarkGreen : Color.DarkRed);

                    if (!targetData.inLineOfSight)
                    {
                        Vector3 midPoint = (targetPosition + thisPosition) * 0.5f;
                        Vector3 direction = (thisPosition - targetPosition).normalized;
                        Vector3 perpendicularDirection = new Vector3(-direction.z, 0, direction.x);

                        Vector3 leftProbe = targetData.leftProbe;
                        Vector3 rightProbe = targetData.rightProbe;

                        if (leftProbe == Vector3.zero)
                        {
                            leftProbe = midPoint - perpendicularDirection * 2f;
                        }

                        if (rightProbe == Vector3.zero)
                        {
                            rightProbe = midPoint + perpendicularDirection * 2f;
                        }

                        // Draw left and right probe lines

                        Color colorLeft = targetData.leftProbe == Vector3.zero ? Color.Maroon : Color.Teal;
                        nDebug.DrawLine(targetPosition, leftProbe, colorLeft);
                        nDebug.DrawLine(thisPosition, leftProbe, colorLeft);
                        nDebug.DrawSphere(leftProbe, 0.18f, colorLeft, nDebug.SphereRes.sixteen);
                        
                        Color colorRight = targetData.rightProbe == Vector3.zero ? Color.Maroon : Color.Salmon;
                        nDebug.DrawLine(targetPosition, rightProbe, colorRight);
                        nDebug.DrawLine(thisPosition, rightProbe, colorRight);
                        nDebug.DrawSphere(rightProbe, 0.18f, colorRight, nDebug.SphereRes.sixteen);
                    }
                }
            }
        }
       
        void OnGUI()
        {
            if (disable)
                return;
            if (!showGizmos)
                return;
            if (m_AbLocator == null || m_AbLocator.targetDataList == null)
                return;
            if(baseLabelStyle == null)
                baseLabelStyle = new GUIStyle(GUI.skin.label);
            if(labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label);
            DrawGizmoInfoOnGUI(m_AbLocator.targetDataList, disable, new Vector3(0, 1, 0), baseLabelStyle,
                modulationCurves);
        }
        
        void DrawGizmoInfoOnGUI(List<TargetData> targetDataList, bool disable, Vector3 labelOffset, GUIStyle labelStyle,
            ModulationCurves modulationCurves)
        {
            for (int i = 0; i < gizmosDataDisplay; i++)
            {
                if (targetDataList.Count <= i)
                    continue;
                
                TargetData targetData = targetDataList[i];
                Transform target = targetData.targetTransform;

                if (targetData.targetAudioSource != null)
                {
                    // Calculate Occlusion
                    float occlusion = GetOcclusionValueFromHighLowPass(i);

                    // Calculate Reverb
                    float allowedPointsRatio = m_AbProbeDistributor.reverbFactor;
                    float modulatedAllowedPointsRatio =
                        modulationCurves.reverbModulationCurve.Evaluate(1 - allowedPointsRatio);
                    float reverb = Mathf.Lerp(MIN_REVERB, MAX_REVERB, modulatedAllowedPointsRatio);

                    // Normalize and invert Reverb for display
                    float reverbNormalized = Mathf.InverseLerp(MIN_REVERB, MAX_REVERB, reverb);

                    // Check if the disable bool is false
                    bool isDisable = disable;


                    string occlusionText = isDisable ? "OFF" : occlusion.ToString("F2");
                    if (targetData.reverbOnly)
                    {
                        occlusionText = "N/A - Reverb Only";
                    }
                    string reverbText = isDisable ? "OFF" : reverbNormalized.ToString("F2");


                    // Find the associated channel
                    string channelName = "";
                    AudioSource targetAudioSource = targetData.targetAudioSource;
                    ChannelData associatedChannel = m_Channels.Find(c => c.audioSource == targetAudioSource);
                    if (associatedChannel.audioSource != null)
                    {
                        channelName = $"CH_{associatedChannel.index:D2}";
                    }

                    string clipName = targetAudioSource.clip != null ? targetAudioSource.clip.name : "None";

                    // Display useful information for each target
                    string labelText = $"Target Name: {target.gameObject.name}\n" +
                                       $"Channel: {channelName}\n" +
                                       $"Audio Clip: {clipName}\n" +
                                       $"Occlusion: {occlusionText}\n" + // Use the occlusionText
                                       $"Reverb: {reverbText}"; // Use the reverbText

                    Vector3 screenPos = Camera.main.WorldToScreenPoint(target.position + labelOffset);
                    if (screenPos.z > 0) // Check if the target is in front of the camera
                    {
                        // Flip screenPos.y since GUI's Y is flipped
                        screenPos.y = Screen.height - screenPos.y;
                        
                        GUI.Label(new Rect(screenPos.x, screenPos.y, 200, 100), labelText, labelStyle);
                    }
                }
            }
        }
    }
}