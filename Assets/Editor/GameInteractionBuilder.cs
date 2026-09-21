#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public static class GameInteractionBuilder
{
    [MenuItem("Tools/Game Interaction/1-Click Setup RollerCoaster Interaction (Method 2)")]
    public static void BuildRollerCoasterInteractionManual()
    {
        BuildRollerCoasterInteraction(silent: false);
    }

    public static void BuildRollerCoasterInteraction(bool silent = false)
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
        Undo.SetCurrentGroupName("Setup RollerCoaster Interaction Method 2");

        // 1. Kiểm tra / Đảm bảo Tag 'Player' cho nhân vật chính (Chihiro / AOTCharacter)
        EnsurePlayerConfig();

        // 2. Tìm mô hình TauLuonSieuToc trong Hierarchy
        GameObject coasterObj = FindRollerCoasterObject();
        if (coasterObj == null)
        {
            Debug.LogError("[GameInteractionBuilder] Không tìm thấy mô hình TauLuonSieuToc trong scene!");
            return;
        }

        // 3. Tạo hoặc lấy object con 'InteractionZone'
        Transform zoneTransform = coasterObj.transform.Find("InteractionZone");
        GameObject zoneObj;
        if (zoneTransform == null)
        {
            zoneObj = new GameObject("InteractionZone");
            zoneObj.transform.SetParent(coasterObj.transform, false);
            zoneObj.transform.localPosition = Vector3.zero;
            zoneObj.transform.localRotation = Quaternion.identity;
            zoneObj.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(zoneObj, "Create InteractionZone");
        }
        else
        {
            zoneObj = zoneTransform.gameObject;
        }

        // Cấu hình BoxCollider (Is Trigger = true)
        BoxCollider boxCol = zoneObj.GetComponent<BoxCollider>();
        if (boxCol == null)
        {
            boxCol = zoneObj.AddComponent<BoxCollider>();
        }
        boxCol.isTrigger = true;
        // Kích thước collider đủ rộng quanh mô hình để nhân vật tiếp cận là nhận trigger
        boxCol.size = new Vector3(32f, 16f, 32f);
        boxCol.center = Vector3.zero;

        // Gắn script RollerCoasterInteraction
        RollerCoasterInteraction interactionScript = zoneObj.GetComponent<RollerCoasterInteraction>();
        if (interactionScript == null)
        {
            interactionScript = zoneObj.AddComponent<RollerCoasterInteraction>();
        }

        // 4. Đảm bảo EventSystem tồn tại
        UnityEngine.EventSystems.EventSystem eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
        }

        // 5. Tạo Canvas UI chuẩn theo CÁCH 2: 1 Canvas duy nhất, xếp lớp (Layering) theo thứ tự Hierarchy
        GameObject oldCanvas = GameObject.Find("Canvas_GameInteraction");
        if (oldCanvas != null)
        {
            Undo.DestroyObjectImmediate(oldCanvas);
        }

        GameObject canvasObj = new GameObject("Canvas_GameInteraction");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30; // Hiển thị ưu tiên trên màn hình

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas_GameInteraction");

        string whitePixelPath = "Assets/MenuAssets/UI/WhitePixel.png";
        Sprite whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(whitePixelPath);

        // =========================================================================
        // TẦNG 1 (NẰM TRÊN TRONG HIERARCHY): PROMPT THÔNG BÁO "NHẤN F ĐỂ CHƠI TRÒ CHƠI"
        // Thiết kế sáng tạo phong cách Genshin / Theme Park hiện đại
        // =========================================================================
        GameObject promptObj = CreateUIElement("Prompt_PlayGame", canvasObj.transform);
        RectTransform promptRt = promptObj.GetComponent<RectTransform>();
        SetAnchors(promptRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(480f, 88f));

        Image promptBg = promptObj.AddComponent<Image>();
        if (whiteSprite != null) promptBg.sprite = whiteSprite;
        promptBg.color = new Color(0.04f, 0.08f, 0.16f, 0.92f); // Nền xanh đen sâu thẳm sang trọng

        // Viền vàng kim neon phát sáng
        Outline promptOutline = promptObj.AddComponent<Outline>();
        promptOutline.effectColor = new Color(1f, 0.78f, 0.25f, 0.95f);
        promptOutline.effectDistance = new Vector2(3, -3);

        // Nút phím [F] 3D nổi bật
        GameObject keyBox = CreateUIElement("KeyBox_F", promptObj.transform);
        RectTransform keyBoxRt = keyBox.GetComponent<RectTransform>();
        SetAnchors(keyBoxRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(60f, 60f));
        Image keyBoxImg = keyBox.AddComponent<Image>();
        if (whiteSprite != null) keyBoxImg.sprite = whiteSprite;
        keyBoxImg.color = Color.white;

        Outline keyOutline = keyBox.AddComponent<Outline>();
        keyOutline.effectColor = new Color(0.2f, 0.2f, 0.25f, 0.8f);
        keyOutline.effectDistance = new Vector2(2, -2);

        GameObject fTextObj = CreateUIElement("Text_F", keyBox.transform);
        StretchFull(fTextObj.GetComponent<RectTransform>());
        TextMeshProUGUI fTmp = AddCrispTMP(fTextObj);
        fTmp.text = "F";
        fTmp.fontSize = 38;
        fTmp.fontStyle = FontStyles.Bold;
        fTmp.alignment = TextAlignmentOptions.Center;
        fTmp.color = new Color(0.10f, 0.14f, 0.24f, 1f);

        // Cụm Text nhắc nhở: Dòng chính + Dòng phụ
        GameObject textContainer = CreateUIElement("TextContainer", promptObj.transform);
        RectTransform tcRt = textContainer.GetComponent<RectTransform>();
        SetAnchors(tcRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(265f, 0f), new Vector2(360f, 65f));

        TextMeshProUGUI promptTmp = AddCrispTMP(textContainer);
        promptTmp.text = "<size=24><b>NHẤN F ĐỂ CHƠI TRÒ CHƠI</b></size>\n<size=15><color=#FFD266>★ TÀU LƯỢN SIÊU TỐC • ROLLER COASTER ★</color></size>";
        promptTmp.lineSpacing = 10;
        promptTmp.alignment = TextAlignmentOptions.MidlineLeft;
        promptTmp.color = new Color(1f, 0.98f, 0.94f, 1f);

        promptObj.SetActive(false); // Mặc định ban đầu ẩn đi

        // =========================================================================
        // TẦNG 2 (NẰM DƯỚI CÙNG TRONG HIERARCHY): HỘP THOẠI XÁC NHẬN (POPUP MODAL)
        // Vì nằm bên dưới Prompt trong Hierarchy, Modal sẽ LUÔN VẼ ĐÈ LÊN TRÊN HẾT!
        // =========================================================================
        GameObject modalObj = CreateUIElement("Panel_ConfirmationModal", canvasObj.transform);
        StretchFull(modalObj.GetComponent<RectTransform>());
        Image modalBackdrop = modalObj.AddComponent<Image>();
        if (whiteSprite != null) modalBackdrop.sprite = whiteSprite;
        modalBackdrop.color = new Color(0.02f, 0.04f, 0.08f, 0.80f); // Phủ mờ toàn màn hình

        // Thẻ trung tâm (Modal Card)
        GameObject modalCard = CreateUIElement("ModalCard", modalObj.transform);
        RectTransform mcRt = modalCard.GetComponent<RectTransform>();
        SetAnchors(mcRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(740f, 460f));
        Image mcImg = modalCard.AddComponent<Image>();
        if (whiteSprite != null) mcImg.sprite = whiteSprite;
        mcImg.color = new Color(0.11f, 0.15f, 0.25f, 0.98f); // Xanh navy hoàng gia đậm chất giải trí

        // Viền bóng phát sáng
        Outline mcOutline = modalCard.AddComponent<Outline>();
        mcOutline.effectColor = new Color(0.25f, 0.75f, 1f, 0.90f); // Viền Cyan công nghệ tươi mát
        mcOutline.effectDistance = new Vector2(6, -6);

        // Header Banner ruy băng
        GameObject bannerObj = CreateUIElement("HeaderBanner", modalCard.transform);
        RectTransform bRt = bannerObj.GetComponent<RectTransform>();
        SetAnchors(bRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(660f, 62f));
        Image bImg = bannerObj.AddComponent<Image>();
        if (whiteSprite != null) bImg.sprite = whiteSprite;
        bImg.color = new Color(1f, 0.60f, 0.15f, 1f); // Cam năng động

        Outline bOutline = bannerObj.AddComponent<Outline>();
        bOutline.effectColor = new Color(0.8f, 0.35f, 0.05f, 1f);
        bOutline.effectDistance = new Vector2(3, -3);

        // Tiêu đề
        GameObject titleObj = CreateUIElement("Title", bannerObj.transform);
        StretchFull(titleObj.GetComponent<RectTransform>());
        TextMeshProUGUI titleTmp = AddCrispTMP(titleObj);
        titleTmp.text = "🎡 CÔNG VIÊN GIẢI TRÍ • TÀU LƯỢN SIÊU TỐC 🎡";
        titleTmp.fontSize = 24;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = Color.white;

        // Nội dung chi tiết
        GameObject msgObj = CreateUIElement("MessageContent", modalCard.transform);
        RectTransform msgRt = msgObj.GetComponent<RectTransform>();
        SetAnchors(msgRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 25f), new Vector2(640f, 160f));
        TextMeshProUGUI msgTmp = AddCrispTMP(msgObj);
        msgTmp.text = "<size=24><color=#FFDD55><b>Bạn có muốn bắt đầu chuyến phiêu lưu mạo hiểm?</b></color></size>\n\n" +
                      "Trải nghiệm những khúc cua nghẹt thở và tốc độ xé gió trên đường ray huyền thoại!\n" +
                      "<size=18><color=#88DDFF>Hãy chuẩn bị tinh thần và thắt chặt dây an toàn!</color></size>";
        msgTmp.lineSpacing = 16;
        msgTmp.alignment = TextAlignmentOptions.Center;
        msgTmp.color = new Color(0.92f, 0.96f, 1f, 0.95f);

        // Nút "Có / Bắt đầu" (Xanh ngọc lục bảo sắc nét)
        GameObject btnStartObj = CreateButton("Btn_ConfirmStart", modalCard.transform,
            new Vector2(0.5f, 0f), new Vector2(-155f, 75f), new Vector2(250f, 62f),
            new Color(0.12f, 0.75f, 0.38f, 1f), whiteSprite);
        SetButtonText(btnStartObj, "Có / Bắt đầu  ➔", 22, Color.white);
        Outline bsOutline = btnStartObj.AddComponent<Outline>();
        bsOutline.effectColor = new Color(0.08f, 0.45f, 0.22f, 1f);
        bsOutline.effectDistance = new Vector2(3, -3);

        // Nút "Không / Hủy" (Đỏ san hô quý phái)
        GameObject btnCancelObj = CreateButton("Btn_CancelClose", modalCard.transform,
            new Vector2(0.5f, 0f), new Vector2(155f, 75f), new Vector2(250f, 62f),
            new Color(0.85f, 0.28f, 0.28f, 1f), whiteSprite);
        SetButtonText(btnCancelObj, "Không / Hủy  ✕", 22, Color.white);
        Outline bcOutline = btnCancelObj.AddComponent<Outline>();
        bcOutline.effectColor = new Color(0.55f, 0.15f, 0.15f, 1f);
        bcOutline.effectDistance = new Vector2(3, -3);

        // Phím tắt gợi ý phía dưới nút bấm
        GameObject shortcutHint = CreateUIElement("ShortcutHint", modalCard.transform);
        RectTransform shRt = shortcutHint.GetComponent<RectTransform>();
        SetAnchors(shRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(500f, 28f));
        TextMeshProUGUI shTmp = AddCrispTMP(shortcutHint);
        shTmp.text = "(Mẹo: Nhấn Enter để Bắt đầu  •  Nhấn Esc để Hủy)";
        shTmp.fontSize = 16;
        shTmp.fontStyle = FontStyles.Italic;
        shTmp.alignment = TextAlignmentOptions.Center;
        shTmp.color = new Color(0.6f, 0.68f, 0.8f, 0.9f);

        modalObj.SetActive(false); // Mặc định ban đầu ẩn đi

        // 6. Gán các tham chiếu vào script RollerCoasterInteraction
        interactionScript.promptUI = promptObj;
        interactionScript.promptText = promptTmp;
        interactionScript.confirmationModalPanel = modalObj;
        interactionScript.confirmStartButton = btnStartObj.GetComponent<Button>();
        interactionScript.cancelCloseButton = btnCancelObj.GetComponent<Button>();
        interactionScript.modalTitleText = titleTmp;
        interactionScript.modalMessageText = msgTmp;
        interactionScript.mainGameSceneName = "Game";
        interactionScript.mainGameSceneIndex = 2; // Scene 3 trong Build Settings

        // 7. Kiểm tra Build Settings
        EnsureScenesInBuildSettings();

        // Lưu scene
        EditorUtility.SetDirty(zoneObj);
        EditorUtility.SetDirty(canvasObj);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Undo.CollapseUndoOperations(groupIndex);

        Debug.Log("<color=#00FF66><b>[GameInteractionBuilder] Thiết lập thành công hệ thống tương tác Tàu Lượn theo Cách 2 (1 Canvas, Layering Hierarchy)!</b></color>");

        if (!silent)
        {
            EditorUtility.DisplayDialog("Game Interaction Setup",
                "Đã hoàn thành thiết lập hệ thống tương tác Tàu Lượn Siêu Tốc (Cách 2):\n\n" +
                "1. Vùng InteractionZone (Is Trigger) gắn trên TauLuonSieuToc.\n" +
                "2. Chihiro (Tag Player) đã được kích hoạt nhận diện.\n" +
                "3. Canvas_GameInteraction gồm 2 tầng trong Hierarchy:\n" +
                "   - Tầng trên: Prompt [F] phong cách Genshin.\n" +
                "   - Tầng dưới: Modal Card Popup vẽ đè lên trên hết kèm phím tắt Enter/Esc.\n" +
                "4. Nút 'Có / Bắt đầu' sẽ chuyển sang scene 3 (Game).",
                "Tuyệt vời");
        }
    }

    private static GameObject FindRollerCoasterObject()
    {
        GameObject obj = GameObject.Find("TauLuonSieuToc");
        if (obj != null) return obj;

        GameObject parentObj = GameObject.Find("MoHinhTroChoi");
        if (parentObj != null)
        {
            foreach (Transform child in parentObj.transform)
            {
                if (child.name.ToLower().Contains("tauluon") || child.name.ToLower().Contains("coaster"))
                {
                    return child.gameObject;
                }
            }
            if (parentObj.transform.childCount > 0)
            {
                return parentObj.transform.GetChild(0).gameObject;
            }
            return parentObj;
        }

        return null;
    }

    private static void EnsurePlayerConfig()
    {
        // Ưu tiên tìm nhân vật chính Chihiro
        GameObject playerObj = GameObject.Find("Chihiro");
        if (playerObj == null)
        {
            playerObj = GameObject.FindGameObjectWithTag("Player");
        }

        if (playerObj != null)
        {
            if (!playerObj.CompareTag("Player"))
            {
                playerObj.tag = "Player";
                EditorUtility.SetDirty(playerObj);
                Debug.Log($"[GameInteractionBuilder] Đã gán Tag 'Player' cho: {playerObj.name}");
            }
        }
    }

    private static void EnsureScenesInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        string gameScenePath = "Assets/Scenes/Game.unity";

        bool found = false;
        foreach (var scene in scenes)
        {
            if (scene.path == gameScenePath)
            {
                found = true;
                scene.enabled = true;
                break;
            }
        }

        if (!found)
        {
            scenes.Add(new EditorBuildSettingsScene(gameScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[GameInteractionBuilder] Đã bổ sung Assets/Scenes/Game.unity vào Build Settings!");
        }
    }

    private static TextMeshProUGUI AddCrispTMP(GameObject go)
    {
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.extraPadding = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static GameObject CreateButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Sprite sprite)
    {
        GameObject btnObj = CreateUIElement(name, parent);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        SetAnchors(rt, anchor, anchor, pos, size);
        Image img = btnObj.AddComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        img.color = color;
        btnObj.AddComponent<Button>();
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
