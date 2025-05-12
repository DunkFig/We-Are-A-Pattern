using System.Collections.Generic;
using UnityEngine;

namespace HSD.AudioBounce.Main
{
    
    /// <summary>
    /// AudioBounce's ProbeDistributor is a component that distributes probes to detect 3D objects and calculate the reverb factor for the audio sources in the scene. The same probes are used by the Locator to calculate Occlusion and bounce the audio.
    /// This is just a skeleton to inherit from when making a custom Distributor. You may want to do that for a Baked or a 2D solution.
    /// </summary>
    [AddComponentMenu("AudioBounce/Internal/AB_Distributor")]
    public class AB_Distributor : MonoBehaviour
    {
        
        
        [HideInInspector]
        public List<Vector3> finalProbePosition = new List<Vector3>(); //The actual final position of the probes

        [HideInInspector] public float reverbFactor; //The reverb factor calculated by the distributor

        public virtual float CalculateReverbFactor()
        {
            return 0;
        }
        
        

    }
}