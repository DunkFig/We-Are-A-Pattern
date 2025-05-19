using UnityEngine;

namespace HSD.AudioBounce.Data
{
    [CreateAssetMenu(fileName = "SharableCurve", menuName = "HSD/AudioBounce/SharableCurves", order = 1)]
    public class SharableCurve : ScriptableObject
    {
        [Tooltip(
            "The curve used to modulate the occlusion effect. The X axis is the amount of calculated Occlusion (0 = free travel (no bouncing), 1 = completely blocked), the Y axis is the amount of occlusion actually applied to the AudioSource (0 to 1).")]
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }
}