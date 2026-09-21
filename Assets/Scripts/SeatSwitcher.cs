using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class SeatSwitcher : MonoBehaviour
{
    [Header("VR Rig / Người Chơi")]
    [Tooltip("Kéo toàn bộ cụm XR Origin (XR Rig) vào đây")]
    public GameObject xrOriginRig;

    [Header("XR Controllers (Ẩn khi tàu chạy)")]
    public GameObject leftController;
    public GameObject rightController;

    [Header("Hai Chế Độ Xoay Chuột")]
    public FreeCamLook freeCamLook;
    public MouseLook mouseLook;

    [Header("Ghế trong tàu")]
    public Transform[] seats;
    private int currentSeatIndex = 0;

    [Header("Ride")]
    public RideController rideController;

    [Header("Rào Chắn Ga")]
    public StationGateController stationGate;

    [Header("UI Sảnh")]
    public GameObject uiPanel;
    public GameObject startButton;

    [Header("Tùy Chọn Đếm Ngược (Setup Trên Inspector)")]
    [Tooltip("Tích chọn để hiển thị chữ 3.. 2.. 1.. GO!")]
    public bool useCountdownText = true;
    [Tooltip("Tích chọn để phát âm thanh đếm ngược")]
    public bool useCountdownAudio = true;

    [Header("Đếm Ngược Bắt Đầu")]
    public TextMeshProUGUI countdownText;
    public int countdownSeconds = 3;

    [Header("Âm Thanh Đếm Ngược")]
    [Tooltip("Kéo AudioSource chứa file Countdown vào đây")]
    public AudioSource countdownAudio;

    [Header("Thời Gian Chờ Mở Rào (Giây)")]
    public float gateOpenDelay = 6.0f;

    private bool isRiding = false;
    private bool isHandlingExit = false;
    private bool isGateAlreadyClosed = false;

    // Quản lý riêng Main Camera
    private Transform mainCameraTransform;
    private Transform originalCamParent;
    private Vector3 originalCamLocalPos;
    private Quaternion originalCamLocalRot;

    void Start()
    {
        if (xrOriginRig != null)
        {
            Camera cam = xrOriginRig.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                mainCameraTransform = cam.transform;
                originalCamParent = mainCameraTransform.parent;
                originalCamLocalPos = mainCameraTransform.localPosition;
                originalCamLocalRot = mainCameraTransform.localRotation;
            }
        }

        ForceBoardingMode();

        if (startButton != null) startButton.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (rideController != null && rideController.currentState == RideController.RideState.WaitingAtStation)
        {
            if (!isRiding && !isHandlingExit && Input.GetKeyDown(KeyCode.Tab))
            {
                NextSeat();
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
        if (seats == null || seats.Length == 0 || mainCameraTransform == null) return;

        currentSeatIndex = index;

        // TẮT toàn bộ cụm XR Rig ở sảnh để ngắt va chạm và rơi tự do
        if (xrOriginRig != null) xrOriginRig.SetActive(false);

        // ĐƯA TRỰC TIẾP MAIN CAMERA VÀO LÀM CON CỦA GHẾ
        mainCameraTransform.SetParent(seats[index]);
        mainCameraTransform.localPosition = Vector3.zero;
        mainCameraTransform.localRotation = Quaternion.identity;

        if (freeCamLook != null) freeCamLook.enabled = false;
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

    private void ForceBoardingMode()
    {
        // BẬT LẠI XR Rig ở sảnh
        if (xrOriginRig != null) xrOriginRig.SetActive(true);

        // Trả Main Camera về lại cụm XR Rig ở sảnh
        if (mainCameraTransform != null && originalCamParent != null)
        {
            mainCameraTransform.SetParent(originalCamParent);
            mainCameraTransform.localPosition = originalCamLocalPos;
            mainCameraTransform.localRotation = originalCamLocalRot;
        }

        if (freeCamLook != null) freeCamLook.enabled = true;
        if (mouseLook != null) mouseLook.enabled = false;

        SetControllersActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (uiPanel != null) uiPanel.SetActive(true);
        if (startButton != null) startButton.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    public void EnterSeatedMode() { }

    public void TriggerEarlyGateClose()
    {
        if (!isGateAlreadyClosed && stationGate != null)
        {
            isGateAlreadyClosed = true;
            stationGate.CloseGate();
        }
    }

    public void ExitCar()
    {
        if (!isHandlingExit && isRiding)
        {
            StartCoroutine(HandleRideEndSequence());
        }
    }

    private IEnumerator StartRideSequence()
    {
        isGateAlreadyClosed = false;

        // 1. Mở rào chắn ga
        if (stationGate != null)
        {
            stationGate.OpenGate();
        }

        bool hasCountdown = useCountdownText || useCountdownAudio;

        if (hasCountdown)
        {
            // Chờ mở rào trước một khoảng thời gian (gateOpenDelay - countdownSeconds)
            float waitBeforeCountdown = Mathf.Max(0f, gateOpenDelay - countdownSeconds);
            if (waitBeforeCountdown > 0f)
            {
                yield return new WaitForSeconds(waitBeforeCountdown);
            }

            // Phát âm thanh đếm ngược nếu được tích chọn
            if (useCountdownAudio && countdownAudio != null)
            {
                countdownAudio.Play();
            }

            // Hiển thị chữ đếm ngược nếu được tích chọn
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
                // Nếu chỉ bật tiếng mà tắt chữ: chờ đúng khoảng thời gian bằng countdownSeconds
                yield return new WaitForSeconds(Mathf.Min(gateOpenDelay, (float)countdownSeconds));
            }
        }
        else
        {
            // NẾU TẮT CẢ TIẾNG LẪN CHỮ: Chờ đủ đúng gateOpenDelay để rào mở xong hoàn toàn
            yield return new WaitForSeconds(gateOpenDelay);
        }

        // 2. Khóa controller VR
        SetControllersActive(false);

        // 3. Tàu bắt đầu lăn bánh
        if (rideController != null)
        {
            rideController.StartRide();
        }

        isRiding = true;
        isHandlingExit = false;
    }

    private IEnumerator HandleRideEndSequence()
    {
        isHandlingExit = true;
        yield return new WaitForSeconds(1.2f);

        if (stationGate != null && stationGate.gateAudioSource != null)
        {
            stationGate.gateAudioSource.Stop();
        }

        // Dừng âm thanh đếm ngược nếu còn sót lại
        if (countdownAudio != null && countdownAudio.isPlaying)
        {
            countdownAudio.Stop();
        }

        isRiding = false;
        isHandlingExit = false;

        ForceBoardingMode();

        if (rideController != null)
        {
            rideController.ResetToStation();
        }
    }

    private void SetControllersActive(bool isActive)
    {
        if (leftController != null) leftController.SetActive(isActive);
        if (rightController != null) rightController.SetActive(isActive);
    }

    public void ReturnToParkMap()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Ghi nhận cờ để ParkScene nhận biết vừa đi tàu lượn xong
        PlayerPrefs.SetInt("HasPlayedCoaster", 1);
        PlayerPrefs.Save();

        // Nạp Scene bất đồng bộ để tránh khựng khung hình
        StartCoroutine(LoadParkSceneAsyncRoutine());
    }

    private IEnumerator LoadParkSceneAsyncRoutine()
    {
        // Hạn chế việc load asset chiếm quyền luồng chính
        Application.backgroundLoadingPriority = ThreadPriority.Low;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("ParkScene");

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        Application.backgroundLoadingPriority = ThreadPriority.Normal;
    }
}