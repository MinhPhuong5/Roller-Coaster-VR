using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// Quản lý Menu theo đúng yêu cầu:
/// 1. Video nền phát lặp lại kèm nhạc.
/// 2. Khi ấn vào bất kỳ đâu trên màn hình:
///    - Chữ Menu và khung nhập biến mất.
///    - Video nền vẫn chạy bình thường.
///    - Thanh Loading xuất hiện chạy từ 0 đến 5 giây (0% -> 100%).
/// 3. Sau khi chạy xong 5 giây:
///    - Xuất hiện màn hình sáng chói (hoặc chạy Video 2 chói sáng).
///    - Chuyển thẳng vào cảnh ParkScene.
/// </summary>
public class MenuController : MonoBehaviour
{
    [Header("Cấu hình Cảnh")]
    public string gameSceneName = "ParkScene";

    [Header("Thời gian Thanh Loading")]
    [Tooltip("Thời gian thanh loading chạy từ 0% đến 100% (mặc định 5 giây)")]
    public float loadingDuration = 5.0f;

    [Header("=== VIDEO 1: MỞ ĐẦU ===")]
    [Tooltip("Kéo video nền của Menu vào đây. Video phát cho đến khi người chơi bấm Start.")]
    public VideoClip idleVideoClip;

    [Tooltip("Bật để Video 1 lặp liên tục trong lúc ở Menu.")]
    public bool loopIdleVideo = true;

    [Header("=== VIDEO 2: CHUYỂN CẢNH (TÙY CHỌN) ===")]
    [Tooltip("Kéo Video 2 vào đây. Nó phát một lần sau thanh loading, trước khi vào ParkScene.")]
    public VideoClip transitionVideoClip;

    [Header("=== Component Video (Tự động kết nối) ===")]
    public VideoPlayer idleVideoPlayer;
    public VideoPlayer transitionVideoPlayer;
    public GameObject transitionVideoDisplay;

    [Header("=== ÂM THANH KHI BẤM START ===")]
    [Tooltip("Kéo file âm thanh click/bắt đầu vào đây. Âm thanh phát một lần khi người chơi bấm Start.")]
    public AudioClip startClickSound;

    [Tooltip("AudioSource để phát âm thanh. Nếu để trống, script tự dùng hoặc tạo AudioSource trên MenuController_Manager.")]
    public AudioSource startClickAudioSource;

    [Header("=== Giao diện Menu ===")]
    public GameObject allMenuUIContainer; // Cụm chữ Menu (START GAME, nhập tên...)
    public TMP_InputField nameInputField; // Ô nhập tên người chơi

    [Header("=== Giao diện Thanh Loading (0 - 5s) ===")]
    public GameObject loadingUIContainer; // Khung chứa thanh loading
    public TextMeshProUGUI loadingText;   // Dòng chữ "Loading... 0%"
    public Image loadingFillBar;          // Thanh chạy màu (Image Type = Filled)

    [Header("=== Màn hình Sáng Chói (White Flash) ===")]
    public CanvasGroup whiteFlashCanvasGroup;
    public float whiteFlashFadeDuration = 0.8f;

    private bool isTransitioning = false;
    private AsyncOperation preloadedSceneLoad;
    private ThreadPriority previousBackgroundLoadingPriority;
    private bool backgroundLoadingPriorityChanged;

