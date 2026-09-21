using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using TMPro;

/// <summary>
/// Quản lý tương tác với mô hình Tàu Lượn Siêu Tốc (TauLuonSieuToc).
/// - Khi Player (Chihiro) bước vào vùng Trigger -> hiện thông báo "Nhấn F (hoặc A trên tay cầm) để chơi trò chơi".
/// - Nhấn phím F / nút A (X) -> mở Hộp thoại Xác nhận ("Có / Bắt đầu" và "Không / Hủy").
/// - Chọn "Có / Bắt đầu" (hoặc Enter / nút A, X) -> chuyển sang Scene thứ 3 (Game).
/// - Chọn "Không / Hủy" (hoặc Esc / nút B, Y) -> đóng popup, tiếp tục chơi bình thường.
/// </summary>
public class RollerCoasterInteraction : MonoBehaviour
{
    [Header("=== Giao Diện Thông Báo (Prompt UI) ===")]
    [Tooltip("Khung UI hiện thông báo 'Nhấn F để chơi trò chơi'")]
    public GameObject promptUI;
    [Tooltip("Text hiển thị dòng chữ nhắc nhở")]
    public TextMeshProUGUI promptText;

    [Header("=== Hộp Thoại Xác Nhận (Confirmation Modal) ===")]
    [Tooltip("Panel hộp thoại xác nhận")]
    public GameObject confirmationModalPanel;
    [Tooltip("Nút 'Có / Bắt đầu'")]
    public Button confirmStartButton;
    [Tooltip("Nút 'Không / Hủy'")]
    public Button cancelCloseButton;
    [Tooltip("Tiêu đề hộp thoại")]
    public TextMeshProUGUI modalTitleText;
    [Tooltip("Nội dung thông điệp hộp thoại")]
    public TextMeshProUGUI modalMessageText;

    [Header("=== Cấu Hình Chuyển Scene (Scene 3) ===")]
    [Tooltip("Tên scene game chính trong Build Settings")]
    public string mainGameSceneName = "Game";
    [Tooltip("Build index của scene game chính (Mặc định: 2 cho scene thứ 3)")]
    public int mainGameSceneIndex = 2;

    [Header("=== Video Chuyển Cảnh (Tùy Chọn) ===")]
    [Tooltip("Kéo VideoClip vào đây. Video phủ toàn màn hình trước khi vào Scene 3.")]
    public VideoClip transitionVideo;

    [Header("=== Trạng Thái Hiện Tại ===")]
    [SerializeField] private bool isPlayerInZone = false;
    [SerializeField] private bool isModalOpen = false;

    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private bool isStartingGame;

    // Nút tay cầm Quest
    private InputAction vrInteract; // A (phải) / X (trái): mở tương tác
    private InputAction vrConfirm;  // A (phải) / X (trái): xác nhận
    private InputAction vrCancel;   // B (phải) / Y (trái): hủy

    private void Awake()
    {
        vrInteract = new InputAction(type: InputActionType.Button);
        vrInteract.AddBinding("<XRController>{RightHand}/primaryButton");   // A
        vrInteract.AddBinding("<XRController>{LeftHand}/primaryButton");    // X

        vrConfirm = new InputAction(type: InputActionType.Button);
        vrConfirm.AddBinding("<XRController>{RightHand}/primaryButton");    // A
        vrConfirm.AddBinding("<XRController>{LeftHand}/primaryButton");     // X

        vrCancel = new InputAction(type: InputActionType.Button);
        vrCancel.AddBinding("<XRController>{RightHand}/secondaryButton");   // B
        vrCancel.AddBinding("<XRController>{LeftHand}/secondaryButton");    // Y
    }

    private void OnEnable()
    {
        vrInteract.Enable();
        vrConfirm.Enable();
        vrCancel.Enable();
    }

    private void OnDisable()
    {
        vrInteract.Disable();
        vrConfirm.Disable();
        vrCancel.Disable();
    }

    private void Start()
    {
        // Ban đầu ẩn toàn bộ UI
        if (promptUI != null) promptUI.SetActive(false);
        if (confirmationModalPanel != null) confirmationModalPanel.SetActive(false);

        if (promptText != null)
        {
            promptText.text = XRSettings.isDeviceActive
                ? "Nhấn A để chơi trò chơi"
                : "Nhấn F để chơi trò chơi";
        }

        // Đăng ký sự kiện nút bấm
        if (confirmStartButton != null)
        {
            confirmStartButton.onClick.RemoveAllListeners();
            confirmStartButton.onClick.AddListener(OnConfirmStartGame);
        }

        if (cancelCloseButton != null)
        {
            cancelCloseButton.onClick.RemoveAllListeners();
            cancelCloseButton.onClick.AddListener(OnCancelModal);
        }
    }

