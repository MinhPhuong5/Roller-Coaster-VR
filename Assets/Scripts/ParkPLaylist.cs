using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quản lý phát nhạc nền CHUẨN 3D SPATIAL AUDIO CỐ ĐỊNH TRONG KHÔNG GIAN CÔNG VIÊN:
/// - Âm thanh phát từ các cột loa 3D (Park Speakers) đặt cố định tại các vị trí thực trong công viên (Quảng trường, Quầy vé, Lối đi...).
/// - Chuẩn 100% 3D Spatial Audio: Người chơi ở gần loa nghe to, đi ra xa âm lượng giảm dần theo cự ly thực tế, quay đầu nghe rõ hướng loa (Trái/Phải/Trước/Sau).
/// - Đồng bộ tất cả các loa cùng phát chung 1 bài hát (Hệ thống truyền thanh công viên PA System).
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

    [Header("3. Danh Sách Các Cột Loa 3D Trong Công Viên")]
    [Tooltip("Kéo các vị trí cột loa 3D trong Scene vào đây (Nếu để trống, script sẽ tự lấy chính GameObject này hoặc các con của nó làm loa 3D)")]
    public List<Transform> speakerPositions = new List<Transform>();

    [Header("4. Giới Hạn Phạm Vi Phát Nhạc")]
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
        // 1. Quản lý âm lượng theo vị trí (Công viên = 100%, Nhà ga & Tàu lượn = Tắt hoàn toàn)
        UpdateZoneVolumeTransition();

        // 2. Tự động chuyển bài khi bài hiện tại kết thúc
        if (speakerChannels.Count > 0 && playlist != null && playlist.Length > 0 && !isManualPaused && currentZoneVolumeFactor > 0.05f)
        {
            AudioSource mainActiveSource = isSourceAPlaying ? speakerChannels[0].sourceA : speakerChannels[0].sourceB;
            if (mainActiveSource != null && !mainActiveSource.isPlaying)
            {
                if (mainActiveSource.time == 0f || (mainActiveSource.clip != null && mainActiveSource.time >= mainActiveSource.clip.length - 0.15f))
                {
                    PlayNextTrack();
                }
            }
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
        src.loop = false;
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

        // Cập nhật âm lượng cho tất cả các loa
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

        // Tự động tạm dừng khi âm lượng về 0 và tiếp tục phát khi quay lại công viên
        if (currentZoneVolumeFactor <= 0.001f)
        {
            foreach (var ch in speakerChannels)
            {
                AudioSource activeSrc = isSourceAPlaying ? ch.sourceA : ch.sourceB;
                if (activeSrc != null && activeSrc.isPlaying && !isManualPaused)
                {
                    activeSrc.Pause();
                }
            }
        }
        else
        {
            foreach (var ch in speakerChannels)
            {
                AudioSource activeSrc = isSourceAPlaying ? ch.sourceA : ch.sourceB;
                if (activeSrc != null && !activeSrc.isPlaying && !isManualPaused && activeSrc.clip != null)
                {
                    activeSrc.UnPause();
                    if (!activeSrc.isPlaying)
                    {
                        activeSrc.Play();
                    }
                }
            }
        }
    }

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

    private int GetNextTrackIndex()
    {
        if (playlist.Length == 1) return 0;

        if (shuffle)
        {
            if (playedIndices.Count >= playlist.Length)
            {
                if (!loopPlaylist) return -1;
                playedIndices.Clear();
            }

            List<int> unplayed = new List<int>();
            for (int i = 0; i < playlist.Length; i++)
            {
                if (!playedIndices.Contains(i) && i != currentTrackIndex)
                {
                    unplayed.Add(i);
                }
            }

            if (unplayed.Count == 0)
            {
                playedIndices.Clear();
                for (int i = 0; i < playlist.Length; i++)
                {
                    if (i != currentTrackIndex) unplayed.Add(i);
                }
            }

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
        // 1. Chuẩn bị nguồn phát cho tất cả các loa
        foreach (var ch in speakerChannels)
        {
            AudioSource fadeInSource = isSourceAPlaying ? ch.sourceB : ch.sourceA;
            if (fadeInSource != null)
            {
                fadeInSource.clip = newClip;
                fadeInSource.time = 0f;
                fadeInSource.volume = 0f;
                fadeInSource.minDistance = minSpeakerDistance;
                fadeInSource.maxDistance = maxSpeakerDistance;
                fadeInSource.spatialBlend = 1.0f;
                fadeInSource.rolloffMode = AudioRolloffMode.Linear;
                fadeInSource.Play();
            }
        }

        isSourceAPlaying = !isSourceAPlaying;

        float duration = Mathf.Max(0.2f, crossfadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float targetVol = masterVolume * currentZoneVolumeFactor;

            foreach (var ch in speakerChannels)
            {
                AudioSource fadeOutSource = isSourceAPlaying ? ch.sourceA : ch.sourceB;
                AudioSource fadeInSource = isSourceAPlaying ? ch.sourceB : ch.sourceA;

                if (fadeInSource != null) fadeInSource.volume = Mathf.Lerp(0f, targetVol, t);
                if (fadeOutSource != null) fadeOutSource.volume = Mathf.Lerp(targetVol, 0f, t);
            }

            yield return null;
        }

        foreach (var ch in speakerChannels)
        {
            AudioSource fadeOutSource = isSourceAPlaying ? ch.sourceA : ch.sourceB;
            AudioSource fadeInSource = isSourceAPlaying ? ch.sourceB : ch.sourceA;

            if (fadeInSource != null) fadeInSource.volume = masterVolume * currentZoneVolumeFactor;
            if (fadeOutSource != null)
            {
                fadeOutSource.Stop();
                fadeOutSource.volume = 0f;
            }
        }

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