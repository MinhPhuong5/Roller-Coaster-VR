using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TicketBoothInteractable : MonoBehaviour
{
    [Header("Âm Thanh Quầy (Tùy Chọn)")]
    public AudioSource audioSource;
    public AudioClip clickSound;

    private Collider boothCollider;

    private void Awake()
    {
        boothCollider = GetComponent<Collider>();
    }

    public void PlaySound()
    {
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);
    }
}