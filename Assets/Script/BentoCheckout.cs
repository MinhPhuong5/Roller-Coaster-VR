using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BentoCheckout : MonoBehaviour
{
    public GameObject promptUI;
    public TextMeshProUGUI promptText;
    public string prompt = "Nhấn F để thanh toán";

    private bool playerNear;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        SetPrompt(false);
    }

    private void Update()
    {
        bool canPay = playerNear && BentoPurchaseManager.Instance != null &&
                      BentoPurchaseManager.Instance.HasCartItems && !BentoPurchaseManager.Instance.IsInvoiceOpen;
        SetPrompt(canPay);
        if (canPay && Input.GetKeyDown(KeyCode.F)) BentoPurchaseManager.Instance.OpenInvoice();
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
