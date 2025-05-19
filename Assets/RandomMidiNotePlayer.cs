using UnityEngine;
using System.Collections;
using MidiJack;

public class RandomMidiNotePlayer : MonoBehaviour
{
    [Header("MIDI Settings")]
    public int channel = 0;             // MIDI channel (0–15)
    public int minNote = 48;            // C3
    public int maxNote = 72;            // C5
    public float velocity = 1.0f;       // MIDI velocity (0–1)
    public float noteLength = 0.2f;     // How long each note lasts (seconds)
    public float interval = 1.0f;       // Time between notes (seconds)

    void Start()
    {
        StartCoroutine(PlayRandomNotes());
    }

    IEnumerator PlayRandomNotes()
    {
        while (true)
        {
            int randomNote = Random.Range(minNote, maxNote + 1);
            Debug.Log($"🎵 Playing MIDI Note: {randomNote}");

            // 🚧 Simulated send - replace with real send call if available
            // MidiMaster.SendNoteOn(channel, randomNote, velocity);

            yield return new WaitForSeconds(noteLength);

            // MidiMaster.SendNoteOff(channel, randomNote);

            yield return new WaitForSeconds(interval - noteLength);
        }
    }
}
