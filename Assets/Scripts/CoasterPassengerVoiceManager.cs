using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quản lý âm thanh tiếng hú hét / la hét và tiếng thở phào nhẹ nhõm của 4 hành khách trên tàu lượn:
/// - Phân chia theo 4 nhóm: Đàn ông (Male), Phụ nữ (Female), Trẻ em (Kids), Người già (Elder).
/// - Mỗi nhóm phân chia: Hét Ngắn (Short Screams), Hét Dài (Long Screams) và Thở Phào Về Ga (Relief Sounds).
/// - NGUYÊN TẮC VÀNG:
///   1. HÉT LÀ PHẢI HÉT HẾT CÂU: Tuyệt đối không ngắt ngang/fade-out đột ngột khi tàu bắt đầu chậm lại. Tiếng hét luôn vang trọn vẹn đến hết sóng âm của clip.
///   2. NỐI ĐUÔI TỰ NHIÊN: Khi vừa hét xong 1 câu mà tàu vẫn đang lao nhanh -> Tự động nối tiếp câu hét/hò reo tiếp theo.
///   3. KHÔNG ĐÈ ÂM THANH: 1 NPC tại 1 thời điểm chỉ phát đúng 1 clip, không bao giờ tự đè 2 âm thanh lên nhau.
///   4. THỞ PHÀO KHI VỀ GA: Khi tàu phanh đỗ an toàn tại ga -> Phát tiếng thở phào nhẹ nhõm (Phùuu, Haizzz, haha).
/// </summary>
public class CoasterPassengerVoiceManager : MonoBehaviour
{
    public enum PassengerVoiceType
    {
        Male,
        Female,
        Kid,
        Elder,
        None
    }

    [Serializable]
    public class VoiceCategory
    {
        [Tooltip("Danh sách âm thanh hét ngắn (0.8s - 2.0s)")]
        public List<AudioClip> shortScreams = new List<AudioClip>();

        [Tooltip("Danh sách âm thanh hét dài (2.5s - 4.5s)")]
        public List<AudioClip> longScreams = new List<AudioClip>();

        [Tooltip("Danh sách âm thanh thở phào nhẹ nhõm khi về ga (Phùuu, Phew, Sigh)")]
        public List<AudioClip> reliefSounds = new List<AudioClip>();

        public AudioClip GetRandomClip(bool isLong, AudioClip excludeClip = null)
        {
            List<AudioClip> targetList = isLong ? longScreams : shortScreams;
            if (targetList == null || targetList.Count == 0)
            {
                targetList = isLong ? shortScreams : longScreams;
            }

            if (targetList == null || targetList.Count == 0) return null;

            List<AudioClip> candidates = new List<AudioClip>(targetList);
            if (candidates.Count > 1 && excludeClip != null)
            {
                candidates.Remove(excludeClip);
            }

            if (candidates.Count == 0) candidates = targetList;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        public AudioClip GetRandomReliefClip(AudioClip excludeClip = null)
        {
            if (reliefSounds == null || reliefSounds.Count == 0) return null;

            List<AudioClip> candidates = new List<AudioClip>(reliefSounds);
            if (candidates.Count > 1 && excludeClip != null)
            {
                candidates.Remove(excludeClip);
            }

            if (candidates.Count == 0) candidates = reliefSounds;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }
    }

    [Header("1. KHO ÂM THANH THEO TỪNG ĐỐI TƯỢNG")]
    public VoiceCategory maleVoices = new VoiceCategory();
    public VoiceCategory femaleVoices = new VoiceCategory();
    public VoiceCategory kidVoices = new VoiceCategory();
    public VoiceCategory elderVoices = new VoiceCategory();

    [Header("2. CẤU HÌNH ÂM LƯỢNG & 3D SPATIAL")]
    [Range(0f, 1.5f)] public float screamVolume = 1.0f;
    [Range(0f, 1.5f)] public float reliefVolume = 0.9f;
    [Range(0.8f, 1.2f)] public float minPitchVariation = 0.96f;
    [Range(0.8f, 1.2f)] public float maxPitchVariation = 1.04f;
    [Tooltip("Khoảng cách tối đa nghe thấy tiếng hét (Mét) - Cho phép nghe rõ từ Flycam trên cao và khắp công viên")]
    public float maxAudioDistance = 250.0f;
    [Tooltip("Khoảng cách tối thiểu đạt 100% âm lượng (Mét)")]
    public float minAudioDistance = 3.5f;
    [Tooltip("Khoảng nghỉ lấy hơi tự nhiên giữa 2 lần hét liên tiếp trong cùng 1 con dốc dài (giây)")]
    public float breathGapBetweenScreams = 0.25f;

