#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class GameInteractionBuilder
{
    [MenuItem("Tools/Game Interaction/1-Click Setup RollerCoaster Kiosk 3D (VR Ready)")]
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
        Undo.SetCurrentGroupName("Setup RollerCoaster Kiosk 3D");

        EnsurePlayerConfig();

        GameObject coasterObj = FindRollerCoasterObject();
        if (coasterObj == null)
        {
            Debug.LogError("[GameInteractionBuilder] Không tìm thấy mô hình TauLuonSieuToc trong scene!");
            return;
        }

        // Tạo hoặc lấy InteractionZone
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

        BoxCollider boxCol = zoneObj.GetComponent<BoxCollider>();
        if (boxCol == null)
        {
            boxCol = zoneObj.AddComponent<BoxCollider>();
        }
        boxCol.isTrigger = true;
        boxCol.size = new Vector3(6f, 4f, 6f);
        boxCol.center = new Vector3(0f, 1f, 0f);

        RollerCoasterInteraction interactionScript = zoneObj.GetComponent<RollerCoasterInteraction>();
        if (interactionScript == null)
        {
            interactionScript = zoneObj.AddComponent<RollerCoasterInteraction>();
        }

        // Đảm bảo EventSystem
        UnityEngine.EventSystems.EventSystem eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
        }

        // Tạo Canvas Kiosk 3D World Space (Không dùng ScreenSpace Overlay dán màn hình nữa)
        GameObject oldCanvas = GameObject.Find("Canvas_GameInteraction");
        if (oldCanvas != null)
        {
            Undo.DestroyObjectImmediate(oldCanvas);
        }

        GameObject canvasObj = new GameObject("Canvas_GameInteraction");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30;

        RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(1920f, 1080f);
        canvasRt.localScale = new Vector3(0.0015f, 0.0015f, 0.0015f); // Tỉ lệ kích thước người thật

        canvasObj.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas_GameInteraction");

        string whitePixelPath = "Assets/MenuAssets/UI/WhitePixel.png";
        Sprite whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(whitePixelPath);

        // =========================================================================
        // BẢNG ĐIỀU KHIỂN KIOSK 3D (LUÔN BẬT SẴN Ở LỐI VÀO)
        // =========================================================================
        GameObject modalObj = CreateUIElement("Panel_ConfirmationModal", canvasObj.transform);
        StretchFull(modalObj.GetComponent<RectTransform>());

        GameObject modalCard = CreateUIElement("ModalCard", modalObj.transform);
        RectTransform mcRt = modalCard.GetComponent<RectTransform>();
        SetAnchors(mcRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 500f));
        Image mcImg = modalCard.AddComponent<Image>();
        if (whiteSprite != null) mcImg.sprite = whiteSprite;
        mcImg.color = new Color(0.10f, 0.14f, 0.24f, 0.98f);

        Outline mcOutline = modalCard.AddComponent<Outline>();
        mcOutline.effectColor = new Color(0.25f, 0.75f, 1f, 0.90f);
        mcOutline.effectDistance = new Vector2(6, -6);

        // Header Banner
        GameObject bannerObj = CreateUIElement("HeaderBanner", modalCard.transform);
        RectTransform bRt = bannerObj.GetComponent<RectTransform>();
        SetAnchors(bRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(720f, 70f));
        Image bImg = bannerObj.AddComponent<Image>();
        if (whiteSprite != null) bImg.sprite = whiteSprite;
        bImg.color = new Color(1f, 0.60f, 0.15f, 1f);

        // Title
        GameObject titleObj = CreateUIElement("Title", bannerObj.transform);
        StretchFull(titleObj.GetComponent<RectTransform>());
        TextMeshProUGUI titleTmp = AddCrispTMP(titleObj);
        titleTmp.text = "🎡 CÔNG VIÊN GIẢI TRÍ • TÀU LƯỢN SIÊU TỐC 🎡";
        titleTmp.fontSize = 28;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = Color.white;

        // Nội dung mô tả
        GameObject msgObj = CreateUIElement("MessageContent", modalCard.transform);
        RectTransform msgRt = msgObj.GetComponent<RectTransform>();
        SetAnchors(msgRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 15f), new Vector2(700f, 180f));
        TextMeshProUGUI msgTmp = AddCrispTMP(msgObj);
        msgTmp.text = "<size=28><color=#FFDD55><b>Bạn có muốn bắt đầu chuyến phiêu lưu mạo hiểm?</b></color></size>\n\n" +
                      "Trải nghiệm những khúc cua nghẹt thở và tốc độ xé gió trên đường ray huyền thoại!\n" +
                      "<size=20><color=#88DDFF>Hãy bấm nút bên dưới để vào ga chọn ghế.</color></size>";
        msgTmp.lineSpacing = 16;
        msgTmp.alignment = TextAlignmentOptions.Center;
        msgTmp.color = new Color(0.92f, 0.96f, 1f, 0.95f);

        // Nút duy nhất: "VÀO CHƠI TÀU LƯỢN" (To rõ, dễ bấm)
        GameObject btnStartObj = CreateButton("Btn_ConfirmStart", modalCard.transform,
            new Vector2(0.5f, 0f), new Vector2(0f, 85f), new Vector2(360f, 75f),
            new Color(0.12f, 0.75f, 0.38f, 1f), whiteSprite);
        SetButtonText(btnStartObj, "VÀO CHƠI NGAY  ➔", 26, Color.white);
        Outline bsOutline = btnStartObj.AddComponent<Outline>();
        bsOutline.effectColor = new Color(0.08f, 0.45f, 0.22f, 1f);
        bsOutline.effectDistance = new Vector2(3, -3);

        modalObj.SetActive(true); // Luôn mở sẵn trong không gian

        // Gán tham chiếu vào RollerCoasterInteraction
        interactionScript.confirmationModalPanel = modalObj;
        interactionScript.confirmStartButton = btnStartObj.GetComponent<Button>();
        interactionScript.modalTitleText = titleTmp;
        interactionScript.modalMessageText = msgTmp;

        // Tự tìm sảnh ga VR_FloorPoint và XR Origin
        GameObject vrFloor = GameObject.Find("VR_FloorPoint");
        if (vrFloor != null) interactionScript.stationEntryPoint = vrFloor.transform;

        GameObject xrRig = GameObject.Find("XR Origin (XR Rig)");
        if (xrRig != null) interactionScript.xrOriginObject = xrRig;

        SeatSwitcher sw = Object.FindFirstObjectByType<SeatSwitcher>();
        if (sw != null) interactionScript.seatSwitcher = sw;

        EditorUtility.SetDirty(zoneObj);
        EditorUtility.SetDirty(canvasObj);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Undo.CollapseUndoOperations(groupIndex);

        Debug.Log("<color=#00FF66><b>[GameInteractionBuilder] Đã thiết lập xong Kiosk 3D thuần UI (Không còn phím bấm)!</b></color>");
        if (!silent)
        {
            EditorUtility.DisplayDialog("Setup Thành Công", "Đã dựng xong Kiosk 3D ở World Space. Không còn dùng phím F hay bàn phím, chỉ việc bấm nút trên bảng.", "OK");
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
                    return child.gameObject;
            }
            if (parentObj.transform.childCount > 0) return parentObj.transform.GetChild(0).gameObject;
            return parentObj;
        }
        return null;
    }

    private static void EnsurePlayerConfig()
    {
        GameObject playerObj = GameObject.Find("XR Origin (XR Rig)");
        if (playerObj == null) playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null && !playerObj.CompareTag("Player"))
        {
            playerObj.tag = "Player";
            EditorUtility.SetDirty(playerObj);
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