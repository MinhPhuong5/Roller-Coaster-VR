using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class SeatSwitcher : MonoBehaviour
{
    [Header("VR Rig / Người Chơi")]
    [Tooltip("Kéo toàn bộ cụm XR Origin (XR Rig) vào đây")]
    public GameObject xrOriginRig;

    [Header("XR Controllers (Ẩn khi tàu chạy)")]
    public GameObject leftController;
    public GameObject rightController;

    [Header("Chế Độ Xoay Chuột")]
    [Tooltip("Kéo MouseLook trên Main Camera của XR Origin vào đây")]
    public MouseLook mouseLook;

    [Header("Ghế trong tàu")]
    public Transform[] seats;
    private int currentSeatIndex = 0;

    [Header("Ride Controller")]
    public RideController rideController;

    [Header("Hành Khách & Body Người Chơi")]
    public CoasterPassengerManager passengerManager;

    [Header("Rào Chắn Ga")]
    [Tooltip("Thanh chắn ga chính (hoặc kéo nhiều thanh chắn vào mảng stationGates)")]
    public StationGateController stationGate;
    [Tooltip("Danh sách các thanh chắn nếu ga có nhiều làn (nếu để trống script tự tìm toàn bộ thanh chắn)")]
    public StationGateController[] stationGates;

    [Header("UI Sảnh")]
    [Tooltip("Kéo RideUIPanel vào đây")]
    public GameObject uiPanel;
    [Tooltip("Kéo SelectSeatGroup vào đây")]
    public GameObject selectSeatGroup;
    [Tooltip("Kéo GameOverGroup vào đây (hỏi chơi tiếp)")]
    public GameObject gameOverPanel;
    public GameObject startButton;

    [Header("Tùy Chọn Đếm Ngược")]
    public bool useCountdownText = true;
    public bool useCountdownAudio = true;

    [Header("Đếm Ngược Bắt Đầu")]
    public TextMeshProUGUI countdownText;
    public int countdownSeconds = 3;

    [Header("Âm Thanh Đếm Ngược")]
    public AudioSource countdownAudio;

    [Header("Thời Gian Chờ Mở Rào (Giây)")]
    public float gateOpenDelay = 6.0f;

    [Header("Điểm Sàn Ga Tàu (VR_FloorPoint)")]
    [Tooltip("Kéo VR_FloorPoint ở sảnh ga vào đây")]
    public Transform stationFloorPoint;

    [Header("Điểm Thoát Về Map Công Viên")]
    [Tooltip("Kéo CoasterReturnMapPoint ở ngoài đường dạo công viên vào đây")]
    public Transform parkReturnPoint;

    private bool isRiding = false;
    private bool isHandlingExit = false;
    private bool isGateAlreadyClosed = false;

    private Transform mainCameraTransform;
    private Transform originalCamParent;
    private float standingEyeHeight = 1.45f;
    private XRFallbackWalkController walkController;

    void Awake()
    {
        CacheInitialCameraRig();
    }

    void Start()
    {
        CacheInitialCameraRig();
        HideUI();

        if (startButton != null) startButton.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (stationFloorPoint == null)
        {
            GameObject floor = GameObject.Find("VR_FloorPoint");
            if (floor != null) stationFloorPoint = floor.transform;
        }

        if (rideController == null)
        {
            rideController = Object.FindAnyObjectByType<RideController>();
        }

        if (passengerManager == null)
        {
            passengerManager = GetComponent<CoasterPassengerManager>();
            if (passengerManager == null) passengerManager = GetComponentInParent<CoasterPassengerManager>();
            if (passengerManager == null) passengerManager = Object.FindAnyObjectByType<CoasterPassengerManager>();
            if (passengerManager == null)
            {
                passengerManager = gameObject.AddComponent<CoasterPassengerManager>();
            }
        }

        // Tự động tìm và gom toàn bộ thanh chắn trong Scene (kể cả các bản sao mới tạo)
        if (stationGates == null || stationGates.Length == 0)
        {
            stationGates = Object.FindObjectsByType<StationGateController>(FindObjectsSortMode.None);
        }

        if (mouseLook != null)
        {
            mouseLook.enabled = false;
        }

        if (walkController != null)
        {
            walkController.FindAndCacheWalkZones(true);
            walkController.enabled = true;
        }
    }

    void Update()
    {
        if (rideController != null && rideController.currentState == RideController.RideState.WaitingAtStation)
        {
            if (!isRiding && !isHandlingExit)
            {
                // New Input System: Phím Tab để đổi ghế nhanh trên PC
                if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    NextSeat();
                }

                // New Input System: Giữ chuột phải để lia góc nhìn ngắm cảnh sảnh ga trên PC
                if (Mouse.current != null)
                {
                    if (Mouse.current.rightButton.wasPressedThisFrame)
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                    else if (Mouse.current.rightButton.wasReleasedThisFrame)
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                }
            }
        }
    }

    private void CacheInitialCameraRig()
    {
        if (xrOriginRig == null)
        {
            xrOriginRig = GameObject.Find("XR Origin (XR Rig)");
            if (xrOriginRig == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) xrOriginRig = player;
            }
        }

        if (xrOriginRig != null)
        {
            if (walkController == null)
            {
                walkController = xrOriginRig.GetComponent<XRFallbackWalkController>();
            }

            if (mainCameraTransform == null)
            {
                Camera cam = xrOriginRig.GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    mainCameraTransform = cam.transform;
                    originalCamParent = mainCameraTransform.parent;

                    if (originalCamParent != null && originalCamParent != xrOriginRig.transform)
                    {
                        standingEyeHeight = originalCamParent.localPosition.y > 0.5f ? originalCamParent.localPosition.y : 1.45f;
                    }
                    else
                    {
                        standingEyeHeight = mainCameraTransform.localPosition.y > 0.5f ? mainCameraTransform.localPosition.y : 1.45f;
                    }

                    if (mouseLook == null)
                    {
                        mouseLook = cam.GetComponent<MouseLook>();
                    }
                }
            }
        }
    }

    public void NextSeat()
    {
        if (seats == null || seats.Length == 0) return;
        currentSeatIndex = (currentSeatIndex + 1) % seats.Length;
        SelectSeatFromUI(currentSeatIndex);
    }

    public void SelectSeatFromUI(int index)
    {
        if (isRiding || isHandlingExit) return;
        SwitchSeat(index);
        StartRideProcess();
    }

    public void SwitchSeat(int index)
    {
        if (seats == null || seats.Length == 0) return;
        CacheInitialCameraRig();

        currentSeatIndex = index;

        if (walkController != null) walkController.enabled = false;

        Transform targetSitPoint = (passengerManager != null) ? passengerManager.GetSitPoint(index) : seats[index];
        Vector3 seatedEyePos = (passengerManager != null) ? passengerManager.GetPlayerHeadEyePosition(index) : (targetSitPoint.position + targetSitPoint.up * 0.95f + targetSitPoint.forward * 0.08f);

        // Căn XR Origin sao cho Camera của người chơi rơi chính xác vào tầm mắt khi ngồi trong khoang tàu
        if (xrOriginRig != null)
        {
            CharacterController cc = xrOriginRig.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            xrOriginRig.transform.SetParent(targetSitPoint);
            xrOriginRig.transform.localRotation = Quaternion.identity;
            xrOriginRig.transform.localScale = Vector3.one;

            if (mainCameraTransform != null)
            {
                Camera cam = mainCameraTransform.GetComponent<Camera>();
                if (cam != null) cam.nearClipPlane = 0.035f;

                // Triệt tiêu chiều cao đứng của XR Origin để đặt Camera vào đúng vị trí mắt khi ngồi
                Vector3 cameraOffsetFromRig = mainCameraTransform.position - xrOriginRig.transform.position;
                xrOriginRig.transform.position = seatedEyePos - cameraOffsetFromRig;
            }
            else
            {
                xrOriginRig.transform.position = seatedEyePos - (Vector3.up * 1.36f);
            }

            if (passengerManager != null)
            {
                passengerManager.SetPlayerPassenger(index, xrOriginRig.transform);
            }

            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
        }
        else if (mainCameraTransform != null)
        {
            mainCameraTransform.SetParent(targetSitPoint);
            mainCameraTransform.position = seatedEyePos;
            mainCameraTransform.localRotation = targetSitPoint.rotation;

            Camera cam = mainCameraTransform.GetComponent<Camera>();
            if (cam != null) cam.nearClipPlane = 0.035f;

            if (passengerManager != null)
            {
                passengerManager.SetPlayerPassenger(index, targetSitPoint);
            }
        }

        // Bật xoay góc nhìn chuột trong khoang tàu
        if (mouseLook != null)
        {
            mouseLook.enabled = true;
            mouseLook.ResetLook(Quaternion.identity);
        }

        EnterSeatedMode();
    }

    public void HideUI()
    {
        if (uiPanel != null) uiPanel.SetActive(false);
        if (selectSeatGroup != null) selectSeatGroup.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (startButton != null) startButton.SetActive(false);
    }

    public void StartRideProcess()
    {
        HideUI();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        StartCoroutine(StartRideSequence());
    }

    public void EnterBoardingMode()
    {
        if (isHandlingExit) return;
        ForceBoardingMode();
    }

    /// <summary>
    /// Đưa người chơi ra sàn ga và phục hồi chiều cao đứng chuẩn nhìn thẳng vào bảng chọn ghế
    /// </summary>
    public void ForceBoardingMode()
    {
        if (passengerManager != null)
        {
            passengerManager.ReleasePlayerPassenger();
            passengerManager.ClearOldPassengers();
        }

        RestoreStandingPlayer(stationFloorPoint);

        // Mở chuột tự do để chọn ghế
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Bật bảng chọn ghế, ẩn bảng kết thúc
        if (uiPanel != null) uiPanel.SetActive(true);
        if (selectSeatGroup != null) selectSeatGroup.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (startButton != null) startButton.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        if (rideController != null)
        {
            rideController.ResetToStation();
        }
    }

    public void EnterSeatedMode() { }

    public void TriggerEarlyGateClose()
    {
        if (!isGateAlreadyClosed)
        {
            isGateAlreadyClosed = true;
            if (stationGates != null && stationGates.Length > 0)
            {
                foreach (var gate in stationGates)
                {
                    if (gate != null) gate.CloseGate();
                }
            }
            else if (stationGate != null)
            {
                stationGate.CloseGate();
            }
        }
    }

    public void ExitCar()
    {
        if (!isHandlingExit)
        {
            StartCoroutine(HandleRideEndSequence());
        }
    }

    private IEnumerator StartRideSequence()
    {
        isGateAlreadyClosed = false;

        // Mở toàn bộ các thanh chắn trong ga
        if (stationGates != null && stationGates.Length > 0)
        {
            foreach (var gate in stationGates)
            {
                if (gate != null) gate.OpenGate();
            }
        }
        else if (stationGate != null)
        {
            stationGate.OpenGate();
        }

        bool hasCountdown = useCountdownText || useCountdownAudio;

        if (hasCountdown)
        {
            float waitBeforeCountdown = Mathf.Max(0f, gateOpenDelay - countdownSeconds);
            if (waitBeforeCountdown > 0f)
            {
                yield return new WaitForSeconds(waitBeforeCountdown);
            }

            if (useCountdownAudio && countdownAudio != null)
            {
                countdownAudio.Play();
            }

            if (useCountdownText && countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
                for (int i = countdownSeconds; i > 0; i--)
                {
                    countdownText.text = i.ToString();
                    yield return new WaitForSeconds(1.0f);
                }
                countdownText.text = "GO!";
                yield return new WaitForSeconds(0.6f);
                countdownText.gameObject.SetActive(false);
            }
            else
            {
                yield return new WaitForSeconds(Mathf.Min(gateOpenDelay, (float)countdownSeconds));
            }
        }
        else
        {
            yield return new WaitForSeconds(gateOpenDelay);
        }

        SetControllersActive(false);

        if (rideController != null)
        {
            rideController.StartRide();
        }

        isRiding = true;
        isHandlingExit = false;
    }

    /// <summary>
    /// Chờ tàu phanh đỗ hẳn vào bến rồi mới tháo người chơi ra sàn và hiện GameOverGroup
    /// </summary>
    private IEnumerator HandleRideEndSequence()
    {
        isHandlingExit = true;

        // Chờ đủ thời gian để tàu phanh từ từ về bến dừng hẳn
        yield return new WaitForSeconds(3.5f);

        if (stationGates != null && stationGates.Length > 0)
        {
            foreach (var gate in stationGates)
            {
                if (gate != null && gate.gateAudioSource != null) gate.gateAudioSource.Stop();
            }
        }
        else if (stationGate != null && stationGate.gateAudioSource != null)
        {
            stationGate.gateAudioSource.Stop();
        }

        if (countdownAudio != null && countdownAudio.isPlaying)
        {
            countdownAudio.Stop();
        }

        isRiding = false;
        isHandlingExit = false;

        // 1. Tháo người chơi và đưa toàn bộ 4 NPC ra đứng tại sảnh ga
        if (passengerManager != null)
        {
            passengerManager.UnseatAllPassengersToStation(stationFloorPoint);
        }

        // 2. Đưa người chơi ra đứng ở VR_FloorPoint với chiều cao mắt đứng chuẩn
        RestoreStandingPlayer(stationFloorPoint);

        // 3. Mở chuột tự do
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 4. Hiện bảng hỏi chơi lại
        if (uiPanel != null) uiPanel.SetActive(true);
        if (selectSeatGroup != null) selectSeatGroup.SetActive(false);
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            Debug.Log("[SeatSwitcher] Đã dừng hẳn tại ga -> Đưa toàn bộ NPC ra sảnh ga và bật GameOverGroup!");
        }
    }

    private void RestoreStandingPlayer(Transform targetSpawnPoint)
    {
        CacheInitialCameraRig();

        // 1. Tháo XR Origin ra khỏi tàu và đưa về vị trí sàn
        if (xrOriginRig != null)
        {
            CharacterController cc = xrOriginRig.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            xrOriginRig.transform.SetParent(null);
            xrOriginRig.transform.localScale = Vector3.one;

            if (targetSpawnPoint != null)
            {
                Vector3 spawnPos = targetSpawnPoint.position;
                RaycastHit hit;
                if (Physics.Raycast(spawnPos + Vector3.up * 1.5f, Vector3.down, out hit, 25.0f, ~0, QueryTriggerInteraction.Ignore))
                {
                    spawnPos = hit.point + Vector3.up * 0.02f;
                }
                xrOriginRig.transform.SetPositionAndRotation(spawnPos, targetSpawnPoint.rotation);
            }

            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
        }

        // 2. Trả Main Camera về cha ban đầu và ĐẶT ĐÚNG TẦM MẮT NGƯỜI ĐỨNG (1.45m)
        if (mainCameraTransform != null)
        {
            if (originalCamParent != null && mainCameraTransform.parent != originalCamParent)
            {
                mainCameraTransform.SetParent(originalCamParent);
            }

            if (originalCamParent != null && originalCamParent != xrOriginRig.transform)
            {
                // Nếu có Camera Offset: đặt Camera Offset ở độ cao mắt và Camera ở (0,0,0)
                originalCamParent.localPosition = new Vector3(0f, standingEyeHeight, 0f);
                originalCamParent.localRotation = Quaternion.identity;
                mainCameraTransform.localPosition = Vector3.zero;
            }
            else
            {
                // Nếu Camera gắn trực tiếp vào XR Origin: đặt Camera ở độ cao mắt
                mainCameraTransform.localPosition = new Vector3(0f, standingEyeHeight, 0f);
            }

            mainCameraTransform.localRotation = Quaternion.identity;
        }

        // 3. Tắt xoay góc nhìn chuột trong tàu, bật bộ điều khiển đi bộ tự do
        if (mouseLook != null)
        {
            mouseLook.enabled = false;
        }

        if (walkController != null)
        {
            walkController.FindAndCacheWalkZones(true);
            walkController.enabled = true;
            walkController.InitCameraAngles();
        }

        SetControllersActive(true);
    }

    private void SetControllersActive(bool isActive)
    {
        if (leftController != null) leftController.SetActive(isActive);
        if (rightController != null) rightController.SetActive(isActive);
    }

    /// <summary>
    /// Thoát khỏi tàu lượn và dịch chuyển trở về đường dạo công viên
    /// </summary>
    public void ReturnToParkMap()
    {
        Time.timeScale = 1f;

        if (passengerManager != null)
        {
            passengerManager.ReleasePlayerPassenger();
            passengerManager.ClearOldPassengers();
        }

        RestoreStandingPlayer(parkReturnPoint);

        HideUI();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}