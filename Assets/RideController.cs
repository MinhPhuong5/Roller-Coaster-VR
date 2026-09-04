using UnityEngine;
using UnityEngine.SceneManagement;

public class RideController : MonoBehaviour
{
    [Header("Animation")]
    public string animationClipName = "BluffTitler Animation";
    private Animation anim;
    private AnimationState state;

    [Header("Station (ga)")]
    [Tooltip("Số giây tính ngược từ cuối clip (Ví dụ: 20 nghĩa là thời gian cuối - 20s)")]
    public float stationTime = 20f;

    [Header("Số vòng chạy")]
    public int numberOfLaps = 2;
    private float totalRideDuration;

    [Header("Phối hợp với ghế / khách")]
    public SeatSwitcher seatSwitcher;

    [Header("Motion Inverter")]
    public CoasterFollower motionInverter; // Hoặc CoasterMotionInverter tuỳ tên script bạn đang dùng

    [Header("Tùy chọn kết thúc")]
    [Tooltip("Tick nếu muốn reload lại toàn bộ Scene khi xong; bỏ tick nếu chỉ muốn xuống xe đứng chọn ghế tại chỗ")]
    public bool reloadSceneOnFinish = false;
    public float delayBeforeReload = 1f;

    public enum RideState { WaitingAtStation, Riding, Finished }
    public RideState currentState = RideState.WaitingAtStation;

    private float traveledSinceDeparture = 0f;

    // Tính mốc thời gian thực tế ở ga (lấy đuôi trừ đi)
    private float ActualStationTime => Mathf.Max(0f, state.length - stationTime);

    void Start()
    {
        anim = GetComponent<Animation>();
        state = anim[animationClipName];

        if (state == null)
        {
            Debug.LogError("Không tìm thấy clip: " + animationClipName);
            return;
        }

        // Bắt buộc bật Loop để khi speed = -1f lùi về 0 sẽ tự vòng lại đuôi clip
        state.wrapMode = WrapMode.Loop;
        totalRideDuration = state.length * numberOfLaps;

        anim.Play(animationClipName);

        // Đặt đúng mốc ga đảo ngược: thời gian cuối - 20s
        state.time = ActualStationTime;
        state.speed = 0f;
        anim.Sample();

        currentState = RideState.WaitingAtStation;
    }

    void Update()
    {
        if (currentState != RideState.Riding) return;

        traveledSinceDeparture += Time.deltaTime;

        if (traveledSinceDeparture >= totalRideDuration)
        {
            FinishRide();
        }
    }

    public void StartRide()
    {
        if (currentState != RideState.WaitingAtStation || state == null) return;

        traveledSinceDeparture = 0f;
        state.speed = -1f; // Chạy lùi ngược timeline để đi xuôi chiều ray
        currentState = RideState.Riding;

        if (seatSwitcher != null)
            seatSwitcher.HideUI();
    }

    private void FinishRide()
    {
        // Dừng tàu lại đúng vị trí ga phẳng ban đầu
        state.time = ActualStationTime;
        state.speed = 0f;
        anim.Sample();
        currentState = RideState.Finished;

        // Cho người chơi rời ghế, hiện lại UI chọn chỗ ban đầu
        if (seatSwitcher != null)
            seatSwitcher.ExitCar();

        if (reloadSceneOnFinish)
        {
            Invoke(nameof(ReloadScene), delayBeforeReload);
        }
        else
        {
            // Cho phép chọn ghế và chơi tiếp lượt mới ngay tại chỗ
            currentState = RideState.WaitingAtStation;
        }
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}