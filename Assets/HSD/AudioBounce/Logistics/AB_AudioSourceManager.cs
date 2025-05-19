using System;
using System.Collections.Generic;
using HSD.AudioBounce.Main;
using UnityEngine;

namespace HSD.AudioBounce.Logistics
{
    /// <summary>
    /// AudioBounce's AudioSourceManager is a singleton that keeps track of all playing audio sources in the scene.
    /// </summary>
    [AddComponentMenu("AudioBounce/Internal/AB_AudioSourceManager")]
    [DisallowMultipleComponent]
    public class AB_AudioSourceManager : MonoBehaviour
    {
        private readonly HashSet<AudioSource> m_PlayingSources = new HashSet<AudioSource>();
        private readonly HashSet<AudioSource> m_ReverbOnlySources = new HashSet<AudioSource>();
        [HideInInspector]public List<AudioSource> playingSourcesList = new List<AudioSource>();
        [HideInInspector]public List<AudioSource> reverbOnlySourcesList = new List<AudioSource>();
        public int PlayingSourcesCount => GetPlayingCount();
        public int ReverbOnlySourcesCount => GetReverbOnlyPlayingCount();
        public static AB_AudioSourceManager Instance;
        [HideInInspector]public AB_MainController mainController;
        
        
        public void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            mainController = AB_MainController.Instance;
        }

        public void Register(AudioSource source)
        {
            m_PlayingSources.Add(source);
        }
        
        public void RegisterReverbOnly(AudioSource source)
        {
            m_ReverbOnlySources.Add(source);
        }

        public void Deregister(AudioSource source)
        {
            m_PlayingSources.Remove(source);
        }
        
        public void DeregisterReverbOnly(AudioSource source)
        {
            m_ReverbOnlySources.Remove(source);
        }

        private int GetPlayingCount()
        {
            // optional cleanup of stopped audio sources before counting
            m_PlayingSources.RemoveWhere(source => !source.isPlaying);
            return m_PlayingSources.Count;
        }
        
        private int GetReverbOnlyPlayingCount()
        {
            // optional cleanup of stopped audio sources before counting
            m_ReverbOnlySources.RemoveWhere(source => !source.isPlaying);
            return m_ReverbOnlySources.Count;
        }

        public void Update()
        {
            playingSourcesList.Clear();
            reverbOnlySourcesList.Clear();
            playingSourcesList.AddRange(m_PlayingSources);
            reverbOnlySourcesList.AddRange(m_ReverbOnlySources);
        }
        
        public void PurgeThisSoundFromMainController(AudioSource source)
        {
            mainController.PurgeThisSound(source);
        }
    }
}