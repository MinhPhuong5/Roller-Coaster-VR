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
///    - Chuyển thẳng vào cảnh Game.
/// </summary>
public class MenuController : MonoBehaviour
{
    [Header("Cấu hình Cảnh")]
    public string gameSceneName = "Game";

    [Header("Thời gian Thanh Loading")]
    [Tooltip("Thời gian thanh loading chạy từ 0% đến 100% (mặc định 5 giây)")]
    public float loadingDuration = 5.0f;

    [Header("=== KÉO FILE VIDEO VÀO ĐÂY ===")]
    [Tooltip("Kéo file Video 1 (.mp4) nền nhạc vào ô này")]
    public VideoClip idleVideoClip;

    [Tooltip("Kéo file Video 2 (.mp4) chói sáng vào ô này (tùy chọn)")]
    public VideoClip transitionVideoClip;

    [Header("=== Component Video (Tự động kết nối) ===")]
    public VideoPlayer idleVideoPlayer;
    public VideoPlayer transitionVideoPlayer;
    public GameObject transitionVideoDisplay;

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

    void Start()
    {
        // Khởi chạy Video 1
        if (idleVideoPlayer != null)
        {
            if (idleVideoClip != null)
            {
                idleVideoPlayer.clip = idleVideoClip;
            }

            idleVideoPlayer.isLooping = true;
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

        // Chuẩn bị trước Video 2 nếu có
        if (transitionVideoPlayer != null)
        {
            if (transitionVideoClip != null)
            {
                transitionVideoPlayer.clip = transitionVideoClip;
            }

            if (transitionVideoPlayer.clip != null)
            {
                transitionVideoPlayer.isLooping = false;
                transitionVideoPlayer.Prepare();
            }
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

    /// <summary>
    /// Bấm vào bất kỳ đâu trên màn hình để bắt đầu quá trình load
    /// </summary>
    public void StartGame()
    {
        if (isTransitioning) return;
        isTransitioning = true;

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
        // 1. Chữ Menu biến mất (Video nền sau vẫn giữ nguyên và tiếp tục chạy)
        if (allMenuUIContainer != null)
        {
            allMenuUIContainer.SetActive(false);
        }

        // Bắt đầu tải ngầm trước cảnh Game
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
        if (asyncLoad != null)
        {
            asyncLoad.allowSceneActivation = false;
        }

        // 2. Bật thanh Loading xuất hiện
        if (loadingUIContainer != null)
        {
            loadingUIContainer.SetActive(true);
        }

        // Chạy thanh loading từ từ 0 đến 5 giây (0% -> 100%)
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

        // 4. Bước vào Game
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
