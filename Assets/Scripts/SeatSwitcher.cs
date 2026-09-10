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

    [Header("UI")]
    public GameObject uiPanel;
    public GameObject startButton;

    void Start()
    {
        EnterBoardingMode();

        if (startButton != null)
            startButton.SetActive(false);
    }

    void Update()
    {
        if (rideController != null && rideController.currentState != RideController.RideState.WaitingAtStation)
            return;

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

        // Copy vị trí và góc quay 180 độ chuẩn của ghế
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
        // 1. Chuyển camera về sân ga
        if (mainCamera != null) mainCamera.SetActive(true);
        if (rideCamera != null) rideCamera.SetActive(false);

        // 2. Mở khóa và hiện lại con trỏ chuột để bấm nút UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. Hiện lại bảng chọn ghế và ẩn nút bắt đầu
        if (uiPanel != null) uiPanel.SetActive(true);
        if (startButton != null) startButton.SetActive(false);
    }

    public void EnterSeatedMode()
    {
        if (mainCamera != null) mainCamera.SetActive(false);
        if (rideCamera != null) rideCamera.SetActive(true);
    }

    // Được gọi khi tàu chạy xong 2 vòng trở về ga
    public void ExitCar()
    {
        EnterBoardingMode();
    }

    public void HideUI()
    {
        if (uiPanel != null) uiPanel.SetActive(false);
        if (startButton != null) startButton.SetActive(false);

        // Khi bắt đầu tàu chạy, khóa chuột để xoay nhìn tự do
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ShowUI()
    {
        if (uiPanel != null) uiPanel.SetActive(true);
    }
}