    private void Update()
    {
        // 1. Khi đang đứng trong vùng trigger và chưa mở popup
        if (isPlayerInZone && !isModalOpen)
        {
            if (Input.GetKeyDown(KeyCode.F) || vrInteract.WasPressedThisFrame())
            {
                OpenConfirmationModal();
            }
        }
        // 2. Khi đang mở hộp thoại xác nhận
        else if (isModalOpen)
        {
            // Nhấn Enter hoặc nút A / X để chọn 'Bắt đầu'
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || vrConfirm.WasPressedThisFrame())
            {
                OnConfirmStartGame();
            }
            // Nhấn Escape hoặc nút B / Y để chọn 'Hủy'
            else if (Input.GetKeyDown(KeyCode.Escape) || vrCancel.WasPressedThisFrame())
            {
                OnCancelModal();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            isPlayerInZone = true;
            if (!isModalOpen)
            {
                ShowPrompt(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            isPlayerInZone = false;
            ShowPrompt(false);
            if (isModalOpen)
            {
                OnCancelModal();
            }
        }
    }

    /// <summary>
    /// Nhận diện Player qua Tag Player hoặc các script điều khiển nhân vật (Chihiro / PlayerMovement).
    /// </summary>
    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;

        if (other.CompareTag("Player")) return true;

        if (other.GetComponent<AnimatorChihiro>() != null || other.GetComponentInParent<AnimatorChihiro>() != null)
            return true;

        if (other.GetComponent<PlayerMovement>() != null || other.GetComponentInParent<PlayerMovement>() != null)
            return true;

        return false;
    }

    private void ShowPrompt(bool visible)
    {
        if (promptUI != null && promptUI.activeSelf != visible)
        {
            promptUI.SetActive(visible);
        }
    }

    public void OpenConfirmationModal()
    {
        isModalOpen = true;
        ShowPrompt(false); // Ẩn prompt nhắc nhở khi mở popup

        // Lưu và mở con trỏ chuột để người chơi bấm nút
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (confirmationModalPanel != null)
        {
            confirmationModalPanel.SetActive(true);
        }
    }

    public void OnCancelModal()
    {
        isModalOpen = false;

        // Trả lại trạng thái chuột trước đó
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;

        if (confirmationModalPanel != null)
        {
            confirmationModalPanel.SetActive(false);
        }

        // Nếu nhân vật vẫn ở trong vùng trigger thì hiện lại thông báo nhắc
        if (isPlayerInZone)
        {
            ShowPrompt(true);
        }
    }

    public void OnConfirmStartGame()
    {
        if (isStartingGame) return;
        isStartingGame = true;

        if (transitionVideo != null)
        {
            StartCoroutine(PlayTransitionVideo());
            return;
        }

        LoadMainGameScene();
    }

    private System.Collections.IEnumerator PlayTransitionVideo()
    {
        Camera targetCamera = Object.FindAnyObjectByType<Camera>();
        if (targetCamera == null)
        {
            LoadMainGameScene();
            yield break;
        }

        VideoPlayer player = gameObject.AddComponent<VideoPlayer>();
        AudioSource audio = gameObject.AddComponent<AudioSource>();
        player.source = VideoSource.VideoClip;
        player.clip = transitionVideo;
        player.renderMode = VideoRenderMode.CameraNearPlane;
        player.targetCamera = targetCamera;
        player.aspectRatio = VideoAspectRatio.FitHorizontally;
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.controlledAudioTrackCount = 1;
        player.playOnAwake = false;
        player.isLooping = false;
        audio.spatialBlend = 0f;

        player.Prepare();
        float prepareTimer = 0f;
        while (!player.isPrepared && prepareTimer < 10f)
        {
            prepareTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!player.isPrepared)
        {
            LoadMainGameScene();
            yield break;
        }

        if (player.audioTrackCount > 0)
        {
            player.EnableAudioTrack(0, true);
            player.SetTargetAudioSource(0, audio);
        }

        bool finished = false;
        float playTimer = 0f;
        float maxPlayTime = Mathf.Max((float)transitionVideo.length + 2f, 5f);
        player.loopPointReached += OnVideoFinished;
        player.Play();
        while (!finished && playTimer < maxPlayTime)
        {
            playTimer += Time.unscaledDeltaTime;
            yield return null;
            finished = !player.isPlaying && player.time > 0d;
        }

        player.loopPointReached -= OnVideoFinished;
        LoadMainGameScene();

        void OnVideoFinished(VideoPlayer _) => finished = true;
    }

    private void LoadMainGameScene()
    {
        Debug.Log($"[RollerCoasterInteraction] Đang chuyển sang Scene game chính: {mainGameSceneName} (Index: {mainGameSceneIndex})...");

        // 1. LƯU TỌA ĐỘ VÀ GÓC XOAY NHÂN VẬT TRƯỚC KHI VÀO TÀU LƯỢN
        AnimatorChihiro chihiro = Object.FindAnyObjectByType<AnimatorChihiro>();
        if (chihiro != null)
        {
            chihiro.SavePositionBeforeEnteringRide();
        }

        // 2. Chuyển sang Scene tàu lượn
        if (mainGameSceneIndex >= 0 && mainGameSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(mainGameSceneIndex);
        }
        else
        {
            SceneManager.LoadScene(mainGameSceneName);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}