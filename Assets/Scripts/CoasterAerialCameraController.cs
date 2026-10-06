using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quản lý Camera Tách Khỏi Tàu Quan Sát Tĩnh Từ Trên Cao (Static Aerial Overview):
/// - Đoạn 1 (Đầu chặng - Lao dốc dài): Camera bay lên cao và XOAY BÁM THEO TÀU (trackCoasterTarget = true) để theo dõi toàn bộ hành trình đổ dốc dài.
/// - Đoạn 2 (Cuối chặng - Cụm xoắn ốc): Camera bay lên điểm quan sát trên cao ĐỨNG YÊN TĨNH (trackCoasterTarget = false) bao trọn toàn bộ tháp xoắn ốc trong khung hình.
/// - Đoạn giữa: Người chơi ngồi trong buồng lái trải nghiệm góc nhìn thứ nhất.
/// - Hỗ trợ chơi tiếp nhiều lần (Play Again): Tự động reset và hoạt động chuẩn xác 100% ở tất cả các lần chơi tiếp theo.
/// - Hỗ trợ Bật/Tắt khi đang Play: Phím 'C' / 'V' hoặc UI Toggle, lưu tùy chọn vào PlayerPrefs.
/// </summary>
public class CoasterAerialCameraController : MonoBehaviour
{
    public enum AerialCameraState
    {
        CockpitSeated,      // Đang ngồi trong buồng lái trên tàu lượn
        FlyingUp,           // Đang bay từ ghế lên điểm quan sát tĩnh trên cao
        StationaryAerial,   // Đang đứng yên trên cao quan sát tàu chạy qua
        ChasingDown,        // Đang bay lao xuống đuổi theo ghế tàu đang di chuyển
        Docking             // Vừa chạm ghế, gán lại làm con của tàu và hoàn tất
    }

    [System.Serializable]
    public class AerialShotSegment
    {
        [Tooltip("Tên đoạn quay để dễ quản lý")]
        public string segmentName = "1. Đại dốc 1 (Đầu chặng)";

        [Tooltip("Điểm quan sát tĩnh trên cao trong Scene (Static_Aerial_Point)")]
        public Transform aerialPoint;

        [Tooltip("Thời điểm bắt đầu bay lên (giây trong vòng lặp) - Mặc định 7.0s khi tàu đang leo dốc")]
        public float startLoopTime = 7.0f;

        [Tooltip("Thời điểm bắt đầu bay xuống đuổi theo tàu (giây trong vòng lặp)")]
        public float endLoopTime = 23.5f;

        [Tooltip("Thời gian bay chuyển tiếp lên / xuống (giây)")]
        public float transitionDuration = 1.5f;

        [Tooltip("Vòng chạy áp dụng (-1: Tất cả các vòng, 0: Vòng 1, 1: Vòng 2...)")]
        public int targetLap = -1;

        [Tooltip("Tự động xoay trục nhìn êm theo tâm toa tàu khi đứng trên cao (Smooth Track)")]
        public bool trackCoasterTarget = true;

        [Tooltip("Tốc độ lia nhìn theo tàu")]
        public float trackingDamping = 3.5f;

        [HideInInspector] public bool hasTriggeredThisLap = false;
    }

    [Header("1. BẬT / TẮT CHẾ ĐỘ FLYCAM TOÀN CẢNH")]
    [Tooltip("Bật/tắt tính năng quay từ trên cao (Có thể bật tắt khi đang Play)")]
    public bool enableAerialCamera = true;

    [Tooltip("Phím tắt bàn phím để bật/tắt nhanh chế độ Flycam trong lúc chơi PC")]
    public Key toggleKey = Key.C;

    [Tooltip("Phím tắt phụ dự phòng (ví dụ phím V)")]
    public Key alternateToggleKey = Key.V;

    [Header("2. UI ĐIỀU KHIỂN BẬT / TẮT (TÙY CHỌN)")]
    public Button toggleUIButton;
    public Toggle uiToggle;
    public TextMeshProUGUI statusTMPText;

