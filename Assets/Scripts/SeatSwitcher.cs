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

    [Header("Rào Chắn & Âm Thanh Ga")]
    public StationGateController stationGate;
    public AudioSource brakeAudioSource;

    [Header("Audio Tàu Chạy")]
    public AudioSource trackAudioSource;
    public float minPitch = 0.8f;
    public float maxPitch = 1.5f;

    [Header("UI")]
    public GameObject uiPanel;
    public GameObject startButton;

    [Header("Thời Gian Chờ Mở Rào (Giây)")]
    [Tooltip("Thời gian chờ rào nâng lên xong trước khi tàu chạy")]
    public float gateOpenDelay = 6.0f;

    private bool isRiding = false;
    private bool isHandlingExit = false;
    private bool isGateAlreadyClosed = false;
    private Vector3 lastRidePosition;

    void Start()
    {
        if (trackAudioSource == null)
            trackAudioSource = GetComponent<AudioSource>();

        StopCoasterAudio();
        ForceBoardingMode();

        if (startButton != null)
            startButton.SetActive(false);
    }

    void Update()
    {
        // 1. Phím chuyển ghế Tab chỉ nhận khi tàu đang đỗ ở ga và không trong quá trình xử lý ra/vào
        if (rideController != null && rideController.currentState == RideController.RideState.WaitingAtStation)
        {
            if (!isRiding && !isHandlingExit && Input.GetKeyDown(KeyCode.Tab))
            {
                NextSeat();
            }
        }

        // 2. Cập nhật Pitch theo vận tốc thực tế của tàu
        if (isRiding && trackAudioSource != null && rideCamera != null && Time.deltaTime > 0f)
        {
            float distance = Vector3.Distance(rideCamera.transform.position, lastRidePosition);
            float speed = distance / Time.deltaTime;
            lastRidePosition = rideCamera.transform.position;

            float speedRatio = Mathf.InverseLerp(1f, 30f, speed);
            trackAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, speedRatio);
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

        rideCamera.transform.localPosition = seats[index].localPosition;
        rideCamera.transform.localRotation = seats[index].localRotation;

        if (rideCameraMouseLook != null)
        {
            rideCameraMouseLook.ResetLook(rideCamera.transform.localRotation);
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
        StopCoasterAudio();

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

    // ĐƯỢC GỌI TỪ RIDECONTROLLER KHI TÀU CHẠY ĐẾN ĐOẠN THẲNG TIẾP CẬN GA
    public void TriggerEarlyGateClose()
    {
        if (!isGateAlreadyClosed && stationGate != null)
        {
            isGateAlreadyClosed = true;
            stationGate.CloseGate();
        }
    }

    // ĐƯỢC GỌI TỪ RIDECONTROLLER KHI TÀU ĐÃ CẬP BẾN DỪNG HẲN
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

        // 3. Tàu bắt đầu lăn bánh
        if (rideController != null)
        {
            rideController.StartRide();
        }

        isRiding = true;
        isHandlingExit = false;

        if (rideCamera != null)
            lastRidePosition = rideCamera.transform.position;

        if (trackAudioSource != null)
        {
            trackAudioSource.pitch = minPitch;
            trackAudioSource.Play();
        }
    }

    private IEnumerator HandleRideEndSequence()
    {
        isHandlingExit = true;

        StopCoasterAudio();
        if (brakeAudioSource != null)
        {
            brakeAudioSource.Play();
        }

        // Vì rào đã hạ đón đầu từ trước, người chơi chỉ cần nán lại 1.2s nhìn sân ga
        yield return new WaitForSeconds(1.2f);

        // Cắt dứt điểm âm thanh rào nếu còn dư âm
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

    private void StopCoasterAudio()
    {
        if (trackAudioSource != null)
        {
            trackAudioSource.Stop();
        }
    }
}