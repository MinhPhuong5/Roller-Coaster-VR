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
    public AnimationCurve speedCurve = new AnimationCurve();
    public float globalSpeedMultiplier = 1.0f;

    [Header("1. Lớp Tiếng Ray & Gió (Track & Wind Loop)")]
    public AudioSource trackWindAudio;
    public float minPitch = 0.8f;
    public float maxPitch = 1.6f;
    public float minVolume = 0.3f;
    public float maxVolume = 1.0f;

    [Header("2. Lớp Tiếng Lạch Cạch Xích Kéo (Chain Lift Loop)")]
    public AudioSource clankAudio;
    public float liftStartProgress = 0.2f;
    public float liftEndProgress = 10.2f;
    public float liftFadeOutDuration = 0.8f;
    public float stationBrakeStartProgress = 86.5f;
    public float stationBrakeEndProgress = 89.8f;

    [Header("3. Lớp Tiếng Két Phanh Vào Ga (Brake Squeal)")]
    public AudioSource brakeAudio;
    public float brakeTriggerProgress = 88.0f;
    private bool hasPlayedBrakeSqueal = false;

    [Header("4. Quản lý UI Chuyển Cảnh & Hỏi Chơi Tiếp")]
    public GameObject rideUIPanel;
    public GameObject selectSeatGroup;
    public GameObject gameOverGroup;

    [Header("5. Dịch Chuyển Người Chơi Về Map")]
    public GameObject xrOriginObject;
    public Transform mapReturnPoint;

    [Header("Tùy chọn kết thúc")]
    public bool reloadSceneOnFinish = false;
    public float delayBeforeReload = 1f;

    public enum RideState { WaitingAtStation, Riding, Finished }
    public RideState currentState = RideState.WaitingAtStation;

    private float traveledAnimationTime = 0f;
    private bool hasTriggeredEarlyGateClose = false;

    public float CurrentTraveledTime => traveledAnimationTime;
    public float ActualStationTime => Mathf.Max(0f, state != null ? state.length - stationTime : 0f);

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

        if (selectSeatGroup != null) selectSeatGroup.SetActive(true);
        if (gameOverGroup != null) gameOverGroup.SetActive(false);

        if (seatSwitcher == null)
        {
            seatSwitcher = Object.FindAnyObjectByType<SeatSwitcher>();
        }

        if (xrOriginObject == null)
        {
            XRFallbackWalkController walk = Object.FindAnyObjectByType<XRFallbackWalkController>();
            if (walk != null) xrOriginObject = walk.gameObject;
        }
    }

    void Update()
    {
        if (currentState != RideState.Riding || state == null) return;

        float clipLength = state.length > 0f ? state.length : 90f;
        int currentLapIndex = Mathf.FloorToInt(traveledAnimationTime / clipLength);
        bool isFinalLap = (currentLapIndex >= numberOfLaps - 1);
        float currentLoopTime = traveledAnimationTime % clipLength;

        // 1. Tính tốc độ tua animation
        float targetSpeed = CalculateLapSpeed(currentLoopTime, currentLapIndex, isFinalLap);
        targetSpeed = Mathf.Max(0.12f, targetSpeed * globalSpeedMultiplier);

        state.speed = -targetSpeed;
        traveledAnimationTime += Time.deltaTime * targetSpeed;

        // 2. Cập nhật âm thanh ray & xích kéo
        UpdateLapAudio(targetSpeed, currentLoopTime, currentLapIndex, isFinalLap);

        // 3. Đóng rào sớm vòng cuối
        float startOfFinalLap = clipLength * (numberOfLaps - 1);
        if (isFinalLap && !hasTriggeredEarlyGateClose && traveledAnimationTime >= (startOfFinalLap + 10.0f))
        {
            hasTriggeredEarlyGateClose = true;
            if (seatSwitcher != null)
            {
                seatSwitcher.TriggerEarlyGateClose();
            }
        }

        // 4. Về đích: Đợi chạy đủ toàn bộ thời lượng vòng chạy
        if (traveledAnimationTime >= (clipLength * numberOfLaps))
        {
            FinishRide();
        }
    }

    private float CalculateLapSpeed(float currentLoopTime, int lapIndex, bool isFinalLap)
    {
        if (currentLoopTime >= 86.0f)
        {
            if (!isFinalLap) return Mathf.Lerp(2.2f, 2.5f, (currentLoopTime - 86f) / 4f);
            return speedCurve.Evaluate(currentLoopTime);
        }

        if (currentLoopTime <= 13.0f)
        {
            if (lapIndex > 0) return Mathf.Lerp(2.5f, 1.8f, currentLoopTime / 13.0f);

            if (currentLoopTime < 5.5f)
            {
                return Mathf.Lerp(0.15f, 0.42f, currentLoopTime / 5.5f);
            }

            if (currentLoopTime <= 9.0f)
            {
                return 0.42f;
            }

            return speedCurve.Evaluate(currentLoopTime);
        }

        return speedCurve.Evaluate(currentLoopTime);
    }

    private void UpdateLapAudio(float currentSpeed, float currentLoopTime, int lapIndex, bool isFinalLap)
    {
        if (trackWindAudio != null)
        {
            float targetT = Mathf.InverseLerp(0.1f, 2.7f, currentSpeed);
            trackWindAudio.pitch = Mathf.MoveTowards(trackWindAudio.pitch, Mathf.Lerp(minPitch, maxPitch, targetT), Time.deltaTime * 0.8f);
            trackWindAudio.volume = Mathf.MoveTowards(trackWindAudio.volume, Mathf.Lerp(minVolume, maxVolume, targetT), Time.deltaTime * 0.8f);
        }

        if (clankAudio != null)
        {
            bool inFirstLap = (lapIndex == 0);
            float targetClankVolume = 0f;

            if (inFirstLap && currentLoopTime >= liftStartProgress && currentLoopTime <= liftEndProgress)
            {
                float remainingLiftTime = liftEndProgress - currentLoopTime;
                targetClankVolume = (remainingLiftTime < liftFadeOutDuration) ? Mathf.Lerp(0f, 0.85f, remainingLiftTime / liftFadeOutDuration) : 0.85f;
            }
            else if (isFinalLap && currentLoopTime >= stationBrakeStartProgress && currentLoopTime <= stationBrakeEndProgress)
            {
                targetClankVolume = 0.6f;
            }

            if (targetClankVolume > 0f)
            {
                if (!clankAudio.isPlaying)
                {
                    clankAudio.loop = true;
                    clankAudio.volume = 0f;
                    clankAudio.pitch = 1.0f;
                    clankAudio.Play();
                }
                clankAudio.volume = Mathf.MoveTowards(clankAudio.volume, targetClankVolume, Time.deltaTime * 2.5f);
            }
            else if (clankAudio.isPlaying)
            {
                clankAudio.volume = Mathf.MoveTowards(clankAudio.volume, 0f, Time.deltaTime * 3.5f);
                if (clankAudio.volume <= 0.005f) clankAudio.Stop();
            }
        }

        if (brakeAudio != null && isFinalLap && !hasPlayedBrakeSqueal && currentLoopTime >= brakeTriggerProgress)
        {
            hasPlayedBrakeSqueal = true;
            brakeAudio.Play();
        }
    }

    public void StartRide()
    {
        if (state == null) return;
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

        if (seatSwitcher != null) seatSwitcher.HideUI();
        if (rideUIPanel != null) rideUIPanel.SetActive(false);
    }

    private void FinishRide()
    {
        if (currentState == RideState.Finished) return;
        currentState = RideState.Finished;

        Debug.Log("[RideController] Tàu bắt đầu vào ga phanh -> Gọi SeatSwitcher xử lý dừng hẳn.");

        state.time = ActualStationTime;
        state.speed = 0f;
        anim.Sample();

        if (trackWindAudio != null) trackWindAudio.Stop();
        if (clankAudio != null) clankAudio.Stop();

        if (seatSwitcher != null)
        {
            seatSwitcher.ExitCar();
        }
        else
        {
            if (rideUIPanel != null) rideUIPanel.SetActive(true);
            if (selectSeatGroup != null) selectSeatGroup.SetActive(false);
            if (gameOverGroup != null) gameOverGroup.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (reloadSceneOnFinish) Invoke(nameof(ReloadScene), delayBeforeReload);
    }

    // Sự kiện nút CÓ (BtnYes)
    public void OnClick_PlayAgain()
    {
        currentState = RideState.WaitingAtStation;
        traveledAnimationTime = 0f;

        if (seatSwitcher != null)
        {
            seatSwitcher.ForceBoardingMode();
        }
        else
        {
            if (gameOverGroup != null) gameOverGroup.SetActive(false);
            if (selectSeatGroup != null) selectSeatGroup.SetActive(true);
        }
    }

    // Sự kiện nút KHÔNG (BtnNo)
    public void OnClick_ExitToMap()
    {
        if (seatSwitcher != null)
        {
            seatSwitcher.ReturnToParkMap();
        }
        else
        {
            if (rideUIPanel != null) rideUIPanel.SetActive(false);
            if (xrOriginObject != null && mapReturnPoint != null)
            {
                xrOriginObject.transform.SetParent(null);
                xrOriginObject.transform.localScale = Vector3.one;
                CharacterController cc = xrOriginObject.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                xrOriginObject.transform.SetPositionAndRotation(mapReturnPoint.position, mapReturnPoint.rotation);
                if (cc != null) cc.enabled = true;
                Physics.SyncTransforms();

                XRFallbackWalkController walkCtrl = xrOriginObject.GetComponent<XRFallbackWalkController>();
                if (walkCtrl != null) walkCtrl.enabled = true;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void ResetToStation() => currentState = RideState.WaitingAtStation;
    private void ReloadScene() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    void Reset() => GenerateSpeedCurve();

    [ContextMenu("Tao Lai Do Thi Chuan")]
    public void GenerateSpeedCurve()
    {
        speedCurve = new AnimationCurve();
        speedCurve.AddKey(new Keyframe(0f, 0.15f));
        speedCurve.AddKey(new Keyframe(5.5f, 0.42f));
        speedCurve.AddKey(new Keyframe(9.0f, 0.42f));
        speedCurve.AddKey(new Keyframe(9.5f, 0.35f));
        speedCurve.AddKey(new Keyframe(9.9f, 0.18f));
        speedCurve.AddKey(new Keyframe(10.4f, 0.35f));
        speedCurve.AddKey(new Keyframe(12.8f, 2.85f));
        speedCurve.AddKey(new Keyframe(17.0f, 1.3f));
        speedCurve.AddKey(new Keyframe(20.0f, 2.0f));
        speedCurve.AddKey(new Keyframe(30.0f, 0.7f));
        speedCurve.AddKey(new Keyframe(35.5f, 2.4f));
        speedCurve.AddKey(new Keyframe(45.0f, 1.7f));
        speedCurve.AddKey(new Keyframe(54.0f, 1.8f));
        speedCurve.AddKey(new Keyframe(62.0f, 2.0f));
        speedCurve.AddKey(new Keyframe(74.0f, 0.6f));
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