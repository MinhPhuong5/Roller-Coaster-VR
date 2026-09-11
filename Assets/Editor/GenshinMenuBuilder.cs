#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

[InitializeOnLoad]
public static class GenshinMenuBuilder
{
    static GenshinMenuBuilder()
    {
        EditorApplication.delayCall += CheckAndAutoSetup;
        EditorSceneManager.sceneOpened += (scene, mode) => CheckAndAutoSetup();
    }

    private static void CheckAndAutoSetup()
    {
        if (Application.isPlaying) return;
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path == "Assets/Scenes/Menu.unity")
        {
            if (GameObject.Find("Canvas_Menu") == null && GameObject.Find("Canvas_GenshinMenu") == null)
            {
                BuildMenuScene(silent: true);
            }
        }
    }

    [MenuItem("Tools/Genshin Menu/1-Click Setup Genshin Menu")]
    public static void BuildMenuSceneManual()
    {
        BuildMenuScene(silent: false);
    }

    public static void BuildMenuScene(bool silent = false)
    {
        string scenePath = "Assets/Scenes/Menu.unity";
        
        if (EditorSceneManager.GetActiveScene().path != scenePath)
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
        Undo.SetCurrentGroupName("Setup Genshin Loading Menu");

        // 1. Camera
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            camObj.AddComponent<AudioListener>();
            Undo.RegisterCreatedObjectUndo(camObj, "Create Camera");
        }
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = Color.black;

        // 2. EventSystem
        UnityEngine.EventSystems.EventSystem eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
        }

        // 3. Xóa các đối tượng cũ
        GameObject oldCanvas1 = GameObject.Find("Canvas_GenshinMenu");
        if (oldCanvas1 != null) Undo.DestroyObjectImmediate(oldCanvas1);

        GameObject oldCanvas2 = GameObject.Find("Canvas_Menu");
        if (oldCanvas2 != null) Undo.DestroyObjectImmediate(oldCanvas2);

        GameObject oldManager = GameObject.Find("MenuController_Manager");
        if (oldManager != null) Undo.DestroyObjectImmediate(oldManager);

        // 4. Render Textures cho 2 Video
        EnsureDirectoryExists("Assets/MenuAssets/Video");
        string rt1Path = "Assets/MenuAssets/Video/IdleVideoRT.renderTexture";
        string rt2Path = "Assets/MenuAssets/Video/TransitionVideoRT.renderTexture";

        RenderTexture rt1 = GetOrCreateRenderTexture(rt1Path, "IdleVideoRT");
        RenderTexture rt2 = GetOrCreateRenderTexture(rt2Path, "TransitionVideoRT");

        // Đảm bảo WhitePixel là Sprite
        string whitePixelPath = "Assets/MenuAssets/UI/WhitePixel.png";
        EnsureSpriteImporter(whitePixelPath);
        Sprite whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(whitePixelPath);

        // 5. Tạo Menu Manager
        GameObject managerObj = new GameObject("MenuController_Manager");
        MenuController menuCtrl = managerObj.AddComponent<MenuController>();
        menuCtrl.gameSceneName = "Game";
        menuCtrl.loadingDuration = 5.0f; // 5 giây load trận

        // Video 1 (Idle Loop)
        VideoPlayer vp1 = managerObj.AddComponent<VideoPlayer>();
        vp1.playOnAwake = true;
        vp1.isLooping = true;
        vp1.renderMode = VideoRenderMode.RenderTexture;
        vp1.targetTexture = rt1;
        vp1.audioOutputMode = VideoAudioOutputMode.Direct;
        vp1.EnableAudioTrack(0, true);
        vp1.SetDirectAudioVolume(0, 1.0f);
        menuCtrl.idleVideoPlayer = vp1;

        // Video 2 (Transition Chói sáng)
        VideoPlayer vp2 = managerObj.AddComponent<VideoPlayer>();
        vp2.playOnAwake = false;
        vp2.isLooping = false;
        vp2.renderMode = VideoRenderMode.RenderTexture;
        vp2.targetTexture = rt2;
        vp2.audioOutputMode = VideoAudioOutputMode.Direct;
        vp2.EnableAudioTrack(0, true);
        vp2.SetDirectAudioVolume(0, 1.0f);
        menuCtrl.transitionVideoPlayer = vp2;

        // Tự động gán video 0909.mp4 nếu đã có trong project
        VideoClip foundClip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Assets/Video/0909.mp4")
            ?? AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Video/0909.mp4");
        if (foundClip != null)
        {
            menuCtrl.idleVideoClip = foundClip;
            vp1.clip = foundClip;
        }

        Undo.RegisterCreatedObjectUndo(managerObj, "Create Menu Manager");

        // 6. Tạo Canvas UI
        GameObject canvasObj = new GameObject("Canvas_Menu");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas_Menu");

        // 6.1. Khung hiển thị Video 1 (Nền nhạc ban đầu)
        GameObject vid1Obj = CreateUIElement("Video1_IdleDisplay", canvasObj.transform);
        StretchFull(vid1Obj.GetComponent<RectTransform>());
        RawImage rawImg1 = vid1Obj.AddComponent<RawImage>();
        rawImg1.texture = rt1;
        rawImg1.color = Color.white;
        rawImg1.raycastTarget = false;

        // 6.2. Khung hiển thị Video 2 (Video chói sáng khi click)
        GameObject vid2Obj = CreateUIElement("Video2_TransitionDisplay", canvasObj.transform);
        StretchFull(vid2Obj.GetComponent<RectTransform>());
        RawImage rawImg2 = vid2Obj.AddComponent<RawImage>();
        rawImg2.texture = rt2;
        rawImg2.color = Color.white;
        rawImg2.raycastTarget = false;
        vid2Obj.SetActive(false);
        menuCtrl.transitionVideoDisplay = vid2Obj;

        // 6.3. Nút bấm tàng hình phủ toàn màn hình (Bấm bất cứ đâu cũng bắt đầu load)
        GameObject tapTriggerObj = CreateUIElement("TapAnywhere_Button", canvasObj.transform);
        StretchFull(tapTriggerObj.GetComponent<RectTransform>());
        Image triggerImg = tapTriggerObj.AddComponent<Image>();
        triggerImg.color = new Color(0, 0, 0, 0.001f);
        Button tapBtn = tapTriggerObj.AddComponent<Button>();
        tapBtn.transition = Selectable.Transition.None;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(tapBtn.onClick, menuCtrl.StartGame);

        // 6.4. Container chứa chữ Menu & Ô nhập tên (sẽ biến mất khi bấm)
        GameObject uiContainer = CreateUIElement("MenuUI_Container", canvasObj.transform);
        StretchFull(uiContainer.GetComponent<RectTransform>());
        menuCtrl.allMenuUIContainer = uiContainer;

        // --- CỤM CHỮ GIỮA MÀN HÌNH ---
        // 1. Tên Game (ở trên chữ START GAME)
        GameObject gameTitleObj = CreateUIElement("GameTitle_Text", uiContainer.transform);
        RectTransform titleRt = gameTitleObj.GetComponent<RectTransform>();
        SetAnchors(titleRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(900f, 50f));
        TextMeshProUGUI titleTmp = gameTitleObj.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "ROLLER COASTER VR";
        titleTmp.fontSize = 34;
        titleTmp.characterSpacing = 10;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(1f, 0.92f, 0.72f, 1f);
        titleTmp.raycastTarget = false;

        // 2. Chữ START GAME (chính giữa to rõ nét căng)
        GameObject startTextObj = CreateUIElement("StartGame_Text", uiContainer.transform);
        RectTransform startRt = startTextObj.GetComponent<RectTransform>();
        SetAnchors(startRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(900f, 90f));
        TextMeshProUGUI startTmp = startTextObj.AddComponent<TextMeshProUGUI>();
        startTmp.text = "START GAME";
        startTmp.fontSize = 76;
        startTmp.characterSpacing = 12;
        startTmp.alignment = TextAlignmentOptions.Center;
        startTmp.fontStyle = FontStyles.Bold;
        startTmp.color = Color.white;
        startTmp.raycastTarget = false;

        // 3. Dải hoa văn trang trí gạch ngang
        GameObject dividerObj = CreateUIElement("Divider_Text", uiContainer.transform);
        RectTransform divRt = dividerObj.GetComponent<RectTransform>();
        SetAnchors(divRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(600f, 30f));
        TextMeshProUGUI divTmp = dividerObj.AddComponent<TextMeshProUGUI>();
        divTmp.text = "—   ❖   —";
        divTmp.fontSize = 26;
        divTmp.alignment = TextAlignmentOptions.Center;
        divTmp.color = new Color(0.9f, 0.95f, 1f, 0.8f);
        divTmp.raycastTarget = false;

        // 4. Chữ CLICK TO BEGIN (lúc ẩn lúc hiện, nhấp nháy nhanh hơn)
        GameObject clickTextObj = CreateUIElement("ClickToBegin_Text", uiContainer.transform);
        RectTransform clickRt = clickTextObj.GetComponent<RectTransform>();
        SetAnchors(clickRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(900f, 45f));
        TextMeshProUGUI clickTmp = clickTextObj.AddComponent<TextMeshProUGUI>();
        clickTmp.text = "CLICK TO BEGIN";
        clickTmp.fontSize = 30;
        clickTmp.characterSpacing = 14;
        clickTmp.alignment = TextAlignmentOptions.Center;
        clickTmp.fontStyle = FontStyles.Bold;
        clickTmp.color = new Color(1f, 0.96f, 0.85f, 1f);
        clickTmp.raycastTarget = false;

        MenuPulseAnimation pulse = clickTextObj.AddComponent<MenuPulseAnimation>();
        pulse.pulseSpeed = 2.8f;
        pulse.minAlpha = 0.2f;
        pulse.maxAlpha = 1.0f;
        pulse.enableScalePulse = true;
        pulse.scaleAmplitude = 0.02f;

        // --- KHUNG XÁM NHẬP TÊN (CĂN GIỮA ĐỒNG BỘ) ---
        GameObject inputFrameObj = CreateUIElement("NameInput_Frame", uiContainer.transform);
        RectTransform inputFrameRt = inputFrameObj.GetComponent<RectTransform>();
        SetAnchors(inputFrameRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -145f), new Vector2(500f, 60f));
        Image frameImg = inputFrameObj.AddComponent<Image>();
        frameImg.color = new Color(0.12f, 0.16f, 0.24f, 0.92f);

        // Icon kim cương nhỏ bên trái khung
        GameObject iconObj = CreateUIElement("Icon_Diamond", inputFrameObj.transform);
        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        SetAnchors(iconRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(30f, 30f));
        TextMeshProUGUI iconTmp = iconObj.AddComponent<TextMeshProUGUI>();
        iconTmp.text = "❖";
        iconTmp.fontSize = 26;
        iconTmp.alignment = TextAlignmentOptions.Center;
        iconTmp.color = new Color(0.85f, 0.92f, 1f, 0.9f);
        iconTmp.raycastTarget = false;

        // TMP_InputField
        TMP_InputField inputField = inputFrameObj.AddComponent<TMP_InputField>();
        inputField.targetGraphic = frameImg;

        GameObject textAreaObj = CreateUIElement("Text Area", inputFrameObj.transform);
        RectTransform textAreaRt = textAreaObj.GetComponent<RectTransform>();
        textAreaRt.anchorMin = Vector2.zero;
        textAreaRt.anchorMax = Vector2.one;
        textAreaRt.offsetMin = new Vector2(52f, 6f);
        textAreaRt.offsetMax = new Vector2(-16f, -6f);
        textAreaObj.AddComponent<RectMask2D>();

        GameObject placeholderObj = CreateUIElement("Placeholder", textAreaObj.transform);
        StretchFull(placeholderObj.GetComponent<RectTransform>());
        TextMeshProUGUI placeholderTmp = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholderTmp.text = "Mời nhập tên của bạn...";
        placeholderTmp.fontSize = 24;
        placeholderTmp.fontStyle = FontStyles.Italic;
        placeholderTmp.color = new Color(0.7f, 0.8f, 0.92f, 0.6f);
        placeholderTmp.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject textEntryObj = CreateUIElement("Text", textAreaObj.transform);
        StretchFull(textEntryObj.GetComponent<RectTransform>());
        TextMeshProUGUI textEntryTmp = textEntryObj.AddComponent<TextMeshProUGUI>();
        textEntryTmp.text = "";
        textEntryTmp.fontSize = 24;
        textEntryTmp.color = Color.white;
        textEntryTmp.alignment = TextAlignmentOptions.MidlineLeft;

        inputField.textViewport = textAreaRt;
        inputField.textComponent = textEntryTmp;
        inputField.placeholder = placeholderTmp;
        inputField.fontAsset = textEntryTmp.font;

        menuCtrl.nameInputField = inputField;

        // 6.5. KHUNG THANH LOAD TRẬN (LOADING... 0-5s)
        GameObject loadContainer = CreateUIElement("LoadingUI_Container", canvasObj.transform);
        RectTransform loadRt = loadContainer.GetComponent<RectTransform>();
        SetAnchors(loadRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(700f, 130f));
        menuCtrl.loadingUIContainer = loadContainer;

        // Chữ "Loading... 0%"
        GameObject loadTextObj = CreateUIElement("Loading_Text", loadContainer.transform);
        RectTransform loadTextRt = loadTextObj.GetComponent<RectTransform>();
        SetAnchors(loadTextRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 32f), new Vector2(600f, 40f));
        TextMeshProUGUI loadTmp = loadTextObj.AddComponent<TextMeshProUGUI>();
        loadTmp.text = "Loading... 0%";
        loadTmp.fontSize = 30;
        loadTmp.characterSpacing = 8;
        loadTmp.alignment = TextAlignmentOptions.Center;
        loadTmp.fontStyle = FontStyles.Bold;
        loadTmp.color = new Color(1f, 0.96f, 0.86f, 1f);
        loadTmp.raycastTarget = false;
        menuCtrl.loadingText = loadTmp;

        // Khung nền đen của thanh progress bar
        GameObject barBgObj = CreateUIElement("ProgressBar_Background", loadContainer.transform);
        RectTransform barBgRt = barBgObj.GetComponent<RectTransform>();
        SetAnchors(barBgRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(560f, 18f));
        Image barBgImg = barBgObj.AddComponent<Image>();
        barBgImg.color = new Color(0.08f, 0.12f, 0.2f, 0.92f);
        barBgImg.raycastTarget = false;

        // Thanh chạy (Fill Area)
        GameObject barFillObj = CreateUIElement("ProgressBar_Fill", barBgObj.transform);
        StretchFull(barFillObj.GetComponent<RectTransform>());
        barFillObj.GetComponent<RectTransform>().offsetMin = new Vector2(2f, 2f);
        barFillObj.GetComponent<RectTransform>().offsetMax = new Vector2(-2f, -2f);
        Image fillImg = barFillObj.AddComponent<Image>();
        if (whiteSprite != null) fillImg.sprite = whiteSprite;
        fillImg.color = new Color(1f, 0.88f, 0.52f, 1f); // Màu vàng phát sáng Genshin
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = 0; // Chạy từ trái sang phải
        fillImg.fillAmount = 0f;
        fillImg.raycastTarget = false;
        menuCtrl.loadingFillBar = fillImg;

        loadContainer.SetActive(false); // Lúc đầu ẩn đi

        // 6.6. MÀN HÌNH SÁNG CHÓI (WHITE FLASH OVERLAY)
        GameObject flashObj = CreateUIElement("WhiteFlash_Overlay", canvasObj.transform);
        StretchFull(flashObj.GetComponent<RectTransform>());
        Image flashImg = flashObj.AddComponent<Image>();
        flashImg.color = Color.white;
        flashImg.raycastTarget = false;
        CanvasGroup flashCg = flashObj.AddComponent<CanvasGroup>();
        flashCg.alpha = 0f;
        flashCg.blocksRaycasts = false;
        menuCtrl.whiteFlashCanvasGroup = flashCg;

        // 7. Lưu scene
        EditorUtility.SetDirty(managerObj);
        EditorUtility.SetDirty(canvasObj);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Undo.CollapseUndoOperations(groupIndex);

        Debug.Log("<color=#00FF66><b>[GenshinMenuBuilder] Đã hoàn tất luồng: Chữ biến mất -> Thanh load chạy 5s trên nền video -> Màn hình sáng -> Vào game!</b></color>");
        if (!silent)
        {
            EditorUtility.DisplayDialog("Menu Setup", "Đã cập nhật hoàn tất theo đúng yêu cầu:\n\n1. Ấn vào màn hình -> Toàn bộ chữ biến mất.\n2. Video nền tiếp tục chạy, xuất hiện thanh Loading chạy từ từ 0 - 5 giây.\n3. Sau 5 giây -> Màn hình sáng chói -> Vào thẳng Game!", "OK");
        }
    }

    private static void EnsureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }
    }

    private static RenderTexture GetOrCreateRenderTexture(string path, string name)
    {
        RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (rt == null)
        {
            rt = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
            rt.name = name;
            AssetDatabase.CreateAsset(rt, path);
            AssetDatabase.SaveAssets();
        }
        return rt;
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

    private static void EnsureDirectoryExists(string relativePath)
    {
        string fullPath = System.IO.Path.Combine(Application.dataPath, "..", relativePath);
        if (!System.IO.Directory.Exists(fullPath))
        {
            System.IO.Directory.CreateDirectory(fullPath);
            AssetDatabase.Refresh();
        }
    }
}
#endif
