using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BentoPickup : MonoBehaviour
{
    public GameObject promptUI;
    public TextMeshProUGUI promptText;
    public string prompt = "Nhấn F để thêm Bento vào giỏ hàng";

    private bool playerNear;
    private bool pickedUp;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        SetPrompt(false);
    }

    private void Update()
    {
        bool canPickUp = playerNear && !pickedUp && BentoPurchaseManager.Instance != null && !BentoPurchaseManager.Instance.IsInvoiceOpen;
        SetPrompt(canPickUp);
        if (canPickUp && Input.GetKeyDown(KeyCode.F) && BentoPurchaseManager.Instance.AddBentoToCart())
        {
            pickedUp = true;
            SetPrompt(false);
            gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other) { if (IsPlayer(other)) playerNear = true; }
    private void OnTriggerExit(Collider other) { if (IsPlayer(other)) playerNear = false; }
    private static bool IsPlayer(Collider other) => other != null &&
        (other.CompareTag("Player") || other.GetComponentInParent<AnimatorChihiro>() != null);

    private void SetPrompt(bool visible)
    {
        if (promptText != null && visible) promptText.text = prompt;
        if (promptUI != null && promptUI.activeSelf != visible) promptUI.SetActive(visible);
    }
}
