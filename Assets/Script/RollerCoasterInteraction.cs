using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quét cự ly tự động cho ga Tàu Lượn:
/// - Người chơi lại gần -> Hiện Panel xác nhận.
/// - Người chơi lùi ra xa -> Tự động ẩn Panel.
/// - Không phụ thuộc Collider/Rigidbody, chạy mượt mà trên cả PC và kính Quest 2.
/// </summary>
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

    [Header("=== Khoảng Cách Kích Hoạt (Mét) ===")]
    [Tooltip("Khoảng cách đứng gần để bảng tự bật lên")]
    public float activationDistance = 5.0f;

    [Header("=== Cấu Hình Vị Trí Đến Ga Tàu ===")]
    [Tooltip("Điểm sàn ga tàu lượn (VR_FloorPoint)")]
    public Transform stationEntryPoint;
    [Tooltip("Đối tượng XR Origin (XR Rig)")]
    public GameObject xrOriginObject;
    [Tooltip("Script quản lý ghế ngồi tàu lượn")]
    public SeatSwitcher seatSwitcher;


    private Transform playerTransform;
    private bool isPlayerNearby = false;

    private void Start()
    {
        // Mặc định ẩn bảng xác nhận khi mới vào Scene
        if (confirmationModalPanel != null)
        {
            confirmationModalPanel.SetActive(false);
        }

        if (confirmStartButton != null)
        {
            confirmStartButton.onClick.RemoveAllListeners();
            confirmStartButton.onClick.AddListener(OnConfirmStartGame);
        }

        if (cancelCloseButton != null)
        {
            cancelCloseButton.onClick.RemoveAllListeners();
            cancelCloseButton.onClick.AddListener(CloseModal);
        }

        FindPlayer();
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        // Tính khoảng cách theo mặt phẳng ngang (bỏ qua độ cao Y)
        Vector3 playerPos = playerTransform.position;
        Vector3 zonePos = transform.position;
        playerPos.y = zonePos.y = 0f;

        float distance = Vector3.Distance(playerPos, zonePos);

        // 1. Khi bước lại gần vùng ga tàu lượn
        if (distance <= activationDistance)
        {
            if (!isPlayerNearby)
            {
                isPlayerNearby = true;
                Debug.Log("[RollerCoasterInteraction] Người chơi đã đến gần ga tàu lượn -> Bật bảng.");
                if (confirmationModalPanel != null)
                {
                    confirmationModalPanel.SetActive(true);
                }
            }
        }
        // 2. Khi lùi ra xa
        else
        {
            if (isPlayerNearby)
            {
                isPlayerNearby = false;
                Debug.Log("[RollerCoasterInteraction] Người chơi đã rời xa ga tàu lượn -> Tắt bảng.");
                if (confirmationModalPanel != null)
                {
                    confirmationModalPanel.SetActive(false);
                }
            }
        }
    }

    private void FindPlayer()
    {
        // 1. Tìm theo tên XR Origin
        GameObject xrRig = GameObject.Find("XR Origin (XR Rig)");
        if (xrRig != null)
        {
            xrOriginObject = xrRig;
            playerTransform = xrRig.transform;
            return;
        }

        // 2. Tìm qua controller đi bộ
        XRFallbackWalkController walk = Object.FindAnyObjectByType<XRFallbackWalkController>();
        if (walk != null)
        {
            xrOriginObject = walk.gameObject;
            playerTransform = walk.transform;
            return;
        }

        // 3. Tìm theo Tag Player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            xrOriginObject = player;
            playerTransform = player.transform;
            return;
        }

        // 4. Tìm qua Camera chính
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            playerTransform = mainCam.transform;
        }
    }

    public void CloseModal()
    {
        if (confirmationModalPanel != null)
        {
            confirmationModalPanel.SetActive(false);
        }
    }

    public void OnConfirmStartGame()
    {
        Debug.Log("[RollerCoasterInteraction] ĐÃ BẤM BẮT ĐẦU -> Dịch chuyển đến ga tàu lượn.");

        if (confirmationModalPanel != null)
        {
            confirmationModalPanel.SetActive(false);
        }

        if (stationEntryPoint == null)
        {
            Debug.LogError("[RollerCoasterInteraction] Chưa gán stationEntryPoint (VR_FloorPoint)!");
            return;
        }

        if (xrOriginObject == null)
        {
            FindPlayer();
        }

        if (xrOriginObject != null)
        {
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

    private void OnDrawGizmos()
    {
        // Vẽ vòng tròn màu vàng bao quanh vùng ga tàu lượn trong cửa sổ Scene
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);
    }
}