    [Header("3. DANH SÁCH 2 ĐOẠN QUAY TRÊN CAO")]
    [Tooltip("Cấu hình 2 đoạn quay trên cao: Đoạn 1 (Đầu chặng xoay bám tàu) và Đoạn 2 (Cuối chặng camera tĩnh ngắm tháp xoắn ốc)")]
    public List<AerialShotSegment> aerialSegments = new List<AerialShotSegment>();

    [Header("4. THAM CHIẾU HỆ THỐNG")]
    public RideController rideController;
    public SeatSwitcher seatSwitcher;
    public GameObject xrOriginRig;
    public Transform realCartTransform;

    [Header("5. TRẠNG THÁI HIỆN TẠI")]
    public AerialCameraState currentState = AerialCameraState.CockpitSeated;
    public int activeSegmentIndex = -1;

    private const string PREF_KEY_ENABLE_AERIAL = "RollerCoaster_EnableAerialCamera";

    // Dữ liệu nội bộ
    private Vector3 flightStartWorldPos;
    private Quaternion flightStartWorldRot;
    private float flightElapsedTime = 0f;
    private float currentFlightDuration = 1.8f;
    private int lastProcessedLap = -1;
    private float lastTraveledProgress = -1f;

    void Awake()
    {
        LoadPlayerPreferences();
        ResolveReferences();
        EnsureDefaultAerialSegments();
    }

    void Start()
    {
        ResolveReferences();
        EnsureDefaultAerialSegments();
        SetupUIListeners();
        UpdateUIStatusDisplay();
        OnRideStart();
    }

    void Update()
    {
        // 1. Lắng nghe phím tắt bật/tắt Flycam trong lúc chơi (C hoặc V)
        HandleKeyboardToggle();

        // 2. Kiểm tra kích hoạt theo tiến trình vòng chạy của tàu lượn
        CheckAerialTriggers();

        // 3. Cập nhật chuyển động bay của Camera theo từng trạng thái máy
        UpdateCameraFlightMovement();
    }

    /// <summary>
    /// Gọi mỗi khi bắt đầu một chuyến đi mới (kể cả khi bấm Chơi Tiếp) để reset 100% các cờ kích hoạt
    /// </summary>
    public void OnRideStart()
    {
        ResetToCockpit();
        lastProcessedLap = -1;
        lastTraveledProgress = -1f;

        if (aerialSegments != null)
        {
            for (int i = 0; i < aerialSegments.Count; i++)
            {
                if (aerialSegments[i] != null)
                {
                    aerialSegments[i].hasTriggeredThisLap = false;
                }
            }
        }

        Debug.Log("<color=#00FF88><b>[CoasterAerialCameraController] ĐÃ RESET SẴN SÀNG CHO CHUYẾN ĐI MỚI (Tất cả đoạn quay Flycam đã mở lại)!</b></color>");
    }

    private void LoadPlayerPreferences()
    {
        if (PlayerPrefs.HasKey(PREF_KEY_ENABLE_AERIAL))
        {
            enableAerialCamera = PlayerPrefs.GetInt(PREF_KEY_ENABLE_AERIAL, 1) == 1;
        }
    }

