using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// Gắn lên Box Collider trigger đặt cạnh Rick hoặc Morty.
/// Thứ tự hoạt động:
/// 1. Nhân vật nói hết câu thoại.
/// 2. Phát âm thanh bắn súng cổng (Portal Gun).
/// 3. Cổng dịch chuyển mới mở ra.
/// PortalExitTrigger gọi BeginTeleport khi player đi vào cổng.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(AudioSource))]
public class PortalNpcController : MonoBehaviour
{
    [Header("Nhận diện player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Cổng và âm thanh")]
    [SerializeField] private GameObject portal;
    [SerializeField] private AudioClip voiceClip;
    [SerializeField] private AudioClip portalGunClip;

    [Header("Điều chỉnh âm lượng")]
    [Range(0f, 3f)]
    [Tooltip("Âm lượng giọng nói của nhân vật (mặc định 2.0 để nghe to và rõ ràng).")]
    [SerializeField] private float voiceVolume = 2.0f;

    [Range(0f, 3f)]
    [Tooltip("Âm lượng tiếng súng bắn cổng.")]
    [SerializeField] private float portalGunVolume = 1.3f;

    [Range(0f, 1f)]
    [Tooltip("0 = Âm thanh 2D (nghe rõ toàn diện, không bị nhỏ khi đứng xa), 1 = 3D (suy giảm theo khoảng cách).")]
    [SerializeField] private float spatialBlend = 0f;

    [Tooltip("Khoảng cách tối thiểu không bị suy giảm âm lượng nếu dùng 3D audio.")]
    [SerializeField] private float minDistance = 15f;

    [Tooltip("Khoảng cách tối đa nghe được âm thanh.")]
    [SerializeField] private float maxDistance = 50f;

    [Header("Thứ tự và thời gian (Timing)")]
    [Tooltip("Đợi nhân vật nói xong hết câu thoại rồi mới tiếp tục bắn súng.")]
    [SerializeField] private bool waitForVoiceToFinish = true;

    [Tooltip("Khoảng nghỉ ngắn sau khi nói xong trước khi bắn súng (giây).")]
    [SerializeField, Min(0f)] private float delayAfterVoice = 0.4f;

    [Tooltip("Thời gian từ lúc phát tiếng bắn súng đến khi cổng dịch chuyển xuất hiện (giây).")]
    [SerializeField, Min(0f)] private float delayBeforePortalOpen = 0.6f;

    [Header("Chuyển scene")]
    [SerializeField] private string targetSceneName = "Game";
    [SerializeField] private GameObject loadingVideoCanvas;
    [SerializeField] private VideoPlayer loadingVideoPlayer;
    [SerializeField, Min(0f)] private float minimumVideoSeconds = 3f;
    [Tooltip("Kéo các script điều khiển của Player vào đây để khóa di chuyển lúc loading.")]
    [SerializeField] private MonoBehaviour[] playerControlsToDisable;

    private AudioSource audioSource;
    private bool portalOpened;
    private bool teleporting;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        SetupAudioSource();

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

        // 1. Nhân vật nói xong câu thoại
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

        // 2. Mới đến âm thanh bắn súng
        if (portalGunClip != null)
        {
            audioSource.PlayOneShot(portalGunClip, portalGunVolume);
        }

        // 3. Chờ âm thanh súng bắn rồi cổng dịch chuyển mới hiện ra
        if (delayBeforePortalOpen > 0f)
        {
            yield return new WaitForSeconds(delayBeforePortalOpen);
        }

        // 4. Cổng dịch chuyển hiện ra
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

        if (loadingVideoCanvas != null)
        {
            TextMeshProUGUI tmpText = loadingVideoCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmpText != null)
            {
                tmpText.text = "⚡ DIMENSION C-137 // WARP IN PROGRESS... ⚡";
                tmpText.color = new Color(0.25f, 1f, 0.35f, 0.85f); // Xanh lá neon Rick
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
            Debug.LogError($"[PortalNpcController] Không tìm thấy scene '{targetSceneName}'.");
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

        // Tự động tìm và vô hiệu hóa / kích hoạt lại điều khiển của Chihiro nếu có
        AnimatorChihiro chihiro = FindFirstObjectByType<AnimatorChihiro>();
        if (chihiro != null) chihiro.enabled = enabled;
    }
}
