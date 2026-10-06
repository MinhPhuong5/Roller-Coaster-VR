using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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
    [Tooltip("Tự động ẩn hoàn toàn tay cầm và tia laser trắng khi chơi trên PC / Desktop (không cắm kính VR)")]
    public bool autoHideControllersOnDesktop = true;
    [Tooltip("Ẩn hoàn toàn tia laser trắng của tay cầm (chỉ giữ lại chuột trên PC và tương tác trên VR)")]
    public bool hideControllerRays = true;

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

    [Header("Flycam Toàn Cảnh")]
    public CoasterAerialCameraController aerialCameraController;

    [Header("UI Sảnh")]
    [Tooltip("Kéo RideUIPanel vào đây")]
    public GameObject uiPanel;
    [Tooltip("Kéo SelectSeatGroup vào đây")]
    public GameObject selectSeatGroup;
    [Tooltip("Kéo GameOverGroup vào đây (hỏi chơi tiếp)")]
    public GameObject gameOverPanel;
    public GameObject startButton;

    public int CurrentSeatIndex => currentSeatIndex;

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

    [Header("Điểm Lưu Vị Trí Công Viên Trước Khi Vào Ga")]
    public Vector3 savedParkPosition;
    public Quaternion savedParkRotation;
    public bool hasSavedParkPosition = false;

    private Vector3 defaultParkSpawnPosition = new Vector3(1.85f, 0.25f, 44.05f);
    private Quaternion defaultParkSpawnRotation = Quaternion.Euler(0f, 180f, 0f);
    private bool hasDefaultSpawn = false;

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

        // Lưu vị trí đứng ban đầu của người chơi ở công viên
        if (xrOriginRig != null && xrOriginRig.transform.position.y < 4.5f)
        {
            defaultParkSpawnPosition = xrOriginRig.transform.position;
            defaultParkSpawnRotation = xrOriginRig.transform.rotation;
            hasDefaultSpawn = true;
        }

        if (stationFloorPoint == null)
        {
            GameObject floor = GameObject.Find("VR_FloorPoint");
            if (floor != null) stationFloorPoint = floor.transform;
        }

        GetOrCreateParkReturnPoint();

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

        if (aerialCameraController == null)
        {
            aerialCameraController = Object.FindAnyObjectByType<CoasterAerialCameraController>();
            if (aerialCameraController == null && rideController != null)
            {
                aerialCameraController = rideController.gameObject.AddComponent<CoasterAerialCameraController>();
            }
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

                    PlayerNPCBodyController playerBodyCtrl = xrOriginRig.GetComponent<PlayerNPCBodyController>();
                    if (playerBodyCtrl != null)
                    {
                        standingEyeHeight = playerBodyCtrl.GetPlayerStandingEyeHeight();
                    }
                    else if (originalCamParent != null && originalCamParent != xrOriginRig.transform)
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

            if (leftController == null || rightController == null)
            {
                Transform camOffset = xrOriginRig.transform.Find("Camera Offset");
                if (camOffset != null)
                {
                    if (leftController == null)
                    {
                        Transform lc = camOffset.Find("Left Controller");
                        if (lc != null) leftController = lc.gameObject;
                    }
                    if (rightController == null)
                    {
                        Transform rc = camOffset.Find("Right Controller");
                        if (rc != null) rightController = rc.gameObject;
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

        // 1. Gán người chơi vào ghế và sinh 3 NPC trước để các model được khởi tạo và nhận tư thế ngồi
        if (passengerManager != null)
        {
            passengerManager.SetPlayerPassenger(index, xrOriginRig != null ? xrOriginRig.transform : targetSitPoint);
        }

        PlayerNPCBodyController playerBody = (xrOriginRig != null) ? xrOriginRig.GetComponent<PlayerNPCBodyController>() : Object.FindAnyObjectByType<PlayerNPCBodyController>();
        Vector3 seatedEyePos = (playerBody != null) ? playerBody.GetSeatedEyePosition(targetSitPoint) : (passengerManager != null ? passengerManager.GetPlayerHeadEyePosition(index) : targetSitPoint.position + targetSitPoint.up * 0.85f);

        // 2. Căn XR Origin sao cho Camera của người chơi rơi chính xác vào tầm mắt khi ngồi trong khoang tàu
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
    /// Đưa người chơi ra sàn ga, sinh sẵn 3 NPC chờ tại ga và mở bảng chọn ghế
    /// </summary>
    public void ForceBoardingMode()
    {
        if (stationFloorPoint == null)
        {
            GameObject floor = GameObject.Find("VR_FloorPoint");
            if (floor != null) stationFloorPoint = floor.transform;
        }

        // 1. Phục hồi người chơi đứng ở sàn ga
        RestoreStandingPlayer(stationFloorPoint);

        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        if (playerBody != null)
        {
            playerBody.SetStationScale();
        }

        // 2. Dọn NPC cũ và sinh 3 NPC MỚI đứng sẵn tại nhà ga (chân chạm sàn 100%, dáng đứng Idle)
        if (passengerManager != null)
        {
            passengerManager.ReleasePlayerPassenger();
            passengerManager.ClearOldPassengers();
            passengerManager.SpawnWaitingStationNPCs(stationFloorPoint);
        }

        // 3. Mở chuột tự do để chọn ghế
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 4. Bật bảng chọn ghế, ẩn bảng kết thúc
        if (uiPanel != null)
        {
            PrepareCanvasForInteraction(uiPanel);
            uiPanel.SetActive(true);
        }
        if (selectSeatGroup != null) selectSeatGroup.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (startButton != null) startButton.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        if (rideController != null)
        {
            rideController.ResetToStation();
        }
    }

    /// <summary>
    /// Mở bảng chọn ghế khi người chơi bấm 'Có / Chơi tiếp' (Chỉ chuyển đổi UI sang chọn ghế, tuyệt đối KHÔNG dịch chuyển vị trí người chơi vì người chơi vốn dĩ đã đứng ở ga)
    /// </summary>
    public void OpenSelectSeatMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (uiPanel != null)
        {
            PrepareCanvasForInteraction(uiPanel);
            uiPanel.SetActive(true);
        }
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (selectSeatGroup != null) selectSeatGroup.SetActive(true);
        if (startButton != null) startButton.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        if (aerialCameraController != null)
        {
            aerialCameraController.OnRideStart();
        }

        if (rideController != null)
        {
            rideController.ResetToStation();
        }
    }

    public void EnterSeatedMode() { }

    public void OpenAllGates()
    {
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
    }

    public void CloseAllGates()
    {
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

    public bool AreAllGatesClosed()
    {
        if (stationGates != null && stationGates.Length > 0)
        {
            foreach (var gate in stationGates)
            {
                if (gate != null && !gate.IsFullyClosed()) return false;
            }
            return true;
        }
        else if (stationGate != null)
        {
            return stationGate.IsFullyClosed();
        }
        return true;
    }

    public void TriggerEarlyGateClose()
    {
        // Rào chắn ga nay chỉ đóng sau khi tàu đã về bến đỗ dừng hẳn an toàn
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

        // Mở toàn bộ các thanh chắn trong ga để tàu xuất phát
        OpenAllGates();

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

        if (aerialCameraController != null)
        {
            aerialCameraController.OnRideStart();
        }

        if (rideController != null)
        {
            rideController.StartRide();
        }

        isRiding = true;
        isHandlingExit = false;
    }

    /// <summary>
    /// Chờ tàu phanh đỗ hẳn vào bến an toàn -> Đóng thanh chắn hoàn toàn + Thở phào xong -> Mới cho phép thoát ra ngoài
    /// </summary>
    private IEnumerator HandleRideEndSequence()
    {
        isHandlingExit = true;

        // 1. Chờ tàu phanh từ từ về bến dừng hẳn an toàn (0.8s)
        yield return new WaitForSeconds(0.8f);

        // 2. TÀU ĐÃ VỀ GA VÀ DỪNG HẲN AN TOÀN -> Bắt đầu đóng thanh chắn ga
        CloseAllGates();

        // 3. ĐỒNG THỜI trong lúc thanh chắn đang hạ đóng: NPC và người chơi thở phào nhẹ nhõm (Phùuu..., Haizzz...)
        CoasterPassengerVoiceManager voiceMgr = (passengerManager != null) ? passengerManager.voiceManager : Object.FindAnyObjectByType<CoasterPassengerVoiceManager>();
        
        // Chờ 0.35s để các coroutine phát âm thanh thở phào bắt đầu kích hoạt
        yield return new WaitForSeconds(0.35f);

        // 4. CHỜ ĐỒNG THỜI:
        //    a) Thanh chắn phải hạ xuống ĐÓNG HOÀN TOÀN 100% (AreAllGatesClosed() == true)
        //    b) Cả 4 hành khách phải phát xong trọn vẹn 100% tiếng thở phào nhẹ nhõm (voiceMgr.IsReliefActive() == false)
        float waitTimer = 0f;
        while ((!AreAllGatesClosed() || (voiceMgr != null && voiceMgr.IsReliefActive())) && waitTimer < 8.0f)
        {
            waitTimer += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        // 5. Khoảng nghỉ tự nhiên sau khi thanh chắn ĐÃ ĐÓNG XONG 100% VÀ thở phào xong (0.5s) trước khi đưa người chơi ra sàn ga
        yield return new WaitForSeconds(0.5f);

        if (countdownAudio != null && countdownAudio.isPlaying)
        {
            countdownAudio.Stop();
        }

        isRiding = false;
        isHandlingExit = false;

        if (stationFloorPoint == null)
        {
            GameObject floor = GameObject.Find("VR_FloorPoint");
            if (floor != null) stationFloorPoint = floor.transform;
        }

        // 6. Đưa người chơi ra đứng ở VR_FloorPoint với chiều cao mắt đứng chuẩn (không tách avatar)
        RestoreStandingPlayer(stationFloorPoint);

        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        if (playerBody != null)
        {
            playerBody.SetStationScale();
        }

        // 7. NGAY KHI VỪA CHƠI XONG: Tháo người chơi, dọn 3 hành khách cũ trên tàu và sinh NGAY LẬP TỨC 3 NPC ngẫu nhiên mới đứng chờ tại sảnh ga (chân chạm sàn 100%, dáng đứng Idle)
        if (passengerManager != null)
        {
            passengerManager.ReleasePlayerPassenger();
            passengerManager.ClearOldPassengers();
            passengerManager.SpawnWaitingStationNPCs(stationFloorPoint);
        }

        // 8. Mở chuột tự do
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 9. Hiện bảng hỏi chơi lại (GameOverGroup)
        if (uiPanel != null)
        {
            PrepareCanvasForInteraction(uiPanel);
            uiPanel.SetActive(true);
        }
        if (selectSeatGroup != null) selectSeatGroup.SetActive(false);
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            Debug.Log("<color=#00FF88><b>[SeatSwitcher] Thanh chắn đã đóng 100% + thở phào xong -> Đã đưa người chơi ra sàn ga và hiện GameOverGroup!</b></color>");
        }
    }

    /// <summary>
    /// Chuẩn bị và bảo vệ Canvas tránh crash KeyNotFoundException của TrackedDeviceGraphicRaycaster
    /// </summary>
    public void PrepareCanvasForInteraction(GameObject panel)
    {
        if (panel == null) return;
        Canvas canvas = panel.GetComponent<Canvas>();
        if (canvas == null) canvas = panel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }

            GraphicRaycaster gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr == null) gr = canvas.gameObject.AddComponent<GraphicRaycaster>();
            gr.enabled = true;

            bool hasHMD = DesktopUIInputFallback.IsHMDConnected();
            var tr = canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            if (tr != null)
            {
                tr.enabled = hasHMD;
            }
        }
    }

    public void SetSavedParkPoint(Vector3 pos, Quaternion rot)
    {
        // Chỉ lưu nếu tọa độ hợp lệ và nằm ở ngoài công viên an toàn (không phải ở trong nhà ga trên cao)
        if (pos.y < 4.5f && (pos.z > 20.0f || pos.sqrMagnitude > 1.0f))
        {
            savedParkPosition = pos;
            savedParkRotation = rot;
            hasSavedParkPosition = true;
        }
    }

    /// <summary>
    /// Tìm điểm mặt sàn thật của công viên / nhà ga (loại bỏ ray tàu, thanh chắn, kiosk UI, trần nhà, NPC, người chơi)
    /// Đảm bảo chân nhân vật đặt chính xác 100% chạm sát mặt đất (hit.point.y)
    /// </summary>
    public static Vector3 FindSolidGroundPosition(Vector3 referencePos)
    {
        // 1. Bắn tia từ độ cao 1.5m trên đầu vị trí tham chiếu thẳng xuống dưới
        Vector3 rayStart = new Vector3(referencePos.x, referencePos.y + 1.5f, referencePos.z);
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 25.0f, ~0, QueryTriggerInteraction.Ignore);

        // Sắp xếp các điểm va chạm theo khoảng cách từ trên xuống dưới (gần rayStart nhất lên đầu)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            string colName = h.collider.gameObject.name.ToLower();
            string rootName = h.collider.transform.root != null ? h.collider.transform.root.name.ToLower() : "";

            // Bỏ qua người chơi, camera, NPC, đường ray, toa tàu, thanh chắn, UI, kiosk
            if (colName.Contains("player") || colName.Contains("origin") || colName.Contains("camera")
                || colName.Contains("npc") || colName.Contains("bot") || colName.Contains("people")
                || colName.Contains("body") || colName.Contains("character") || colName.Contains("track")
                || colName.Contains("rail") || colName.Contains("coaster") || colName.Contains("kiosk")
                || colName.Contains("cart") || colName.Contains("canvas") || colName.Contains("bar")
                || colName.Contains("gate") || rootName.Contains("coasterrig") || rootName.Contains("player")
                || rootName.Contains("origin") || rootName.Contains("npc"))
            {
                continue;
            }

            if (h.collider.GetComponentInParent<ParkNPCWanderer>() != null 
                || h.collider.GetComponentInParent<CoasterPassengerManager>() != null
                || h.collider.GetComponentInParent<PlayerNPCBodyController>() != null)
            {
                continue;
            }

            // Điểm mặt sàn đầu tiên bên dưới nhân vật -> Chân nhân vật chạm đất chính xác tại h.point.y
            return new Vector3(referencePos.x, h.point.y, referencePos.z);
        }

        // Fallback: Raycast đơn giản
        if (Physics.Raycast(referencePos + Vector3.up * 1.5f, Vector3.down, out RaycastHit singleHit, 15.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!singleHit.collider.isTrigger 
                && singleHit.collider.GetComponentInParent<ParkNPCWanderer>() == null
                && singleHit.collider.GetComponentInParent<CoasterPassengerManager>() == null
                && singleHit.collider.GetComponentInParent<PlayerNPCBodyController>() == null)
            {
                return new Vector3(referencePos.x, singleHit.point.y, referencePos.z);
            }
        }

        return referencePos;
    }

    public Transform GetOrCreateParkReturnPoint()
    {
        // 1. Tìm Object mốc quay về có sẵn trong Scene (CoasterReturrnMapPoint / ReturnMapPoint / CoasterParkReturnPoint)
        if (parkReturnPoint == null)
        {
            string[] possibleNames = new string[] 
            { 
                "CoasterReturrnMapPoint", 
                "CoasterReturnMapPoint", 
                "returnmappoint", 
                "ReturnMapPoint", 
                "CoasterParkReturnPoint" 
            };

            foreach (var name in possibleNames)
            {
                GameObject foundObj = GameObject.Find(name);
                if (foundObj != null)
                {
                    parkReturnPoint = foundObj.transform;
                    break;
                }
            }
        }

        Vector3 targetPos;
        Quaternion targetRot;

        // Ưu tiên cao nhất: Dùng chính xác Object điểm quay về (CoasterReturrnMapPoint) đặt trước UI
        if (parkReturnPoint != null)
        {
            targetPos = parkReturnPoint.position;
            targetRot = parkReturnPoint.rotation;
        }
        else if (hasSavedParkPosition && savedParkPosition.y < 4.5f && savedParkPosition.z > 18.0f)
        {
            targetPos = savedParkPosition;
            targetRot = savedParkRotation;
        }
        else
        {
            GameObject walkZone = GameObject.Find("WalkZone");
            if (walkZone != null)
            {
                targetPos = walkZone.transform.position;
                targetRot = walkZone.transform.rotation;
            }
            else
            {
                targetPos = defaultParkSpawnPosition;
                targetRot = defaultParkSpawnRotation;
            }
        }

        Vector3 groundPos = FindSolidGroundPosition(targetPos);
        if (parkReturnPoint == null)
        {
            GameObject returnObj = new GameObject("CoasterReturrnMapPoint");
            parkReturnPoint = returnObj.transform;
        }
        parkReturnPoint.position = groundPos;
        parkReturnPoint.rotation = targetRot;
        return parkReturnPoint;
    }

    private void RestoreStandingPlayer(Transform targetSpawnPoint)
    {
        CacheInitialCameraRig();

        if (targetSpawnPoint == null)
        {
            targetSpawnPoint = GetOrCreateParkReturnPoint();
            if (targetSpawnPoint == null)
            {
                if (stationFloorPoint != null) targetSpawnPoint = stationFloorPoint;
                else
                {
                    GameObject floor = GameObject.Find("VR_FloorPoint");
                    if (floor != null) targetSpawnPoint = floor.transform;
                }
            }
        }

        // 1. Lấy chiều cao mắt thực tế của NPC đại diện
        float eyeHeight = 1.50f;
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        if (playerBody != null)
        {
            eyeHeight = playerBody.GetPlayerStandingEyeHeight();
        }
        else if (standingEyeHeight > 0.5f)
        {
            eyeHeight = standingEyeHeight;
        }

        // 2. Tháo XR Origin ra khỏi tàu và đưa về vị trí sàn (Raycast chuẩn chạm mặt sàn công viên)
        if (xrOriginRig != null)
        {
            CharacterController cc = xrOriginRig.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                cc.height = Mathf.Max(1.6f, eyeHeight + 0.15f);
                cc.center = new Vector3(0f, cc.height / 2f, 0f);
            }

            XRFallbackWalkController walkCtrl = xrOriginRig.GetComponent<XRFallbackWalkController>();
            if (walkCtrl != null)
            {
                walkCtrl.enabled = false;
                walkCtrl.ResetVerticalVelocity();
            }

            xrOriginRig.transform.SetParent(null);
            xrOriginRig.transform.localScale = Vector3.one;

            if (targetSpawnPoint != null)
            {
                Vector3 spawnPos = FindSolidGroundPosition(targetSpawnPoint.position);
                xrOriginRig.transform.SetPositionAndRotation(spawnPos, targetSpawnPoint.rotation);
            }

            if (cc != null)
            {
                cc.enabled = true;
                cc.Move(Vector3.down * 0.05f); // Ép CharacterController chạm đất ngay lập tức
            }
            Physics.SyncTransforms();

            if (walkCtrl != null)
            {
                walkCtrl.FindAndCacheWalkZones(true);
                walkCtrl.enabled = true;
                walkCtrl.ResetVerticalVelocity();
                walkCtrl.InitCameraAngles();
            }
        }

        // 3. Trả Main Camera về cha ban đầu và ĐẶT ĐÚNG TẦM MẮT THEO CHIỀU CAO THỰC CỦA NPC ĐẠI DIỆN
        if (mainCameraTransform != null)
        {
            if (originalCamParent != null && mainCameraTransform.parent != originalCamParent)
            {
                mainCameraTransform.SetParent(originalCamParent);
            }

            if (originalCamParent != null && originalCamParent != xrOriginRig.transform)
            {
                // Nếu có Camera Offset: đặt Camera Offset ở độ cao mắt và dịch nhẹ về trước 0.06m
                originalCamParent.localPosition = new Vector3(0f, eyeHeight, 0.06f);
                originalCamParent.localRotation = Quaternion.identity;
                mainCameraTransform.localPosition = Vector3.zero;
            }
            else
            {
                // Nếu Camera gắn trực tiếp vào XR Origin: đặt Camera ở độ cao mắt
                mainCameraTransform.localPosition = new Vector3(0f, eyeHeight, 0.06f);
            }

            mainCameraTransform.localRotation = Quaternion.identity;
        }

        // 4. Tắt xoay góc nhìn chuột trong tàu, bật bộ điều khiển đi bộ tự do
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

    public void SetControllersActive(bool isActive)
    {
        bool isHMD = DesktopUIInputFallback.IsHMDConnected();

        // 1. Khi chơi trên Desktop (không có kính VR thật) và bật autoHideControllersOnDesktop:
        // Luôn tắt 2 tay cầm và triệt tiêu toàn bộ tia laser trắng để màn hình thông thoáng 100%
        if (autoHideControllersOnDesktop && !isHMD)
        {
            if (leftController != null) leftController.SetActive(false);
            if (rightController != null) rightController.SetActive(false);
            DisableAllControllerLineVisuals();
            return;
        }

        // 2. Khi cắm kính VR thật
        if (leftController != null) leftController.SetActive(isActive);
        if (rightController != null) rightController.SetActive(isActive);

        if (!isActive || hideControllerRays)
        {
            DisableAllControllerLineVisuals();
        }
        else
        {
            EnableAllControllerLineVisuals();
        }
    }

    /// <summary>
    /// Vô hiệu hóa triệt để tất cả LineRenderer và InteractorLineVisual trên toàn bộ XR Origin
    /// </summary>
    public void DisableAllControllerLineVisuals()
    {
        if (xrOriginRig == null) CacheInitialCameraRig();
        if (xrOriginRig == null) return;

        LineRenderer[] lines = xrOriginRig.GetComponentsInChildren<LineRenderer>(true);
        foreach (var l in lines)
        {
            if (l != null && l.enabled) l.enabled = false;
        }

        MonoBehaviour[] scripts = xrOriginRig.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in scripts)
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            if (n.Contains("LineVisual") || n.Contains("XRInteractorLineVisual") || n.Contains("RayVisual") || n.Contains("CurveVisual"))
            {
                mb.enabled = false;
            }
        }
    }

    public void EnableAllControllerLineVisuals()
    {
        if (xrOriginRig == null) CacheInitialCameraRig();
        if (xrOriginRig == null) return;

        bool isHMD = DesktopUIInputFallback.IsHMDConnected();
        if (!isHMD && autoHideControllersOnDesktop) return;

        LineRenderer[] lines = xrOriginRig.GetComponentsInChildren<LineRenderer>(true);
        foreach (var l in lines)
        {
            if (l != null && !l.enabled) l.enabled = true;
        }

        MonoBehaviour[] scripts = xrOriginRig.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in scripts)
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            if (n.Contains("LineVisual") || n.Contains("XRInteractorLineVisual") || n.Contains("RayVisual") || n.Contains("CurveVisual"))
            {
                mb.enabled = true;
            }
        }
    }

    /// <summary>
    /// Thoát khỏi tàu lượn và dịch chuyển trở về đường dạo công viên
    /// </summary>
    public void ReturnToParkMap()
    {
        Time.timeScale = 1f;

        if (aerialCameraController != null)
        {
            aerialCameraController.ResetToCockpit();
        }

        if (passengerManager != null)
        {
            passengerManager.ReleasePlayerPassenger();
            passengerManager.ClearOldPassengers();
            passengerManager.ClearWaitingStationNPCs();
        }

        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        if (playerBody != null)
        {
            playerBody.SetParkScale();
        }

        Transform returnPoint = GetOrCreateParkReturnPoint();
        RestoreStandingPlayer(returnPoint);

        HideUI();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log($"<color=#00FF88><b>[SeatSwitcher] Đã thoát khỏi tàu lượn và trở về công viên an toàn tại: {(returnPoint != null ? returnPoint.position.ToString() : "Park")}</b></color>");
    }
}