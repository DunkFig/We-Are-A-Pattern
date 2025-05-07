using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[ExecuteAlways]
[RequireComponent(typeof(PlayableDirector))]
public class TownTimelineController : MonoBehaviour
{
    [Header("Timeline Speed")]
    [Range(-5f, 5f)]
    [Tooltip("Negative = reverse, 0 = paused, positive = forward.")]
    public float playbackSpeed = 1f;

    [Header("Audio Sources")]
    [Tooltip("Assign all AudioSources you want driven by the timeline.")]
    public List<AudioSource> audioSources = new List<AudioSource>();

    PlayableDirector director;
    Playable rootPlayable;
    double duration;

    void OnEnable()
    {
        director     = GetComponent<PlayableDirector>();
        rootPlayable = director.playableGraph.GetRootPlayable(0);
        duration     = director.duration;

        director.Play();
        rootPlayable.SetSpeed(playbackSpeed);

        foreach (var src in audioSources)
            if (src.clip != null && !src.isPlaying)
                src.Play();
    }

    void Update()
    {
        if (!rootPlayable.IsValid()) return;

        rootPlayable.SetSpeed(playbackSpeed);

        bool loopedForward  = false;
        bool loopedBackward = false;
        double t = director.time;

        // Loop forward
        if (playbackSpeed > 0f && t >= duration)
        {
            director.time = 0;
            director.Evaluate();
            loopedForward = true;
        }
        // Loop backward
        else if (playbackSpeed < 0f && t <= 0)
        {
            director.time = duration;
            director.Evaluate();
            loopedBackward = true;
        }
        // Pause
        else if (playbackSpeed == 0f)
        {
            director.Pause();
            foreach (var src in audioSources)
                src.Pause();
            return;
        }
        else if (director.state != PlayState.Playing)
        {
            director.Play();
        }

        foreach (var src in audioSources)
        {
            if (src.clip == null) continue;

            // match speed (pitch) including negative for reverse
            src.pitch = playbackSpeed;

            if (!src.isPlaying) src.Play();

            if (loopedForward)
            {
                // restart at zero for forward loops
                src.time = 0f;
            }
            else if (loopedBackward)
            {
                // jump to last sample so negative pitch plays backwards
                src.timeSamples = src.clip.samples - 1;
            }
        }
    }
}
