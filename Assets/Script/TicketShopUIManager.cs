using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TicketShopUIManager : MonoBehaviour
{
    public static TicketShopUIManager Instance { get; private set; }

    [Header("Welcome Popup")]
    [Tooltip("Panel thông báo chào mừng xuất hiện khi vừa vào Scene")]
    public GameObject welcomePanel;
    public Button welcomeCloseButton;

    [Header("Main Ticket Shop UI (6 Games)")]
    [Tooltip("Panel chính chứa lưới 6 trò chơi phong cách Sóc Nhí")]
    public GameObject shopPanel;
    public Button shopCloseButton;
    public Button rollerCoasterButton; // Nút Tàu lượn siêu tốc (mở khóa)
    public Button[] lockedGameButtons; // 5 trò chơi còn lại bị khóa

    [Header("Ticket Quantity Popup (1 - 99)")]
    [Tooltip("Panel chọn số lượng vé")]
    public GameObject quantityPanel;
    public Slider ticketSlider;
    public TextMeshProUGUI ticketCountText;
    public TextMeshProUGUI totalPriceText;
    public Button decreaseButton;
    public Button increaseButton;
    public Button confirmBuyButton;
    public Button quantityCloseButton;

    [Header("Success Popup")]
    [Tooltip("Panel thông báo mua vé thành công")]
    public GameObject successPanel;
    public TextMeshProUGUI successMessageText;
    public Button successOkButton;
    public Button successCloseButton;

    [Header("Locked Game Notice Popup (Optional)")]
    public GameObject lockedNoticePanel;
    public TextMeshProUGUI lockedNoticeText;

    [Header("Settings")]
    public int ticketPrice = 50000; // Giá 50.000 VNĐ / vé
    public int currentTicketCount = 1;
    public int purchasedTickets = 0;
    public int PurchasedTicketCount => purchasedTickets;

    public bool ConsumeTicket()
    {
        if (purchasedTickets <= 0) return false;
        purchasedTickets--;
        return true;
    }

    private Coroutine lockedNoticeCoroutine;

    public bool IsModalOpen =>
        (welcomePanel != null && welcomePanel.activeSelf) ||
        (shopPanel != null && shopPanel.activeSelf) ||
        (quantityPanel != null && quantityPanel.activeSelf) ||
        (successPanel != null && successPanel.activeSelf);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 1. Cấu hình Slider từ 1 đến 99
        if (ticketSlider != null)
        {
            ticketSlider.minValue = 1;
            ticketSlider.maxValue = 99;
            ticketSlider.wholeNumbers = true;
            ticketSlider.value = 1;
            ticketSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        // 2. Lắng nghe các nút
        if (welcomeCloseButton != null) welcomeCloseButton.onClick.AddListener(CloseWelcome);
        if (shopCloseButton != null) shopCloseButton.onClick.AddListener(CloseAllShopUI);
        
        if (rollerCoasterButton != null) rollerCoasterButton.onClick.AddListener(OpenQuantityPopup);
        
        if (lockedGameButtons != null)
        {
            foreach (var btn in lockedGameButtons)
            {
                if (btn != null)
                {
                    btn.onClick.AddListener(OnLockedGameClicked);
                }
            }
        }

        if (decreaseButton != null) decreaseButton.onClick.AddListener(DecreaseTicket);
        if (increaseButton != null) increaseButton.onClick.AddListener(IncreaseTicket);
        if (confirmBuyButton != null) confirmBuyButton.onClick.AddListener(ConfirmPurchase);
        if (quantityCloseButton != null) quantityCloseButton.onClick.AddListener(CloseQuantityPopup);

        if (successOkButton != null) successOkButton.onClick.AddListener(CloseSuccessPopup);
        if (successCloseButton != null) successCloseButton.onClick.AddListener(CloseSuccessPopup);

        // 3. Khởi tạo trạng thái mặc định
        if (shopPanel != null) shopPanel.SetActive(false);
        if (quantityPanel != null) quantityPanel.SetActive(false);
        if (successPanel != null) successPanel.SetActive(false);
        if (lockedNoticePanel != null) lockedNoticePanel.SetActive(false);

        StyleTicketUi();
        UpdateTicketDisplay();

        // 4. Mở Popup Chào Mừng khi vừa vào Scene (nếu được kích hoạt)
        if (welcomePanel != null && welcomePanel.activeSelf)
        {
            LockPlayerMovement(true);
        }
        else
        {
            LockPlayerMovement(false);
        }
    }

    private void Update()
    {
        // Hội thoại mở đầu phải chạy hết; không cho Esc đóng Welcome để bỏ qua game gate.
        if (welcomePanel != null && welcomePanel.activeSelf &&
            welcomePanel.GetComponent<IntroDialogueController>() != null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (welcomePanel != null && welcomePanel.activeSelf) CloseWelcome();
            else if (IsModalOpen) CloseAllShopUI();
            return;
        }

        // Nhấn Enter hoặc Space để đóng thông báo chào mừng nhanh (nếu không dùng IntroDialogueController)
        if (welcomePanel != null && welcomePanel.activeSelf)
        {
            if (welcomePanel.GetComponent<IntroDialogueController>() == null)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                {
                    CloseWelcome();
                }
            }
        }
    }

    public void OpenShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            if (quantityPanel != null) quantityPanel.SetActive(false);
            if (successPanel != null) successPanel.SetActive(false);
            LockPlayerMovement(true);
        }
    }

    public void CloseAllShopUI()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
        if (quantityPanel != null) quantityPanel.SetActive(false);
        if (successPanel != null) successPanel.SetActive(false);
        if (lockedNoticePanel != null) lockedNoticePanel.SetActive(false);
        LockPlayerMovement(false);
    }

    public void CloseWelcome()
    {
        if (welcomePanel != null)
        {
            welcomePanel.SetActive(false);
        }
        if ((shopPanel == null || !shopPanel.activeSelf) &&
            (quantityPanel == null || !quantityPanel.activeSelf))
        {
            LockPlayerMovement(false);
        }
    }

    public void OpenQuantityPopup()
    {
        currentTicketCount = 1;
        if (ticketSlider != null) ticketSlider.value = 1;
        UpdateTicketDisplay();

        if (quantityPanel != null)
        {
            quantityPanel.SetActive(true);
        }
    }

    public void CloseQuantityPopup()
    {
        if (quantityPanel != null)
        {
            quantityPanel.SetActive(false);
        }
    }

    private void OnSliderValueChanged(float val)
    {
        currentTicketCount = Mathf.Clamp(Mathf.RoundToInt(val), 1, 99);
        UpdateTicketDisplay();
    }

    private void DecreaseTicket()
    {
        if (currentTicketCount > 1)
        {
            currentTicketCount--;
            if (ticketSlider != null) ticketSlider.value = currentTicketCount;
            UpdateTicketDisplay();
        }
    }

    private void IncreaseTicket()
    {
        if (currentTicketCount < 99)
        {
            currentTicketCount++;
            if (ticketSlider != null) ticketSlider.value = currentTicketCount;
            UpdateTicketDisplay();
        }
    }

    private void UpdateTicketDisplay()
    {
        if (ticketCountText != null)
        {
            ticketCountText.text = currentTicketCount.ToString();
        }
        if (totalPriceText != null)
        {
            int total = currentTicketCount * ticketPrice;
            totalPriceText.text = $"Tổng tiền: {total:N0} VNĐ";
        }
    }

    private void ConfirmPurchase()
    {
        purchasedTickets += currentTicketCount;

        if (quantityPanel != null) quantityPanel.SetActive(false);

        if (successPanel != null)
        {
            successPanel.SetActive(true);
            if (successMessageText != null)
            {
                successMessageText.text = $"Chúc mừng bạn đã mua thành công <color=#FFD700>{currentTicketCount} vé</color> Tàu Lượn Siêu Tốc!\nChúc bạn có những phút giây cảm giác mạnh tuyệt vời!";
            }
        }
    }

    public void CloseSuccessPopup()
    {
        CloseAllShopUI();
    }

    private void OnLockedGameClicked()
    {
        ShowNotice("Trò chơi này đang bảo trì hoặc chưa mở cửa!\nVui lòng chọn Tàu Lượn Siêu Tốc.");
    }

    public void ShowNotice(string message)
    {
        if (lockedNoticePanel != null)
        {
            if (lockedNoticeText != null) lockedNoticeText.text = message;
            lockedNoticePanel.SetActive(true);

            if (lockedNoticeCoroutine != null) StopCoroutine(lockedNoticeCoroutine);
            lockedNoticeCoroutine = StartCoroutine(HideNoticeRoutine());
        }
    }

    private IEnumerator HideNoticeRoutine()
    {
        yield return new WaitForSeconds(2.5f);
        if (lockedNoticePanel != null) lockedNoticePanel.SetActive(false);
    }

    private void LockPlayerMovement(bool isLocked)
    {
        PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm != null)
        {
            pm.enabled = !isLocked;
        }

        AnimatorChihiro ac = Object.FindFirstObjectByType<AnimatorChihiro>();
        if (ac != null)
        {
            ac.enabled = !isLocked;
        }

        if (isLocked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // Keeps the generated prompt readable even in scenes made before the new builder style.
    private void StyleInteractionPrompt()
    {
        Transform prompt = transform.Find("Genshin_InteractionPrompt");
        if (prompt == null) return;

        RectTransform promptRect = prompt.GetComponent<RectTransform>();
        promptRect.anchoredPosition = new Vector2(0f, -180f);
        promptRect.sizeDelta = new Vector2(390f, 82f);

        Image background = prompt.GetComponent<Image>();
        if (background != null) background.color = new Color(0.025f, 0.04f, 0.08f, 0.96f);

        Outline outline = prompt.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = new Color(1f, 0.7f, 0.2f, 0.95f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        Transform keyBox = prompt.Find("KeyBox");
        if (keyBox != null)
        {
            RectTransform keyRect = keyBox.GetComponent<RectTransform>();
            keyRect.anchoredPosition = new Vector2(28f, 0f);
            keyRect.sizeDelta = new Vector2(58f, 58f);
            TextMeshProUGUI keyText = keyBox.GetComponentInChildren<TextMeshProUGUI>();
            if (keyText != null) keyText.fontSize = 36f;
        }

        Transform label = prompt.Find("Prompt_Label");
        if (label == null) return;
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchoredPosition = new Vector2(225f, 0f);
        labelRect.sizeDelta = new Vector2(285f, 58f);
        TextMeshProUGUI labelText = label.GetComponent<TextMeshProUGUI>();
        if (labelText != null)
        {
            labelText.text = "MUA VÉ";
            labelText.fontSize = 30f;
        }
    }

    private void StyleTicketUi()
    {
        Color ink = new Color(0.025f, 0.04f, 0.08f, 1f);
        Color surface = new Color(0.07f, 0.11f, 0.18f, 1f);
        Color card = new Color(0.11f, 0.16f, 0.25f, 1f);
        Color gold = new Color(1f, 0.69f, 0.24f, 1f);
        Color cream = new Color(1f, 0.94f, 0.82f, 1f);

        if (welcomePanel != null && welcomePanel.transform.Find("WelcomeCard") != null)
        {
            SetImageColor(welcomePanel, "", new Color(0.01f, 0.02f, 0.05f, 0.84f));
            SetImageColor(welcomePanel, "WelcomeCard", surface);
            SetImageColor(welcomePanel, "WelcomeCard/InnerWhiteCard", card);
            SetImageColor(welcomePanel, "WelcomeCard/HeaderBanner", gold);
            SetImageColor(welcomePanel, "WelcomeCard/InnerWhiteCard/Btn_KhamPha", gold);
            SetText(welcomePanel, "WelcomeCard/HeaderBanner/Title", Color.black);
            SetText(welcomePanel, "WelcomeCard/InnerWhiteCard/Content", cream,
                "<size=30><color=#FFE0A3><b>Chào mừng đến Công Viên Vui Chơi</b></color></size>\n\nĐến <b>Quầy Bán Vé</b> để chọn vé và bắt đầu hành trình của bạn.\n\n<size=22><color=#F6B74A>★ Một chuyến đi tuyệt vời đang chờ bạn ★</color></size>");
            SetText(welcomePanel, "WelcomeCard/InnerWhiteCard/EnterHint", new Color(0.72f, 0.78f, 0.88f, 1f));
        }

        SetImageColor(shopPanel, "", new Color(0.01f, 0.02f, 0.05f, 0.86f));
        SetImageColor(shopPanel, "ShopFrame_SocNhiStyle", surface);
        SetImageColor(shopPanel, "ShopFrame_SocNhiStyle/InnerWhiteBox", ink);
        SetText(shopPanel, "ShopFrame_SocNhiStyle/ShopTitle", gold);
        SetImageColor(shopPanel, "ShopFrame_SocNhiStyle/Btn_CloseShop", new Color(0.75f, 0.22f, 0.2f, 1f));

        for (int i = 1; i <= 6; i++)
        {
            string slot = "ShopFrame_SocNhiStyle/InnerWhiteBox/Grid_6Games/GameSlot_" + i;
            bool unlocked = i == 1;
            SetImageColor(shopPanel, slot, unlocked ? new Color(0.22f, 0.17f, 0.1f, 1f) : new Color(0.09f, 0.13f, 0.2f, 1f));
            SetImageColor(shopPanel, slot + "/GameImage_Placeholder", unlocked ? new Color(0.92f, 0.46f, 0.12f, 1f) : new Color(0.16f, 0.2f, 0.29f, 1f));
            SetText(shopPanel, slot + "/GameName", unlocked ? gold : new Color(0.63f, 0.7f, 0.8f, 1f));
            if (!unlocked) SetImageColor(shopPanel, slot + "/LockOverlay", new Color(0.01f, 0.02f, 0.05f, 0.74f));
        }

        SetImageColor(quantityPanel, "", new Color(0.01f, 0.02f, 0.05f, 0.86f));
        SetImageColor(quantityPanel, "QtyCard/Btn_ConfirmBuy", gold);
        SetImageColor(successPanel, "", new Color(0.01f, 0.02f, 0.05f, 0.86f));
        SetImageColor(successPanel, "SuccessCard/Btn_OkSuccess", gold);
        StyleInteractionPrompt();
    }

    private static void SetImageColor(GameObject panel, string path, Color color)
    {
        if (panel == null) return;
        Transform target = string.IsNullOrEmpty(path) ? panel.transform : panel.transform.Find(path);
        Image image = target != null ? target.GetComponent<Image>() : null;
        if (image != null) image.color = color;
    }

    private static void SetText(GameObject panel, string path, Color color, string content = null)
    {
        if (panel == null) return;
        Transform target = panel.transform.Find(path);
        TextMeshProUGUI text = target != null ? target.GetComponent<TextMeshProUGUI>() : null;
        if (text == null) return;
        text.color = color;
        if (content != null) text.text = content;
    }
}
