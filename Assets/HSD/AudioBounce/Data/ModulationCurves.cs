using UnityEngine;

namespace HSD.AudioBounce.Data
{
    [CreateAssetMenu(fileName = "ModulationCurves", menuName = "HSD/AudioBounce/ModulationCurves", order = 1)]
    public class ModulationCurves : ScriptableObject
    {
        [Tooltip(
            "The curve used to modulate the occlusion effect. The X axis is the amount of calculated Occlusion (0 = free travel (no bouncing), 1 = completely blocked), the Y axis is the amount of occlusion actually applied to the AudioSource (0 to 1).")]
        public AnimationCurve occlusionModulationCurve;

        [Tooltip(
            "This curve modulates the audio source's occlusion out based on proximity and it's meant to simulate sound propagation through surfaces. It works with MIN and MAX distances to adjust the occlusion amount. A value of 0.1 at 1 means 10% less occlusion at MIN or less distance.")]
        public AnimationCurve occlusionDistanceFalloff;

        [Tooltip(
            "Occlusion Distance Falloff MIN distance. The Occlusion Distance Falloff curve is at 1 at 'less than this distance' from the audio source removing about 10% of Occlusion effect on standard settings.")]
        public float oDF_MIN = 0.1f;

        [Tooltip(
            "Occlusion Distance Falloff MAX distance. The Occlusion Distance Falloff curve is at 0 at 'more than this distance' from the audio source and the full amount of Occlusion effect will be applied to the audio source.")]
        public float oDF_MAX = 5f;
        
        [Tooltip(
            "The curve used to modulate the reverb effect. The X axis is the amount of calculated Reverb (0 = open space, 1 = very small closed space), the Y axis is the amount of reverb actually applied to the AudioSource (0 to 1).")]
        public AnimationCurve reverbModulationCurve;
        
        [Tooltip(
            "This curve modulates the audio source's reverb out based on proximity and it's meant to cancel out reverberation up close. It works with MIN and MAX distances to adjust the reverb amount.")]
        public AnimationCurve reverbDistanceFalloff;
        
        [Tooltip(
            "Reverb Distance Falloff MIN distance. The Reverb Distance Falloff curve is at 1 at 'less than this distance' from the audio source removing about 10% of Reverb effect on standard settings.")]
        public float rDF_MIN = 0.1f;
        
        [Tooltip(
            "Reverb Distance Falloff MAX distance. The Reverb Distance Falloff curve is at 0 at 'more than this distance' from the audio source and the full amount of Reverb effect will be applied to the audio source.")]
        public float rDF_MAX = 2f;
    }
}