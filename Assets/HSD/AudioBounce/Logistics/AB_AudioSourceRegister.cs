using System;
using UnityEngine;

namespace HSD.AudioBounce.Logistics
{
    [AddComponentMenu("AudioBounce/Internal/AB_AudioSourceRegister")]
    [RequireComponent(typeof(AudioSource))]
    public class AB_AudioSourceRegister : MonoBehaviour
    {
        private AudioSource m_Source;
        private AB_AudioSourceManager m_Manager;
        private bool m_WasPlaying;
        [Tooltip("If true, this audio source will be registered as a high priority source.This means it will always be processed by AudioBounce to keep it's values updated even when it's not playing. Useful for sudden loud sounds that can be activated at any time near the player.")]
        public bool highPriority = false;
        [Tooltip("If true, this audio source will only be registered for reverb calculations. It will not be processed by AudioBounce for occlusion calculations.")]
        public bool reverbOnly = false;

        private void Awake()
        {
            m_Source = GetComponent<AudioSource>(); //Unity Audio Component
        }

        private void Start()
        {
            m_Manager = AB_AudioSourceManager.Instance;
        }

        private void Update()
        {
            if (m_Source.isPlaying && !m_WasPlaying || highPriority)
            {
                if (reverbOnly)
                {
                    m_Manager.RegisterReverbOnly(m_Source);
                }
                else
                {
                    m_Manager.Register(m_Source);
                }
                
                m_WasPlaying = true;
            }
            else if (!m_Source.isPlaying && m_WasPlaying && !highPriority)
            {
                if (reverbOnly)
                {
                    m_Manager.DeregisterReverbOnly(m_Source);
                }
                else
                {
                    m_Manager.Deregister(m_Source);
                }
                
                m_WasPlaying = false;
            }
        }

        private void OnDestroy()
        {
            if (m_Manager == null)
                return;
            
            if (reverbOnly)
            {
                m_Manager.DeregisterReverbOnly(m_Source);
            }
            else
            {
                m_Manager.Deregister(m_Source);
            }
            
            m_Manager.PurgeThisSoundFromMainController(m_Source);
        }
    }
}