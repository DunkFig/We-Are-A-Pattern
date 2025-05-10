using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(PlayableDirector))]
public class TownTimelineController : MonoBehaviour
{
    [Range(-5f, 5f)] public float playbackSpeed = 1f;
    [Tooltip("Time in seconds to reach new speed")]
    public float transitionDuration = 2f;
    public List<AudioSource> audioSources = new();

    private PlayableDirector director;
    private Playable rootPlayable;
    private double duration;

    private float targetSpeed;
    private float speedVelocity;

    void OnEnable()
    {
        director = GetComponent<PlayableDirector>();
        rootPlayable = director.playableGraph.GetRootPlayable(0);
        duration = director.duration;

        targetSpeed = playbackSpeed;
        director.Play();
        rootPlayable.SetSpeed(playbackSpeed);

        foreach (var src in audioSources)
            if (src.clip != null && !src.isPlaying)
                src.Play();
    }

    public void SetTargetSpeed(float newSpeed)
    {
        targetSpeed = Mathf.Clamp(newSpeed, -5f, 5f);
    }

    void Update()
    {
        if (!rootPlayable.IsValid()) return;

        playbackSpeed = Mathf.SmoothDamp(playbackSpeed, targetSpeed, ref speedVelocity, transitionDuration);
        rootPlayable.SetSpeed(playbackSpeed);

        double t = director.time;
        if (playbackSpeed > 0f && t >= duration)
        {
            director.time = 0;
            director.Evaluate();
        }
        else if (playbackSpeed < 0f && t <= 0)
        {
            director.time = duration;
            director.Evaluate();
        }

        if (playbackSpeed == 0f)
        {
            director.Pause();
            foreach (var src in audioSources) src.Pause();
            return;
        }

        if (director.state != PlayState.Playing) director.Play();

        foreach (var src in audioSources)
        {
            if (src.clip == null) continue;
            src.pitch = playbackSpeed;
            if (!src.isPlaying) src.Play();
        }
    }
}
