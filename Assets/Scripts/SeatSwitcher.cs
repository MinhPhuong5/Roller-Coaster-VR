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

    [Header("Thời Gian Chờ Rào Chắn (Giây)")]
    [Tooltip("Thời gian chờ rào nâng lên xong trước khi tàu chạy")]
    public float gateOpenDelay = 6.0f;
    [Tooltip("Thời gian chờ rào hạ xuống xong trước khi thoát ra màn hình chọn ghế")]
    public float gateCloseDelay = 6.0f;

    private bool isRiding = false;
    private bool isHandlingExit = false;
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
        // 1. KIỂM TRA KHI TÀU VỀ GA
        if (rideController != null && rideController.currentState == RideController.RideState.WaitingAtStation)
        {
            // Chỉ kích hoạt chu trình hạ rào nếu tàu vừa chạy xong và chưa từng vào trạng thái chờ thoát
            if (isRiding && !isHandlingExit)
            {
                ExitCar();
            }

            // Chỉ cho phép chuyển ghế khi đã thoát hẳn ra ngoài (không còn trong hành trình)
            if (!isRiding && !isHandlingExit && Input.GetKeyDown(KeyCode.Tab))
            {
                NextSeat();
            }

            return;
        }

        // 2. TÀU ĐANG TRÊN ĐƯỜNG CHẠY: Cập nhật Pitch theo vận tốc
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

    // Khóa hàm này: Nếu đang trong tiến trình chờ hạ rào 6s thì cấm mọi script bên ngoài ép thoát
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

    // Được gọi từ RideController hoặc hàm Update
    public void ExitCar()
    {
        if (!isHandlingExit && isRiding)
        {
            StartCoroutine(HandleRideEndSequence());
        }
    }

    // Được gọi khi bấm nút Start
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
        // 1. Mở rào chắn và phát âm thanh mở
        if (stationGate != null)
        {
            stationGate.OpenGate();
        }

        // 2. Chờ đủ thời gian khai báo để rào mở hẳn
        yield return new WaitForSeconds(gateOpenDelay);

        // 3. Kích hoạt tàu lăn bánh
        if (rideController != null)
        {
            rideController.StartRide();
        }

        // 4. Bật âm thanh ray tàu
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

        yield return new WaitForSeconds(1.0f);

        if (stationGate != null)
        {
            stationGate.CloseGate();
        }

        // Chờ hết thời gian rào hạ xuống
        yield return new WaitForSeconds(gateCloseDelay + 0.3f);

        if (stationGate != null && stationGate.gateAudioSource != null)
        {
            stationGate.gateAudioSource.Stop();
        }

        isRiding = false;
        isHandlingExit = false;
        ForceBoardingMode();

        // BÁO CHO RIDECONTROLLER BIẾT ĐÃ HOÀN TẤT, SẴN SÀNG ĐÓN LƯỢT MỚI
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