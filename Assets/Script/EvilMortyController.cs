using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// Gắn lên Empty GameObject 'MortyPortalTrigger' đặt cạnh Evil Morty.
/// Trình tự hoạt động:
/// 1. Evil Morty nói xong toàn bộ câu thoại (âm lượng to rõ 2D).
/// 2. Bắn súng mở cổng vàng (Portal Gun).
/// 3. Cổng dịch chuyển vàng (CongDichChuyenMorty) hiện ra.
/// Khi người chơi bước qua cổng vàng, MortyPortalExitTrigger gọi BeginTeleport để:
/// - Tái sử dụng PortalLoadingCanvas của Rick (đổi chữ & màu vàng neon).
/// - Dịch chuyển về Scene Menu (Trang chủ).
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(AudioSource))]
public class EvilMortyController : MonoBehaviour
{
    [Header("Nhận diện player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Cổng và âm thanh")]
    [Tooltip("Kéo object CongDichChuyenMorty vào đây")]
    [SerializeField] private GameObject portal;
    [Tooltip("File âm thanh giọng nói của Evil Morty")]
    [SerializeField] private AudioClip voiceClip;
    [Tooltip("File âm thanh bắn súng mở cổng vàng")]
    [SerializeField] private AudioClip portalGunClip;

    [Header("Điều chỉnh âm lượng")]
    [Range(0f, 3f)]
    [Tooltip("Âm lượng giọng nói nhân vật (mặc định 2.0 để nghe to và rõ ràng).")]
    [SerializeField] private float voiceVolume = 2.0f;

    [Range(0f, 3f)]
    [Tooltip("Âm lượng tiếng súng bắn cổng.")]
    [SerializeField] private float portalGunVolume = 1.3f;

    [Range(0f, 1f)]
    [Tooltip("0 = Âm thanh 2D (nghe rõ toàn diện, không bị nhỏ khi đứng xa), 1 = 3D.")]
    [SerializeField] private float spatialBlend = 0f;

    [Tooltip("Khoảng cách tối thiểu không bị suy giảm âm lượng nếu dùng 3D audio.")]
    [SerializeField] private float minDistance = 15f;

    [Tooltip("Khoảng cách tối đa nghe được âm thanh.")]
    [SerializeField] private float maxDistance = 50f;

    [Header("Thứ tự và thời gian (Timing)")]
    [Tooltip("Đợi Evil Morty nói xong hết câu thoại rồi mới tiếp tục bắn súng.")]
    [SerializeField] private bool waitForVoiceToFinish = true;

    [Tooltip("Khoảng nghỉ ngắn sau khi nói xong trước khi bắn súng (giây).")]
    [SerializeField, Min(0f)] private float delayAfterVoice = 0.4f;

    [Tooltip("Thời gian từ lúc phát tiếng bắn súng đến khi cổng vàng xuất hiện (giây).")]
    [SerializeField, Min(0f)] private float delayBeforePortalOpen = 0.6f;

    [Header("Chuyển scene (Tái sử dụng Canvas của Rick)")]
    [Tooltip("Tên scene đích khi đi qua cổng (Menu để quay về trang chủ).")]
    [SerializeField] private string targetSceneName = "Menu";

    [Tooltip("Tự động tìm hoặc kéo PortalLoadingCanvas của Rick vào đây.")]
    [SerializeField] private GameObject loadingVideoCanvas;

    [Tooltip("Tự động tìm hoặc kéo PortalLoadingVideo vào đây.")]
    [SerializeField] private VideoPlayer loadingVideoPlayer;

    [SerializeField, Min(0f)] private float minimumVideoSeconds = 2.5f;

    [Tooltip("Nội dung chữ hiển thị khi qua cổng vàng về Menu.")]
    [SerializeField] private string portalSubtitle = "⚡ RETURNING TO MAIN MENU // WARP IN PROGRESS... ⚡";

    [Tooltip("Màu chữ neon cho cổng vàng của Evil Morty.")]
    [SerializeField] private Color subtitleColor = new Color(1f, 0.85f, 0.2f, 0.9f);

    [Tooltip("Kéo các script điều khiển của Player vào đây để khóa di chuyển lúc loading.")]
    [SerializeField] private MonoBehaviour[] playerControlsToDisable;

    private AudioSource audioSource;
    private bool portalOpened;
    private bool teleporting;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        SetupAudioSource();
        AutoFindLoadingCanvas();