    private void SavePlayerPreferences()
    {
        PlayerPrefs.SetInt(PREF_KEY_ENABLE_AERIAL, enableAerialCamera ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ResolveReferences()
    {
        if (rideController == null)
        {
            rideController = GetComponent<RideController>();
            if (rideController == null) rideController = GetComponentInParent<RideController>();
            if (rideController == null) rideController = Object.FindAnyObjectByType<RideController>();
        }

        if (seatSwitcher == null)
        {
            seatSwitcher = GetComponent<SeatSwitcher>();
            if (seatSwitcher == null) seatSwitcher = GetComponentInParent<SeatSwitcher>();
            if (seatSwitcher == null) seatSwitcher = Object.FindAnyObjectByType<SeatSwitcher>();
        }

        if (xrOriginRig == null)
        {
            if (seatSwitcher != null && seatSwitcher.xrOriginRig != null)
            {
                xrOriginRig = seatSwitcher.xrOriginRig;
            }
            else
            {
                GameObject rig = GameObject.Find("XR Origin (XR Rig)");
                if (rig == null) rig = GameObject.FindGameObjectWithTag("Player");
                if (rig != null) xrOriginRig = rig;
            }
        }

        if (realCartTransform == null)
        {
            CoasterFollower follower = Object.FindAnyObjectByType<CoasterFollower>();
            if (follower != null && follower.realCart != null)
            {
                realCartTransform = follower.realCart;
            }
            else if (rideController != null)
            {
                realCartTransform = rideController.transform;
            }
        }
    }

    /// <summary>
    /// Tạo sẵn 2 điểm mốc và cấu hình 2 góc quay chuẩn theo đúng yêu cầu:
    /// - Đoạn 1: Bắt đầu ở 11.5s -> 23.5s, trackCoasterTarget = TRUE (Camera xoay êm bám theo con tàu đổ dốc dài).
    /// - Đoạn 2: Bắt đầu ở 76.5s -> 86.0s, trackCoasterTarget = FALSE (Camera tĩnh 100% bao trọn toàn bộ tháp xoắn ốc như Ảnh 2).
    /// </summary>
    public void EnsureDefaultAerialSegments()
    {
        Vector3 baseCoasterPos = GetCoasterBasePosition();

        if (aerialSegments == null || aerialSegments.Count == 0)
        {
            aerialSegments = new List<AerialShotSegment>();

            // Đoạn 1: Đầu chặng - Lao đại dốc 1 (Bay lên ở 7.0s khi đang leo dốc -> Hạ cánh ở 23.5s)
            AerialShotSegment seg1 = new AerialShotSegment
            {
                segmentName = "1. Đại dốc 1 (Đầu chặng - Bám tàu)",
                startLoopTime = 7.0f,
                endLoopTime = 23.5f,
                transitionDuration = 1.5f,
                targetLap = -1,
                trackCoasterTarget = true,
                trackingDamping = 3.5f
            };
            seg1.aerialPoint = GetOrCreateAerialPoint("Static_Aerial_Point_Start", baseCoasterPos + new Vector3(-20.0f, 30.0f, 22.0f), Quaternion.Euler(30f, 130f, 0f));
            aerialSegments.Add(seg1);

            // Đoạn 2: Cuối chặng - Tháp xoắn ốc về ga (Bay lên ở 72.0s khi chuẩn bị lên đỉnh xoắn -> Hạ cánh ở 86.0s)
            AerialShotSegment seg2 = new AerialShotSegment
            {
                segmentName = "2. Tháp xoắn ốc (Cuối chặng - Camera tĩnh)",
                startLoopTime = 72.0f,
                endLoopTime = 86.0f,
                transitionDuration = 1.5f,
                targetLap = -1,
                trackCoasterTarget = false, // CAMERA TĨNH KHÔNG XOAY BÁM
                trackingDamping = 0f
            };
            seg2.aerialPoint = GetOrCreateAerialPoint("Static_Aerial_Point_End", baseCoasterPos + new Vector3(25.0f, 35.0f, -28.0f), Quaternion.Euler(40f, 315f, 0f));
            aerialSegments.Add(seg2);
        }
        else
        {
            // Cập nhật và đảm bảo có đủ 2 mốc Transform
            if (aerialSegments.Count >= 1)
            {
                aerialSegments[0].trackCoasterTarget = true; // Đoạn 1 luôn bám tàu
                // Nếu giá trị cũ đang quá muộn (> 8.0s), tự động điều chỉnh về 7.0s để bay lên khi đang leo dốc
                if (aerialSegments[0].startLoopTime > 8.0f)
                {
                    aerialSegments[0].startLoopTime = 7.0f;
                    aerialSegments[0].transitionDuration = 1.5f;
                }

                if (aerialSegments[0].aerialPoint == null)
                {
                    aerialSegments[0].aerialPoint = GetOrCreateAerialPoint("Static_Aerial_Point_Start", baseCoasterPos + new Vector3(-20.0f, 30.0f, 22.0f), Quaternion.Euler(30f, 130f, 0f));
                }
            }

            if (aerialSegments.Count >= 2)
            {
                aerialSegments[1].trackCoasterTarget = false; // Đoạn 2 luôn tĩnh ngắm tháp xoắn
                // Nếu giá trị cũ đang quá muộn (> 74.0s), tự động điều chỉnh về 72.0s để bay lên sớm đón đầu cú xoắn
                if (aerialSegments[1].startLoopTime > 74.0f)
                {
                    aerialSegments[1].startLoopTime = 72.0f;
                    aerialSegments[1].transitionDuration = 1.5f;
                }

                if (aerialSegments[1].aerialPoint == null)
                {
                    aerialSegments[1].aerialPoint = GetOrCreateAerialPoint("Static_Aerial_Point_End", baseCoasterPos + new Vector3(25.0f, 35.0f, -28.0f), Quaternion.Euler(40f, 315f, 0f));
                }
            }
        }
    }

    private Vector3 GetCoasterBasePosition()
    {
        if (realCartTransform != null) return realCartTransform.position;
        if (rideController != null) return rideController.transform.position;
        return transform.position;
    }

    private Transform GetOrCreateAerialPoint(string pointName, Vector3 defaultWorldPos, Quaternion defaultRot)
    {
        GameObject found = GameObject.Find(pointName);
        if (found != null)
        {
            return found.transform;
        }

        // Tạo mốc mới đặt cố định trong World Space
        GameObject newPoint = new GameObject(pointName);
        newPoint.transform.SetParent(null); // Không gắn vào tàu
        newPoint.transform.position = defaultWorldPos;
        newPoint.transform.rotation = defaultRot;
        return newPoint.transform;
    }

    private void SetupUIListeners()
    {
        if (toggleUIButton != null)
        {
            toggleUIButton.onClick.RemoveListener(ToggleAerialCamera);
            toggleUIButton.onClick.AddListener(ToggleAerialCamera);
        }

        if (uiToggle != null)
        {
            uiToggle.onValueChanged.RemoveListener(SetAerialCameraEnabled);
            uiToggle.isOn = enableAerialCamera;
            uiToggle.onValueChanged.AddListener(SetAerialCameraEnabled);
        }
    }

    public void ToggleAerialCamera()
    {
        SetAerialCameraEnabled(!enableAerialCamera);
    }

    public void SetAerialCameraEnabled(bool enabled)
    {
        enableAerialCamera = enabled;
        SavePlayerPreferences();
        UpdateUIStatusDisplay();

        Debug.Log($"<color={(enableAerialCamera ? "#00FF88" : "#FF9900")}><b>[CoasterAerialCameraController] Chế độ Flycam toàn cảnh: {(enableAerialCamera ? "ĐÃ BẬT (ON)" : "ĐÃ TẮT (OFF)")}</b></color>");

        if (!enableAerialCamera && (currentState == AerialCameraState.FlyingUp || currentState == AerialCameraState.StationaryAerial))
        {
            TriggerEarlyReturnToCockpit();
        }
    }

    private void UpdateUIStatusDisplay()
    {
        if (uiToggle != null && uiToggle.isOn != enableAerialCamera)
        {
            uiToggle.SetIsOnWithoutNotify(enableAerialCamera);
        }

        if (statusTMPText != null)
        {
            statusTMPText.text = enableAerialCamera ? "Flycam: BẬT" : "Flycam: TẮT";
            statusTMPText.color = enableAerialCamera ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.4f, 0.4f);
        }
    }

    private void HandleKeyboardToggle()
    {
        if (Keyboard.current == null) return;

        bool pressedToggle = (toggleKey != Key.None && Keyboard.current[toggleKey].wasPressedThisFrame)
                          || (alternateToggleKey != Key.None && Keyboard.current[alternateToggleKey].wasPressedThisFrame);

        if (pressedToggle)
        {
            ToggleAerialCamera();
        }
    }

    /// <summary>
    /// Kiểm tra các mốc thời gian để kích hoạt bay lên / bay xuống (hỗ trợ hoàn hảo mọi lần chơi tiếp theo)
    /// </summary>
    private void CheckAerialTriggers()
    {
        if (rideController == null || rideController.currentState != RideController.RideState.Riding)
        {
            if (currentState != AerialCameraState.CockpitSeated)
            {
                ResetToCockpit();
            }
            return;
        }

        // Đọc tiến trình thời gian của vòng chạy
        float progress = rideController.CurrentTraveledTime;
        float clipLength = rideController.ClipLength > 0f ? rideController.ClipLength : 90f;
        int currentLap = Mathf.FloorToInt(progress / clipLength);
        float currentLoopTime = progress % clipLength;

        // 1. TỰ ĐỘNG PHÁT HIỆN KHI BẮT ĐẦU VÒNG CHẠY MỚI HOẶC CHƠI LẠI
        if (currentLap != lastProcessedLap || progress < lastTraveledProgress || progress < 0.8f)
        {
            lastProcessedLap = currentLap;
            if (aerialSegments != null)
            {
                foreach (var seg in aerialSegments)
                {
                    if (seg != null) seg.hasTriggeredThisLap = false;
                }
            }
        }
        lastTraveledProgress = progress;

        // Nếu tính năng đang TẮT -> không kích hoạt bay lên
        if (!enableAerialCamera) return;

        if (aerialSegments == null || aerialSegments.Count == 0) return;

        // 2. KIỂM TRA KÍCH HOẠT BAY LÊN (Pha 1)
        if (currentState == AerialCameraState.CockpitSeated)
        {
            for (int i = 0; i < aerialSegments.Count; i++)
            {
                AerialShotSegment seg = aerialSegments[i];
                if (seg == null || seg.aerialPoint == null || seg.hasTriggeredThisLap) continue;

                if (seg.targetLap >= 0 && seg.targetLap != currentLap) continue;

                if (currentLoopTime >= seg.startLoopTime && currentLoopTime < seg.endLoopTime)
                {
                    seg.hasTriggeredThisLap = true;
                    StartFlyUpSequence(i);
                    break;
                }
            }
        }
        // 3. KIỂM TRA KÍCH HOẠT BAY XUỐNG ĐUỔI THEO TÀU (Pha 3)
        else if (currentState == AerialCameraState.StationaryAerial)
        {
            if (activeSegmentIndex >= 0 && activeSegmentIndex < aerialSegments.Count)
            {
                AerialShotSegment currentSeg = aerialSegments[activeSegmentIndex];
                if (currentLoopTime >= currentSeg.endLoopTime || currentLoopTime < currentSeg.startLoopTime - 1.0f)
                {
                    StartChaseDownSequence();
                }
            }
        }
    }

    private void StartFlyUpSequence(int segmentIndex)
    {
        ResolveReferences();
        if (xrOriginRig == null) return;

        activeSegmentIndex = segmentIndex;
        AerialShotSegment seg = aerialSegments[segmentIndex];

        flightStartWorldPos = xrOriginRig.transform.position;
        flightStartWorldRot = xrOriginRig.transform.rotation;
        xrOriginRig.transform.SetParent(null);
        xrOriginRig.transform.localScale = Vector3.one;

        // Bật Mesh cho Avatar người chơi trên tàu
        SetPlayerAvatarVisibleFromAerial(true);

        flightElapsedTime = 0f;
        currentFlightDuration = Mathf.Max(0.5f, seg.transitionDuration);
        currentState = AerialCameraState.FlyingUp;

        Debug.Log($"<color=#00FFFF><b>[CoasterAerialCameraController] PHA 1: Bắt đầu tách tàu và bay lên '{seg.segmentName}' ({currentFlightDuration:F1}s)...</b></color>");
    }

    private void StartChaseDownSequence()
    {
        if (xrOriginRig == null) return;

        flightStartWorldPos = xrOriginRig.transform.position;
        flightStartWorldRot = xrOriginRig.transform.rotation;
        flightElapsedTime = 0f;

        float dur = 1.8f;
        if (activeSegmentIndex >= 0 && activeSegmentIndex < aerialSegments.Count)
        {
            dur = aerialSegments[activeSegmentIndex].transitionDuration;
        }
        currentFlightDuration = Mathf.Max(0.5f, dur);
        currentState = AerialCameraState.ChasingDown;

        Debug.Log($"<color=#FFCC00><b>[CoasterAerialCameraController] PHA 3: Hết đoạn mạo hiểm -> Đang bay lao xuống đuổi theo tàu ({currentFlightDuration:F1}s)...</b></color>");
    }

    private void UpdateCameraFlightMovement()
    {
        if (xrOriginRig == null) return;

        switch (currentState)
        {
            case AerialCameraState.FlyingUp:
                UpdateFlyingUp();
                break;

            case AerialCameraState.StationaryAerial:
                UpdateStationaryAerial();
                break;

            case AerialCameraState.ChasingDown:
                UpdateChasingDown();
                break;

            case AerialCameraState.Docking:
                FinalizeDocking();
                break;

            case AerialCameraState.CockpitSeated:
            default:
                break;
        }
    }

    private void UpdateFlyingUp()
    {
        if (activeSegmentIndex < 0 || activeSegmentIndex >= aerialSegments.Count)
        {
            currentState = AerialCameraState.StationaryAerial;
            return;
        }

        AerialShotSegment seg = aerialSegments[activeSegmentIndex];
        if (seg.aerialPoint == null)
        {
            currentState = AerialCameraState.StationaryAerial;
            return;
        }

        flightElapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(flightElapsedTime / currentFlightDuration);
        float smoothT = Mathf.SmoothStep(0f, 1f, progress);

        xrOriginRig.transform.position = Vector3.Lerp(flightStartWorldPos, seg.aerialPoint.position, smoothT);
        xrOriginRig.transform.rotation = Quaternion.Slerp(flightStartWorldRot, seg.aerialPoint.rotation, smoothT);

        if (progress >= 1.0f)
        {
            xrOriginRig.transform.position = seg.aerialPoint.position;
            xrOriginRig.transform.rotation = seg.aerialPoint.rotation;
            currentState = AerialCameraState.StationaryAerial;
            Debug.Log($"<color=#00FF88><b>[CoasterAerialCameraController] PHA 2: Đã đến '{seg.segmentName}' -> Đứng yên ngắm cảnh (Bám tàu: {seg.trackCoasterTarget})!</b></color>");
        }
    }

    private void UpdateStationaryAerial()
    {
        if (activeSegmentIndex < 0 || activeSegmentIndex >= aerialSegments.Count) return;

        AerialShotSegment seg = aerialSegments[activeSegmentIndex];
        if (seg.aerialPoint == null) return;

        // Vị trí giữ cố định 100% tại điểm trên cao
        xrOriginRig.transform.position = seg.aerialPoint.position;

        // Đoạn 1: Tự động xoay êm theo tâm tàu (trackCoasterTarget == true)
        // Đoạn 2: Tĩnh 100%, giữ nguyên góc quay chuẩn của aerialPoint (trackCoasterTarget == false)
        if (seg.trackCoasterTarget && realCartTransform != null)
        {
            Vector3 dirToCart = (realCartTransform.position - xrOriginRig.transform.position).normalized;
            if (dirToCart.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToCart, Vector3.up);
                xrOriginRig.transform.rotation = Quaternion.Slerp(xrOriginRig.transform.rotation, targetRot, Time.deltaTime * seg.trackingDamping);
            }
        }
        else
        {
            xrOriginRig.transform.rotation = seg.aerialPoint.rotation;
        }
    }

    private void UpdateChasingDown()
    {
        flightElapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(flightElapsedTime / currentFlightDuration);
        float smoothT = Mathf.SmoothStep(0f, 1f, progress);

        Transform targetSitPoint = GetActivePlayerSitPoint();
        Vector3 targetEyeWorldPos = GetTargetSeatedEyeWorldPos(targetSitPoint);
        Quaternion targetEyeWorldRot = (targetSitPoint != null) ? targetSitPoint.rotation : (realCartTransform != null ? realCartTransform.rotation : Quaternion.identity);

        xrOriginRig.transform.position = Vector3.Lerp(flightStartWorldPos, targetEyeWorldPos, smoothT);
        xrOriginRig.transform.rotation = Quaternion.Slerp(flightStartWorldRot, targetEyeWorldRot, smoothT);

        float remainingDistance = Vector3.Distance(xrOriginRig.transform.position, targetEyeWorldPos);

        if (progress >= 1.0f || remainingDistance < 0.15f)
        {
            currentState = AerialCameraState.Docking;
        }
    }

    private void FinalizeDocking()
    {
        Transform targetSitPoint = GetActivePlayerSitPoint();
        if (targetSitPoint == null && realCartTransform != null) targetSitPoint = realCartTransform;

        if (xrOriginRig != null && targetSitPoint != null)
        {
            CharacterController cc = xrOriginRig.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            xrOriginRig.transform.SetParent(targetSitPoint);
            xrOriginRig.transform.localRotation = Quaternion.identity;
            xrOriginRig.transform.localScale = Vector3.one;

            PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
            Vector3 seatedEyePos = (playerBody != null) ? playerBody.GetSeatedEyePosition(targetSitPoint) : targetSitPoint.position + (targetSitPoint.up * 0.85f);

            Camera cam = xrOriginRig.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cam.nearClipPlane = 0.035f;
                Vector3 camOffset = cam.transform.position - xrOriginRig.transform.position;
                xrOriginRig.transform.position = seatedEyePos - camOffset;
            }
            else
            {
                xrOriginRig.transform.position = seatedEyePos - (Vector3.up * 1.36f);
            }

            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();

            SetPlayerAvatarVisibleFromAerial(false);

            MouseLook mouseLook = xrOriginRig.GetComponentInChildren<MouseLook>();
            if (mouseLook != null)
            {
                mouseLook.enabled = true;
                mouseLook.ResetLook(Quaternion.identity);
            }
        }

        currentState = AerialCameraState.CockpitSeated;
        activeSegmentIndex = -1;

        Debug.Log("<color=#00FF88><b>[CoasterAerialCameraController] PHA 4: Đã gắn lại vào buồng lái an toàn! Tiếp tục chạy góc nhìn người lái.</b></color>");
    }

