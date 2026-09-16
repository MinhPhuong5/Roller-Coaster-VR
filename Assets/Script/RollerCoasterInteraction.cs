using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Quản lý tương tác với mô hình Tàu Lượn Siêu Tốc (TauLuonSieuToc).
/// - Khi Player (Chihiro) bước vào vùng Trigger -> hiện thông báo "Nhấn F để chơi trò chơi".
/// - Nhấn phím F -> mở Hộp thoại Xác nhận ("Có / Bắt đầu" và "Không / Hủy").
/// - Chọn "Có / Bắt đầu" (hoặc Enter) -> chuyển sang Scene thứ 3 (Game).
/// - Chọn "Không / Hủy" (hoặc Esc) -> đóng popup, tiếp tục chơi bình thường.
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

    [Header("=== Trạng Thái Hiện Tại ===")]
    [SerializeField] private bool isPlayerInZone = false;
    [SerializeField] private bool isModalOpen = false;

    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    private void Start()
    {
        // Ban đầu ẩn toàn bộ UI
        if (promptUI != null) promptUI.SetActive(false);
        if (confirmationModalPanel != null) confirmationModalPanel.SetActive(false);

        if (promptText != null)
        {
            promptText.text = "Nhấn F để chơi trò chơi";
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
            if (Input.GetKeyDown(KeyCode.F))
            {
                OpenConfirmationModal();
            }
        }
        // 2. Khi đang mở hộp thoại xác nhận
        else if (isModalOpen)
        {
            // Nhấn phím Enter hoặc Space để chọn 'Bắt đầu'
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnConfirmStartGame();
            }
            // Nhấn phím Escape để chọn 'Hủy'
            else if (Input.GetKeyDown(KeyCode.Escape))
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

        // Nếu nhân vật vẫn ở trong vùng trigger thì hiện lại thông báo nhắc F
        if (isPlayerInZone)
        {
            ShowPrompt(true);
        }
    }

    public void OnConfirmStartGame()
    {
        Debug.Log($"[RollerCoasterInteraction] Đang chuyển sang Scene game chính: {mainGameSceneName} (Index: {mainGameSceneIndex})...");

        // Tải Scene thứ 3
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
