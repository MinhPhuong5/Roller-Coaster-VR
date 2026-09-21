using System.Collections;
using UnityEngine;
using TMPro;

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

        // 1. Mở rào ga
        if (stationGate != null)
        {
            stationGate.OpenGate();
        }

        // 2. Chờ mở rào
        float waitBeforeCountdown = Mathf.Max(0f, gateOpenDelay - countdownSeconds);
        if (waitBeforeCountdown > 0f)
        {
            yield return new WaitForSeconds(waitBeforeCountdown);
        }

        // 3. Phát âm thanh Countdown
        if (countdownAudio != null)
        {
            countdownAudio.Play();
        }

        // 4. Đồng bộ chữ hiển thị 3... 2... 1... GO!
        if (countdownText != null)
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

        // 5. Khóa controller VR
        SetControllersActive(false);

        // 6. Tàu lăn bánh
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
}