    private void TriggerEarlyReturnToCockpit()
    {
        if (currentState == AerialCameraState.FlyingUp || currentState == AerialCameraState.StationaryAerial)
        {
            StartChaseDownSequence();
        }
    }

    public void ResetToCockpit()
    {
        StopAllCoroutines();
        currentState = AerialCameraState.CockpitSeated;
        activeSegmentIndex = -1;
        SetPlayerAvatarVisibleFromAerial(false);
    }

    private Transform GetActivePlayerSitPoint()
    {
        if (seatSwitcher != null && seatSwitcher.passengerManager != null)
        {
            PlayerNPCBodyController pBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
            if (pBody != null && pBody.currentNPCBody != null && pBody.currentNPCBody.transform.parent != null)
            {
                return pBody.currentNPCBody.transform.parent;
            }
        }

        if (seatSwitcher != null && seatSwitcher.seats != null && seatSwitcher.seats.Length > 0)
        {
            return seatSwitcher.seats[0];
        }

        return realCartTransform;
    }

    private Vector3 GetTargetSeatedEyeWorldPos(Transform sitPoint)
    {
        if (sitPoint == null)
        {
            return realCartTransform != null ? realCartTransform.position + Vector3.up * 1.2f : transform.position;
        }

        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        if (playerBody != null)
        {
            return playerBody.GetSeatedEyePosition(sitPoint);
        }

        return sitPoint.position + (sitPoint.up * 0.85f * 1.22f) + (sitPoint.forward * 0.08f);
    }

