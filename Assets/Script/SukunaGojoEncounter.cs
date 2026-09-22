using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SukunaGojoEncounter : MonoBehaviour
{
    [Header("Nhân vật")]
    [SerializeField] private Transform sukuna;
    [SerializeField] private Transform gojo;

    [Header("Lời thoại")]
    [SerializeField] private AudioClip sukunaChallenge;
    [SerializeField] private AudioClip gojoReply;
    [SerializeField] private bool playOnce = true;

    private bool hasPlayed;

    private void Awake() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        TryStartDialogue(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryStartDialogue(other);
    }

    private void TryStartDialogue(Collider other)
    {
        if (hasPlayed || (!other.CompareTag("Player") && other.GetComponentInParent<AnimatorChihiro>() == null)) return;

        if (sukunaChallenge == null || gojoReply == null)
        {
            Debug.LogWarning("[SukunaGojoEncounter] Chưa gán đủ hai AudioClip.", this);
            return;
        }

        hasPlayed = playOnce;
        StartCoroutine(PlayDialogue());
    }

    private IEnumerator PlayDialogue()
    {
        if (sukunaChallenge != null)
        {
            PlayClip(sukuna, sukunaChallenge);
            yield return new WaitForSeconds(sukunaChallenge.length);
        }

        if (gojoReply != null)
            PlayClip(gojo, gojoReply);
    }

    private static void PlayClip(Transform speaker, AudioClip clip)
    {
        GameObject speakerObject = speaker != null ? speaker.gameObject : null;
        if (speakerObject == null) return;

        AudioSource source = speakerObject.GetComponent<AudioSource>();
        if (source == null) source = speakerObject.AddComponent<AudioSource>();

        source.spatialBlend = 0f;
        source.loop = false;
        source.playOnAwake = false;
        source.clip = clip;
        source.Play();
    }
}
