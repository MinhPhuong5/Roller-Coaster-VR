using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quản lý phát nhạc nền CHUẨN 3D SPATIAL AUDIO CỐ ĐỊNH TRONG KHÔNG GIAN CÔNG VIÊN:
/// - Âm thanh phát từ các cột loa 3D (Park Speakers) đặt cố định tại các vị trí thực trong công viên (Quảng trường, Quầy vé, Lối đi...).
/// - Chuẩn 100% 3D Spatial Audio: Người chơi ở gần loa nghe to, đi ra xa âm lượng giảm dần theo cự ly thực tế, quay đầu nghe rõ hướng loa (Trái/Phải/Trước/Sau).
/// - Đồng bộ tất cả các loa cùng phát chung 1 bài hát (Hệ thống truyền thanh công viên PA System).
/// - Phát ngẫu nhiên (Shuffle) và TỰ ĐỘNG CHUYỂN BÀI khi hết bài (hoặc gần hết bài bằng Crossfade êm ái), không bao giờ lặp lại 1 bài đơn lẻ.
/// - Hỗ trợ phím tắt N (Next bài) / B (Lùi bài) để test nhanh trên PC.
/// - Khi đi sang khu vực ga tàu lượn / lên tàu: Âm thanh tự động tắt hoàn toàn để không ảnh hưởng đến trải nghiệm tàu lượn.
/// </summary>
public class ParkPlaylist : MonoBehaviour
{
    public static ParkPlaylist Instance { get; private set; }

    [Header("1. Danh Sách Nhạc Công Viên")]
    [Tooltip("Danh sách các bài hát (Tự động nạp từ thư mục Assets/Sound/Park)")]
    public AudioClip[] playlist;

    [Header("2. Cấu Hình Âm Lượng & 3D Spatial")]
    [Range(0f, 1.5f)]
    [Tooltip("Âm lượng tối đa của các loa công viên")]
    public float masterVolume = 0.85f;

    [Tooltip("Khoảng cách tối thiểu đạt 100% âm lượng quanh cột loa (Mét)")]
    public float minSpeakerDistance = 4.0f;

    [Tooltip("Khoảng cách tối đa nghe thấy tiếng loa (Mét) - Vượt quá khoảng cách này âm thanh tắt hẳn 0%")]
    public float maxSpeakerDistance = 32.0f;

    [Tooltip("Tự động phát xáo trộn ngẫu nhiên")]
    public bool shuffle = true;

    [Tooltip("Lặp lại danh sách khi phát hết")]
    public bool loopPlaylist = true;

    [Tooltip("Thời gian chuyển tiếp êm giữa các bài hát (Giây)")]
    public float crossfadeDuration = 2.0f;

    [Tooltip("Tự động phát nhạc ngay khi Play game")]
    public bool autoPlayOnStart = true;

    [Header("3. Phím Tắt Bàn Phím (PC Testing)")]
    [Tooltip("Bật phím tắt N (Next bài) và B (Previous bài) trên máy tính")]
    public bool enableKeyboardShortcuts = true;

    [Header("4. Danh Sách Các Cột Loa 3D Trong Công Viên")]
    [Tooltip("Kéo các vị trí cột loa 3D trong Scene vào đây (Nếu để trống, script sẽ tự lấy chính GameObject này hoặc các con của nó làm loa 3D)")]
    public List<Transform> speakerPositions = new List<Transform>();

    [Header("5. Giới Hạn Phạm Vi Phát Nhạc")]
    [Tooltip("Tắt âm hoàn toàn khi người chơi bước vào ga tàu lượn hoặc đang đi tàu")]
    public bool muteInStationAndCoaster = true;

    [Tooltip("Độ cao mặt sàn tối đa của công viên (Mét) - Ga tàu lượn nằm trên cao > 4.5m")]
    public float parkMaxAltitude = 4.5f;

    // Quản lý AudioSources trên từng loa
    private class SpeakerChannel
    {
        public Transform speakerTransform;
        public AudioSource sourceA;
        public AudioSource sourceB;
    }

    private List<SpeakerChannel> speakerChannels = new List<SpeakerChannel>();
    private bool isSourceAPlaying = true;