    private void SetPlayerAvatarVisibleFromAerial(bool isAerial)
    {
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        if (playerBody != null)
        {
            playerBody.SetAerialViewActive(isAerial);
        }
    }

    [ContextMenu("Tự Động Tạo / Căn Chỉnh Lại 2 Điểm Quan Sát Mẫu")]
    public void CreateDefaultPointsInScene()
    {
        EnsureDefaultAerialSegments();
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
        Debug.Log("<color=#00FF88><b>[CoasterAerialCameraController] Đã khởi tạo và căn chỉnh 2 điểm mốc quan sát trên cao!</b></color>");
    }

#if UNITY_EDITOR
    [ContextMenu("Gán Góc Nhìn Hiện Tại Của Scene Cho Đoạn 1 (Start)")]
    public void AlignPoint1ToSceneView()
    {
        if (SceneView.lastActiveSceneView != null)
        {
            Camera scCam = SceneView.lastActiveSceneView.camera;
            if (scCam != null && aerialSegments.Count > 0 && aerialSegments[0].aerialPoint != null)
            {
                Undo.RecordObject(aerialSegments[0].aerialPoint, "Align Point 1 to Scene View");
                aerialSegments[0].aerialPoint.position = scCam.transform.position;
                aerialSegments[0].aerialPoint.rotation = scCam.transform.rotation;
                EditorUtility.SetDirty(aerialSegments[0].aerialPoint);
                Debug.Log($"<color=#00FF88><b>[CoasterAerialCameraController] Đã gán góc nhìn Scene cho Đoạn 1: {scCam.transform.position}</b></color>");
            }
        }
    }

