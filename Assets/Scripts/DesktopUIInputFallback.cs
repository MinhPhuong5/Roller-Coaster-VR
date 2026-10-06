using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Quản lý UI Input và Raycasters đồng bộ giữa PC Desktop và Kính VR:
/// - Khi test trên PC/Laptop: Tắt TrackedDeviceGraphicRaycaster để tránh crash KeyNotFoundException của Unity XRI, bật GraphicRaycaster chuẩn cho chuột.
/// - Khi cắm kính VR thật (HMD): Kích hoạt TrackedDeviceGraphicRaycaster cho tay cầm VR và gán Camera.main làm worldCamera.
/// </summary>
public class DesktopUIInputFallback : MonoBehaviour
{
    private InputSystemUIInputModule desktopModule;
    private bool lastHMDStatus = false;
    private float checkRaycastersTimer = 0f;

    void Awake()
    {
        desktopModule = GetComponent<InputSystemUIInputModule>();
        if (desktopModule == null)
        {
            desktopModule = gameObject.AddComponent<InputSystemUIInputModule>();
        }

        UpdateRaycastersAndCanvases(IsHMDConnected());
    }

    void Start()
    {
        UpdateRaycastersAndCanvases(IsHMDConnected());
    }

    void OnEnable()
    {
        UpdateRaycastersAndCanvases(IsHMDConnected());
    }

    /// <summary>
    /// Kiểm tra xem có kính VR thật (HeadMounted Display) đang kết nối và hoạt động hay không
    /// </summary>
    public static bool IsHMDConnected()
    {
        var inputDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, inputDevices);
        foreach (var dev in inputDevices)
        {
            if (dev.isValid) return true;
        }
        return false;
    }

    void LateUpdate()
    {
        bool hasHMD = IsHMDConnected();

        // Kiểm tra và cập nhật raycasters khi trạng thái kính đổi hoặc định kỳ 1.0s (khi UI mới được mở)
        checkRaycastersTimer += Time.unscaledDeltaTime;
        if (hasHMD != lastHMDStatus || checkRaycastersTimer > 1.0f)
        {
            checkRaycastersTimer = 0f;
            lastHMDStatus = hasHMD;
            UpdateRaycastersAndCanvases(hasHMD);
        }

        // Nếu KHÔNG có kính VR thật (đang test trên Laptop/PC)
        if (!hasHMD)
        {
            // Tắt tất cả các module khác của XR trên EventSystem
            BaseInputModule[] allModules = GetComponents<BaseInputModule>();
            foreach (var mod in allModules)
            {
                if (mod != desktopModule && mod.enabled)
                {
                    mod.enabled = false;
                }
            }

            // Ép module chuột luôn luôn hoạt động
            if (desktopModule != null && !desktopModule.enabled)
            {
                desktopModule.enabled = true;
            }

            SilenceXRControllersAndRays();
        }
    }

    private static GameObject cachedXROrigin;

    /// <summary>
    /// Vô hiệu hóa triệt để toàn bộ tia laser LineRenderer / XRInteractorLineVisual trên XR Origin khi chơi trên Desktop
    /// </summary>
    public static void SilenceXRControllersAndRays()
    {
        if (cachedXROrigin == null)
        {
            cachedXROrigin = GameObject.Find("XR Origin (XR Rig)");
            if (cachedXROrigin == null) cachedXROrigin = GameObject.FindGameObjectWithTag("Player");
        }

        if (cachedXROrigin != null)
        {
            LineRenderer[] lines = cachedXROrigin.GetComponentsInChildren<LineRenderer>(true);
            foreach (var l in lines)
            {
                if (l != null && l.enabled) l.enabled = false;
            }

            MonoBehaviour[] scripts = cachedXROrigin.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in scripts)
            {
                if (mb == null) continue;
                string n = mb.GetType().Name;
                if (n.Contains("LineVisual") || n.Contains("XRInteractorLineVisual") || n.Contains("RayVisual") || n.Contains("CurveVisual"))
                {
                    if (mb.enabled) mb.enabled = false;
                }
            }
        }
    }

    /// <summary>
    /// Cập nhật tất cả Canvas và Raycasters trong toàn bộ Scene để loại bỏ hoàn toàn lỗi KeyNotFoundException
    /// </summary>
    public static void UpdateRaycastersAndCanvases(bool hasHMD)
    {
        Camera mainCam = Camera.main;

        // 1. Quản lý và thiết lập tất cả Canvases trong Scene
        Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var canvas in allCanvases)
        {
            if (canvas == null) continue;

            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                if (canvas.worldCamera == null && mainCam != null)
                {
                    canvas.worldCamera = mainCam;
                }
            }

            // Đảm bảo luôn có GraphicRaycaster để nhận chuột máy tính
            GraphicRaycaster gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr == null)
            {
                gr = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            if (!gr.enabled)
            {
                gr.enabled = true;
            }
        }

        // 2. Quản lý TrackedDeviceGraphicRaycaster của Unity XRI
        var trackedRaycasters = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var tr in trackedRaycasters)
        {
            if (tr == null) continue;

            // Trên PC không có kính VR: TẮT TrackedDeviceGraphicRaycaster để triệt tiêu lỗi KeyNotFoundException của Unity XRI
            if (!hasHMD)
            {
                if (tr.enabled) tr.enabled = false;
            }
            else
            {
                if (!tr.enabled) tr.enabled = true;
            }
        }
    }
}