    [Header("3. CẤU HÌNH TIẾNG HÉT CỦA NGƯỜI CHƠI")]
    [Tooltip("Bật tiếng hét cho chính nhân vật NPC đại diện của người chơi")]
    public bool includePlayerVoice = true;
    [Tooltip("Hệ số âm lượng riêng cho người chơi (Mặc định 1.0)")]
    [Range(0f, 1.5f)] public float playerVolumeMultiplier = 1.0f;
    [HideInInspector] public int playerSeatIndex = -1;

    [Header("4. 4 NGUỒN PHÁT 3D TƯƠNG ỨNG 4 GHẾ")]
    public AudioSource[] seatAudioSources = new AudioSource[4];

    // Trạng thái của 4 ghế
    private PassengerVoiceType[] assignedVoiceTypes = new PassengerVoiceType[4]
    {
        PassengerVoiceType.None,
        PassengerVoiceType.None,
        PassengerVoiceType.None,
        PassengerVoiceType.None
    };

    private float[] seatNextPlayTimers = new float[4];
    private Coroutine[] activePlayCoroutines = new Coroutine[4];
    private AudioClip[] lastPlayedClips = new AudioClip[4];
    private bool isCurrentlyInThrill = false;
    private bool isCurrentThrillMajor = false;
    private bool hasPlayedReliefThisRide = false;

    void Awake()
    {
        EnsureAudioSources();
        AutoLoadDefaultClipsIfEmpty();
    }

    void Start()
    {
        EnsureAudioSources();
        AutoLoadDefaultClipsIfEmpty();
    }

    void Update()
    {
        // 1. Cập nhật bộ đếm thời gian cho từng ghế
        for (int i = 0; i < 4; i++)
        {
            if (seatNextPlayTimers[i] > 0f)
            {
                seatNextPlayTimers[i] -= Time.deltaTime;
            }
        }

        // Đảm bảo 4 AudioSource luôn bám theo 4 ghế trên toa tàu
        EnsureAudioSourcesAttachedToTrain();

        // 2. NỐI TIẾP ÂM THANH LIỀN MẠCH:
        // Nếu tàu đang trong đoạn tốc độ cao / cảm giác mạnh, ghế nào vừa hét xong câu trước thì tự động lấy hơi và hét nối tiếp câu sau
        if (isCurrentlyInThrill)
        {
            ContinuousScreamMaintainer();
        }
    }

    /// <summary>
    /// Đảm bảo có đủ 4 AudioSource 3D cho 4 ghế
    /// </summary>
    public void EnsureAudioSources()
    {
        if (seatAudioSources == null || seatAudioSources.Length != 4)
        {
            seatAudioSources = new AudioSource[4];
        }

        for (int i = 0; i < 4; i++)
        {
            if (seatAudioSources[i] == null)
            {
                Transform childSrc = transform.Find($"SeatVoiceSource_{i}");
                GameObject srcObj;
                if (childSrc == null)
                {
                    srcObj = new GameObject($"SeatVoiceSource_{i}");
                    srcObj.transform.SetParent(transform, false);
                    srcObj.transform.localPosition = Vector3.zero;
                }
                else
                {
                    srcObj = childSrc.gameObject;
                }

                AudioSource src = srcObj.GetComponent<AudioSource>();
                if (src == null) src = srcObj.AddComponent<AudioSource>();
                seatAudioSources[i] = src;
            }

            // Luôn áp dụng cấu hình 3D Spatial Audio chuẩn xác 100%
            if (seatAudioSources[i] != null)
            {
                ConfigureTrue3DSpatialAudio(seatAudioSources[i]);
            }
        }

        EnsureAudioSourcesAttachedToTrain();
    }

