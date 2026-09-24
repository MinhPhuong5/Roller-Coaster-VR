#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// Tool tu dong tao Canvas Portal Loading (Buoc 6 & 7) voi 1 click.
/// Menu: Tools -> Rick Portal -> Tao Canvas Loading Video Dich Chuyen
/// </summary>
public static class PortalCanvasBuilder
{
    private const string RT_PATH = "Assets/RenderTexture/PortalLoadingRT.renderTexture";

    [MenuItem("Tools/Rick Portal/Tao Canvas Loading Video Dich Chuyen (1-Click)", priority = 10)]
    public static void SetupPortalCanvas()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Portal Loading Canvas");

        // 1. Tao thu muc va RenderTexture
        RenderTexture rt = GetOrCreateRenderTexture();

        // 2. Tim hoac tao Canvas
        GameObject canvasObj = GameObject.Find("PortalLoadingCanvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("PortalLoadingCanvas");
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create PortalLoadingCanvas");
        }

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvasObj.GetComponent<GraphicRaycaster>();
        if (raycaster == null) canvasObj.AddComponent<GraphicRaycaster>();

        // 3. Lop nen den Background (chong chop giat man hinh truoc khi video chay)
        Transform bgTrans = canvasObj.transform.Find("Background_Black");
        GameObject bgObj;
        if (bgTrans == null)
        {
            bgObj = new GameObject("Background_Black");
            bgObj.transform.SetParent(canvasObj.transform, false);
            Undo.RegisterCreatedObjectUndo(bgObj, "Create Background_Black");
        }
        else
        {
            bgObj = bgTrans.gameObject;
        }
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        if (bgRect == null) bgRect = bgObj.AddComponent<RectTransform>();
        SetStretchFull(bgRect);

        Image bgImage = bgObj.GetComponent<Image>();
        if (bgImage == null) bgImage = bgObj.AddComponent<Image>();
        bgImage.color = Color.black;

        // 4. Man hinh video RawImage full screen
        Transform rawTrans = canvasObj.transform.Find("RawImage_VideoScreen");
        GameObject rawObj;
        if (rawTrans == null)
        {
            rawObj = new GameObject("RawImage_VideoScreen");
            rawObj.transform.SetParent(canvasObj.transform, false);
            Undo.RegisterCreatedObjectUndo(rawObj, "Create RawImage_VideoScreen");
        }
        else
        {
            rawObj = rawTrans.gameObject;
        }
        RectTransform rawRect = rawObj.GetComponent<RectTransform>();
        if (rawRect == null) rawRect = rawObj.AddComponent<RectTransform>();
        SetStretchFull(rawRect);

        RawImage rawImage = rawObj.GetComponent<RawImage>();
        if (rawImage == null) rawImage = rawObj.AddComponent<RawImage>();
        rawImage.texture = rt;
        rawImage.color = Color.white;

