#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class TicketSystemBuilder
{
    [MenuItem("Tools/Ticket System/1-Click Setup Ticket UI")]
    public static void BuildTicketSystemUIManual()
    {
        BuildTicketSystemUI(silent: false);
    }

    public static void BuildTicketSystemUI(bool silent = false)
    {
        string scenePath = "Assets/Scenes/ParkScene.unity";
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != scenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
            }
            else
            {
                return;
            }
        }

        Undo.IncrementCurrentGroup();
        int groupIndex = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Ticket System UI");

        // 1. Tìm hoặc tạo EventSystem
        UnityEngine.EventSystems.EventSystem eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
        }

        // 2. Xóa Canvas cũ nếu đã tạo
        GameObject oldCanvas = GameObject.Find("Canvas_TicketSystem");
        if (oldCanvas != null) Undo.DestroyObjectImmediate(oldCanvas);

        // 3. Tạo Canvas UI chính
        GameObject canvasObj = new GameObject("Canvas_TicketSystem");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas_TicketSystem");

        // Gắn TicketShopUIManager vào Canvas
        TicketShopUIManager uiMgr = canvasObj.AddComponent<TicketShopUIManager>();

        // Sprite 1x1 trắng
        string whitePixelPath = "Assets/MenuAssets/UI/WhitePixel.png";
        Sprite whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(whitePixelPath);

        // ==========================================
        // A. PROMPT TƯƠNG TÁC PHÍM [F] PHONG CÁCH GENSHIN (NẰM Ở CHÍNH GIỮA BÊN PHẢI NHÂN VẬT)
        // ==========================================
        GameObject promptObj = CreateUIElement("Genshin_InteractionPrompt", canvasObj.transform);
        RectTransform promptRt = promptObj.GetComponent<RectTransform>();
        // Một prompt lớn, tương phản cao, đặt ngay dưới tâm màn hình.
        SetAnchors(promptRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -180f), new Vector2(390f, 82f));
        
        // Nền tối trong suốt viền bo mờ
        Image promptBg = promptObj.AddComponent<Image>();
        if (whiteSprite != null) promptBg.sprite = whiteSprite;
        promptBg.color = new Color(0.025f, 0.04f, 0.08f, 0.96f);

        // Viền vàng nhẹ phong cách Genshin
        Outline promptOutline = promptObj.AddComponent<Outline>();
        promptOutline.effectColor = new Color(1f, 0.7f, 0.2f, 0.95f);
        promptOutline.effectDistance = new Vector2(3, -3);

        // Ô phím [F] hình chữ nhật viền trắng sáng
        GameObject keyBox = CreateUIElement("KeyBox", promptObj.transform);
        RectTransform keyBoxRt = keyBox.GetComponent<RectTransform>();
        SetAnchors(keyBoxRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(58f, 58f));
        Image keyBoxImg = keyBox.AddComponent<Image>();
        if (whiteSprite != null) keyBoxImg.sprite = whiteSprite;
        keyBoxImg.color = Color.white;

        // Chữ F
        GameObject fText = CreateUIElement("Text_F", keyBox.transform);
        StretchFull(fText.GetComponent<RectTransform>());
        TextMeshProUGUI fTmp = AddCrispTMP(fText);
        fTmp.text = "F";
        fTmp.fontSize = 36;
        fTmp.fontStyle = FontStyles.Bold;
        fTmp.alignment = TextAlignmentOptions.Center;
        fTmp.color = new Color(0.12f, 0.14f, 0.2f, 1f);

        // Ghi chú bên cạnh: Icon tay + Chữ Mua Vé
        GameObject labelObj = CreateUIElement("Prompt_Label", promptObj.transform);
        RectTransform labelRt = labelObj.GetComponent<RectTransform>();
        SetAnchors(labelRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(225f, 0f), new Vector2(285f, 58f));
        TextMeshProUGUI labelTmp = AddCrispTMP(labelObj);
        labelTmp.text = "MUA VÉ";
        labelTmp.fontSize = 30;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        labelTmp.color = new Color(1f, 0.96f, 0.88f, 1f);

        promptObj.SetActive(false); // Mặc định ẩn, khi lại gần mới hiện

        // ==========================================
        // B. CẢNH MỞ ĐẦU HỘI THOẠI HAKU (VISUAL NOVEL STYLE)
        // ==========================================
        string hakuImgPath = "Assets/Video/Image/Haku_Dialogue_Clean.png";
        TextureImporter ti = AssetImporter.GetAtPath(hakuImgPath) as TextureImporter;
        if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }
        Sprite hakuSprite = AssetDatabase.LoadAssetAtPath<Sprite>(hakuImgPath);

        GameObject welcomeObj = CreateUIElement("Panel_Welcome", canvasObj.transform);
        StretchFull(welcomeObj.GetComponent<RectTransform>());
        
        // Nền tối mờ toàn màn hình
        Image welcomeBackdrop = welcomeObj.AddComponent<Image>();
        if (whiteSprite != null) welcomeBackdrop.sprite = whiteSprite;
        welcomeBackdrop.color = new Color(0.01f, 0.01f, 0.02f, 0.96f);

        // Nút bấm vô hình bao trùm toàn màn hình để người chơi click bất kỳ đâu cũng next câu
        Button screenClickBtn = welcomeObj.AddComponent<Button>();
        screenClickBtn.transition = Selectable.Transition.None;

        // Container giữ đúng tỷ lệ hình nền Haku (1024x554 hoặc Fullscreen)
        GameObject cutsceneContainer = CreateUIElement("Cutscene_Haku", welcomeObj.transform);
        StretchFull(cutsceneContainer.GetComponent<RectTransform>());
        Image hakuImg = cutsceneContainer.AddComponent<Image>();
        if (hakuSprite != null) hakuImg.sprite = hakuSprite;
        hakuImg.preserveAspect = true;

        // Khung hiển thị nội dung câu thoại (TextMeshPro) nằm đúng vị trí khung chat
        GameObject dialogueTextObj = CreateUIElement("Text_DialogueContent", cutsceneContainer.transform);
        RectTransform dtRt = dialogueTextObj.GetComponent<RectTransform>();
        // Căn đúng vào vùng lòng khung thoại: x từ 17% đến 84%, y từ 10% đến 25%
        dtRt.anchorMin = new Vector2(0.17f, 0.095f);
        dtRt.anchorMax = new Vector2(0.84f, 0.245f);
        dtRt.offsetMin = Vector2.zero;
        dtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI dialogueTmp = AddCrispTMP(dialogueTextObj);
        dialogueTmp.fontSize = 27;
        dialogueTmp.lineSpacing = 14;
        dialogueTmp.alignment = TextAlignmentOptions.TopLeft;
        dialogueTmp.color = new Color(0.96f, 0.91f, 0.82f, 1f); // Màu kem ngà phong cách visual novel
        dialogueTmp.enableWordWrapping = true;
        dialogueTmp.text = "";

        Shadow dtShadow = dialogueTextObj.AddComponent<Shadow>();
        dtShadow.effectColor = new Color(0.05f, 0.02f, 0.02f, 0.85f);
        dtShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Nút gợi ý TIẾP TỤC ▽ ở góc dưới bên phải
        GameObject continuePromptObj = CreateUIElement("Prompt_TiepTuc", cutsceneContainer.transform);
        RectTransform cpRt = continuePromptObj.GetComponent<RectTransform>();
        cpRt.anchorMin = new Vector2(0.76f, 0.02f);
        cpRt.anchorMax = new Vector2(0.93f, 0.08f);
        cpRt.offsetMin = Vector2.zero;
        cpRt.offsetMax = Vector2.zero;

        TextMeshProUGUI cpTmp = AddCrispTMP(continuePromptObj);
        cpTmp.text = "TIẾP TỤC  ▼";
        cpTmp.fontSize = 20;
        cpTmp.fontStyle = FontStyles.Bold;
        cpTmp.alignment = TextAlignmentOptions.MidlineRight;
        cpTmp.color = new Color(0.96f, 0.78f, 0.42f, 1f); // Màu vàng cam ấm áp

        Shadow cpShadow = continuePromptObj.AddComponent<Shadow>();
        cpShadow.effectColor = new Color(0.08f, 0.03f, 0.03f, 0.9f);
        cpShadow.effectDistance = new Vector2(2f, -2f);

        // Nguồn âm thanh click khi chuyển thoại
        AudioSource audioSource = welcomeObj.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        AudioClip clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Video/Sound/YTSave_YouTube_Mouse-Click-Sound-Effect_Media_i0DON3AjhW4_009_128k.mp3");

        // Gắn controller quản lý hội thoại
        IntroDialogueController dialogueCtrl = welcomeObj.AddComponent<IntroDialogueController>();
        dialogueCtrl.dialoguePanel = welcomeObj;
        dialogueCtrl.backgroundImage = hakuImg;
        dialogueCtrl.dialogueText = dialogueTmp;
        dialogueCtrl.continuePrompt = continuePromptObj;
        dialogueCtrl.fullScreenClickButton = screenClickBtn;
        dialogueCtrl.typingSpeed = 0.032f;
        dialogueCtrl.audioSource = audioSource;
        dialogueCtrl.advanceSound = clickClip;
        dialogueCtrl.dialogueLines = new string[]
        {
            "Chào bạn! Tôi là Haku, nhân viên của công viên này. Rất vui được đón tiếp bạn đến với thế giới giải trí kỳ thú!",
            "Đầu tiên, bạn hãy tiến lại Quầy Bán Vé ngay phía trước để nhận vé tàu lượn siêu tốc nhé.",
            "Sau khi có vé, hãy đi theo biển chỉ dẫn đến khu vực đường ray để bắt đầu chuyến đi. Chúc bạn có những phút giây thật tuyệt vời!"
        };

        uiMgr.welcomePanel = welcomeObj;
        uiMgr.welcomeCloseButton = null;

        // ==========================================
        // C. GIAO DIỆN CHỌN TRÒ CHƠI (6 Ô PHONG CÁCH SÓC NHÍ)
        // ==========================================
        GameObject shopObj = CreateUIElement("Panel_TicketShop", canvasObj.transform);
        StretchFull(shopObj.GetComponent<RectTransform>());
        Image shopBackdrop = shopObj.AddComponent<Image>();
        if (whiteSprite != null) shopBackdrop.sprite = whiteSprite;
        shopBackdrop.color = new Color(0f, 0f, 0f, 0.7f);

        // Khung xanh bo tròn phong cách Sóc Nhí
        GameObject shopFrame = CreateUIElement("ShopFrame_SocNhiStyle", shopObj.transform);
        RectTransform sfRt = shopFrame.GetComponent<RectTransform>();
        SetAnchors(sfRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1050f, 720f));
        Image sfImg = shopFrame.AddComponent<Image>();
        if (whiteSprite != null) sfImg.sprite = whiteSprite;
        sfImg.color = new Color(0.25f, 0.65f, 0.95f, 1f);

        Outline sfOutline = shopFrame.AddComponent<Outline>();
        sfOutline.effectColor = new Color(0.15f, 0.45f, 0.75f, 1f);
        sfOutline.effectDistance = new Vector2(5, -5);

        // Nền trắng bên trong khung
        GameObject innerBox = CreateUIElement("InnerWhiteBox", shopFrame.transform);
        RectTransform ibRt = innerBox.GetComponent<RectTransform>();
        SetAnchors(ibRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(980f, 580f));
        Image ibImg = innerBox.AddComponent<Image>();
        if (whiteSprite != null) ibImg.sprite = whiteSprite;
        ibImg.color = new Color(0.96f, 0.98f, 1f, 1f);

        // Header Công Viên Game
        GameObject spTitle = CreateUIElement("ShopTitle", shopFrame.transform);
        RectTransform spTitleRt = spTitle.GetComponent<RectTransform>();
        SetAnchors(spTitleRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180f, -50f), new Vector2(450f, 50f));
        TextMeshProUGUI spTitleTmp = AddCrispTMP(spTitle);
        spTitleTmp.text = "🎮 CÔNG VIÊN TRÒ CHƠI - CHỌN VÉ";
        spTitleTmp.fontSize = 28;
        spTitleTmp.fontStyle = FontStyles.Bold;
        spTitleTmp.alignment = TextAlignmentOptions.MidlineLeft;
        spTitleTmp.color = Color.white;

        // Nút [X] Đóng ở góc trên bên phải
        GameObject closeShopBtnObj = CreateButton("Btn_CloseShop", shopFrame.transform, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(50f, 50f), new Color(0.95f, 0.3f, 0.3f, 1f));
        SetButtonText(closeShopBtnObj, "✕", 28, Color.white);
        uiMgr.shopCloseButton = closeShopBtnObj.GetComponent<Button>();
        uiMgr.shopPanel = shopObj;

        // Grid 6 ô trò chơi (2 hàng x 3 cột)
        GameObject gridObj = CreateUIElement("Grid_6Games", innerBox.transform);
        RectTransform gridRt = gridObj.GetComponent<RectTransform>();
        StretchFull(gridRt);
        gridRt.offsetMin = new Vector2(25f, 25f);
        gridRt.offsetMax = new Vector2(-25f, -25f);

        GridLayoutGroup gridLayout = gridObj.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(290f, 240f);
        gridLayout.spacing = new Vector2(28f, 30f);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.MiddleCenter;

        string[] gameNames = new string[] {
            "TÀU LƯỢN SIÊU TỐC\n(Roller Coaster)",
            "ĐU QUAY KHỔNG LỒ\n(Ferris Wheel)",
            "XE ĐIỆN ĐỤNG\n(Bumper Cars)",
            "NHÀ MA RÙNG RỢN\n(Haunted House)",
            "VÒNG XOAY NGỰA GỖ\n(Carousel)",
            "THÁP RƠI TỰ DO\n(Drop Tower)"
        };

        uiMgr.lockedGameButtons = new Button[5];
        int lockedIndex = 0;

        for (int i = 0; i < 6; i++)
        {
            bool isUnlocked = (i == 0);
            GameObject slotObj = CreateUIElement($"GameSlot_{i + 1}", gridObj.transform);
            
            Image slotBg = slotObj.AddComponent<Image>();
            if (whiteSprite != null) slotBg.sprite = whiteSprite;
            slotBg.color = isUnlocked ? new Color(1f, 0.96f, 0.88f, 1f) : new Color(0.88f, 0.9f, 0.93f, 0.95f);

            Button slotBtn = slotObj.AddComponent<Button>();

            Outline outline = slotObj.AddComponent<Outline>();
            outline.effectColor = isUnlocked ? new Color(1f, 0.65f, 0.1f, 1f) : new Color(0.6f, 0.65f, 0.7f, 0.5f);
            outline.effectDistance = new Vector2(4, -4);

            // Ảnh đại diện game
            GameObject previewImgObj = CreateUIElement("GameImage_Placeholder", slotObj.transform);
            RectTransform pRt = previewImgObj.GetComponent<RectTransform>();
            SetAnchors(pRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 25f), new Vector2(260f, 150f));
            Image pImg = previewImgObj.AddComponent<Image>();
            if (whiteSprite != null) pImg.sprite = whiteSprite;
            pImg.color = isUnlocked ? new Color(0.92f, 0.55f, 0.2f, 0.85f) : new Color(0.55f, 0.6f, 0.65f, 0.65f);

            // Tên trò chơi ở dưới
            GameObject nameObj = CreateUIElement("GameName", slotObj.transform);
            RectTransform nameRt = nameObj.GetComponent<RectTransform>();
            SetAnchors(nameRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 32f), new Vector2(270f, 50f));
            TextMeshProUGUI nameTmp = AddCrispTMP(nameObj);
            nameTmp.text = gameNames[i];
            nameTmp.fontSize = 17;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = isUnlocked ? new Color(0.85f, 0.35f, 0.05f, 1f) : new Color(0.4f, 0.45f, 0.5f, 0.85f);

            if (isUnlocked)
            {
                // Tag "HOT 🔥"
                GameObject badge = CreateUIElement("Badge_Hot", slotObj.transform);
                RectTransform bRt = badge.GetComponent<RectTransform>();
                SetAnchors(bRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-15f, -15f), new Vector2(110f, 32f));
                Image bImg = badge.AddComponent<Image>();
                if (whiteSprite != null) bImg.sprite = whiteSprite;
                bImg.color = new Color(0.95f, 0.2f, 0.2f, 1f);
                
                GameObject bTxt = CreateUIElement("BadgeText", badge.transform);
                StretchFull(bTxt.GetComponent<RectTransform>());
                TextMeshProUGUI bTmp = AddCrispTMP(bTxt);
                bTmp.text = "HOT 🔥";
                bTmp.fontSize = 15;
                bTmp.fontStyle = FontStyles.Bold;
                bTmp.alignment = TextAlignmentOptions.Center;
                bTmp.color = Color.white;

                uiMgr.rollerCoasterButton = slotBtn;
            }
            else
            {
                // Biểu tượng khóa chéo và ổ khóa chính giữa
                GameObject lockOverlay = CreateUIElement("LockOverlay", slotObj.transform);
                StretchFull(lockOverlay.GetComponent<RectTransform>());
                Image lockBg = lockOverlay.AddComponent<Image>();
                if (whiteSprite != null) lockBg.sprite = whiteSprite;
                lockBg.color = new Color(0.12f, 0.16f, 0.22f, 0.68f);

                GameObject lockIcon = CreateUIElement("LockIcon", lockOverlay.transform);
                StretchFull(lockIcon.GetComponent<RectTransform>());
                TextMeshProUGUI lockTmp = AddCrispTMP(lockIcon);
                lockTmp.text = "🔒\n<size=16>ĐANG BẢO TRÌ</size>";
                lockTmp.fontSize = 42;
                lockTmp.fontStyle = FontStyles.Bold;
                lockTmp.alignment = TextAlignmentOptions.Center;
                lockTmp.color = new Color(1f, 0.95f, 0.95f, 0.95f);

                uiMgr.lockedGameButtons[lockedIndex] = slotBtn;
                lockedIndex++;
            }
        }

        // ==========================================
        // D. POPUP CHỌN SỐ LƯỢNG VÉ (SLIDER 1 - 99)
        // ==========================================
        GameObject qtyObj = CreateUIElement("Panel_QuantitySelect", canvasObj.transform);
        StretchFull(qtyObj.GetComponent<RectTransform>());
        Image qtyBackdrop = qtyObj.AddComponent<Image>();
        if (whiteSprite != null) qtyBackdrop.sprite = whiteSprite;
        qtyBackdrop.color = new Color(0f, 0f, 0f, 0.75f);

        GameObject qtyCard = CreateUIElement("QtyCard", qtyObj.transform);
        RectTransform qcRt = qtyCard.GetComponent<RectTransform>();
        SetAnchors(qcRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 520f));
        Image qcImg = qtyCard.AddComponent<Image>();
        if (whiteSprite != null) qcImg.sprite = whiteSprite;
        qcImg.color = new Color(0.14f, 0.18f, 0.28f, 0.98f);

        Outline qcOutline = qtyCard.AddComponent<Outline>();
        qcOutline.effectColor = new Color(0.25f, 0.6f, 0.9f, 0.8f);
        qcOutline.effectDistance = new Vector2(4, -4);

        // Nút X đóng Quantity
        GameObject closeQtyBtn = CreateButton("Btn_CloseQty", qtyCard.transform, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(40f, 40f), new Color(0.85f, 0.3f, 0.3f, 1f));
        SetButtonText(closeQtyBtn, "✕", 24, Color.white);
        uiMgr.quantityCloseButton = closeQtyBtn.GetComponent<Button>();

        // Tiêu đề
        GameObject qcTitle = CreateUIElement("Title", qtyCard.transform);
        RectTransform qcTitleRt = qcTitle.GetComponent<RectTransform>();
        SetAnchors(qcTitleRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(600f, 50f));
        TextMeshProUGUI qcTitleTmp = AddCrispTMP(qcTitle);
        qcTitleTmp.text = "CHỌN SỐ LƯỢNG VÉ";
        qcTitleTmp.fontSize = 28;
        qcTitleTmp.fontStyle = FontStyles.Bold;
        qcTitleTmp.alignment = TextAlignmentOptions.Center;
        qcTitleTmp.color = new Color(1f, 0.88f, 0.45f, 1f);

        // Trò chơi: Tàu lượn siêu tốc
        GameObject gameSub = CreateUIElement("SubTitle", qtyCard.transform);
        RectTransform gsRt = gameSub.GetComponent<RectTransform>();
        SetAnchors(gsRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(600f, 40f));
        TextMeshProUGUI gsTmp = AddCrispTMP(gameSub);
        gsTmp.text = "Trò chơi: <b>Tàu Lượn Siêu Tốc (Roller Coaster)</b>\nĐơn giá: 50.000 VNĐ / vé";
        gsTmp.fontSize = 19;
        gsTmp.alignment = TextAlignmentOptions.Center;
        gsTmp.color = new Color(0.85f, 0.92f, 1f, 0.85f);

        // Hiển thị số lượng to ở giữa (kèm nút - và +)
        GameObject counterContainer = CreateUIElement("CounterContainer", qtyCard.transform);
        RectTransform ccRt = counterContainer.GetComponent<RectTransform>();
        SetAnchors(ccRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(380f, 80f));

        GameObject minusBtnObj = CreateButton("Btn_Minus", counterContainer.transform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(60f, 60f), new Color(0.25f, 0.35f, 0.5f, 1f));
        SetButtonText(minusBtnObj, "—", 28, Color.white);
        uiMgr.decreaseButton = minusBtnObj.GetComponent<Button>();

        GameObject countTextObj = CreateUIElement("Text_Count", counterContainer.transform);
        RectTransform ctRt = countTextObj.GetComponent<RectTransform>();
        SetAnchors(ctRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 70f));
        TextMeshProUGUI ctTmp = AddCrispTMP(countTextObj);
        ctTmp.text = "1";
        ctTmp.fontSize = 54;
        ctTmp.fontStyle = FontStyles.Bold;
        ctTmp.alignment = TextAlignmentOptions.Center;
        ctTmp.color = new Color(1f, 0.9f, 0.3f, 1f);
        uiMgr.ticketCountText = ctTmp;

        GameObject plusBtnObj = CreateButton("Btn_Plus", counterContainer.transform, new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(60f, 60f), new Color(0.25f, 0.35f, 0.5f, 1f));
        SetButtonText(plusBtnObj, "+", 32, Color.white);
        uiMgr.increaseButton = plusBtnObj.GetComponent<Button>();

        // Thanh trượt Slider 1 - 99
        GameObject sliderObj = CreateUIElement("Slider_Tickets", qtyCard.transform);
        RectTransform sRt = sliderObj.GetComponent<RectTransform>();
        SetAnchors(sRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(460f, 32f));
        Slider slider = sliderObj.AddComponent<Slider>();

        GameObject sBg = CreateUIElement("Background", sliderObj.transform);
        StretchFull(sBg.GetComponent<RectTransform>());
        Image sBgImg = sBg.AddComponent<Image>();
        if (whiteSprite != null) sBgImg.sprite = whiteSprite;
        sBgImg.color = new Color(0.08f, 0.12f, 0.18f, 1f);

        GameObject fillArea = CreateUIElement("Fill Area", sliderObj.transform);
        StretchFull(fillArea.GetComponent<RectTransform>());
        fillArea.GetComponent<RectTransform>().offsetMin = new Vector2(5f, 0f);
        fillArea.GetComponent<RectTransform>().offsetMax = new Vector2(-5f, 0f);

        GameObject sFill = CreateUIElement("Fill", fillArea.transform);
        StretchFull(sFill.GetComponent<RectTransform>());
        Image sFillImg = sFill.AddComponent<Image>();
        if (whiteSprite != null) sFillImg.sprite = whiteSprite;
        sFillImg.color = new Color(0.2f, 0.7f, 1f, 1f);
        slider.fillRect = sFill.GetComponent<RectTransform>();

        GameObject handleArea = CreateUIElement("Handle Slide Area", sliderObj.transform);
        StretchFull(handleArea.GetComponent<RectTransform>());
        handleArea.GetComponent<RectTransform>().offsetMin = new Vector2(10f, 0f);
        handleArea.GetComponent<RectTransform>().offsetMax = new Vector2(-10f, 0f);

        GameObject sHandle = CreateUIElement("Handle", handleArea.transform);
        RectTransform hRt = sHandle.GetComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(34f, 44f);
        Image hImg = sHandle.AddComponent<Image>();
        if (whiteSprite != null) hImg.sprite = whiteSprite;
        hImg.color = new Color(1f, 0.88f, 0.35f, 1f);
        slider.handleRect = hRt;
        slider.targetGraphic = hImg;
        uiMgr.ticketSlider = slider;

        GameObject minLabel = CreateUIElement("Min_1", qtyCard.transform);
        SetAnchors(minLabel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-255f, -40f), new Vector2(40f, 30f));
        TextMeshProUGUI minTmp = AddCrispTMP(minLabel);
        minTmp.text = "1";
        minTmp.fontSize = 18;
        minTmp.alignment = TextAlignmentOptions.Center;
        minTmp.color = Color.gray;

        GameObject maxLabel = CreateUIElement("Max_99", qtyCard.transform);
        SetAnchors(maxLabel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(255f, -40f), new Vector2(40f, 30f));
        TextMeshProUGUI maxTmp = AddCrispTMP(maxLabel);
        maxTmp.text = "99";
        maxTmp.fontSize = 18;
        maxTmp.alignment = TextAlignmentOptions.Center;
        maxTmp.color = Color.gray;

        // Tổng tiền
        GameObject totalObj = CreateUIElement("Text_TotalPrice", qtyCard.transform);
        RectTransform totalRt = totalObj.GetComponent<RectTransform>();
        SetAnchors(totalRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(500f, 40f));
        TextMeshProUGUI totalTmp = AddCrispTMP(totalObj);
        totalTmp.text = "Tổng tiền: 50.000 VNĐ";
        totalTmp.fontSize = 24;
        totalTmp.fontStyle = FontStyles.Bold;
        totalTmp.alignment = TextAlignmentOptions.Center;
        totalTmp.color = new Color(0.4f, 1f, 0.6f, 1f);
        uiMgr.totalPriceText = totalTmp;

        // Nút Xác Nhận Mua
        GameObject confirmBtnObj = CreateButton("Btn_ConfirmBuy", qtyCard.transform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(280f, 55f), new Color(0.18f, 0.72f, 0.35f, 1f));
        SetButtonText(confirmBtnObj, "XÁC NHẬN MUA VÉ", 22, Color.white);
        uiMgr.confirmBuyButton = confirmBtnObj.GetComponent<Button>();

        uiMgr.quantityPanel = qtyObj;

        // ==========================================
        // E. POPUP MUA VÉ THÀNH CÔNG
        // ==========================================
        GameObject successObj = CreateUIElement("Panel_SuccessNotice", canvasObj.transform);
        StretchFull(successObj.GetComponent<RectTransform>());
        Image successBackdrop = successObj.AddComponent<Image>();
        if (whiteSprite != null) successBackdrop.sprite = whiteSprite;
        successBackdrop.color = new Color(0f, 0f, 0f, 0.75f);

        GameObject sCard = CreateUIElement("SuccessCard", successObj.transform);
        RectTransform scRt = sCard.GetComponent<RectTransform>();
        SetAnchors(scRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 380f));
        Image scImg = sCard.AddComponent<Image>();
        if (whiteSprite != null) scImg.sprite = whiteSprite;
        scImg.color = new Color(0.12f, 0.18f, 0.26f, 0.98f);

        Outline scOutline = sCard.AddComponent<Outline>();
        scOutline.effectColor = new Color(0.2f, 0.8f, 0.4f, 0.8f);
        scOutline.effectDistance = new Vector2(4, -4);

        // Icon tích xanh thành công
        GameObject sIcon = CreateUIElement("CheckmarkIcon", sCard.transform);
        SetAnchors(sIcon.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(80f, 80f));
        TextMeshProUGUI sIconTmp = AddCrispTMP(sIcon);
        sIconTmp.text = "✓";
        sIconTmp.fontSize = 60;
        sIconTmp.fontStyle = FontStyles.Bold;
        sIconTmp.alignment = TextAlignmentOptions.Center;
        sIconTmp.color = new Color(0.25f, 0.9f, 0.45f, 1f);

        // Tiêu đề
        GameObject scTitle = CreateUIElement("Title", sCard.transform);
        SetAnchors(scTitle.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(500f, 40f));
        TextMeshProUGUI scTitleTmp = AddCrispTMP(scTitle);
        scTitleTmp.text = "MUA VÉ THÀNH CÔNG!";
        scTitleTmp.fontSize = 28;
        scTitleTmp.fontStyle = FontStyles.Bold;
        scTitleTmp.alignment = TextAlignmentOptions.Center;
        scTitleTmp.color = Color.white;

        // Nội dung chi tiết
        GameObject scMsg = CreateUIElement("Message", sCard.transform);
        SetAnchors(scMsg.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(540f, 100f));
        TextMeshProUGUI scMsgTmp = AddCrispTMP(scMsg);
        scMsgTmp.text = "Chúc mừng bạn đã mua vé thành công!\nChúc bạn có những trải nghiệm thật thú vị trên Tàu Lượn!";
        scMsgTmp.fontSize = 20;
        scMsgTmp.alignment = TextAlignmentOptions.Center;
        scMsgTmp.color = new Color(0.9f, 0.95f, 1f, 0.9f);
        uiMgr.successMessageText = scMsgTmp;

        // Nút OK
        GameObject okBtnObj = CreateButton("Btn_OkSuccess", sCard.transform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(220f, 50f), new Color(0.2f, 0.65f, 0.95f, 1f));
        SetButtonText(okBtnObj, "HOÀN TẤT", 22, Color.white);
        uiMgr.successOkButton = okBtnObj.GetComponent<Button>();

        // Nút X ở góc
        GameObject closeScBtn = CreateButton("Btn_CloseSc", sCard.transform, new Vector2(1f, 1f), new Vector2(-25f, -25f), new Vector2(36f, 36f), new Color(0.85f, 0.3f, 0.3f, 1f));
        SetButtonText(closeScBtn, "✕", 20, Color.white);
        uiMgr.successCloseButton = closeScBtn.GetComponent<Button>();

        uiMgr.successPanel = successObj;

        // ==========================================
        // F. TOAST THÔNG BÁO KHÓA GAME
        // ==========================================
        GameObject noticeObj = CreateUIElement("Panel_LockedToast", canvasObj.transform);
        SetAnchors(noticeObj.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 100f), new Vector2(540f, 70f));
        Image nImg = noticeObj.AddComponent<Image>();
        if (whiteSprite != null) nImg.sprite = whiteSprite;
        nImg.color = new Color(0.9f, 0.2f, 0.2f, 0.92f);

        GameObject nTxt = CreateUIElement("Text", noticeObj.transform);
        StretchFull(nTxt.GetComponent<RectTransform>());
        TextMeshProUGUI nTmp = AddCrispTMP(nTxt);
        nTmp.text = "Trò chơi này đang bảo trì hoặc chưa mở cửa!";
        nTmp.fontSize = 20;
        nTmp.fontStyle = FontStyles.Bold;
        nTmp.alignment = TextAlignmentOptions.Center;
        nTmp.color = Color.white;
        uiMgr.lockedNoticePanel = noticeObj;
        uiMgr.lockedNoticeText = nTmp;
        noticeObj.SetActive(false);

        // ==========================================
        // G. GẮN COMPONENT TƯƠNG TÁC VÀO QuayBanVe
        // ==========================================
        GameObject boothObj = GameObject.Find("QuayBanVe");
        if (boothObj != null)
        {
            TicketBoothInteractable booth = boothObj.GetComponent<TicketBoothInteractable>();
            if (booth == null) booth = boothObj.AddComponent<TicketBoothInteractable>();
            
            booth.interactionDistance = 4.0f;
            booth.interactionPromptUI = promptObj;

            BoxCollider boxCol = boothObj.GetComponent<BoxCollider>();
            if (boxCol == null)
            {
                boxCol = boothObj.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(3f, 4f, 3f);
            }
            boxCol.isTrigger = true;
            EditorUtility.SetDirty(boothObj);
        }

        // Let Unity save with the user's normal scene workflow.
        EditorUtility.SetDirty(canvasObj);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Undo.CollapseUndoOperations(groupIndex);

        Debug.Log("<color=#00FF66><b>[TicketSystemBuilder] Hoàn tất dựng hệ thống mua vé với font sắc nét, UI Sóc Nhí tươi mới, phím F nhạy!</b></color>");
        if (!silent)
        {
            EditorUtility.DisplayDialog("Ticket System Setup", "Đã cập nhật hệ thống mua vé:\n\n1. Prompt [F] lớn, tương phản cao và dễ đọc.\n2. Tương tác tính từ mép collider của quầy.\n3. Box Collider luôn bật Is Trigger.\n4. Esc hoặc nút X đóng giao diện và trả điều khiển cho nhân vật.", "Đã hiểu");
        }
    }

    private static TextMeshProUGUI AddCrispTMP(GameObject go)
    {
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.extraPadding = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static GameObject CreateButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
    {
        GameObject btnObj = CreateUIElement(name, parent);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        SetAnchors(rt, anchor, anchor, pos, size);
        Image img = btnObj.AddComponent<Image>();
        string whitePixelPath = "Assets/MenuAssets/UI/WhitePixel.png";
        Sprite whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(whitePixelPath);
        if (whiteSprite != null) img.sprite = whiteSprite;
        img.color = color;
        Button btn = btnObj.AddComponent<Button>();
        return btnObj;
    }

    private static void SetButtonText(GameObject btnObj, string text, float fontSize, Color color)
    {
        GameObject txtObj = CreateUIElement("Text", btnObj.transform);
        StretchFull(txtObj.GetComponent<RectTransform>());
        TextMeshProUGUI tmp = AddCrispTMP(txtObj);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
    }

    private static GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }
}
#endif
