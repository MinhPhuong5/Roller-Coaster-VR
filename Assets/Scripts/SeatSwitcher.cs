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

    [Header("Audio")]
    public AudioSource trackAudioSource;
    public float minPitch = 0.8f;
    public float maxPitch = 1.5f;

    [Header("UI")]
    public GameObject uiPanel;
    public GameObject startButton;

    private bool isRiding = false;
    private Vector3 lastRidePosition;

    void Start()
    {
        // Tự lấy AudioSource nếu quên chưa kéo vào Inspector
        if (trackAudioSource == null)
            trackAudioSource = GetComponent<AudioSource>();

        if (trackAudioSource != null)
        {
            trackAudioSource.playOnAwake = false;
            trackAudioSource.loop = true;
            trackAudioSource.Stop();
        }

        EnterBoardingMode();

        if (startButton != null)
            startButton.SetActive(false);
    }

    void Update()
    {
        if (rideController != null && rideController.currentState != RideController.RideState.WaitingAtStation)
        {
            // Đang trong hành trình: biến thiên âm thanh theo tốc độ di chuyển thực tế
            if (isRiding && trackAudioSource != null && rideCamera != null && Time.deltaTime > 0f)
            {
                float distance = Vector3.Distance(rideCamera.transform.position, lastRidePosition);
                float speed = distance / Time.deltaTime;
                lastRidePosition = rideCamera.transform.position;

                // Tăng dần độ gầm rú (Pitch) theo vận tốc tàu
                float speedRatio = Mathf.InverseLerp(1f, 30f, speed);
                trackAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, speedRatio);
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
            NextSeat();
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
        isRiding = false;
        if (trackAudioSource != null) trackAudioSource.Stop();

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

    public void ExitCar()
    {
        EnterBoardingMode();
    }

    // ĐƯỢC GỌI KHI BẤM NÚT START ĐỂ TÀU CHẠY
    public void HideUI()
    {
        if (uiPanel != null) uiPanel.SetActive(false);
        if (startButton != null) startButton.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // BẬT ÂM THANH CHUẨN XÁC TẠI ĐÂY
        isRiding = true;
        if (rideCamera != null) lastRidePosition = rideCamera.transform.position;
        if (trackAudioSource != null) trackAudioSource.Play();
    }

    public void ShowUI()
    {
        if (uiPanel != null) uiPanel.SetActive(true);
    }
}