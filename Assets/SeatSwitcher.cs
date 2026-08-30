using UnityEngine;

/// <summary>
/// Quản lý 2 chế độ camera:
/// - "Boarding" (đang chọn ghế): dùng mainCamera — object TĨNH, đặt sẵn 1 lần
///   trong Scene view ở ngoài track, nhìn thấy toàn bộ tàu.
/// - "Seated" (đã chọn ghế / đang chạy): dùng rideCamera — object là con của
///   tàu (KexLSMfoSketchfab), tự động trôi theo animation của tàu.
///   Vị trí bên trong ghế được set bằng cách copy local position/rotation
///   từ Seat, vì rideCamera và Seats CÙNG CHA nên phép copy local này đúng.
/// </summary>
public class SeatSwitcher : MonoBehaviour
{
    [Header("2 Camera")]
    [Tooltip("Camera tĩnh dùng lúc đang chọn ghế, đứng ngoài nhìn tàu (Main Camera mặc định của project)")]
    public GameObject mainCamera;
    [Tooltip("Camera POV bên trong tàu, là con của model tàu (BluffTitler Camera Layer 9-Camera)")]
    public GameObject rideCamera;

    [Tooltip("MouseLook gắn trên rideCamera — cần reset góc nhìn mỗi lần đổi ghế")]
    public MouseLook rideCameraMouseLook;

    [Header("Ghế trong tàu (con của model tàu, cùng cha với rideCamera)")]
    public Transform[] seats; // 0: Front-Left, 1: Front-Right, 2: Back-Left, 3: Back-Right
    private int currentSeatIndex = 0;

    [Header("Ride")]
    public RideController rideController;

    [Header("UI")]
    public GameObject uiPanel;
    public GameObject startButton;

    void Start()
    {
        EnterBoardingMode(); // mới vào scene: dùng Main Camera, đứng ngoài nhìn tàu

        if (startButton != null)
            startButton.SetActive(false); // ẩn nút Bắt đầu lúc mới vào
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

    /// Gọi khi khách bấm nút chọn 1 ghế cụ thể (UI) — "lên tàu": chuyển sang rideCamera
    public void SwitchSeat(int index)
    {
        if (seats.Length == 0 || rideCamera == null) return;

        // Đặt rideCamera đúng vị trí ghế đã chọn (local, vì cùng cha với seats)
        rideCamera.transform.localPosition = seats[index].localPosition;
        rideCamera.transform.localRotation = seats[index].localRotation;

        if (rideCameraMouseLook != null)
            rideCameraMouseLook.ResetLook(seats[index].localRotation);

        EnterSeatedMode();

        if (startButton != null)
            startButton.SetActive(true); // đã chọn ghế -> hiện nút Bắt đầu
    }

    /// Bật Main Camera, tắt rideCamera — dùng lúc chờ chọn ghế
    public void EnterBoardingMode()
    {
        if (mainCamera != null) mainCamera.SetActive(true);
        if (rideCamera != null) rideCamera.SetActive(false);
    }

    /// Bật rideCamera, tắt Main Camera — dùng khi đã ngồi vào ghế / đang chạy
    public void EnterSeatedMode()
    {
        if (mainCamera != null) mainCamera.SetActive(false);
        if (rideCamera != null) rideCamera.SetActive(true);
    }

    /// Gọi từ RideController.FinishRide() khi kết thúc chuyến đi — "xuống tàu"
    public void ExitCar() => EnterBoardingMode();

    public void HideUI()
    {
        if (uiPanel != null) uiPanel.SetActive(false);
    }

    public void ShowUI()
    {
        if (uiPanel != null) uiPanel.SetActive(true);
    }
}