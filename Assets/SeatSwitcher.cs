using UnityEngine;

/// <summary>
/// Quản lý vị trí camera của khách: đứng chờ ngoài tàu (boardingSpot),
/// hoặc ngồi ở 1 trong 4 ghế. Tất cả mốc (boardingSpot, seats) đều là
/// object CON của tàu, nên khi tàu đứng yên ở ga thì các mốc này luôn
/// đúng vị trí — không cần script di chuyển nhân vật riêng.
/// </summary>
public class SeatSwitcher : MonoBehaviour
{
    [Header("Camera")]
    public Transform playerCamera;
    //public MouseLook mouseLook;

    [Header("Vị trí chờ lên tàu (đứng ngoài, đối diện tàu)")]
    public Transform boardingSpot;

    [Header("Ghế trong tàu")]
    public Transform[] seats; // 0: Front-Left, 1: Front-Right, 2: Back-Left, 3: Back-Right
    private int currentSeatIndex = 0;

    [Header("Ride")]
    public RideController rideController;

    void Start()
    {
        StandAtBoardingSpot(); // mới vào scene: đứng chờ, chưa vào ghế nào
    }

    void Update()
    {
        // Không cho đổi ghế khi tàu đang chạy hoặc đã kết thúc
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

    /// Gọi khi khách bấm nút chọn 1 ghế cụ thể (UI) — đồng thời là hành động "lên tàu"
    public void SwitchSeat(int index)
    {
        if (seats.Length == 0 || playerCamera == null) return;

        playerCamera.localPosition = seats[index].localPosition;
        playerCamera.localRotation = seats[index].localRotation;

        //if (mouseLook != null)
            //mouseLook.ResetLook(seats[index].localRotation);
    }

    /// Gọi lúc mới vào scene, hoặc từ RideController.FinishRide() để "xuống tàu"
    public void StandAtBoardingSpot()
    {
        if (boardingSpot == null || playerCamera == null) return;

        playerCamera.localPosition = boardingSpot.localPosition;
        playerCamera.localRotation = boardingSpot.localRotation;

        //if (mouseLook != null)
            //mouseLook.ResetLook(boardingSpot.localRotation);
    }

    /// Alias cho rõ ngữ nghĩa khi RideController gọi
    public void ExitCar() => StandAtBoardingSpot();
}