using UnityEngine;

public class SeatSwitcher : MonoBehaviour
{
    [Header("Camera / XR Origin")]
    public Transform playerCamera;

    [Header("Seat Positions")]
    public Transform[] seats; // 0: Front-Left, 1: Front-Right, 2: Back-Left, 3: Back-Right
    private int currentSeatIndex = 0;

    void Start()
    {
        SwitchSeat(0); // Mặc định vào ghế đầu tiên khi bắt đầu
    }

    void Update()
    {
        // Nhấn phím Tab (hoặc nút bấm trên tay cầm VR) để đổi ghế
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            NextSeat();
        }
    }

    public void NextSeat()
    {
        currentSeatIndex = (currentSeatIndex + 1) % seats.Length;
        SwitchSeat(currentSeatIndex);
    }

    public void SwitchSeat(int index)
    {
        if (seats.Length == 0 || playerCamera == null) return;

        // Đặt camera về đúng vị trí mốc ghế
        playerCamera.position = seats[index].position;
        playerCamera.rotation = seats[index].rotation;
    }
}