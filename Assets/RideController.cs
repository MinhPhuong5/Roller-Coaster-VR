using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Điều khiển toàn bộ vòng đời 1 lượt chơi:
/// - Mới vào scene: tàu đứng yên tại ga, khách đứng ngoài tàu (boardingSpot)
/// - Khách bấm nút chọn ghế -> "lên tàu" (SeatSwitcher snap camera vào ghế)
/// - Khách bấm nút UI "Bắt đầu" -> gọi StartRide()
/// - Animation gốc là LOOP nên không thể để nó tự dừng; ta tự đếm thời gian
///   thực (Time.deltaTime) để biết khi nào đã đi đủ đúng 1 vòng
/// - Hết 1 vòng -> ép animation về đúng mốc ga -> Finished -> đưa khách ra
///   khỏi ghế -> load lại scene (coi như chơi lại từ đầu)
/// </summary>
public class RideController : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("Tên clip animation gốc lấy từ BluffTitler/Sketchfab")]
    public string animationClipName = "BluffTitler Animation";
    private Animation anim;
    private AnimationState state;

    [Header("Station (ga)")]
    [Tooltip("Số giây đo bằng TimeDisplay, TÍNH TỪ LÚC animation chạy NGƯỢC (speed = -1)")]
    public float secondsToStationFromReverseStart = 20f;
    private float stationTime; // mốc state.time ứng với lúc tàu ở đúng ga

    [Header("Phối hợp với ghế / khách")]
    public SeatSwitcher seatSwitcher;

    [Header("Reload sau khi kết thúc")]
    [Tooltip("Delay trước khi load lại scene — để kịp hiệu ứng/âm thanh kết thúc nếu bên Audio có làm")]
    public float delayBeforeReload = 1f;

    public enum RideState { WaitingAtStation, Riding, Finished }
    public RideState currentState = RideState.WaitingAtStation;

    private float traveledSinceDeparture = 0f;

    void Start()
    {
        anim = GetComponent<Animation>();
        state = anim[animationClipName];

        // Vì animation chạy NGƯỢC (speed = -1, xuất phát từ cuối clip),
        // nên giây đo được bằng Time.time thật phải quy đổi ngược lại
        stationTime = state.length - secondsToStationFromReverseStart;

        // Đặt animation về đúng ga và đứng yên ngay khi scene vừa load
        anim.Play(animationClipName);
        state.time = stationTime;
        state.speed = 0f;
        anim.Sample(); // ép Unity vẽ đúng tư thế tại thời điểm này ngay lập tức

        currentState = RideState.WaitingAtStation;
    }

    void Update()
    {
        if (currentState != RideState.Riding) return;

        // |speed| = 1 nên 1 giây thật (Time.deltaTime) = 1 giây trong animation
        traveledSinceDeparture += Time.deltaTime;

        if (traveledSinceDeparture >= state.length)
            FinishRide();
    }

    /// Gọi từ nút UI "Bắt đầu" — chỉ có tác dụng khi đang đứng ở ga
    public void StartRide()
    {
        if (currentState != RideState.WaitingAtStation) return;

        traveledSinceDeparture = 0f;
        state.speed = -1f;
        currentState = RideState.Riding;
    }

    private void FinishRide()
    {
        // Ép cứng về lại đúng mốc ga (tránh sai số cộng dồn do Time.deltaTime)
        state.time = stationTime;
        state.speed = 0f;
        currentState = RideState.Finished;

        if (seatSwitcher != null)
            seatSwitcher.ExitCar(); // đưa camera ra khỏi ghế, đứng lại vị trí chờ lên tàu

        Invoke(nameof(ReloadScene), delayBeforeReload);
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}