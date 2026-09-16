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
    public int numberOfLaps = 2;

    [Header("Phối hợp với ghế / khách")]
    public SeatSwitcher seatSwitcher;

    [Header("Motion Inverter")]
    public CoasterFollower motionInverter;

    [Header("Đồ Thị Tốc Độ (Speed Curve)")]
    [Tooltip("Đường cong tốc độ chuẩn của vòng 1 (có kéo dốc và khựng lại)")]
    public AnimationCurve speedCurve = new AnimationCurve();
    [Tooltip("Hệ số nhân tốc độ tổng thể (1.0 là chuẩn)")]
    public float globalSpeedMultiplier = 1.0f;

    [Header("1. Lớp Tiếng Ray & Gió (Track & Wind Loop)")]
    public AudioSource trackWindAudio;
    public float minPitch = 0.8f;   // Giữ tối thiểu 0.8 để tránh vỡ buffer DSP
    public float maxPitch = 1.6f;
    public float minVolume = 0.3f;
    public float maxVolume = 1.0f;

    [Header("2. Lớp Tiếng Lạch Cạch Xích Kéo (Chain Lift Loop)")]
    public AudioSource clankAudio;
    public float liftStartProgress = 0.2f;          // Bắt đầu dốc 1
    public float liftEndProgress = 10.5f;          // Kết thúc hẳn xích kéo dốc 1
    public float liftFadeOutDuration = 1.2f;       // Thời gian mờ âm dần ở đỉnh dốc
    public float stationBrakeStartProgress = 86.5f;
    public float stationBrakeEndProgress = 89.8f;

    [Header("3. Lớp Tiếng Két Phanh Vào Ga (Brake Squeal)")]
    public AudioSource brakeAudio;
    public float brakeTriggerProgress = 88.0f;
    private bool hasPlayedBrakeSqueal = false;

    [Header("Tùy chọn kết thúc")]
    public bool reloadSceneOnFinish = false;
    public float delayBeforeReload = 1f;

    public enum RideState { WaitingAtStation, Riding, Finished }
    public RideState currentState = RideState.WaitingAtStation;

    private float traveledAnimationTime = 0f;
    private bool hasTriggeredEarlyGateClose = false;

    private float ActualStationTime => Mathf.Max(0f, state != null ? state.length - stationTime : 0f);

    void Awake()
    {
        if (speedCurve == null || speedCurve.length == 0)
        {
            GenerateSpeedCurve();
        }
    }

    void Start()
    {
        anim = GetComponent<Animation>();
        if (anim == null) return;

        state = anim[animationClipName];
        if (state == null) return;

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

        int currentLapIndex = Mathf.FloorToInt(traveledAnimationTime / clipLength);
        bool isFinalLap = (currentLapIndex >= numberOfLaps - 1);
        float currentLoopTime = traveledAnimationTime % clipLength;

        // 1. TÍNH TOÁN TỐC ĐỘ THEO VÒNG
        float targetSpeed = CalculateLapSpeed(currentLoopTime, currentLapIndex, isFinalLap);
        targetSpeed = Mathf.Max(0.05f, targetSpeed * globalSpeedMultiplier);

        state.speed = -targetSpeed;
        traveledAnimationTime += Time.deltaTime * targetSpeed;

        // 2. HÒA ÂM CHỐNG BỤP
        UpdateLapAudio(targetSpeed, currentLoopTime, currentLapIndex, isFinalLap);

        // 3. ĐÓNG RÀO GA SỚM Ở VÒNG CUỐI
        float startOfFinalLap = clipLength * (numberOfLaps - 1);
        if (isFinalLap && !hasTriggeredEarlyGateClose && traveledAnimationTime >= (startOfFinalLap + 10.0f))
        {
            hasTriggeredEarlyGateClose = true;
            if (seatSwitcher != null)
            {
                seatSwitcher.TriggerEarlyGateClose();
            }
        }

        // 4. VỀ ĐÍCH
        if (traveledAnimationTime >= clipLength * numberOfLaps)
        {
            FinishRide();
        }
    }

    private float CalculateLapSpeed(float currentLoopTime, int lapIndex, bool isFinalLap)
    {
        if (currentLoopTime >= 86.0f)
        {
            if (!isFinalLap)
            {
                return Mathf.Lerp(2.2f, 2.5f, (currentLoopTime - 86f) / 4f);
            }
            return speedCurve.Evaluate(currentLoopTime);
        }

        if (currentLoopTime <= 13.0f)
        {
            if (lapIndex > 0)
            {
                return Mathf.Lerp(2.5f, 1.8f, currentLoopTime / 13.0f);
            }
            return speedCurve.Evaluate(currentLoopTime);
        }

        return speedCurve.Evaluate(currentLoopTime);
    }

    private void UpdateLapAudio(float currentSpeed, float currentLoopTime, int lapIndex, bool isFinalLap)
    {
        // 1. Tiếng ray & gió: Giữ pitch và volume biến thiên mềm mại
        if (trackWindAudio != null)
        {
            float targetT = Mathf.InverseLerp(0.1f, 2.7f, currentSpeed);
            float targetPitch = Mathf.Lerp(minPitch, maxPitch, targetT);
            float targetVol = Mathf.Lerp(minVolume, maxVolume, targetT);

            trackWindAudio.pitch = Mathf.MoveTowards(trackWindAudio.pitch, targetPitch, Time.deltaTime * 0.8f);
            trackWindAudio.volume = Mathf.MoveTowards(trackWindAudio.volume, targetVol, Time.deltaTime * 0.8f);
        }

        // 2. Tiếng xích kéo (Loop liên tục nhưng fade êm dịu, triệt tiêu tiếng bụp)
        if (clankAudio != null)
        {
            bool inFirstLap = (lapIndex == 0);
            float targetClankVolume = 0f;

            if (inFirstLap && currentLoopTime >= liftStartProgress && currentLoopTime <= liftEndProgress)
            {
                // Tính khoảng cách tới đỉnh dốc để fade-out mượt mà
                float remainingLiftTime = liftEndProgress - currentLoopTime;
                if (remainingLiftTime < liftFadeOutDuration)
                {
                    targetClankVolume = Mathf.Lerp(0f, 0.85f, remainingLiftTime / liftFadeOutDuration);
                }
                else
                {
                    targetClankVolume = 0.85f;
                }
            }
            else if (isFinalLap && currentLoopTime >= stationBrakeStartProgress && currentLoopTime <= stationBrakeEndProgress)
            {
                targetClankVolume = 0.6f;
            }

            // Điều khiển phát/dừng dựa trên volume
            if (targetClankVolume > 0f)
            {
                if (!clankAudio.isPlaying)
                {
                    clankAudio.loop = true;
                    clankAudio.volume = 0f;
                    clankAudio.pitch = 1.0f;
                    clankAudio.Play();
                }
                clankAudio.volume = Mathf.MoveTowards(clankAudio.volume, targetClankVolume, Time.deltaTime * 2.0f);
            }
            else
            {
                if (clankAudio.isPlaying)
                {
                    clankAudio.volume = Mathf.MoveTowards(clankAudio.volume, 0f, Time.deltaTime * 3.0f);
                    if (clankAudio.volume <= 0.005f)
                    {
                        clankAudio.Stop();
                    }
                }
            }
        }

        // 3. Tiếng két phanh vào ga
        if (brakeAudio != null && isFinalLap && !hasPlayedBrakeSqueal)
        {
            if (currentLoopTime >= brakeTriggerProgress)
            {
                hasPlayedBrakeSqueal = true;
                brakeAudio.Play();
            }
        }
    }

    public void StartRide()
    {
        if (currentState != RideState.WaitingAtStation || state == null) return;

        traveledAnimationTime = 0f;
        hasTriggeredEarlyGateClose = false;
        hasPlayedBrakeSqueal = false;
        currentState = RideState.Riding;

        if (trackWindAudio != null)
        {
            trackWindAudio.pitch = minPitch;
            trackWindAudio.volume = minVolume;
            trackWindAudio.Play();
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

        if (trackWindAudio != null) trackWindAudio.Stop();
        if (clankAudio != null) clankAudio.Stop();

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

    void Reset()
    {
        GenerateSpeedCurve();
    }

    [ContextMenu("Tao Lai Do Thi Chuan")]
    public void GenerateSpeedCurve()
    {
        speedCurve = new AnimationCurve();
        speedCurve.AddKey(new Keyframe(0f, 0.5f));
        speedCurve.AddKey(new Keyframe(7.5f, 0.35f));
        speedCurve.AddKey(new Keyframe(9.8f, 0.08f));
        speedCurve.AddKey(new Keyframe(10.6f, 0.15f));
        speedCurve.AddKey(new Keyframe(13.2f, 2.75f));
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
        speedCurve.AddKey(new Keyframe(87.0f, 0.9f));
        speedCurve.AddKey(new Keyframe(88.5f, 0.35f));
        speedCurve.AddKey(new Keyframe(89.8f, 0.05f));

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}