    private int currentTrackIndex = -1;
    private List<int> playedIndices = new List<int>();
    private Coroutine crossfadeCoroutine;
    private SeatSwitcher seatSwitcher;
    private RideController rideController;
    private Transform playerCameraOrRig;
    private float currentZoneVolumeFactor = 1.0f;
    private bool isManualPaused = false;
    private bool isZonePaused = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        playedIndices.Clear();
        Setup3DSpeakers();
        AutoLoadClipsIfEmpty();
    }

    void Start()
    {
        Setup3DSpeakers();
        AutoLoadClipsIfEmpty();

        seatSwitcher = Object.FindAnyObjectByType<SeatSwitcher>();
        rideController = Object.FindAnyObjectByType<RideController>();

        currentZoneVolumeFactor = IsPlayerInParkArea() ? 1.0f : 0f;

        if (autoPlayOnStart && playlist != null && playlist.Length > 0)
        {
            PlayNextTrack();
        }
    }

    void Update()
    {
        // 1. Phím tắt chuyển bài nhanh khi test trên PC (N: Next, B: Previous)
        if (enableKeyboardShortcuts)
        {
            HandleKeyboardInput();
        }

        // 2. Quản lý âm lượng theo vị trí (Công viên = 100%, Nhà ga & Tàu lượn = Tắt hoàn toàn)
        UpdateZoneVolumeTransition();

        // 3. Tự động chuyển bài khi bài hiện tại kết thúc hoặc sắp hết (Crossfade mượt mà)
        CheckAutoTrackAdvance();
    }

    private void HandleKeyboardInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.nKey.wasPressedThisFrame)
            {
                Debug.Log("<color=cyan>[ParkPlaylist] Phím N -> Chuyển sang bài ngẫu nhiên tiếp theo!</color>");
                PlayNextTrack();
            }
            else if (Keyboard.current.bKey.wasPressedThisFrame)
            {
                Debug.Log("<color=cyan>[ParkPlaylist] Phím B -> Quay lại bài trước!</color>");
                PlayPreviousTrack();
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.N))
        {
            PlayNextTrack();
        }
        else if (Input.GetKeyDown(KeyCode.B))
        {
            PlayPreviousTrack();
        }