    /// <summary>
    /// Cấu hình chuẩn 100% 3D Spatial Audio: Định vị âm thanh chính xác theo không gian 3D, Doppler và suy giảm âm lượng tự nhiên
    /// </summary>
    public void ConfigureTrue3DSpatialAudio(AudioSource src)
    {
        if (src == null) return;
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 1.0f; // 100% 3D Spatial Audio (Tuyệt đối không pha 2D stereo trung tâm)
        src.spread = 0f; // 0 độ để phân tách tuyệt đối 2 tai trái/phải theo vị trí góc nhìn
        src.dopplerLevel = 1.0f; // Hiệu ứng Doppler vật lý sống động khi tàu lượn lao vun vút
        src.minDistance = 2.5f;
        src.maxDistance = 160.0f;

        // Thiết lập đường cong suy giảm âm lượng tự nhiên theo cự ly thực tế (Ngồi trong tàu: 100%, Flycam trên cao 35m: ~48% rõ nét và chân thực)
        src.rolloffMode = AudioRolloffMode.Custom;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 1.0f);
        curve.AddKey(2.5f / 160f, 1.0f);
        curve.AddKey(15.0f / 160f, 0.72f);
        curve.AddKey(35.0f / 160f, 0.48f);
        curve.AddKey(65.0f / 160f, 0.28f);
        curve.AddKey(100.0f / 160f, 0.12f);
        curve.AddKey(1.0f, 0.0f);
        for (int k = 0; k < curve.keys.Length; k++)
        {
            curve.SmoothTangents(k, 0f);
        }
        src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
        src.volume = screamVolume;
    }

