using UnityEngine;
using UnityEngine.SceneManagement;

public class RideController : MonoBehaviour
{
    [Header("Animation")]
    public string animationClipName = "BluffTitler Animation";
    private Animation anim;
    private AnimationState state;

    [Header("Station (ga)")]
    [Tooltip("Mốc giây CHÍNH XÁC đọc trực tiếp từ cửa sổ Animation window, lúc tàu nằm đúng ở ga")]
    public float stationTime = 20f; // <- điền đúng số bạn vừa đọc được ở bước 3

    [Header("Số vòng chạy")]
    public int numberOfLaps = 2;
    private float totalRideDuration;

    [Header("Phối hợp với ghế / khách")]
    public SeatSwitcher seatSwitcher;

    [Header("Reload sau khi kết thúc")]
    public float delayBeforeReload = 1f;

    public enum RideState { WaitingAtStation, Riding, Finished }
    public RideState currentState = RideState.WaitingAtStation;

    private float traveledSinceDeparture = 0f;

    void Start()
    {
        anim = GetComponent<Animation>();
        state = anim[animationClipName];

        totalRideDuration = state.length * numberOfLaps;

        anim.Play(animationClipName);
        state.time = stationTime; // <- dùng thẳng, không trừ gì cả
        state.speed = 0f;
        anim.Sample();

        currentState = RideState.WaitingAtStation;
    }

    void Update()
    {
        if (currentState != RideState.Riding) return;

        traveledSinceDeparture += Time.deltaTime;

        if (traveledSinceDeparture >= totalRideDuration)
            FinishRide();
    }

    public void StartRide()
    {
        if (currentState != RideState.WaitingAtStation) return;

        traveledSinceDeparture = 0f;
        state.speed = -1f;
        currentState = RideState.Riding;

        if (seatSwitcher != null)
            seatSwitcher.HideUI();
    }

    private void FinishRide()
    {
        state.time = stationTime;
        state.speed = 0f;
        currentState = RideState.Finished;

        if (seatSwitcher != null)
            seatSwitcher.ExitCar();

        Invoke(nameof(ReloadScene), delayBeforeReload);
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}