#endif
    }

    /// <summary>
    /// Kiểm tra trạng thái phát để tự động chuyển sang bài tiếp theo khi bài hiện tại kết thúc
    /// </summary>
    private void CheckAutoTrackAdvance()
    {
        if (speakerChannels.Count == 0 || playlist == null || playlist.Length == 0) return;
        if (isManualPaused || isZonePaused) return;
        if (crossfadeCoroutine != null) return; // Đang chuyển bài dở dang thì bỏ qua

        AudioSource mainActiveSource = isSourceAPlaying ? speakerChannels[0].sourceA : speakerChannels[0].sourceB;
        if (mainActiveSource == null || mainActiveSource.clip == null) return;

        // Trường hợp 1: Bài hát đang phát và sắp hết (còn lại <= crossfadeDuration giây) -> Tự động crossfade sang bài mới
        if (mainActiveSource.isPlaying)
        {
            // Chỉ bắt đầu kiểm tra thời gian còn lại khi bài đã chạy được ít nhất 3 giây (tránh trigger ngay lúc mới bắt đầu)
            if (mainActiveSource.time > 3.0f)
            {
                float remainingTime = mainActiveSource.clip.length - mainActiveSource.time;
                if (remainingTime <= Mathf.Max(0.5f, crossfadeDuration))
                {
                    Debug.Log($"<color=#00FFAA><b>[ParkPlaylist] Bài '{mainActiveSource.clip.name}' sắp hết ({remainingTime:F1}s) -> Tự động chuyển tiếp sang bài mới...</b></color>");
                    PlayNextTrack();
                }
            }
        }
        // Trường hợp 2: Bài hát đã chạy xong hoàn toàn (isPlaying == false) và người chơi đang ở công viên
        else if (!mainActiveSource.isPlaying && currentZoneVolumeFactor > 0.05f)
        {
            Debug.Log($"<color=#00FFAA><b>[ParkPlaylist] Bài '{mainActiveSource.clip.name}' đã phát xong -> Chuyển sang bài tiếp theo...</b></color>");
            PlayNextTrack();
        }
    }

    /// <summary>
    /// Khởi tạo và thiết lập các nguồn phát 3D Spatial Audio cho từng cột loa
    /// </summary>
    public void Setup3DSpeakers()
    {
        // 1. Thu thập danh sách vị trí loa
        if (speakerPositions == null) speakerPositions = new List<Transform>();
        speakerPositions.RemoveAll(item => item == null);

        if (speakerPositions.Count == 0)
        {
            // Tìm các con có tên Speaker
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (var c in children)
            {
                if (c != transform && (c.name.ToLower().Contains("speaker") || c.name.ToLower().Contains("loa")))
                {
                    speakerPositions.Add(c);
                }
            }
        }

        // Nếu vẫn chưa có loa con -> Dùng chính vị trí của GameObject này làm loa 3D
        if (speakerPositions.Count == 0)
        {
            speakerPositions.Add(transform);
        }

        // 2. Tạo Dual AudioSource cho từng loa để hỗ trợ crossfade 3D
        speakerChannels.Clear();
        foreach (var pos in speakerPositions)
        {
            if (pos == null) continue;

            SpeakerChannel ch = new SpeakerChannel();
            ch.speakerTransform = pos;

            AudioSource[] existingSources = pos.GetComponents<AudioSource>();
            if (existingSources.Length >= 2)
            {
                ch.sourceA = existingSources[0];
                ch.sourceB = existingSources[1];
            }
            else if (existingSources.Length == 1)
            {
                ch.sourceA = existingSources[0];
                ch.sourceB = pos.gameObject.AddComponent<AudioSource>();
            }
            else
            {
                ch.sourceA = pos.gameObject.AddComponent<AudioSource>();
                ch.sourceB = pos.gameObject.AddComponent<AudioSource>();
            }

            Configure3DAudioSource(ch.sourceA);
            Configure3DAudioSource(ch.sourceB);

            speakerChannels.Add(ch);
        }
    }

    private void Configure3DAudioSource(AudioSource src)
    {
        if (src == null) return;
        src.playOnAwake = false;
        src.loop = false; // Tuyệt đối không loop trên AudioSource để không bị kẹt lặp lại 1 bài
        src.spatialBlend = 1.0f; // 100% 3D Spatial Audio trong không gian thế giới thực
        src.rolloffMode = AudioRolloffMode.Linear; // Giảm âm tuyến tính mượt mà theo cự ly
        src.minDistance = minSpeakerDistance;
        src.maxDistance = maxSpeakerDistance;
        src.dopplerLevel = 0f; // Loa cố định, không tạo doppler
        src.spread = 60f;
        src.volume = 0f;
    }

    /// <summary>
    /// Kiểm tra người chơi có đang ở trong phạm vi công viên hay không
    /// </summary>
    public bool IsPlayerInParkArea()
    {
        if (!muteInStationAndCoaster) return true;

        if (seatSwitcher == null) seatSwitcher = Object.FindAnyObjectByType<SeatSwitcher>();
        if (rideController == null) rideController = Object.FindAnyObjectByType<RideController>();

        // 1. Tàu lượn đang chạy -> TẮT NHẠC
        if (rideController != null && rideController.currentState == RideController.RideState.Riding)
        {
            return false;
        }

        // 2. Tìm vị trí người chơi
        if (playerCameraOrRig == null)
        {
            if (Camera.main != null) playerCameraOrRig = Camera.main.transform;
            else
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerCameraOrRig = player.transform;
            }
        }

        if (playerCameraOrRig != null)
        {
            Vector3 pos = playerCameraOrRig.position;

            // Người chơi ở trên cao (ga tàu lượn y > 4.5m) -> TẮT NHẠC
            if (pos.y > parkMaxAltitude)
            {
                return false;
            }

            // Gần điểm sàn nhà ga tàu lượn -> TẮT NHẠC
            if (seatSwitcher != null && seatSwitcher.stationFloorPoint != null)
            {
                float distToStation = Vector3.Distance(new Vector3(pos.x, 0, pos.z),
                                                       new Vector3(seatSwitcher.stationFloorPoint.position.x, 0, seatSwitcher.stationFloorPoint.position.z));
                if (distToStation < 12.0f && pos.y > 3.0f)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void UpdateZoneVolumeTransition()
    {
        bool inPark = IsPlayerInParkArea();
        float targetFactor = inPark ? 1.0f : 0f;

        currentZoneVolumeFactor = Mathf.MoveTowards(currentZoneVolumeFactor, targetFactor, Time.unscaledDeltaTime / 1.5f);

        // Cập nhật âm lượng cho tất cả các loa nếu không đang trong crossfade
        if (crossfadeCoroutine == null)
        {
            float finalVol = masterVolume * currentZoneVolumeFactor;
            foreach (var ch in speakerChannels)
            {
                AudioSource activeSrc = isSourceAPlaying ? ch.sourceA : ch.sourceB;
                if (activeSrc != null)
                {
                    activeSrc.volume = finalVol;
                }
            }
        }

        // Tự động tạm dừng khi âm lượng về 0 (khi bước vào ga tàu hoặc lên tàu lượn)
        if (currentZoneVolumeFactor <= 0.001f)
        {
            if (!isZonePaused)
            {
                isZonePaused = true;
                foreach (var ch in speakerChannels)
                {
                    AudioSource activeSrc = isSourceAPlaying ? ch.sourceA : ch.sourceB;
                    if (activeSrc != null && activeSrc.isPlaying && !isManualPaused)
                    {
                        activeSrc.Pause();
                    }
                }
            }
        }
        // Tiếp tục phát lại bài hát khi quay trở lại công viên
        else
        {
            if (isZonePaused)
            {
                isZonePaused = false;
                foreach (var ch in speakerChannels)
                {
                    AudioSource activeSrc = isSourceAPlaying ? ch.sourceA : ch.sourceB;
                    if (activeSrc != null && !isManualPaused && activeSrc.clip != null)
                    {
                        activeSrc.UnPause();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Chuyển sang bài hát ngẫu nhiên tiếp theo
    /// </summary>
    public void PlayNextTrack()
    {
        if (playlist == null || playlist.Length == 0) return;

        int nextIndex = GetNextTrackIndex();
        if (nextIndex < 0 || nextIndex >= playlist.Length) return;

        currentTrackIndex = nextIndex;
        AudioClip nextClip = playlist[currentTrackIndex];
        if (nextClip == null) return;

        if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
        crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(nextClip));
    }

    /// <summary>
    /// Quay lại bài hát đã phát trước đó
    /// </summary>
    public void PlayPreviousTrack()
    {
        if (playlist == null || playlist.Length == 0) return;

        int prevIndex = -1;
        if (shuffle)
        {
            if (playedIndices.Count > 1)
            {
                // Bỏ bài hiện tại ra khỏi lịch sử vừa phát
                playedIndices.RemoveAt(playedIndices.Count - 1);
                prevIndex = playedIndices[playedIndices.Count - 1];
            }
            else
            {
                prevIndex = GetNextTrackIndex();
            }
        }
        else
        {
            prevIndex = currentTrackIndex - 1;
            if (prevIndex < 0) prevIndex = playlist.Length - 1;
        }

        if (prevIndex < 0 || prevIndex >= playlist.Length) return;

        currentTrackIndex = prevIndex;
        AudioClip prevClip = playlist[currentTrackIndex];
        if (prevClip == null) return;

        if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
        crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(prevClip));
    }

    private int GetNextTrackIndex()
    {
        if (playlist == null || playlist.Length == 0) return -1;
        if (playlist.Length == 1) return 0;

        if (shuffle)
        {
            // Nếu đã phát hết toàn bộ danh sách -> Làm mới danh sách để lặp lại chu kỳ mới
            if (playedIndices.Count >= playlist.Length)
            {
                if (!loopPlaylist) return -1;
                playedIndices.Clear();
            }

            List<int> unplayed = new List<int>();
            for (int i = 0; i < playlist.Length; i++)
            {
                if (playlist[i] == null) continue;
                // Không chọn bài vừa phát xong và các bài đã phát trong chu kỳ này
                if (!playedIndices.Contains(i) && i != currentTrackIndex)
                {
                    unplayed.Add(i);
                }
            }

            // Nếu tất cả bài còn lại đã phát hết hoặc chỉ còn trùng bài hiện tại
            if (unplayed.Count == 0)
            {
                playedIndices.Clear();
                for (int i = 0; i < playlist.Length; i++)
                {
                    if (playlist[i] != null && i != currentTrackIndex)
                    {
                        unplayed.Add(i);
                    }
                }
            }

            if (unplayed.Count == 0) return 0;

            int picked = unplayed[Random.Range(0, unplayed.Count)];
            playedIndices.Add(picked);
            return picked;
        }
        else
        {
            int next = (currentTrackIndex + 1);
            if (next >= playlist.Length)
            {
                if (!loopPlaylist) return -1;
                next = 0;
            }
            return next;
        }
    }

    private IEnumerator CrossfadeRoutine(AudioClip newClip)
    {
        // Xác định rõ ràng nguồn cũ (đang phát) và nguồn mới (sẽ nhận bài mới)
        bool oldIsSourceA = isSourceAPlaying;
        bool newIsSourceA = !isSourceAPlaying;

        // 1. Chuẩn bị bài hát mới trên nguồn mới cho tất cả các loa
        foreach (var ch in speakerChannels)
        {
            if (ch == null) continue;
            AudioSource newSrc = newIsSourceA ? ch.sourceA : ch.sourceB;
            if (newSrc != null)
            {
                newSrc.clip = newClip;
                newSrc.time = 0f;
                newSrc.volume = 0f;
                newSrc.loop = false; // Đảm bảo loop = false để không tự lặp lại 1 bài
                newSrc.minDistance = minSpeakerDistance;
                newSrc.maxDistance = maxSpeakerDistance;
                newSrc.spatialBlend = 1.0f;
                newSrc.rolloffMode = AudioRolloffMode.Linear;
                newSrc.Play();
            }
        }

        float duration = Mathf.Max(0.2f, crossfadeDuration);
        float elapsed = 0f;

        // 2. Chuyển đổi âm lượng từ từ: nguồn cũ giảm dần về 0, nguồn mới tăng dần lên targetVol
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float targetVol = masterVolume * currentZoneVolumeFactor;

            foreach (var ch in speakerChannels)
            {
                if (ch == null) continue;
                AudioSource oldSrc = oldIsSourceA ? ch.sourceA : ch.sourceB;
                AudioSource newSrc = newIsSourceA ? ch.sourceA : ch.sourceB;

                if (oldSrc != null) oldSrc.volume = Mathf.Lerp(targetVol, 0f, t);
                if (newSrc != null) newSrc.volume = Mathf.Lerp(0f, targetVol, t);
            }

            yield return null;
        }

        // 3. Hoàn tất chuyển bài: Tắt hẳn nguồn cũ, giữ nguồn mới ở âm lượng chuẩn
        foreach (var ch in speakerChannels)
        {
            if (ch == null) continue;
            AudioSource oldSrc = oldIsSourceA ? ch.sourceA : ch.sourceB;
            AudioSource newSrc = newIsSourceA ? ch.sourceA : ch.sourceB;

            if (oldSrc != null)
            {
                oldSrc.Stop();
                oldSrc.volume = 0f;
            }
            if (newSrc != null)
            {
                newSrc.volume = masterVolume * currentZoneVolumeFactor;
            }
        }

        // 4. CHÍNH THỨC CẬP NHẬT nguồn mới thành nguồn đang hoạt động
        isSourceAPlaying = newIsSourceA;
        crossfadeCoroutine = null;

        Debug.Log($"<color=#00FFAA><b>[ParkPlaylist 3D] Đang phát đồng bộ trên {speakerChannels.Count} loa: '{newClip.name}' (Bài {currentTrackIndex + 1}/{playlist.Length})</b></color>");
    }

    public void Pause()
    {
        isManualPaused = true;
        foreach (var ch in speakerChannels)
        {
            if (ch.sourceA != null) ch.sourceA.Pause();
            if (ch.sourceB != null) ch.sourceB.Pause();
        }
    }

    public void Resume()
    {
        isManualPaused = false;
        foreach (var ch in speakerChannels)
        {
            if (ch.sourceA != null && ch.sourceA.clip != null) ch.sourceA.UnPause();
            if (ch.sourceB != null && ch.sourceB.clip != null) ch.sourceB.UnPause();
        }
    }

    public void SetVolume(float vol)
    {
        masterVolume = Mathf.Clamp(vol, 0f, 1.5f);
    }

    /// <summary>
    /// Tự động quét và nạp toàn bộ file âm thanh từ thư mục 'Assets/Sound/Park'
    /// </summary>
    [ContextMenu("Tự Động Nạp Nhạc Từ Assets/Sound/Park")]
    public void AutoLoadClipsIfEmpty()
    {
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sound/Park" });
        if (guids == null || guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sound" });
        }

        List<AudioClip> loadedClips = new List<AudioClip>();
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.ToLower().Contains("/npc/") || path.ToLower().Contains("/rc/")) continue;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null && !loadedClips.Contains(clip))
            {
                loadedClips.Add(clip);
            }
        }

        if (loadedClips.Count > 0)
        {
            playlist = loadedClips.ToArray();
            EditorUtility.SetDirty(this);
            Debug.Log($"<color=#00FF99><b>[ParkPlaylist] Đã nạp {playlist.Length} bài nhạc từ Assets/Sound/Park!</b></color>");
        }
#endif
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ phạm vi phát thanh 3D của từng loa trong Scene View
        if (speakerPositions != null)
        {
            foreach (var pos in speakerPositions)
            {
                if (pos == null) continue;

                Gizmos.color = new Color(0f, 1f, 0.8f, 0.4f);
                Gizmos.DrawWireSphere(pos.position, minSpeakerDistance);

                Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.25f);
                Gizmos.DrawWireSphere(pos.position, maxSpeakerDistance);

                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(pos.position, 0.35f);
            }
        }
    }
}