    /// <summary>
    /// Đảm bảo 4 AudioSource luôn bám chặt theo 4 ghế trên toa tàu đang di chuyển
    /// </summary>
    public void EnsureAudioSourcesAttachedToTrain()
    {
        CoasterPassengerManager passengerMgr = GetComponent<CoasterPassengerManager>();
        if (passengerMgr == null) passengerMgr = GetComponentInParent<CoasterPassengerManager>();
        if (passengerMgr == null) passengerMgr = Object.FindAnyObjectByType<CoasterPassengerManager>();

        if (passengerMgr != null && passengerMgr.sitPoints != null)
        {
            for (int i = 0; i < 4; i++)
            {
                if (seatAudioSources[i] == null) continue;
                if (i < passengerMgr.sitPoints.Length && passengerMgr.sitPoints[i] != null)
                {
                    Transform target = (passengerMgr.passengerHeadBones != null && i < passengerMgr.passengerHeadBones.Length && passengerMgr.passengerHeadBones[i] != null)
                        ? passengerMgr.passengerHeadBones[i]
                        : passengerMgr.sitPoints[i];

                    if (seatAudioSources[i].transform.parent != target)
                    {
                        seatAudioSources[i].transform.SetParent(target, false);
                        seatAudioSources[i].transform.localPosition = Vector3.zero;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gắn vị trí AudioSource bám sát theo xương đầu hoặc đệm ghế của từng hành khách
    /// </summary>
    public void AttachAudioSourcesToSeats(Transform[] seatTransforms, Transform[] headTransforms)
    {
        EnsureAudioSources();

        for (int i = 0; i < 4; i++)
        {
            if (seatAudioSources[i] == null) continue;

            Transform targetAttach = null;
            if (headTransforms != null && i < headTransforms.Length && headTransforms[i] != null)
            {
                targetAttach = headTransforms[i];
            }
            else if (seatTransforms != null && i < seatTransforms.Length && seatTransforms[i] != null)
            {
                targetAttach = seatTransforms[i];
            }

            if (targetAttach != null)
            {
                seatAudioSources[i].transform.SetParent(targetAttach, false);
                seatAudioSources[i].transform.localPosition = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// Phân loại nhóm giọng cho ghế dựa trên tên Prefab hoặc GameObject của nhân vật
    /// </summary>
    public void SetPassengerVoiceType(int seatIndex, GameObject passengerObject)
    {
        if (seatIndex < 0 || seatIndex >= 4) return;

        if (passengerObject == null)
        {
            assignedVoiceTypes[seatIndex] = PassengerVoiceType.None;
            return;
        }

        string objName = passengerObject.name.ToLower();
        assignedVoiceTypes[seatIndex] = DetectVoiceTypeFromName(objName);
        hasPlayedReliefThisRide = false;

        Debug.Log($"<color=#00FFFF>[CoasterPassengerVoiceManager] Ghế {seatIndex} ({passengerObject.name}) -> Gán nhóm giọng: {assignedVoiceTypes[seatIndex]}</color>");
    }

    public static PassengerVoiceType DetectVoiceTypeFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return PassengerVoiceType.Male;
        string lower = name.ToLower();

        // 1. Trẻ em
        if (lower.Contains("kid") || lower.Contains("boy") || lower.Contains("child") || lower.Contains("little"))
        {
            return PassengerVoiceType.Kid;
        }

        // 2. Người già
        if (lower.Contains("elder") || lower.Contains("old") || lower.Contains("grandma") || lower.Contains("grandpa"))
        {
            return PassengerVoiceType.Elder;
        }

        // 3. Nữ
        if (lower.Contains("female") || lower.Contains("woman") || lower.Contains("girl") || lower.Contains("police") || lower.Contains("prosthetic"))
        {
            return PassengerVoiceType.Female;
        }

        // 4. Nam (mặc định cho các nhân vật nam/casual_male/doctor/worker)
        return PassengerVoiceType.Male;
    }

    /// <summary>
    /// Cập nhật trạng thái cảm giác mạnh liên tục từ CoasterPassengerManager mỗi frame
    /// </summary>
    public void UpdateThrillState(bool isThrilling, bool isMajorSection)
    {
        isCurrentThrillMajor = isMajorSection;

        if (isThrilling)
        {
            if (!isCurrentlyInThrill)
            {
                // BẮT ĐẦU VÀO ĐOẠN CẢM GIÁC MẠNH: Kích hoạt tiếng hét
                isCurrentlyInThrill = true;
                TriggerThrillScreamAll(isMajorSection);
            }
        }
        else
        {
            // THOÁT KHỎI ĐOẠN CẢM GIÁC MẠNH (Tàu bắt đầu chậm lại / lên dốc):
            // CHỈ chuyển cờ isCurrentlyInThrill = false để KHÔNG phát thêm câu mới.
            // TUYỆT ĐỐI KHÔNG ngắt ngang hay tắt tiếng câu hét đang phát dở, để câu hét vang trọn vẹn đến hết sóng âm tự nhiên!
            isCurrentlyInThrill = false;
        }
    }

    /// <summary>
    /// Tự động kiểm tra và nối tiếp tiếng hét cho các ghế đã phát xong câu trước nhưng tàu vẫn đang lao nhanh trên ray
    /// </summary>
    private void ContinuousScreamMaintainer()
    {
        for (int i = 0; i < 4; i++)
        {
            if (assignedVoiceTypes[i] == PassengerVoiceType.None) continue;
            if (i == playerSeatIndex && !includePlayerVoice) continue;

            AudioSource src = seatAudioSources[i];
            if (src == null) continue;

            // TUYỆT ĐỐI KHÔNG ĐÈ ÂM THANH: Chỉ khi ghế này đã phát xong 100% câu trước (src.isPlaying == false) và đã hết thời gian lấy hơi
            if (!src.isPlaying && seatNextPlayTimers[i] <= 0f && activePlayCoroutines[i] == null)
            {
                VoiceCategory category = GetCategoryByVoiceType(assignedVoiceTypes[i]);
                if (category == null) continue;

                // Nối tiếp câu hét tiếp theo (Ưu tiên Hét Dài nếu là đoạn dốc lớn)
                bool preferLong = isCurrentThrillMajor && (UnityEngine.Random.value > 0.35f);
                AudioClip clip = category.GetRandomClip(preferLong, lastPlayedClips[i]);
                if (clip == null) continue;

                lastPlayedClips[i] = clip;

                float stagger = UnityEngine.Random.Range(0.04f, 0.25f);
                float pitch = UnityEngine.Random.Range(minPitchVariation, maxPitchVariation);

                if (gameObject.activeInHierarchy)
                {
                    activePlayCoroutines[i] = StartCoroutine(PlayScreamWithDelayRoutine(i, clip, stagger, pitch));
                }
                else
                {
                    PlaySingleScreamImmediate(i, clip, pitch);
                }

                // Khóa thời gian chờ đúng bằng độ dài của clip mới + khoảng nghỉ lấy hơi tự nhiên
                seatNextPlayTimers[i] = clip.length + UnityEngine.Random.Range(breathGapBetweenScreams, breathGapBetweenScreams + 0.35f);
            }
        }
    }

    /// <summary>
    /// Kích hoạt tiếng hét mở màn cho các ghế khi bắt đầu vào đoạn cảm giác mạnh
    /// </summary>
    public void TriggerThrillScreamAll(bool isLongDrop)
    {
        EnsureAudioSources();

        List<AudioClip> pickedClipsThisTrigger = new List<AudioClip>();

        for (int i = 0; i < 4; i++)
        {
            if (assignedVoiceTypes[i] == PassengerVoiceType.None) continue;
            if (i == playerSeatIndex && !includePlayerVoice) continue;

            AudioSource src = seatAudioSources[i];
            // Nếu ghế này ĐANG HÉT DỞ CÂU TRƯỚC -> ĐỂ HÉT CHO HẾT, TUYỆT ĐỐI KHÔNG NGẮT HAY ĐÈ LÊN!
            if (src != null && src.isPlaying) continue;

            VoiceCategory category = GetCategoryByVoiceType(assignedVoiceTypes[i]);
            if (category == null) continue;

            // Bốc ngẫu nhiên clip (Ưu tiên Hét Dài cho đoạn dốc chính)
            AudioClip clip = category.GetRandomClip(isLongDrop, lastPlayedClips[i]);
            if (clip == null) continue;

            if (pickedClipsThisTrigger.Contains(clip))
            {
                AudioClip alternateClip = category.GetRandomClip(isLongDrop, clip);
                if (alternateClip != null) clip = alternateClip;
            }

            pickedClipsThisTrigger.Add(clip);
            lastPlayedClips[i] = clip;

            float randomStaggerDelay = UnityEngine.Random.Range(0.01f, 0.18f);
            float pitch = UnityEngine.Random.Range(minPitchVariation, maxPitchVariation);

            if (gameObject.activeInHierarchy)
            {
                if (activePlayCoroutines[i] != null) StopCoroutine(activePlayCoroutines[i]);
                activePlayCoroutines[i] = StartCoroutine(PlayScreamWithDelayRoutine(i, clip, randomStaggerDelay, pitch));
            }
            else
            {
                PlaySingleScreamImmediate(i, clip, pitch);
            }

            seatNextPlayTimers[i] = clip.length + UnityEngine.Random.Range(breathGapBetweenScreams, breathGapBetweenScreams + 0.35f);
        }
    }

    private IEnumerator PlayScreamWithDelayRoutine(int seatIndex, AudioClip clip, float delay, float pitch)
    {
        if (delay > 0.001f)
        {
            yield return new WaitForSeconds(delay);
        }

        PlaySingleScreamImmediate(seatIndex, clip, pitch);
        activePlayCoroutines[seatIndex] = null;
    }

    private void PlaySingleScreamImmediate(int seatIndex, AudioClip clip, float pitch)
    {
        if (seatIndex < 0 || seatIndex >= 4 || seatAudioSources[seatIndex] == null || clip == null) return;

        AudioSource src = seatAudioSources[seatIndex];

        float finalVolume = screamVolume;
        if (seatIndex == playerSeatIndex)
        {
            finalVolume = screamVolume * playerVolumeMultiplier;
        }

        // Cấu hình 100% True 3D Spatial Audio
        ConfigureTrue3DSpatialAudio(src);
        src.clip = clip;
        src.pitch = pitch;
        src.volume = finalVolume;
        src.loop = false;
        src.Play();
    }

    private int activeReliefPlayingCount = 0;
    private bool isReliefRoutineActive = false;

    /// <summary>
    /// Kiểm tra xem hiện tại có bất kỳ âm thanh thở phào nào đang được chuẩn bị hoặc đang phát trên 4 ghế không
    /// </summary>
    public bool IsReliefActive()
    {
        if (isReliefRoutineActive) return true;
        if (activeReliefPlayingCount > 0) return true;
        for (int i = 0; i < 4; i++)
        {
            if (seatAudioSources[i] != null && seatAudioSources[i].isPlaying) return true;
        }
        return false;
    }

    public bool IsAnyReliefPlaying()
    {
        return IsReliefActive();
    }

    /// <summary>
    /// Kích hoạt tiếng thở phào nhẹ nhõm đồng loạt cho các hành khách khi tàu về đến ga an toàn
    /// </summary>
    public void TriggerStationArrivalReliefAll()
    {
        if (hasPlayedReliefThisRide) return;
        hasPlayedReliefThisRide = true;
        isCurrentlyInThrill = false;

        StartCoroutine(StationArrivalReliefRoutine());
    }

    private IEnumerator StationArrivalReliefRoutine()
    {
        isReliefRoutineActive = true;
        // Chờ 0.2s để tiếng hét cuối dứt hẳn và tàu bắt đầu trôi vào vạch dừng ga
        yield return new WaitForSeconds(0.2f);

        for (int i = 0; i < 4; i++)
        {
            if (assignedVoiceTypes[i] == PassengerVoiceType.None) continue;
            if (i == playerSeatIndex && !includePlayerVoice) continue;

            VoiceCategory category = GetCategoryByVoiceType(assignedVoiceTypes[i]);
            AudioClip reliefClip = null;
            if (category != null)
            {
                reliefClip = category.GetRandomReliefClip();
            }

            // Fallback nếu nhóm giọng này chưa có clip thở phào riêng
            if (reliefClip == null)
            {
                if (femaleVoices != null && femaleVoices.reliefSounds.Count > 0)
                    reliefClip = femaleVoices.GetRandomReliefClip();
                else if (maleVoices != null && maleVoices.reliefSounds.Count > 0)
                    reliefClip = maleVoices.GetRandomReliefClip();
            }

            if (reliefClip == null) continue;

            float delay = UnityEngine.Random.Range(0.08f, 0.45f);
            float pitch = UnityEngine.Random.Range(0.98f, 1.02f);

            StartCoroutine(PlayReliefSoundWithDelay(i, reliefClip, delay, pitch));
        }

        isReliefRoutineActive = false;
    }

    private IEnumerator PlayReliefSoundWithDelay(int seatIndex, AudioClip clip, float delay, float pitch)
    {
        activeReliefPlayingCount++;
        try
        {
            if (delay > 0.001f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (seatIndex < 0 || seatIndex >= 4 || seatAudioSources[seatIndex] == null || clip == null) yield break;

            AudioSource src = seatAudioSources[seatIndex];
            // Chờ nếu ghế này đang phát tiếng hét dở thì đợi nó kết thúc trọn vẹn
            while (src.isPlaying)
            {
                yield return new WaitForSeconds(0.1f);
            }

            float finalVol = reliefVolume;
            if (seatIndex == playerSeatIndex) finalVol *= playerVolumeMultiplier;

            src.clip = clip;
            src.pitch = pitch;
            src.volume = finalVol;
            src.loop = false;
            src.Play();

            // Đợi phát trọn vẹn toàn bộ clip thở phào
            yield return new WaitForSeconds(clip.length / Mathf.Max(0.5f, pitch));
        }
        finally
        {
            activeReliefPlayingCount = Mathf.Max(0, activeReliefPlayingCount - 1);
        }
    }

    public void StopAllScreamsImmediate()
    {
        isCurrentlyInThrill = false;
        hasPlayedReliefThisRide = false;
        activeReliefPlayingCount = 0;
        isReliefRoutineActive = false;

        for (int i = 0; i < 4; i++)
        {
            if (activePlayCoroutines[i] != null)
            {
                StopCoroutine(activePlayCoroutines[i]);
                activePlayCoroutines[i] = null;
            }
            if (seatAudioSources[i] != null)
            {
                seatAudioSources[i].Stop();
                seatAudioSources[i].volume = screamVolume;
            }
            seatNextPlayTimers[i] = 0f;
        }
    }

    public VoiceCategory GetCategoryByVoiceType(PassengerVoiceType type)
    {
        switch (type)
        {
            case PassengerVoiceType.Male: return maleVoices;
            case PassengerVoiceType.Female: return femaleVoices;
            case PassengerVoiceType.Kid: 
                if (kidVoices != null && (kidVoices.shortScreams.Count > 0 || kidVoices.longScreams.Count > 0 || kidVoices.reliefSounds.Count > 0)) 
                    return kidVoices;
                return femaleVoices;
            case PassengerVoiceType.Elder: 
                if (elderVoices != null && (elderVoices.shortScreams.Count > 0 || elderVoices.longScreams.Count > 0 || elderVoices.reliefSounds.Count > 0)) 
                    return elderVoices;
                return femaleVoices;
            default: return null;
        }
    }

    /// <summary>
    /// Tự động nạp toàn bộ các file âm thanh từ folder Assets/Sound/NPC vào đúng nhóm (kể cả tiếng thở phào relief)
    /// </summary>
    [ContextMenu("Tự Động Nạp Âm Thanh Từ Folder NPC")]
    public void AutoLoadDefaultClipsIfEmpty()
    {
#if UNITY_EDITOR
        maleVoices.shortScreams.Clear();
        maleVoices.longScreams.Clear();
        maleVoices.reliefSounds.Clear();

        femaleVoices.shortScreams.Clear();
        femaleVoices.longScreams.Clear();
        femaleVoices.reliefSounds.Clear();

        kidVoices.shortScreams.Clear();
        kidVoices.longScreams.Clear();
        kidVoices.reliefSounds.Clear();

        elderVoices.shortScreams.Clear();
        elderVoices.longScreams.Clear();
        elderVoices.reliefSounds.Clear();

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sound/NPC" });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;

            string fileName = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();

            // 1. Tiếng thở phào nhẹ nhõm (Relief / Sigh / Sight / Phew / Tho_Phao / Haizz)
            if (fileName.Contains("relief") || fileName.Contains("sigh") || fileName.Contains("sight") || fileName.Contains("phew") || fileName.Contains("phu") || fileName.Contains("tho") || fileName.Contains("haizz"))
            {
                if (fileName.StartsWith("m") || fileName.Contains("male") || fileName.Contains("man"))
                {
                    if (!maleVoices.reliefSounds.Contains(clip)) maleVoices.reliefSounds.Add(clip);
                }
                else if (fileName.StartsWith("w") || fileName.StartsWith("f") || fileName.Contains("female") || fileName.Contains("woman") || fileName.Contains("girl"))
                {
                    if (!femaleVoices.reliefSounds.Contains(clip)) femaleVoices.reliefSounds.Add(clip);
                }
                else if (fileName.StartsWith("c") || fileName.StartsWith("k") || fileName.Contains("kid") || fileName.Contains("child"))
                {
                    if (!kidVoices.reliefSounds.Contains(clip)) kidVoices.reliefSounds.Add(clip);
                }
                else if (fileName.StartsWith("e") || fileName.Contains("elder") || fileName.Contains("old"))
                {
                    if (!elderVoices.reliefSounds.Contains(clip)) elderVoices.reliefSounds.Add(clip);
                }
                else
                {
                    if (!maleVoices.reliefSounds.Contains(clip)) maleVoices.reliefSounds.Add(clip);
                    if (!femaleVoices.reliefSounds.Contains(clip)) femaleVoices.reliefSounds.Add(clip);
                }
                continue;
            }

            // 2. Tiếng hét Nam (M)
            if (fileName.StartsWith("m"))
            {
                if (fileName.Contains("long"))
                {
                    if (!maleVoices.longScreams.Contains(clip)) maleVoices.longScreams.Add(clip);
                }
                else
                {
                    if (!maleVoices.shortScreams.Contains(clip)) maleVoices.shortScreams.Add(clip);
                }
            }
            // 3. Tiếng hét Nữ (W hoặc F)
            else if (fileName.StartsWith("w") || fileName.StartsWith("f"))
            {
                if (fileName.Contains("long"))
                {
                    if (!femaleVoices.longScreams.Contains(clip)) femaleVoices.longScreams.Add(clip);
                }
                else
                {
                    if (!femaleVoices.shortScreams.Contains(clip)) femaleVoices.shortScreams.Add(clip);
                }
            }
            // 4. Tiếng hét Trẻ em (C hoặc K)
            else if (fileName.StartsWith("c") || fileName.StartsWith("k"))
            {
                if (fileName.Contains("long"))
                {
                    if (!kidVoices.longScreams.Contains(clip)) kidVoices.longScreams.Add(clip);
                }
                else
                {
                    if (!kidVoices.shortScreams.Contains(clip)) kidVoices.shortScreams.Add(clip);
                }
            }
            // 5. Tiếng hét Người già (E)
            else if (fileName.StartsWith("e"))
            {
                if (fileName.Contains("long"))
                {
                    if (!elderVoices.longScreams.Contains(clip)) elderVoices.longScreams.Add(clip);
                }
                else
                {
                    if (!elderVoices.shortScreams.Contains(clip)) elderVoices.shortScreams.Add(clip);
                }
            }
        }

        EditorUtility.SetDirty(this);
        Debug.Log($"<color=#00FF99><b>[CoasterPassengerVoiceManager] Cập nhật kho âm thanh: Nam(S:{maleVoices.shortScreams.Count}, L:{maleVoices.longScreams.Count}, Relief:{maleVoices.reliefSounds.Count}), Nữ(S:{femaleVoices.shortScreams.Count}, L:{femaleVoices.longScreams.Count}, Relief:{femaleVoices.reliefSounds.Count}), Trẻ em(S:{kidVoices.shortScreams.Count}, L:{kidVoices.longScreams.Count}, Relief:{kidVoices.reliefSounds.Count})</b></color>");
#endif
    }
}
