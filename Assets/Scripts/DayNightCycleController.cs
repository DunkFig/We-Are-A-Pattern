using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(PlayableDirector))]
public class DayNightCycleController : MonoBehaviour
{
    [Header("Timeline")]
    [Tooltip("The PlayableDirector driving your town timeline")]
    public PlayableDirector timelineDirector;

    [Header("Sun Light")]
    [Tooltip("Your scene’s directional (sun) light")]
    public Light sunLight;

    [Header("Intensity (0 = midnight, 1 = midday)")]
    [Tooltip("Intensity at midnight (t=0 or t=1)")]
    public float minIntensity = 0.1f;
    [Tooltip("Intensity at midday (t=0.5)")]
    public float maxIntensity = 1.0f;

    [Header("Sun Rotation")]
    [Tooltip("Rotation at midnight (Vector3 Euler)")]
    public Vector3 midnightEuler = new Vector3(-90f, 0f, 0f);
    [Tooltip("Rotation at midday (Vector3 Euler)")]
    public Vector3 middayEuler   = new Vector3(90f, 0f, 0f);

    void Reset()
    {
        // try to auto‑assign if you forget in the Inspector
        timelineDirector = GetComponent<PlayableDirector>();
        sunLight = FindObjectOfType<Light>();
    }

    void Update()
    {
        if (timelineDirector == null || sunLight == null)
            return;

        double tRaw = timelineDirector.time;
        double duration = timelineDirector.duration;
        if (duration <= 0) return;

        // normalize 0→1 over the loop
        float t = (float)(tRaw / duration);

        // —— ROTATION ——  
        // build quaternions for your two key poses
        Quaternion midnightQuat = Quaternion.Euler(midnightEuler);
        Quaternion middayQuat   = Quaternion.Euler(middayEuler);

        // if t in [0, .5] lerp midnight→midday, else midday→midnight
        Quaternion sunRot;
        if (t <= 0.5f)
            sunRot = Quaternion.Slerp(midnightQuat, middayQuat, t * 2f);
        else
            sunRot = Quaternion.Slerp(middayQuat, midnightQuat, (t - 0.5f) * 2f);

        sunLight.transform.rotation = sunRot;

        // —— INTENSITY ——  
        // use a sine curve so intensity = 0 at t=0/1, 1 at t=0.5
        float intensityPct = Mathf.Sin(Mathf.PI * t);
        sunLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, intensityPct);
    }
}