        if (portal != null) portal.SetActive(false);
        if (loadingVideoCanvas != null) loadingVideoCanvas.SetActive(false);
    }

    private void OnValidate()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.spatialBlend = spatialBlend;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
        }

        if (loadingVideoCanvas == null)
        {
            AutoFindLoadingCanvas();
        }
    }

    private void SetupAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.volume = 1f;
            audioSource.spatialBlend = spatialBlend;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
        }
    }

    private void AutoFindLoadingCanvas()
    {
        if (loadingVideoCanvas == null)
        {
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas c in allCanvases)
            {
                if (c != null && c.gameObject.name == "PortalLoadingCanvas")
                {
                    loadingVideoCanvas = c.gameObject;
                    break;
                }
            }
        }

        if (loadingVideoCanvas != null && loadingVideoPlayer == null)
        {
            loadingVideoPlayer = loadingVideoCanvas.GetComponentInChildren<VideoPlayer>(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!portalOpened && IsPlayer(other))
        {
            StartCoroutine(OpenPortal());
        }
    }

    private IEnumerator OpenPortal()
    {
        portalOpened = true;

        // 1. Evil Morty nói câu thoại
        if (voiceClip != null)
        {
            audioSource.PlayOneShot(voiceClip, voiceVolume);

            if (waitForVoiceToFinish)
            {
                yield return new WaitForSeconds(voiceClip.length);
            }
        }

        // Khoảng dừng tự nhiên sau khi nói xong
        if (delayAfterVoice > 0f)
        {
            yield return new WaitForSeconds(delayAfterVoice);
        }

        // 2. Âm thanh bắn súng cổng
        if (portalGunClip != null)
        {
            audioSource.PlayOneShot(portalGunClip, portalGunVolume);
        }

        // 3. Chờ âm thanh súng bắn rồi cổng vàng mới hiện ra
        if (delayBeforePortalOpen > 0f)
        {
            yield return new WaitForSeconds(delayBeforePortalOpen);
        }

        // 4. Cổng dịch chuyển vàng hiện ra
        if (portal != null)
        {
            portal.SetActive(true);
        }
    }

    public void BeginTeleport(Collider other)
    {
        if (!teleporting && portalOpened && IsPlayer(other))
        {
            StartCoroutine(LoadTargetScene());
        }
    }

    private IEnumerator LoadTargetScene()
    {
        teleporting = true;
        SetPlayerControls(false);

        // Tận dụng PortalLoadingCanvas của Rick: đổi text & màu vàng neon
        if (loadingVideoCanvas != null)
        {
            TextMeshProUGUI tmpText = loadingVideoCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmpText != null && !string.IsNullOrEmpty(portalSubtitle))
            {
                tmpText.text = portalSubtitle;
                tmpText.color = subtitleColor;
            }

            loadingVideoCanvas.SetActive(true);
        }

        if (loadingVideoPlayer != null)
        {
            loadingVideoPlayer.isLooping = true;
            loadingVideoPlayer.Play();
        }

        // Vẽ Canvas/video trước khi Unity bắt đầu nạp scene.
        yield return null;

        AsyncOperation load = SceneManager.LoadSceneAsync(targetSceneName);
        if (load == null)
        {
            Debug.LogError($"[EvilMortyController] Không tìm thấy scene '{targetSceneName}'.");
            SetPlayerControls(true);
            teleporting = false;
            yield break;
        }

        load.allowSceneActivation = false;
        float videoEndTime = Time.unscaledTime + minimumVideoSeconds;
        while (load.progress < 0.9f || Time.unscaledTime < videoEndTime)
            yield return null;

        load.allowSceneActivation = true;
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;

        return other.CompareTag(playerTag)
            || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag))
            || (other.transform.root != null && other.transform.root.CompareTag(playerTag))
            || other.GetComponentInParent<AnimatorChihiro>() != null
            || other.GetComponentInParent<PlayerMovement>() != null;
    }

    private void SetPlayerControls(bool enabled)
    {
        if (playerControlsToDisable != null)
        {
            foreach (MonoBehaviour control in playerControlsToDisable)
            {
                if (control != null) control.enabled = enabled;
            }
        }

        // Tự động tìm và khóa / mở khóa điều khiển nhân vật Chihiro
        AnimatorChihiro chihiro = FindFirstObjectByType<AnimatorChihiro>();
        if (chihiro != null) chihiro.enabled = enabled;
    }
}
