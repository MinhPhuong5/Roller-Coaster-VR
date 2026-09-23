using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class RollerCoasterInteraction : MonoBehaviour
{
    [Header("=== Bảng Điều Khiển Kiosk 3D (World Space) ===")]
    [Tooltip("Panel xác nhận đặt cố định trong công viên")]
    public GameObject confirmationModalPanel;
    [Tooltip("Nút 'Có / Bắt đầu'")]
    public Button confirmStartButton;
    [Tooltip("Nút 'Không / Hủy' (Tùy chọn)")]
    public Button cancelCloseButton;
    [Tooltip("Tiêu đề hộp thoại")]
    public TextMeshProUGUI modalTitleText;
    [Tooltip("Nội dung thông điệp hộp thoại")]
    public TextMeshProUGUI modalMessageText;

    [Header("=== Cấu Hình Vị Trí Đến Ga Tàu ===")]
    [Tooltip("Điểm sàn ga tàu lượn (VR_FloorPoint)")]
    public Transform stationEntryPoint;
    [Tooltip("Đối tượng XR Origin (XR Rig)")]
    public GameObject xrOriginObject;
    [Tooltip("Script quản lý ghế ngồi tàu lượn")]
    public SeatSwitcher seatSwitcher;

    // Giữ lại các trường này để tương thích với các script Editor / Builder
    [HideInInspector] public GameObject promptUI;
    [HideInInspector] public TextMeshProUGUI promptText;
    [HideInInspector] public string mainGameSceneName = "Game";
    [HideInInspector] public int mainGameSceneIndex = 2;

    [Header("=== Trạng Thái Hiện Tại ===")]
    [SerializeField] private bool isPlayerInZone = false;

    // Input cho tay cầm VR
    private InputAction vrConfirm;

    private void Awake()
    {
        vrConfirm = new InputAction(type: InputActionType.Button);
        vrConfirm.AddBinding("<XRController>{RightHand}/primaryButton");
        vrConfirm.AddBinding("<XRController>{LeftHand}/primaryButton");
    }

    private void OnEnable()
    {
        if (vrConfirm != null) vrConfirm.Enable();
    }

    private void OnDisable()
    {
        if (vrConfirm != null) vrConfirm.Disable();
    }

    private void Start()
    {
        if (confirmationModalPanel != null)
        {
            confirmationModalPanel.SetActive(true);
        }

        if (confirmStartButton != null)
        {
            confirmStartButton.onClick.RemoveAllListeners();
            confirmStartButton.onClick.AddListener(OnConfirmStartGame);
        }

        if (xrOriginObject == null)
        {
            XRFallbackWalkController walk = Object.FindAnyObjectByType<XRFallbackWalkController>();
            if (walk != null)
            {
                xrOriginObject = walk.gameObject;
            }
        }
    }

    private void Update()
    {
        if (isPlayerInZone)
        {
            // Bấm Enter (Laptop) hoặc nút A/X (Kính VR) để kích hoạt nhanh
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || (vrConfirm != null && vrConfirm.WasPressedThisFrame()))
            {
                OnConfirmStartGame();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            isPlayerInZone = true;
            Debug.Log("[RollerCoasterInteraction] Đã đến trước bảng Kiosk.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            isPlayerInZone = false;
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (xrOriginObject != null && (other.gameObject == xrOriginObject || other.transform.IsChildOf(xrOriginObject.transform))) return true;
        return other.GetComponent<CharacterController>() != null || other.GetComponent<XRFallbackWalkController>() != null;
    }

    public void OnConfirmStartGame()
    {
        Debug.Log("[RollerCoasterInteraction] ĐÃ BẤM BẮT ĐẦU -> Dịch chuyển đến ga tàu lượn.");

        if (stationEntryPoint == null)
        {
            Debug.LogError("[RollerCoasterInteraction] Chưa gán stationEntryPoint (VR_FloorPoint)!");
            return;
        }

        if (xrOriginObject == null)
        {
            XRFallbackWalkController walk = Object.FindAnyObjectByType<XRFallbackWalkController>();
            if (walk != null) xrOriginObject = walk.gameObject;
        }

        if (xrOriginObject != null)
        {
            // Tắt bộ điều khiển đi bộ tự do
            XRFallbackWalkController walkCtrl = xrOriginObject.GetComponent<XRFallbackWalkController>();
            if (walkCtrl != null) walkCtrl.enabled = false;

            CharacterController cc = xrOriginObject.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            xrOriginObject.transform.SetParent(null);
            xrOriginObject.transform.localScale = Vector3.one;
            xrOriginObject.transform.SetPositionAndRotation(stationEntryPoint.position, stationEntryPoint.rotation);

            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
        }

        if (seatSwitcher != null)
        {
            seatSwitcher.ForceBoardingMode();
        }
        else
        {
            SeatSwitcher foundSwitcher = Object.FindAnyObjectByType<SeatSwitcher>();
            if (foundSwitcher != null) foundSwitcher.ForceBoardingMode();
        }
    }
}