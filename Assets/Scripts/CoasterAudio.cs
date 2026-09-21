using UnityEngine;

public class CoasterAudio : MonoBehaviour
{
    [Header("Audio Components")]
    public AudioSource trackAudioSource;

    [Header("Speed Thresholds")]
    public float minSpeed = 0.5f;
    public float maxSpeed = 30f;

    [Header("Audio Modulation")]
    public float minVolume = 0.15f;
    public float maxVolume = 1.0f;
    public float minPitch = 0.7f;
    public float maxPitch = 1.6f;

    private Vector3 lastPosition;
    private float currentSpeed;
    private bool isStarted = false;

    void Start()
    {
        lastPosition = transform.position;

        if (trackAudioSource != null)
        {
            trackAudioSource.loop = true;
            trackAudioSource.playOnAwake = false;
            trackAudioSource.Stop();
        }
    }

    void Update()
    {
        if (!isStarted || Time.deltaTime <= 0f) return;

        float distance = Vector3.Distance(transform.position, lastPosition);
        currentSpeed = distance / Time.deltaTime;
        lastPosition = transform.position;

        if (trackAudioSource != null)
        {
            float speedRatio = Mathf.InverseLerp(minSpeed, maxSpeed, currentSpeed);
            trackAudioSource.volume = Mathf.Lerp(minVolume, maxVolume, speedRatio);
            trackAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, speedRatio);
        }
    }

    public void StartCoasterAudio()
    {
        isStarted = true;
        lastPosition = transform.position;
        if (trackAudioSource != null && !trackAudioSource.isPlaying)
        {
            trackAudioSource.Play();
        }
    }

    public void StopCoasterAudio()
    {
        isStarted = false;
        if (trackAudioSource != null)
        {
            trackAudioSource.Stop();
        }
    }
}