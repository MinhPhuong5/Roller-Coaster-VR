using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(AudioSource))]
public class ProximityAudio : MonoBehaviour
{
    [SerializeField] private AudioClip audioClip;
    [SerializeField] private bool loop = true;

    private AudioSource audioSource;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.loop = loop;
        audioSource.playOnAwake = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<AnimatorChihiro>() != null)
            if (audioClip != null && !audioSource.isPlaying) audioSource.Play();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<AnimatorChihiro>() != null)
            audioSource.Stop();
    }
}
