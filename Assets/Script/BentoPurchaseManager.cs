using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Giỏ Bento tạm thời và hóa đơn thanh toán cho bản prototype một sản phẩm.</summary>
public class BentoPurchaseManager : MonoBehaviour
{
    public static BentoPurchaseManager Instance { get; private set; }

    [Header("Bento")]
    public string productName = "Bento Nhật Bản";
    [Min(0)] public int unitPrice = 30000;
    [Range(0f, 1f)] public float vatRate = 0.1f;
    [SerializeField] private int cartBentoCount;
    [SerializeField] private int ownedBentoCount;

    [Header("Video ăn Bento")]
    [Tooltip("Video toàn màn hình phát sau khi Chihiro dùng Bento khi đang ngồi.")]
    public VideoClip eatingVideo;

    [Header("Hóa đơn")]
    public GameObject invoicePanel;
    public TextMeshProUGUI productNameText;
    public TextMeshProUGUI quantityText;
    public TextMeshProUGUI unitPriceText;
    public TextMeshProUGUI vatText;
    public TextMeshProUGUI totalText;
    public Button payButton;
    public Button cancelButton;

    public bool HasCartItems => cartBentoCount > 0;
    public int OwnedBentoCount => ownedBentoCount;
    public bool IsInvoiceOpen => invoicePanel != null && invoicePanel.activeSelf;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (GetComponent<InventoryUIManager>() == null)
            gameObject.AddComponent<InventoryUIManager>();
        BuildInvoiceUi();
        if (invoicePanel != null) invoicePanel.SetActive(false);
        if (payButton != null) payButton.onClick.AddListener(Pay);
        if (cancelButton != null) cancelButton.onClick.AddListener(CloseInvoice);
    }

    public bool AddBentoToCart()
    {
        if (cartBentoCount > 0) return false;
        cartBentoCount = 1;
        return true;
    }

    public void OpenInvoice()
    {
        if (!HasCartItems || invoicePanel == null) return;
        UpdateInvoiceText();
        invoicePanel.SetActive(true);
        SetPlayerLocked(true);
    }

    public void CloseInvoice()
    {
        if (invoicePanel != null) invoicePanel.SetActive(false);
        SetPlayerLocked(false);
    }

    public void Pay()
    {
        if (!HasCartItems) { CloseInvoice(); return; }

        ownedBentoCount += cartBentoCount;
        cartBentoCount = 0;
        CloseInvoice();
        Debug.Log($"[Bento] Thanh toán thành công. Bento trong túi: {ownedBentoCount}");
    }

    public bool ConsumeBento()
    {
        if (ownedBentoCount <= 0) return false;
        ownedBentoCount--;
        return true;
    }

    public void PlayEatingVideo()
    {
        if (eatingVideo != null) StartCoroutine(PlayEatingVideoRoutine());
    }

    private System.Collections.IEnumerator PlayEatingVideoRoutine()
    {
        Camera targetCamera = Object.FindFirstObjectByType<Camera>();
        if (targetCamera == null) yield break;

        AnimatorChihiro player = Object.FindFirstObjectByType<AnimatorChihiro>();
        if (player != null) player.SetInputLocked(true);

        VideoPlayer video = gameObject.AddComponent<VideoPlayer>();
        AudioSource audio = gameObject.AddComponent<AudioSource>();
        video.source = VideoSource.VideoClip;
        video.clip = eatingVideo;
        video.renderMode = VideoRenderMode.CameraNearPlane;
        video.targetCamera = targetCamera;
        video.aspectRatio = VideoAspectRatio.FitHorizontally;
        video.audioOutputMode = VideoAudioOutputMode.AudioSource;
        video.controlledAudioTrackCount = 1;
        audio.spatialBlend = 0f;

        video.Prepare();
        float preparationTime = 0f;
        while (!video.isPrepared && preparationTime < 10f)
        {
            preparationTime += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!video.isPrepared)
        {
            Destroy(video);
            Destroy(audio);
            yield break;
        }

        if (video.audioTrackCount > 0)
        {
            video.EnableAudioTrack(0, true);
            video.SetTargetAudioSource(0, audio);
        }

        video.Play();
        while (video.isPlaying) yield return null;

        Destroy(video);
        Destroy(audio);
        if (player != null) player.SetInputLocked(SeatInteraction.IsPlayerSitting);
    }

    private void UpdateInvoiceText()
    {
        int subTotal = cartBentoCount * unitPrice;
        int vat = Mathf.RoundToInt(subTotal * vatRate);
        int total = subTotal + vat;
        if (productNameText != null) productNameText.text = productName;
        if (quantityText != null) quantityText.text = cartBentoCount.ToString();
        if (unitPriceText != null) unitPriceText.text = $"{unitPrice:N0} VNĐ";
        if (vatText != null) vatText.text = $"{vat:N0} VNĐ ({vatRate:P0})";
        if (totalText != null) totalText.text = $"{total:N0} VNĐ";
    }

    private static void SetPlayerLocked(bool locked)
    {
        AnimatorChihiro player = Object.FindFirstObjectByType<AnimatorChihiro>();
        if (player != null) player.SetInputLocked(locked);
        Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = locked;
    }

    // Build one small, self-contained receipt UI so the Bento flow does not depend on Ticket UI.
    private void BuildInvoiceUi()
    {
        if (invoicePanel == null) return;

        foreach (Transform child in invoicePanel.transform)
            child.gameObject.SetActive(false);

        RectTransform panelRect = invoicePanel.GetComponent<RectTransform>();
        if (panelRect == null) panelRect = invoicePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        Image backdrop = invoicePanel.GetComponent<Image>();
        if (backdrop == null) backdrop = invoicePanel.AddComponent<Image>();
        backdrop.color = new Color(0.02f, 0.025f, 0.04f, 0.78f);

        GameObject card = CreateUiObject("BentoInvoiceCard", invoicePanel.transform);
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = new Color(0.12f, 0.085f, 0.075f, 1f);
        Outline cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.94f, 0.62f, 0.24f, 1f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(.5f, .5f);
        cardRect.pivot = new Vector2(.5f, .5f);
        cardRect.sizeDelta = new Vector2(720f, 590f);
        cardRect.anchoredPosition = new Vector2(0f, 55f);

        VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(42, 42, 34, 34);
        layout.spacing = 12;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText(card.transform, "InvoiceTitle", "HÓA ĐƠN THANH TOÁN", 34, TextAlignmentOptions.Center,
            new Color(1f, .78f, .38f, 1f), 55);
        CreateText(card.transform, "InvoiceStore", "CỬA HÀNG CÔNG VIÊN  •  Cảm ơn bạn đã ghé thăm", 18,
            TextAlignmentOptions.Center, new Color(.83f, .78f, .7f, 1f), 30);
        CreateDivider(card.transform);

        productNameText = CreateRow(card.transform, "Sản phẩm", 24).value;
        quantityText = CreateRow(card.transform, "Số lượng", 24).value;
        unitPriceText = CreateRow(card.transform, "Đơn giá", 24).value;
        vatText = CreateRow(card.transform, "Thuế VAT", 24).value;
        CreateDivider(card.transform);
        totalText = CreateRow(card.transform, "TỔNG CỘNG", 28, true).value;

        GameObject buttons = CreateUiObject("InvoiceButtons", card.transform);
        LayoutElement buttonsLayout = buttons.AddComponent<LayoutElement>();
        buttonsLayout.preferredHeight = 62;
        HorizontalLayoutGroup buttonsGroup = buttons.AddComponent<HorizontalLayoutGroup>();
        buttonsGroup.spacing = 18;
        buttonsGroup.childControlWidth = true;
        buttonsGroup.childForceExpandWidth = true;

        payButton = CreateButton(buttons.transform, "Btn_Pay", "THANH TOÁN", new Color(.91f, .48f, .16f, 1f));
        cancelButton = CreateButton(buttons.transform, "Btn_Cancel", "HỦY", new Color(.29f, .25f, .28f, 1f));
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
        TextAlignmentOptions alignment, Color color, float height)
    {
        GameObject obj = CreateUiObject(name, parent);
        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color;
        label.enableWordWrapping = false;
        LayoutElement layout = obj.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        return label;
    }

    private static (TextMeshProUGUI label, TextMeshProUGUI value) CreateRow(Transform parent, string title,
        float size, bool emphasize = false)
    {
        GameObject row = CreateUiObject("Row_" + title, parent);
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 43;
        HorizontalLayoutGroup rowGroup = row.AddComponent<HorizontalLayoutGroup>();
        rowGroup.childAlignment = TextAnchor.MiddleCenter;
        rowGroup.spacing = 10;
        rowGroup.childControlWidth = true;
        rowGroup.childForceExpandWidth = false;

        Color titleColor = emphasize ? new Color(1f, .79f, .4f, 1f) : new Color(.92f, .88f, .8f, 1f);
        TextMeshProUGUI label = CreateText(row.transform, "Label", title, size, TextAlignmentOptions.MidlineLeft,
            titleColor, 43);
        label.fontStyle = emphasize ? FontStyles.Bold : FontStyles.Normal;
        LayoutElement labelLayout = label.GetComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1;

        TextMeshProUGUI value = CreateText(row.transform, "Value", "-", size, TextAlignmentOptions.MidlineRight,
            Color.white, 43);
        value.fontStyle = emphasize ? FontStyles.Bold : FontStyles.Normal;
        LayoutElement valueLayout = value.GetComponent<LayoutElement>();
        valueLayout.preferredWidth = 250;
        return (label, value);
    }

    private static void CreateDivider(Transform parent)
    {
        GameObject divider = CreateUiObject("Divider", parent);
        Image image = divider.AddComponent<Image>();
        image.color = new Color(1f, .7f, .28f, .38f);
        LayoutElement layout = divider.AddComponent<LayoutElement>();
        layout.preferredHeight = 2;
    }

    private static Button CreateButton(Transform parent, string name, string label, Color color)
    {
        GameObject obj = CreateUiObject(name, parent);
        Image image = obj.AddComponent<Image>();
        image.color = color;
        Button button = obj.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, .2f);
        colors.pressedColor = Color.Lerp(color, Color.black, .25f);
        button.colors = colors;

        TextMeshProUGUI text = CreateText(obj.transform, "Label", label, 24, TextAlignmentOptions.Center,
            Color.white, 58);
        text.fontStyle = FontStyles.Bold;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        return button;
    }
}
