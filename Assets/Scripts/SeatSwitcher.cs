using System.Collections;
using UnityEngine;

public class SeatSwitcher : MonoBehaviour
{
    [Header("2 Camera")]
    public GameObject mainCamera;
    public GameObject rideCamera;
    public MouseLook rideCameraMouseLook;

    [Header("Ghế trong tàu")]
    public Transform[] seats;
    private int currentSeatIndex = 0;

    [Header("Ride")]
    public RideController rideController;

    [Header("Rào Chắn Ga")]
    public StationGateController stationGate;

    [Header("UI")]
    public GameObject uiPanel;
    public GameObject startButton;

    [Header("Thời Gian Chờ Mở Rào (Giây)")]
    [Tooltip("Thời gian chờ rào nâng lên xong trước khi tàu chạy")]
    public float gateOpenDelay = 6.0f;

    private bool isRiding = false;
    private bool isHandlingExit = false;
    private bool isGateAlreadyClosed = false;

    void Start()
    {
        ForceBoardingMode();

        if (startButton != null)
            startButton.SetActive(false);
    }

    void Update()
    {
        // 1. Phím chuyển ghế Tab chỉ nhận khi tàu đang đỗ ở ga và không trong tiến trình xử lý
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
        currentSeatIndex = (currentSeatIndex + 1) % seats.Length;
        SwitchSeat(currentSeatIndex);
    }

    public void SwitchSeat(int index)
    {
        if (seats.Length == 0 || rideCamera == null) return;

        currentSeatIndex = index;

        // GHIM CHẶT CAMERA VÀO GHẾ ĐƯỢC CHỌN (Gắn làm con trực tiếp)
        rideCamera.transform.SetParent(seats[index]);
        rideCamera.transform.localPosition = Vector3.zero;
        rideCamera.transform.localRotation = Quaternion.identity;

        if (rideCameraMouseLook != null)
        {
            rideCameraMouseLook.ResetLook(Quaternion.identity);
        }

        EnterSeatedMode();

        if (startButton != null)
            startButton.SetActive(true);
    }

    public void EnterBoardingMode()
    {
        if (isHandlingExit) return;
        ForceBoardingMode();
    }

    private void ForceBoardingMode()
    {
        if (mainCamera != null) mainCamera.SetActive(true);
        if (rideCamera != null) rideCamera.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (uiPanel != null) uiPanel.SetActive(true);
        if (startButton != null) startButton.SetActive(false);
    }

    public void EnterSeatedMode()
    {
        if (mainCamera != null) mainCamera.SetActive(false);
        if (rideCamera != null) rideCamera.SetActive(true);
    }

    // Được gọi từ RideController để hạ rào từ xa
    public void TriggerEarlyGateClose()
    {
        if (!isGateAlreadyClosed && stationGate != null)
        {
            isGateAlreadyClosed = true;
            stationGate.CloseGate();
        }
    }

    // Được gọi từ RideController khi tàu dừng hẳn tại ga
    public void ExitCar()
    {
        if (!isHandlingExit && isRiding)
        {
            StartCoroutine(HandleRideEndSequence());
        }
    }

    public void HideUI()
    {
        if (uiPanel != null) uiPanel.SetActive(false);
        if (startButton != null) startButton.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StartCoroutine(StartRideSequence());
    }

    private IEnumerator StartRideSequence()
    {
        isGateAlreadyClosed = false;

        // 1. Mở rào chắn xuất phát
        if (stationGate != null)
        {
            stationGate.OpenGate();
        }

        // 2. Chờ đủ thời gian rào mở hẳn
        yield return new WaitForSeconds(gateOpenDelay);

        // 3. Tàu bắt đầu lăn bánh (RideController sẽ tự bật âm thanh 3 lớp)
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

        // Rào đã hạ sẵn từ trước, nán lại 1.2s nhìn sân ga
        yield return new WaitForSeconds(1.2f);

        // Dập âm thanh động cơ rào nếu còn sót
        if (stationGate != null && stationGate.gateAudioSource != null)
        {
            stationGate.gateAudioSource.Stop();
        }

        isRiding = false;
        isHandlingExit = false;
        ForceBoardingMode();

        // Báo cho RideController chuyển sang trạng thái chờ lượt mới
        if (rideController != null)
        {
            rideController.ResetToStation();
        }
    }
}