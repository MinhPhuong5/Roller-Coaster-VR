using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Inventory UI riêng, hiện Bento đã thanh toán và sẵn sàng mở rộng cho vé/đồ ăn khác.</summary>
public class InventoryUIManager : MonoBehaviour
{
    private BentoPurchaseManager purchases;
    private TicketShopUIManager ticketShop;
    private GameObject panel;
    private Button bagButton;
    private Button allTab;
    private Button foodTab;
    private Button ticketTab;
    private Button bentoItemButton;
    private Button ticketItemButton;
    private Button useButton;
    private TextMeshProUGUI useButtonLabel;
    private GameObject ticketConfirmPanel;
    private TextMeshProUGUI bentoCountText;
    private TextMeshProUGUI ticketCountText;
    private TextMeshProUGUI detailTitle;
    private TextMeshProUGUI detailCount;
    private TextMeshProUGUI detailDescription;
    private TextMeshProUGUI statusText;
    private string selectedCategory = "TẤT CẢ";
    private string selectedItem = "BENTO";

    private readonly Color ink = new Color(.045f, .055f, .09f, 1f);
    private readonly Color panelColor = new Color(.09f, .075f, .12f, 1f);
    private readonly Color cardColor = new Color(.15f, .12f, .18f, 1f);
    private readonly Color gold = new Color(1f, .66f, .25f, 1f);
    private readonly Color cream = new Color(1f, .94f, .84f, 1f);

