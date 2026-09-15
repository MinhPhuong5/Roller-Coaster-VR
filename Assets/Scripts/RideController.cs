using UnityEngine;
using UnityEngine.SceneManagement;

public class RideController : MonoBehaviour
{
    [Header("Animation")]
    public string animationClipName = "BluffTitler Animation";
    private Animation anim;
    private AnimationState state;

    [Header("Station (ga)")]
    [Tooltip("Số giây tính ngược từ cuối clip (Mốc phẳng của nhà ga)")]
    public float stationTime = 20.5f;

    [Header("Số vòng chạy")]
    public int numberOfLaps = 1;

    [Header("Phối hợp với ghế / khách")]
    public SeatSwitcher seatSwitcher;

    [Header("Motion Inverter")]
    public CoasterFollower motionInverter;

    [Header("Đồ Thị Tốc Độ (Speed Curve)")]
    [Tooltip("Đường cong tốc độ theo tiến độ clip 90 giây")]
    public AnimationCurve speedCurve = new AnimationCurve();
    [Tooltip("Hệ số nhân tốc độ tổng thể (1.0 là chuẩn)")]
    public float globalSpeedMultiplier = 1.0f;

    [Header("Hạ Rào Đón Đầu")]
    [Tooltip("Hạ rào trước khi tàu về ga bao nhiêu giây của clip (Khoảng 6.5s là vừa vặn khi hết xoắn 4 tầng)")]
    public float gateCloseTriggerOffset = 6.5f;
    private bool hasTriggeredEarlyGateClose = false;

    [Header("Âm Thanh Biến Thiên Theo Tốc Độ")]
    public AudioSource coasterAudio;
    public float minPitch = 0.65f;
    public float maxPitch = 1.7f;
    public float minVolume = 0.5f;
    public float maxVolume = 1.0f;

    [Header("Tùy chọn kết thúc")]
    public bool reloadSceneOnFinish = false;
    public float delayBeforeReload = 1f;

    public enum RideState { WaitingAtStation, Riding, Finished }
    public RideState currentState = RideState.WaitingAtStation;

    private float traveledAnimationTime = 0f;

    private float ActualStationTime => Mathf.Max(0f, state != null ? state.length - stationTime : 0f);

    void Awake()
    {
        InitializeDefaultSpeedCurve();
    }

    void Start()
    {
        anim = GetComponent<Animation>();
        if (anim == null)
        {
            Debug.LogError("Không tìm thấy component Animation trên " + gameObject.name);
            return;
        }

        state = anim[animationClipName];
        if (state == null)
        {
            Debug.LogError("Không tìm thấy clip: " + animationClipName);
            return;
        }

        state.wrapMode = WrapMode.Loop;
        anim.Play(animationClipName);

        state.time = ActualStationTime;
        state.speed = 0f;
        anim.Sample();

        currentState = RideState.WaitingAtStation;
    }

    void Update()
    {
        if (currentState != RideState.Riding || state == null) return;

        float clipLength = state.length > 0f ? state.length : 90f;
        float currentLoopTime = traveledAnimationTime % clipLength;

        // 1. Đọc tốc độ từ đồ thị
        float targetSpeed = speedCurve.Evaluate(currentLoopTime) * globalSpeedMultiplier;
        targetSpeed = Mathf.Max(0.05f, targetSpeed);

        state.speed = -targetSpeed;
        traveledAnimationTime += Time.deltaTime * targetSpeed;

        UpdateAudioDynamics(targetSpeed);

        // 2. ĐÓNG RÀO NẰM CHỜ SẴN TỪ SỚM:
        // Đóng ngay khi tàu bắt đầu vào vòng chạy cuối (hoặc sau khi rời ga 10s đối với lượt 1 vòng)
        float startOfFinalLap = clipLength * (numberOfLaps - 1);
        if (!hasTriggeredEarlyGateClose && traveledAnimationTime >= (startOfFinalLap + 10.0f))
        {
            hasTriggeredEarlyGateClose = true;
            if (seatSwitcher != null)
            {
                seatSwitcher.TriggerEarlyGateClose();
            }
        }

        // 3. Tàu về ga dừng hẳn
        if (traveledAnimationTime >= clipLength * numberOfLaps)
        {
            FinishRide();
        }
    }

    private void UpdateAudioDynamics(float currentSpeed)
    {
        if (coasterAudio == null) return;

        float t = Mathf.InverseLerp(0.4f, 2.7f, currentSpeed);
        coasterAudio.pitch = Mathf.Lerp(minPitch, maxPitch, t);
        coasterAudio.volume = Mathf.Lerp(minVolume, maxVolume, t);
    }

    public void StartRide()
    {
        if (currentState != RideState.WaitingAtStation || state == null) return;

        traveledAnimationTime = 0f;
        hasTriggeredEarlyGateClose = false;
        currentState = RideState.Riding;

        if (coasterAudio != null)
        {
            coasterAudio.pitch = minPitch;
            coasterAudio.volume = minVolume;
            coasterAudio.Play();
        }

        if (seatSwitcher != null)
            seatSwitcher.HideUI();
    }

    private void FinishRide()
    {
        state.time = ActualStationTime;
        state.speed = 0f;
        anim.Sample();

        currentState = RideState.Finished;

        if (coasterAudio != null)
        {
            coasterAudio.Stop();
        }

        if (seatSwitcher != null)
            seatSwitcher.ExitCar();

        if (reloadSceneOnFinish)
        {
            Invoke(nameof(ReloadScene), delayBeforeReload);
        }
    }

    public void ResetToStation()
    {
        currentState = RideState.WaitingAtStation;
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void InitializeDefaultSpeedCurve()
    {
        if (speedCurve != null && speedCurve.length > 0) return;

        speedCurve = new AnimationCurve();
        speedCurve.AddKey(new Keyframe(0f, 0.45f));
        speedCurve.AddKey(new Keyframe(9.5f, 0.4f));
        speedCurve.AddKey(new Keyframe(13.0f, 2.6f));
        speedCurve.AddKey(new Keyframe(17.0f, 1.2f));
        speedCurve.AddKey(new Keyframe(20.0f, 1.9f));
        speedCurve.AddKey(new Keyframe(30.0f, 0.65f));
        speedCurve.AddKey(new Keyframe(35.5f, 2.4f));
        speedCurve.AddKey(new Keyframe(45.0f, 1.7f));
        speedCurve.AddKey(new Keyframe(54.0f, 1.8f));
        speedCurve.AddKey(new Keyframe(62.0f, 2.0f));
        speedCurve.AddKey(new Keyframe(74.0f, 0.55f));
        speedCurve.AddKey(new Keyframe(80.0f, 2.7f));
        speedCurve.AddKey(new Keyframe(85.5f, 2.3f));
        speedCurve.AddKey(new Keyframe(87.0f, 1.2f));
        speedCurve.AddKey(new Keyframe(88.5f, 0.45f));
        speedCurve.AddKey(new Keyframe(89.8f, 0.05f));
    }
}