    [ContextMenu("Gán Góc Nhìn Hiện Tại Của Scene Cho Đoạn 2 (End)")]
    public void AlignPoint2ToSceneView()
    {
        if (SceneView.lastActiveSceneView != null)
        {
            Camera scCam = SceneView.lastActiveSceneView.camera;
            if (scCam != null && aerialSegments.Count > 1 && aerialSegments[1].aerialPoint != null)
            {
                Undo.RecordObject(aerialSegments[1].aerialPoint, "Align Point 2 to Scene View");
                aerialSegments[1].aerialPoint.position = scCam.transform.position;
                aerialSegments[1].aerialPoint.rotation = scCam.transform.rotation;
                EditorUtility.SetDirty(aerialSegments[1].aerialPoint);
                Debug.Log($"<color=#00FF88><b>[CoasterAerialCameraController] Đã gán góc nhìn Scene cho Đoạn 2: {scCam.transform.position}</b></color>");
            }
        }
    }
#endif

    private void OnDrawGizmosSelected()
    {
        if (aerialSegments == null) return;

        for (int i = 0; i < aerialSegments.Count; i++)
        {
            var seg = aerialSegments[i];
            if (seg != null && seg.aerialPoint != null)
            {
                Gizmos.color = (i == 0) ? Color.cyan : Color.magenta;
                Gizmos.DrawWireSphere(seg.aerialPoint.position, 1.2f);
                Gizmos.DrawRay(seg.aerialPoint.position, seg.aerialPoint.forward * 5.0f);
            }
        }
    }
}
