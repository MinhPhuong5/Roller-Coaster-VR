using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TicketShopUIManager : MonoBehaviour
{
    public static TicketShopUIManager Instance { get; private set; }

    [Header("Main Ticket Shop UI (Bảng đặt cố định trước quầy)")]
    [Tooltip("Panel chính chứa 6 trò chơi")]
    public GameObject shopPanel;
    public Button shopCloseButton;
    public Button rollerCoasterButton; // Nút Tàu lượn siêu tốc
    public Button[] lockedGameButtons; // Các nút game còn lại

    [Header("Ticket Quantity Popup (1 - 99)")]
    public GameObject quantityPanel;
    public Slider ticketSlider;
    public TextMeshProUGUI ticketCountText;
    public TextMeshProUGUI totalPriceText;
    public Button decreaseButton;
    public Button increaseButton;
    public Button confirmBuyButton;
    public Button quantityCloseButton;

    [Header("Success Popup")]
    public GameObject successPanel;
    public TextMeshProUGUI successMessageText;
    public Button successOkButton;
    public Button successCloseButton;
    [Tooltip("Thời gian hiển thị thông báo mua vé thành công trước khi tự động đóng toàn bộ UI (giây)")]
    public float successAutoCloseDelay = 2.0f;

    [Header("Locked Game Notice Popup")]
    public GameObject lockedNoticePanel;
    public TextMeshProUGUI lockedNoticeText;

    [Header("Settings")]
    public int ticketPrice = 50000;
    public int currentTicketCount = 1;
    public int purchasedTickets = 0;
    public int PurchasedTicketCount => purchasedTickets;

    private Coroutine lockedNoticeCoroutine;
    private Coroutine autoCloseSuccessCoroutine;

    public bool ConsumeTicket()
    {
        if (purchasedTickets <= 0) return false;
        purchasedTickets--;
        return true;
    }

    public bool IsModalOpen =>
        (quantityPanel != null && quantityPanel.activeSelf) ||
        (successPanel != null && successPanel.activeSelf);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 1. Cấu hình Slider chọn vé
        if (ticketSlider != null)
        {
            ticketSlider.minValue = 1;
            ticketSlider.maxValue = 99;
            ticketSlider.wholeNumbers = true;
            ticketSlider.value = 1;
            ticketSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        // 2. Gán sự kiện click nút
        if (shopCloseButton != null) shopCloseButton.onClick.AddListener(CloseShopPanel);
        if (rollerCoasterButton != null) rollerCoasterButton.onClick.AddListener(OpenQuantityPopup);

        if (lockedGameButtons != null)
        {
            foreach (var btn in lockedGameButtons)
            {
                if (btn != null) btn.onClick.AddListener(OnLockedGameClicked);
            }
        }

        if (decreaseButton != null) decreaseButton.onClick.AddListener(DecreaseTicket);
        if (increaseButton != null) increaseButton.onClick.AddListener(IncreaseTicket);
        if (confirmBuyButton != null) confirmBuyButton.onClick.AddListener(ConfirmPurchase);
        if (quantityCloseButton != null) quantityCloseButton.onClick.AddListener(CloseQuantityPopup);

        if (successOkButton != null) successOkButton.onClick.AddListener(CloseSuccessPopup);
        if (successCloseButton != null) successCloseButton.onClick.AddListener(CloseSuccessPopup);

        // Ẩn các popup phụ
        if (quantityPanel != null) quantityPanel.SetActive(false);
        if (successPanel != null) successPanel.SetActive(false);
        if (lockedNoticePanel != null) lockedNoticePanel.SetActive(false);

        // Chuột luôn tự do trên PC để click
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UpdateTicketDisplay();
    }

    public void OpenShop()
    {
        gameObject.SetActive(true);

        if (shopPanel != null) shopPanel.SetActive(true);
        if (quantityPanel != null) quantityPanel.SetActive(false);
        if (successPanel != null) successPanel.SetActive(false);
        if (lockedNoticePanel != null) lockedNoticePanel.SetActive(false);

        if (autoCloseSuccessCoroutine != null)
        {
            StopCoroutine(autoCloseSuccessCoroutine);
            autoCloseSuccessCoroutine = null;
        }
    }

    public void CloseShopPanel()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
        if (quantityPanel != null) quantityPanel.SetActive(false);
        if (successPanel != null) successPanel.SetActive(false);
        if (lockedNoticePanel != null) lockedNoticePanel.SetActive(false);

        // Ẩn luôn Canvas_TicketSystem nếu component gắn trên Canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    public void OpenQuantityPopup()
    {
        currentTicketCount = 1;
        if (ticketSlider != null) ticketSlider.value = 1;
        UpdateTicketDisplay();

        if (quantityPanel != null) quantityPanel.SetActive(true);
    }

    public void CloseQuantityPopup()
    {
        if (quantityPanel != null) quantityPanel.SetActive(false);
    }

    private void OnSliderValueChanged(float value)
    {
        currentTicketCount = Mathf.RoundToInt(value);
        UpdateTicketDisplay();
    }

    public void IncreaseTicket()
    {
        if (currentTicketCount < 99)
        {
            currentTicketCount++;
            if (ticketSlider != null) ticketSlider.value = currentTicketCount;
            UpdateTicketDisplay();
        }
    }

    public void DecreaseTicket()
    {
        if (currentTicketCount > 1)
        {
            currentTicketCount--;
            if (ticketSlider != null) ticketSlider.value = currentTicketCount;
            UpdateTicketDisplay();
        }
    }

    private void UpdateTicketDisplay()
    {
        if (ticketCountText != null) ticketCountText.text = currentTicketCount.ToString();
        if (totalPriceText != null)
        {
            int total = currentTicketCount * ticketPrice;
            totalPriceText.text = string.Format("{0:N0} VNĐ", total);
        }
    }

    /// <summary>
    /// Khi xác nhận mua vé:
    /// - Cộng số vé đã mua
    /// - Hiện Popup thông báo thành công
    /// - Tự động ẩn toàn bộ bảng vé sau khi hết thời gian chờ hoặc khi bấm Đóng
    /// </summary>
    public void ConfirmPurchase()
    {
        purchasedTickets += currentTicketCount;
        CloseQuantityPopup();

        if (successPanel != null)
        {
            if (successMessageText != null)
            {
                successMessageText.text = $"Bạn đã mua thành công {currentTicketCount} vé Tàu lượn siêu tốc!";
            }
            successPanel.SetActive(true);

            // Tự động đóng popup thành công và ẩn bảng vé sau 2 giây
            if (autoCloseSuccessCoroutine != null) StopCoroutine(autoCloseSuccessCoroutine);
            autoCloseSuccessCoroutine = StartCoroutine(AutoCloseSuccessAndShopRoutine());
        }
        else
        {
            CloseShopPanel();
        }
    }

    private IEnumerator AutoCloseSuccessAndShopRoutine()
    {
        yield return new WaitForSeconds(successAutoCloseDelay);
        CloseSuccessPopup();
    }

    public void CloseSuccessPopup()
    {
        if (autoCloseSuccessCoroutine != null)
        {
            StopCoroutine(autoCloseSuccessCoroutine);
            autoCloseSuccessCoroutine = null;
        }

        if (successPanel != null) successPanel.SetActive(false);

        // Sau khi mua vé xong -> Ẩn toàn bộ bảng vé
        CloseShopPanel();
    }

    private void OnLockedGameClicked()
    {
        if (lockedNoticePanel != null)
        {
            lockedNoticePanel.SetActive(true);
            if (lockedNoticeCoroutine != null) StopCoroutine(lockedNoticeCoroutine);
            lockedNoticeCoroutine = StartCoroutine(HideLockedNoticeRoutine());
        }
    }

    private IEnumerator HideLockedNoticeRoutine()
    {
        yield return new WaitForSeconds(2.0f);
        if (lockedNoticePanel != null) lockedNoticePanel.SetActive(false);
    }
}