        // 5. Text sci-fi trang tri Portal Jump (tuy chon, rat dep)
        Transform textTrans = canvasObj.transform.Find("Text_PortalSubtitle");
        GameObject textObj;
        if (textTrans == null)
        {
            textObj = new GameObject("Text_PortalSubtitle");
            textObj.transform.SetParent(canvasObj.transform, false);
            Undo.RegisterCreatedObjectUndo(textObj, "Create Text_PortalSubtitle");
        }
        else
        {
            textObj = textTrans.gameObject;
        }
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        if (textRect == null) textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.05f);
        textRect.anchorMax = new Vector2(1f, 0.12f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmpText = textObj.GetComponent<TextMeshProUGUI>();
        if (tmpText == null) tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = "⚡ DIMENSION C-137 // WARP IN PROGRESS... ⚡";
        tmpText.fontSize = 24;
        tmpText.fontStyle = FontStyles.Bold;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = new Color(0.25f, 1f, 0.35f, 0.85f); // Xanh la neon portal

        // 6. Tao object PortalLoadingVideo va VideoPlayer
        Transform videoTrans = canvasObj.transform.Find("PortalLoadingVideo");
        GameObject videoObj;
        if (videoTrans == null)
        {
            videoObj = new GameObject("PortalLoadingVideo");
            videoObj.transform.SetParent(canvasObj.transform, false);
            Undo.RegisterCreatedObjectUndo(videoObj, "Create PortalLoadingVideo");
        }
        else
        {
            videoObj = videoTrans.gameObject;
        }

        VideoPlayer vp = videoObj.GetComponent<VideoPlayer>();
        if (vp == null) vp = videoObj.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.waitForFirstFrame = true;
        vp.isLooping = true;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = rt;
        vp.audioOutputMode = VideoAudioOutputMode.Direct;

        // 7. Tu dong tim RickPortalTrigger (PortalNpcController) de ket noi
        PortalNpcController portalController = Object.FindFirstObjectByType<PortalNpcController>();
        string connectStatus = "";

        if (portalController != null)
        {
            SerializedObject so = new SerializedObject(portalController);
            SerializedProperty canvasProp = so.FindProperty("loadingVideoCanvas");
            SerializedProperty playerProp = so.FindProperty("loadingVideoPlayer");
            SerializedProperty controlsProp = so.FindProperty("playerControlsToDisable");

            if (canvasProp != null) canvasProp.objectReferenceValue = canvasObj;
            if (playerProp != null) playerProp.objectReferenceValue = vp;

            // Tim Player Chihiro
            PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null && controlsProp != null)
            {
                controlsProp.arraySize = 1;
                controlsProp.GetArrayElementAtIndex(0).objectReferenceValue = pm;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(portalController);
            connectStatus = $"\n\n✔ Đã tự động kết nối vào {portalController.gameObject.name} (Loading Video Canvas & Video Player)\n✔ Đã tự động thêm PlayerMovement của {(pm != null ? pm.gameObject.name : "Player")} vào 'Player Controls To Disable'!";
        }
        else
        {
            connectStatus = "\n\n(Lưu ý: Chưa thấy object gắn PortalNpcController trên Scene. Nếu bạn chưa tạo RickPortalTrigger, hãy tạo và kéo PortalLoadingCanvas + PortalLoadingVideo vào nhé!)";
        }

        // Kiem tra RickPortalExitTrigger neu co
        PortalExitTrigger exitTrigger = Object.FindFirstObjectByType<PortalExitTrigger>();
        if (exitTrigger != null && portalController != null)
        {
            SerializedObject soExit = new SerializedObject(exitTrigger);
            SerializedProperty pcProp = soExit.FindProperty("portalController");
            if (pcProp != null && pcProp.objectReferenceValue == null)
            {
                pcProp.objectReferenceValue = portalController;
                soExit.ApplyModifiedProperties();
                EditorUtility.SetDirty(exitTrigger);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = canvasObj;

        EditorUtility.DisplayDialog("Setup Thành Công!",
            "Đã tạo hoàn tất Canvas màn hình chuyển cảnh:\n" +
            "• Canvas: PortalLoadingCanvas (Screen Space - Overlay, Sort Order 100)\n" +
            "• RawImage full screen hiển thị video mượt mà\n" +
            "• Video Player: PortalLoadingVideo -> Render Texture (PortalLoadingRT)" +
            connectStatus +
            "\n\nBước cuối cùng: Chọn object 'PortalLoadingVideo' trong Hierarchy và kéo file Video Clip của bạn vào ô 'Video Clip' là xong!",
            "Đã Hiểu");

        Debug.Log("<color=#00FF88><b>[PortalCanvasBuilder]</b> Đã tạo thành công PortalLoadingCanvas và cấu hình toàn bộ hệ thống!</color>");
    }

    private static RenderTexture GetOrCreateRenderTexture()
    {
        string folder = "Assets/RenderTexture";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "RenderTexture");
        }

        RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RT_PATH);
        if (rt == null)
        {
            rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            rt.name = "PortalLoadingRT";
            AssetDatabase.CreateAsset(rt, RT_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        return rt;
    }

    private static void SetStretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
#endif
