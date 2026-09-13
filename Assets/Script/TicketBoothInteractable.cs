using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TicketBoothInteractable : MonoBehaviour
{
    public Transform playerTransform;
    [Tooltip("Khoảng cách từ mép collider của quầy, không phải tâm model.")]
    [Min(0f)] public float interactionDistance = 4f;
    public GameObject interactionPromptUI;
    public AudioSource audioSource;
    public AudioClip openShopSound;

    private Collider boothCollider;
    private bool playerInTrigger;

    private void Awake() => boothCollider = GetComponent<Collider>();

    private void Start()
    {
        FindPlayer();
        SetPrompt(false);
    }

    private void Update()
    {
        if (playerTransform == null) FindPlayer();
        if (playerTransform == null) return;

        bool canInteract = !IsUiBlockingInteraction() && (playerInTrigger || IsNearBooth());
        SetPrompt(canInteract);
        if (canInteract && Input.GetKeyDown(KeyCode.F)) Interact();
    }

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    // The booth model is scaled, so measure from the collider surface instead of its pivot.
    private bool IsNearBooth()
    {
        if (boothCollider == null || !boothCollider.enabled) return false;
        Vector3 playerPosition = playerTransform.position;
        Vector3 closestPoint = boothCollider.ClosestPoint(playerPosition);
        playerPosition.y = closestPoint.y = 0f;
        return Vector3.Distance(playerPosition, closestPoint) <= interactionDistance;
    }

    private bool IsUiBlockingInteraction() =>
        TicketShopUIManager.Instance != null && TicketShopUIManager.Instance.IsModalOpen;

    private void SetPrompt(bool visible)
    {
        if (interactionPromptUI != null && interactionPromptUI.activeSelf != visible)
            interactionPromptUI.SetActive(visible);
    }

    public void Interact()
    {
        if (IsUiBlockingInteraction()) return;
        SetPrompt(false);
        if (audioSource != null && openShopSound != null) audioSource.PlayOneShot(openShopSound);
        if (TicketShopUIManager.Instance != null) TicketShopUIManager.Instance.OpenShop();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null)
            playerInTrigger = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null)
            playerInTrigger = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (boothCollider == null) boothCollider = GetComponent<Collider>();
        if (boothCollider == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(boothCollider.bounds.center, boothCollider.bounds.size);
    }
}