    void Start()
    {
        if (startClickAudioSource == null)
        {
            startClickAudioSource = GetComponent<AudioSource>();
            if (startClickAudioSource == null)
            {
                startClickAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Khởi chạy Video 1
        if (idleVideoPlayer != null)
        {
            if (idleVideoClip != null)
            {
                idleVideoPlayer.clip = idleVideoClip;
            }

            idleVideoPlayer.isLooping = loopIdleVideo;
            idleVideoPlayer.Play();
        }

        // Ẩn Video 2 lúc đầu
        if (transitionVideoDisplay != null)
        {
            transitionVideoDisplay.SetActive(false);
        }

        // Ẩn thanh Loading lúc đầu
        if (loadingUIContainer != null)
        {
            loadingUIContainer.SetActive(false);
        }

        // Ẩn màn hình sáng chói lúc đầu
        if (whiteFlashCanvasGroup != null)
        {
            whiteFlashCanvasGroup.alpha = 0f;
            whiteFlashCanvasGroup.blocksRaycasts = false;
        }

        // Chuẩn bị trước Video 2 nếu dùng một VideoPlayer riêng.
        // Không gán clip vào đây khi hai ô Video Player cùng trỏ đến một component,
        // vì sẽ làm Video 1 bị thay thế ngay khi Menu mở.
        if (transitionVideoPlayer != null && transitionVideoPlayer != idleVideoPlayer && transitionVideoClip != null)
        {
            transitionVideoPlayer.clip = transitionVideoClip;
            transitionVideoPlayer.isLooping = false;
            transitionVideoPlayer.Prepare();
        }

        // Tải lại tên đã lưu
        if (nameInputField != null)
        {
            string savedName = PlayerPrefs.GetString("PlayerName", "");
            if (!string.IsNullOrEmpty(savedName))
            {
                nameInputField.text = savedName;
            }
        }

        // Tải ParkScene từ lúc Menu đã hiện để cú bấm Start không phải bắt đầu tải asset nặng.
        StartCoroutine(PreloadParkScene());
    }

    void Update()
    {
        if (isTransitioning) return;

        // Nếu đang gõ tên trong input field
        if (nameInputField != null && nameInputField.isFocused)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                StartGame();
            }
            return;
        }

        // Phím Space hoặc Enter để vào game
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            StartGame();
        }
    }

    private void OnDestroy()
    {
        if (backgroundLoadingPriorityChanged)
        {
            Application.backgroundLoadingPriority = previousBackgroundLoadingPriority;
        }
    }

    private IEnumerator PreloadParkScene()
    {
        // Không chặn frame đầu tiên của Menu.
        yield return null;
        BeginScenePreload();
    }

    private AsyncOperation BeginScenePreload()
    {
        if (preloadedSceneLoad != null)
        {
            return preloadedSceneLoad;
        }

        // Hạn chế thời gian Unity tích hợp asset vào main thread để video vẫn mượt.
        previousBackgroundLoadingPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        backgroundLoadingPriorityChanged = true;

        preloadedSceneLoad = SceneManager.LoadSceneAsync(gameSceneName);
        if (preloadedSceneLoad != null)
        {
            preloadedSceneLoad.allowSceneActivation = false;
        }

        return preloadedSceneLoad;
    }

    /// <summary>
    /// Bấm vào bất kỳ đâu trên màn hình để bắt đầu quá trình load
    /// </summary>
    public void StartGame()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        if (startClickSound != null && startClickAudioSource != null)
        {
            startClickAudioSource.PlayOneShot(startClickSound);
        }

        // Lưu tên người chơi
        if (nameInputField != null && !string.IsNullOrEmpty(nameInputField.text))
        {
            PlayerPrefs.SetString("PlayerName", nameInputField.text.Trim());
            PlayerPrefs.Save();
        }

        // Bắt đầu chuỗi: Chữ biến mất -> Thanh load chạy 0-5s -> Màn hình sáng -> Vào game
        StartCoroutine(StartLoadingSequence());
    }

    private IEnumerator StartLoadingSequence()
    {
        // 1. Hiện loading trước để người chơi thấy phản hồi ngay lập tức.
        if (allMenuUIContainer != null)
        {
            allMenuUIContainer.SetActive(false);
        }

        if (loadingUIContainer != null)
        {
            loadingUIContainer.SetActive(true);
        }
        if (loadingFillBar != null) loadingFillBar.fillAmount = 0f;
        if (loadingText != null) loadingText.text = "Loading... 0%";

        // Cho Unity một frame để vẽ loading UI trước các thao tác có thể chặn main thread.
        yield return null;

        // ParkScene đã được tải nền từ khi mở Menu. Nếu người chơi bấm quá nhanh,
        // bắt đầu tải tại đây sau khi loading UI đã được vẽ.
        AsyncOperation asyncLoad = BeginScenePreload();

        // 2. Chạy thanh loading từ từ 0 đến 5 giây (0% -> 100%).
        float timer = 0f;
        while (timer < loadingDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / loadingDuration);

            if (loadingFillBar != null)
            {
                loadingFillBar.fillAmount = progress;
            }

            if (loadingText != null)
            {
                int percent = Mathf.RoundToInt(progress * 100f);
                loadingText.text = $"Loading... {percent}%";
            }

            yield return null;
        }

        // Đảm bảo đạt 100% khi kết thúc 5s
        if (loadingFillBar != null) loadingFillBar.fillAmount = 1f;
        if (loadingText != null) loadingText.text = "Loading... 100%";

        yield return new WaitForSeconds(0.2f);

        // Ẩn thanh Loading
        if (loadingUIContainer != null)
        {
            loadingUIContainer.SetActive(false);
        }

        // 3. Màn hình sáng chói (hoặc chạy Video 2 chói sáng nếu có)
        VideoClip clipToPlay = transitionVideoClip != null ? transitionVideoClip : (transitionVideoPlayer != null ? transitionVideoPlayer.clip : null);

        if (transitionVideoPlayer != null && clipToPlay != null)
        {
            // Bật Video 2 chói sáng
            if (transitionVideoDisplay != null) transitionVideoDisplay.SetActive(true);
            if (idleVideoPlayer != null) idleVideoPlayer.Stop();

            transitionVideoPlayer.clip = clipToPlay;
            transitionVideoPlayer.isLooping = false;
            transitionVideoPlayer.Play();

            bool done = false;
            transitionVideoPlayer.loopPointReached += (vp) => { done = true; };

            float vidDuration = (float)transitionVideoPlayer.clip.length;
            float t = 0f;
            while (!done && t < vidDuration)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            // Hiệu ứng màn hình sáng chói (White Flash)
            if (whiteFlashCanvasGroup != null)
            {
                whiteFlashCanvasGroup.blocksRaycasts = true;
                float flashTimer = 0f;
                while (flashTimer < whiteFlashFadeDuration)
                {
                    flashTimer += Time.deltaTime;
                    whiteFlashCanvasGroup.alpha = Mathf.Clamp01(flashTimer / whiteFlashFadeDuration);
                    yield return null;
                }
                whiteFlashCanvasGroup.alpha = 1f;
            }
            yield return new WaitForSeconds(0.3f);
        }

        // 4. Bước vào ParkScene
        if (asyncLoad != null)
        {
            while (asyncLoad.progress < 0.9f)
            {
                yield return null;
            }
            asyncLoad.allowSceneActivation = true;
        }
        else
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
