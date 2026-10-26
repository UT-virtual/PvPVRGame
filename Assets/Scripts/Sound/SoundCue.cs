using System;
using UnityEngine;

[Serializable]
public class SoundCue
{
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0.0f, 2.0f)] private float volume = 1.0f;
    [SerializeField, Range(0.1f, 3.0f)] private float pitch = 1.0f;
    [SerializeField] private bool randomizePitch = false;
    [SerializeField, Range(0.0f, 0.5f)] private float pitchRandomRange = 0.05f;

    public AudioClip Clip => clip;
    public float Volume => volume;
    public bool IsValid => clip != null;

    public SoundCue() { }

    public SoundCue(AudioClip defaultClip, float defaultVolume = 1.0f)
    {
        clip = defaultClip;
        volume = defaultVolume;
    }

    public float GetPitch()
    {
        if (!randomizePitch)
        {
            return pitch;
        }
        return Mathf.Clamp(
            pitch + UnityEngine.Random.Range(-pitchRandomRange, pitchRandomRange),
            0.1f,
            3.0f
        );
    }

    public void PlayOneShot(AudioSource source, AudioClip fallbackClip = null)
    {
        AudioClip targetClip = clip != null ? clip : fallbackClip;
        if (source == null || targetClip == null)
        {
            return;
        }

        source.pitch = GetPitch();
        source.PlayOneShot(targetClip, volume);
    }
}