    private void Start()
    {
        purchases = GetComponent<BentoPurchaseManager>();
        ticketShop = Object.FindFirstObjectByType<TicketShopUIManager>();
        BuildUi();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I) && !IsPurchaseModalOpen()) Toggle();
        if (ticketConfirmPanel != null && ticketConfirmPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelUseTicket();
            return;
        }
        if (panel != null && panel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private bool IsPurchaseModalOpen() => purchases != null && purchases.IsInvoiceOpen;

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("Canvas_InventorySystem", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        bagButton = CreateBagButton(canvasObject.transform);
        RectTransform bagRect = bagButton.GetComponent<RectTransform>();
        bagRect.anchorMin = bagRect.anchorMax = new Vector2(1f, 0f);
        bagRect.pivot = new Vector2(1f, 0f);
        bagRect.anchoredPosition = new Vector2(-42f, 42f);
        bagRect.sizeDelta = new Vector2(178f, 68f);
        bagButton.onClick.AddListener(Toggle);

        panel = CreateObject("InventoryPanel", canvasObject.transform);
        Stretch(panel.GetComponent<RectTransform>());
        Image dimmer = panel.AddComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, .72f);

        GameObject window = CreateObject("InventoryWindow", panel.transform);
        Image windowImage = window.AddComponent<Image>();
        windowImage.color = panelColor;
        Outline outline = window.AddComponent<Outline>();
        outline.effectColor = gold;
        outline.effectDistance = new Vector2(2f, -2f);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(.5f, .5f);
        windowRect.sizeDelta = new Vector2(1260f, 710f);
        windowRect.anchoredPosition = new Vector2(0f, 35f);

        CreateText(window.transform, "Title", "TÚI ĐỒ", 38, TextAlignmentOptions.Left, gold,
            new Vector2(44f, -34f), new Vector2(520f, 52f), new Vector2(0f, 1f));

        Button close = CreateButton(window.transform, "Btn_CloseInventory", "×", new Color(.38f, .2f, .22f, 1f), 34);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-34f, -30f);
        closeRect.sizeDelta = new Vector2(56f, 56f);
        close.onClick.AddListener(Close);

        GameObject tabs = CreateObject("Tabs", window.transform);
        RectTransform tabsRect = tabs.GetComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0f, 0f);
        tabsRect.anchorMax = new Vector2(0f, 1f);
        tabsRect.pivot = new Vector2(0f, .5f);
        tabsRect.anchoredPosition = new Vector2(34f, -20f);
        tabsRect.sizeDelta = new Vector2(215f, -150f);
        VerticalLayoutGroup tabsLayout = tabs.AddComponent<VerticalLayoutGroup>();
        tabsLayout.padding = new RectOffset(0, 0, 18, 0);
        tabsLayout.spacing = 12;
        tabsLayout.childControlHeight = true;
        tabsLayout.childForceExpandHeight = false;

        allTab = CreateTab(tabs.transform, "TẤT CẢ", "TẤT CẢ");
        foodTab = CreateTab(tabs.transform, "ĐỒ ĂN", "ĐỒ ĂN");
        ticketTab = CreateTab(tabs.transform, "VÉ TRÒ CHƠI", "VÉ");

        GameObject grid = CreateObject("ItemGrid", window.transform);
        Image gridImage = grid.AddComponent<Image>();
        gridImage.color = ink;
        RectTransform gridRect = grid.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0f, 0f);
        gridRect.anchorMax = new Vector2(0f, 1f);
        gridRect.pivot = new Vector2(0f, .5f);
        gridRect.anchoredPosition = new Vector2(270f, -20f);
        gridRect.sizeDelta = new Vector2(470f, -150f);

        bentoItemButton = CreateButton(grid.transform, "Item_Bento", "BENTO", cardColor, 23);
        RectTransform itemRect = bentoItemButton.GetComponent<RectTransform>();
        itemRect.anchorMin = itemRect.anchorMax = new Vector2(0f, 1f);
        itemRect.pivot = new Vector2(0f, 1f);
        itemRect.anchoredPosition = new Vector2(24f, -24f);
        itemRect.sizeDelta = new Vector2(190f, 190f);
        bentoItemButton.onClick.AddListener(SelectBento);
        bentoCountText = CreateText(bentoItemButton.transform, "Count", "x0", 20, TextAlignmentOptions.BottomRight,
            cream, new Vector2(-14f, 13f), new Vector2(80f, 35f), new Vector2(1f, 0f));

        ticketItemButton = CreateButton(grid.transform, "Item_RollerCoasterTicket", "VÉ TÀU LƯỢN", cardColor, 18);
        RectTransform ticketRect = ticketItemButton.GetComponent<RectTransform>();
        ticketRect.anchorMin = ticketRect.anchorMax = new Vector2(0f, 1f);
        ticketRect.pivot = new Vector2(0f, 1f);
        ticketRect.anchoredPosition = new Vector2(238f, -24f);
        ticketRect.sizeDelta = new Vector2(190f, 190f);
        ticketItemButton.onClick.AddListener(SelectTicket);
        ticketCountText = CreateText(ticketItemButton.transform, "Count", "x0", 20, TextAlignmentOptions.BottomRight,
            cream, new Vector2(-14f, 13f), new Vector2(80f, 35f), new Vector2(1f, 0f));

        GameObject detail = CreateObject("ItemDetail", window.transform);
        Image detailImage = detail.AddComponent<Image>();
        detailImage.color = new Color(.11f, .09f, .14f, 1f);
        RectTransform detailRect = detail.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(1f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.pivot = new Vector2(1f, .5f);
        detailRect.anchoredPosition = new Vector2(-34f, -20f);
        detailRect.sizeDelta = new Vector2(430f, -150f);

        detailTitle = CreateText(detail.transform, "DetailTitle", "Bento Nhật Bản", 29, TextAlignmentOptions.Center,
            gold, new Vector2(0f, -88f), new Vector2(360f, 48f), new Vector2(.5f, 1f));
        detailCount = CreateText(detail.transform, "DetailCount", "Đang có: 0", 19, TextAlignmentOptions.Center,
            cream, new Vector2(0f, -136f), new Vector2(330f, 34f), new Vector2(.5f, 1f));
        detailDescription = CreateText(detail.transform, "DetailDescription",
            "Một hộp cơm Bento đầy đủ dinh dưỡng.\nChỉ có thể thưởng thức khi Chihiro đang ngồi trên ghế.", 18,
            TextAlignmentOptions.Center, new Color(.8f, .77f, .82f, 1f), new Vector2(0f, -208f),
            new Vector2(350f, 84f), new Vector2(.5f, 1f));
        useButton = CreateButton(detail.transform, "Btn_UseBento", "SỬ DỤNG", gold, 25);
        RectTransform useRect = useButton.GetComponent<RectTransform>();
        useRect.anchorMin = useRect.anchorMax = new Vector2(.5f, 0f);
        useRect.pivot = new Vector2(.5f, 0f);
        useRect.anchoredPosition = new Vector2(0f, 72f);
        useRect.sizeDelta = new Vector2(290f, 64f);
        useButtonLabel = useButton.GetComponentInChildren<TextMeshProUGUI>();
        useButton.onClick.AddListener(UseSelectedItem);
        statusText = CreateText(detail.transform, "Status", "", 16, TextAlignmentOptions.Center,
            new Color(.98f, .78f, .37f, 1f), new Vector2(0f, 30f), new Vector2(360f, 32f), new Vector2(.5f, 0f));

        panel.SetActive(false);
        BuildTicketConfirmUi(canvasObject.transform);
    }

    private Button CreateTab(Transform parent, string title, string category)
    {
        Button button = CreateButton(parent, "Tab_" + category, title, cardColor, 19);
        button.gameObject.AddComponent<LayoutElement>().preferredHeight = 58;
        button.onClick.AddListener(() => SetCategory(category));
        return button;
    }

    private void Toggle()
    {
        if (panel.activeSelf) Close(); else Open();
    }

    private void Open()
    {
        if (IsPurchaseModalOpen()) return;
        Refresh();
        panel.SetActive(true);
        SetPlayerLocked(true);
    }

    private void Close()
    {
        panel.SetActive(false);
        SetPlayerLocked(false);
    }

    private void SetCategory(string category)
    {
        selectedCategory = category;
        if (category == "ĐỒ ĂN") selectedItem = "BENTO";
        if (category == "VÉ") selectedItem = "TICKET";
        statusText.text = string.Empty;
        Refresh();
    }

    private void SelectBento()
    {
        selectedItem = "BENTO";
        statusText.text = string.Empty;
        Refresh();
    }

    private void SelectTicket()
    {
        selectedItem = "TICKET";
        statusText.text = string.Empty;
        Refresh();
    }

    private void UseSelectedItem()
    {
        if (selectedItem == "TICKET") UseTicket();
        else UseBento();
    }

    private void UseBento()
    {
        if (purchases == null || purchases.OwnedBentoCount <= 0)
        {
            statusText.text = "Bạn chưa có Bento trong túi.";
            return;
        }
        if (!SeatInteraction.IsPlayerSitting)
        {
            statusText.text = "Hãy ngồi xuống ghế để dùng Bento.";
            return;
        }

        purchases.ConsumeBento();
        statusText.text = string.Empty;
        Close();
        purchases.PlayEatingVideo();
        Refresh();
    }

    private void UseTicket()
    {
        if (ticketShop == null) ticketShop = Object.FindFirstObjectByType<TicketShopUIManager>();
        RollerCoasterInteraction coaster = Object.FindFirstObjectByType<RollerCoasterInteraction>();
        if (ticketShop == null || coaster == null || ticketShop.PurchasedTicketCount <= 0)
        {
            statusText.text = "Không thể dùng vé lúc này.";
            Refresh();
            return;
        }

        panel.SetActive(false);
        ticketConfirmPanel.SetActive(true);
    }

    private void ConfirmUseTicket()
    {
        if (ticketShop == null) ticketShop = Object.FindFirstObjectByType<TicketShopUIManager>();
        RollerCoasterInteraction coaster = Object.FindFirstObjectByType<RollerCoasterInteraction>();
        if (ticketShop == null || coaster == null || !ticketShop.ConsumeTicket())
        {
            CancelUseTicket();
            return;
        }

        ticketConfirmPanel.SetActive(false);
        SetPlayerLocked(false);
        coaster.OnConfirmStartGame();
    }

    private void CancelUseTicket()
    {
        ticketConfirmPanel.SetActive(false);
        panel.SetActive(true);
        Refresh();
    }

    private void Refresh()
    {
        int count = purchases != null ? purchases.OwnedBentoCount : 0;
        if (ticketShop == null) ticketShop = Object.FindFirstObjectByType<TicketShopUIManager>();
        int ticketCount = ticketShop != null ? ticketShop.PurchasedTicketCount : 0;
        bool showFood = selectedCategory == "TẤT CẢ" || selectedCategory == "ĐỒ ĂN";
        bool showTickets = selectedCategory == "TẤT CẢ" || selectedCategory == "VÉ";
        if (selectedCategory == "TẤT CẢ" && selectedItem == "BENTO" && count == 0 && ticketCount > 0)
            selectedItem = "TICKET";
        if (selectedCategory == "TẤT CẢ" && selectedItem == "TICKET" && ticketCount == 0 && count > 0)
            selectedItem = "BENTO";
        bool hasBento = showFood && count > 0;
        bool hasTickets = showTickets && ticketCount > 0;
        bentoItemButton.gameObject.SetActive(hasBento);
        ticketItemButton.gameObject.SetActive(hasTickets);
        RectTransform ticketRect = ticketItemButton.GetComponent<RectTransform>();
        ticketRect.anchoredPosition = new Vector2(hasBento ? 238f : 24f, -24f);
        bentoCountText.text = $"x{count}";
        ticketCountText.text = $"x{ticketCount}";

        if (selectedItem == "TICKET" && ticketCount > 0)
        {
            detailTitle.text = "Vé Tàu Lượn Siêu Tốc";
            detailCount.text = $"Đang có: {ticketCount}";
            detailDescription.text = "Dùng vé để vào trò chơi Tàu Lượn Siêu Tốc.";
            useButton.gameObject.SetActive(true);
            if (useButtonLabel != null) useButtonLabel.text = "DÙNG VÉ";
        }
        else if (selectedItem == "BENTO" && count > 0)
        {
            detailTitle.text = "Bento Nhật Bản";
            detailCount.text = $"Đang có: {count}";
            detailDescription.text = "Một hộp cơm Bento đầy đủ dinh dưỡng.\nChỉ có thể thưởng thức khi Chihiro đang ngồi trên ghế.";
            useButton.gameObject.SetActive(true);
            if (useButtonLabel != null) useButtonLabel.text = "SỬ DỤNG";
        }
        else
        {
            detailTitle.text = "Túi đồ trống";
            detailCount.text = "Đang có: 0";
            detailDescription.text = selectedCategory == "VÉ"
                ? "Hãy đến quầy vé để mua vé Tàu Lượn Siêu Tốc."
                : "Hãy ghé cửa hàng, lấy Bento và thanh toán tại quầy.";
            useButton.gameObject.SetActive(false);
            statusText.text = string.Empty;
        }
        SetTabColor(allTab, selectedCategory == "TẤT CẢ");
        SetTabColor(foodTab, selectedCategory == "ĐỒ ĂN");
        SetTabColor(ticketTab, selectedCategory == "VÉ");
    }

    private void SetTabColor(Button button, bool selected)
    {
        if (button != null) button.GetComponent<Image>().color = selected ? gold : cardColor;
    }

    private void SetPlayerLocked(bool openingInventory)
    {
        AnimatorChihiro player = Object.FindFirstObjectByType<AnimatorChihiro>();
        if (player != null) player.SetInputLocked(openingInventory || SeatInteraction.IsPlayerSitting);
        Cursor.lockState = openingInventory ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = openingInventory;
    }

    private GameObject CreateObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string value, float fontSize,
        TextAlignmentOptions alignment, Color color, Vector2 position, Vector2 size, Vector2 anchor)
    {
        GameObject obj = CreateObject(name, parent);
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = true;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return text;
    }

    private Button CreateButton(Transform parent, string name, string label, Color color, float fontSize)
    {
        GameObject obj = CreateObject(name, parent);
        Image image = obj.AddComponent<Image>();
        image.color = color;
        Button button = obj.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, .9f);
        colors.pressedColor = new Color(.7f, .7f, .7f, 1f);
        button.colors = colors;

        TextMeshProUGUI text = CreateText(obj.transform, "Label", label, fontSize, TextAlignmentOptions.Center,
            Color.white, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.pivot = new Vector2(.5f, .5f);
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.fontStyle = FontStyles.Bold;
        return button;
    }

    private void BuildTicketConfirmUi(Transform canvas)
    {
        ticketConfirmPanel = CreateObject("Panel_ConfirmUseTicket", canvas);
        Stretch(ticketConfirmPanel.GetComponent<RectTransform>());
        Image dimmer = ticketConfirmPanel.AddComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, .76f);

        GameObject card = CreateObject("ConfirmTicketCard", ticketConfirmPanel.transform);
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = panelColor;
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = gold;
        outline.effectDistance = new Vector2(2f, -2f);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(.5f, .5f);
        cardRect.pivot = new Vector2(.5f, .5f);
        cardRect.sizeDelta = new Vector2(610f, 340f);
        cardRect.anchoredPosition = new Vector2(0f, 50f);

        CreateText(card.transform, "Title", "XÁC NHẬN SỬ DỤNG VÉ", 29, TextAlignmentOptions.Center, gold,
            new Vector2(0f, -54f), new Vector2(520f, 48f), new Vector2(.5f, 1f));
        CreateText(card.transform, "Message", "Bạn sẽ sử dụng 1 Vé Tàu Lượn Siêu Tốc\nđể bắt đầu chuyến đi.", 21,
            TextAlignmentOptions.Center, cream, new Vector2(0f, -140f), new Vector2(500f, 80f), new Vector2(.5f, 1f));
        CreateText(card.transform, "Note", "Vé sẽ được trừ sau khi bạn xác nhận.", 16, TextAlignmentOptions.Center,
            new Color(.75f, .72f, .8f, 1f), new Vector2(0f, -205f), new Vector2(460f, 34f), new Vector2(.5f, 1f));

        Button cancel = CreateButton(card.transform, "Btn_CancelUseTicket", "HỦY", new Color(.32f, .25f, .3f, 1f), 22);
        RectTransform cancelRect = cancel.GetComponent<RectTransform>();
        cancelRect.anchorMin = new Vector2(.5f, 0f);
        cancelRect.anchorMax = new Vector2(.5f, 0f);
        cancelRect.pivot = new Vector2(1f, 0f);
        cancelRect.anchoredPosition = new Vector2(-12f, 36f);
        cancelRect.sizeDelta = new Vector2(205f, 58f);
        cancel.onClick.AddListener(CancelUseTicket);

        Button confirm = CreateButton(card.transform, "Btn_ConfirmUseTicket", "BẮT ĐẦU", gold, 22);
        RectTransform confirmRect = confirm.GetComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(.5f, 0f);
        confirmRect.anchorMax = new Vector2(.5f, 0f);
        confirmRect.pivot = new Vector2(0f, 0f);
        confirmRect.anchoredPosition = new Vector2(12f, 36f);
        confirmRect.sizeDelta = new Vector2(205f, 58f);
        confirm.onClick.AddListener(ConfirmUseTicket);

        ticketConfirmPanel.SetActive(false);
    }

    private Button CreateBagButton(Transform parent)
    {
        GameObject obj = CreateObject("Btn_Inventory", parent);
        Image background = obj.AddComponent<Image>();
        background.color = new Color(.12f, .095f, .15f, 1f);
        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, .66f, .25f, .7f);
        outline.effectDistance = new Vector2(1f, -1f);
        Button button = obj.AddComponent<Button>();

        CreateBagPart(obj.transform, "BagBody", new Vector2(-52f, -4f), new Vector2(30f, 26f), gold);
        CreateBagPart(obj.transform, "BagHandleTop", new Vector2(-52f, 14f), new Vector2(18f, 4f), gold);
        CreateBagPart(obj.transform, "BagHandleLeft", new Vector2(-61f, 9f), new Vector2(4f, 12f), gold);
        CreateBagPart(obj.transform, "BagHandleRight", new Vector2(-43f, 9f), new Vector2(4f, 12f), gold);
        CreateText(obj.transform, "Label", "Túi đồ", 21, TextAlignmentOptions.MidlineLeft, cream,
            new Vector2(14f, 0f), new Vector2(104f, 44f), new Vector2(.5f, .5f));
        return button;
    }

    private static void CreateBagPart(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
