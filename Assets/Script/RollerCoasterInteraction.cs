using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quét cự ly tự động cho ga Tàu Lượn:
/// - Khi Play: Mặc định ẩn hoàn toàn bảng UI.
/// - Người chơi lại gần trong bán kính activationDistance -> Hiện Panel xác nhận.
/// - Người chơi lùi ra xa -> Tự động ẩn Panel.
/// - Bấm 'Có / Bắt đầu': Dịch chuyển người chơi đến ga tàu lượn (VR_FloorPoint) và mở bảng chọn ghế (SeatSwitcher).
/// - Bấm 'Không / Hủy': Đóng bảng xác nhận.
/// - Tự động tìm kiếm & liên kết đầy đủ các component (UI Panel, Button, XR Origin, VR_FloorPoint, SeatSwitcher) nếu trong Inspector chưa gán.
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

    private void Awake()
    {
        ResolveReferences();
        SetupUIStyling();
        BindButtonEvents();
        HideModalImmediate();
    }

    private void Start()
    {
        ResolveReferences();
        HideModalImmediate();
        BindButtonEvents();
        FindPlayer();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindButtonEvents();
    }

    /// <summary>
    /// Tự động tìm kiếm các tham chiếu bị thiếu trong Scene
    /// </summary>
    public void ResolveReferences()
    {
        // 1. Tìm Panel xác nhận (Panel_ConfirmationModal hoặc Canvas_GameInteraction)
        if (confirmationModalPanel == null)
        {
            GameObject modalObj = GameObject.Find("Panel_ConfirmationModal");
            if (modalObj != null)
            {
                confirmationModalPanel = modalObj;
            }
            else
            {
                GameObject canvasObj = GameObject.Find("Canvas_GameInteraction");
                if (canvasObj != null)
                {
                    Transform panelTrans = canvasObj.transform.Find("Panel_ConfirmationModal");
                    if (panelTrans != null)
                        confirmationModalPanel = panelTrans.gameObject;
                    else
                        confirmationModalPanel = canvasObj;
                }
            }
        }

        // 2. Tìm điểm sàn ga tàu (VR_FloorPoint)
        if (stationEntryPoint == null)
        {
            GameObject vrFloor = GameObject.Find("VR_FloorPoint");
            if (vrFloor != null)
            {
                stationEntryPoint = vrFloor.transform;
            }
            else
            {
                GameObject station = GameObject.Find("WalkZone_Station");
                if (station != null) stationEntryPoint = station.transform;
            }
        }

        // 3. Tìm SeatSwitcher
        if (seatSwitcher == null)
        {
            seatSwitcher = Object.FindAnyObjectByType<SeatSwitcher>();
        }

        // 4. Tìm XR Origin
        if (xrOriginObject == null)
        {
            FindPlayer();
        }
    }

    private void SetupUIStyling()
    {
        if (confirmationModalPanel != null)
        {
            Canvas canvas = confirmationModalPanel.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
                {
                    canvas.worldCamera = Camera.main;
                }

                // Đảm bảo có GraphicRaycaster để nhận click chuột / VR
                GraphicRaycaster gr = canvas.GetComponent<GraphicRaycaster>();
                if (gr == null)
                {
                    gr = canvas.gameObject.AddComponent<GraphicRaycaster>();
                }
                gr.enabled = true;
            }

            // Tắt hoàn toàn Image nền của Panel cha (xóa bỏ viền panel/khung tối thừa bên ngoài)
            Image panelImg = confirmationModalPanel.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.enabled = false;
            }

            // Giữ ModalCard ở kích thước gọn gàng chuẩn (740x460), căn giữa, KHÔNG kéo tràn viền
            Transform modalCard = confirmationModalPanel.transform.Find("ModalCard");
            if (modalCard != null)
            {
                RectTransform mcRt = modalCard.GetComponent<RectTransform>();
                if (mcRt != null)
                {
                    mcRt.anchorMin = new Vector2(0.5f, 0.5f);
                    mcRt.anchorMax = new Vector2(0.5f, 0.5f);
                    mcRt.pivot = new Vector2(0.5f, 0.5f);
                    mcRt.anchoredPosition = Vector2.zero;
                    mcRt.sizeDelta = new Vector2(740f, 460f);
                    mcRt.localScale = Vector3.one;
                }
            }
        }
    }

    /// <summary>
    /// Quét và gắn sự kiện click cho TẤT CẢ nút bấm trong Modal để đảm bảo 100% không bị sót nút
    /// </summary>
    public void BindButtonEvents()
    {
        if (confirmationModalPanel == null) return;

        Button[] allButtons = confirmationModalPanel.GetComponentsInChildren<Button>(true);
        foreach (var btn in allButtons)
        {
            if (btn == null) continue;

            string btnName = btn.gameObject.name.ToLower();
            string btnText = "";
            TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) btnText = tmp.text.ToLower();

            // Phân loại nút dựa theo tên hoặc chữ hiển thị
            if (btnName.Contains("cancel") || btnName.Contains("close") || btnName.Contains("huy") || btnName.Contains("khong") || btnText.Contains("không") || btnText.Contains("hủy"))
            {
                cancelCloseButton = btn;
                btn.onClick.RemoveListener(CloseModal);
                btn.onClick.AddListener(CloseModal);
            }
            else
            {
                // Mặc định các nút còn lại (hoặc chứa start/confirm/co/batdau/play) là nút Bắt đầu
                if (confirmStartButton == null || btnName.Contains("start") || btnName.Contains("confirm") || btnName.Contains("co") || btnName.Contains("batdau"))
                {
                    confirmStartButton = btn;
                }
                btn.onClick.RemoveListener(OnConfirmStartGame);
                btn.onClick.AddListener(OnConfirmStartGame);
            }
        }

        // Đảm bảo nếu confirmStartButton được gán riêng cũng được gắn listener
        if (confirmStartButton != null)
        {
            confirmStartButton.onClick.RemoveListener(OnConfirmStartGame);
            confirmStartButton.onClick.AddListener(OnConfirmStartGame);
        }

        if (cancelCloseButton != null)
        {
            cancelCloseButton.onClick.RemoveListener(CloseModal);
            cancelCloseButton.onClick.AddListener(CloseModal);
        }
    }

    private void HideModalImmediate()
    {
        isPlayerNearby = false;
        if (confirmationModalPanel != null)
        {
            confirmationModalPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        if (confirmationModalPanel == null)
        {
            ResolveReferences();
            BindButtonEvents();
        }

        // Tính khoảng cách theo mặt phẳng ngang (bỏ qua độ cao Y)
        Vector3 playerPos = playerTransform.position;
        if (Camera.main != null)
        {
            playerPos = Camera.main.transform.position;
        }

        Vector3 zonePos = transform.position;
        playerPos.y = zonePos.y = 0f;

        float distance = Vector3.Distance(playerPos, zonePos);

        // 1. Khi bước lại gần vùng ga tàu lượn
        if (distance <= activationDistance)
        {
            if (!isPlayerNearby)
            {
                isPlayerNearby = true;
                Debug.Log($"[RollerCoasterInteraction] Người chơi đến gần ga tàu lượn ({distance:F2}m) -> Bật bảng.");
                if (confirmationModalPanel != null)
                {
                    confirmationModalPanel.SetActive(true);
                    BindButtonEvents();
                }
            }
        }
        // 2. Khi lùi ra xa
        else
        {
            if (isPlayerNearby)
            {
                isPlayerNearby = false;
                Debug.Log($"[RollerCoasterInteraction] Người chơi rời xa ga tàu lượn ({distance:F2}m) -> Tắt bảng.");
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
            if (xrOriginObject == null) xrOriginObject = mainCam.transform.root.gameObject;
        }
    }

    public void CloseModal()
    {
        if (confirmationModalPanel != null)
        {
            confirmationModalPanel.SetActive(false);
        }
        isPlayerNearby = true;
    }

    public void OnConfirmStartGame()
    {
        Debug.Log("<color=#00FF66><b>[RollerCoasterInteraction] ĐÃ NHẬN LỆNH BẮT ĐẦU -> Dịch chuyển người chơi đến ga tàu lượn...</b></color>");

        if (confirmationModalPanel != null)
        {
            confirmationModalPanel.SetActive(false);
        }
        isPlayerNearby = false;

        // Đảm bảo đủ các tham chiếu
        ResolveReferences();

        if (stationEntryPoint == null)
        {
            GameObject vrFloor = GameObject.Find("VR_FloorPoint");
            if (vrFloor != null) stationEntryPoint = vrFloor.transform;
        }

        if (xrOriginObject == null)
        {
            FindPlayer();
        }

        if (seatSwitcher == null)
        {
            seatSwitcher = Object.FindAnyObjectByType<SeatSwitcher>();
        }

        // 1. Dịch chuyển XR Origin đến ga tàu
        if (xrOriginObject != null && stationEntryPoint != null)
        {
            // Lưu lại vị trí đứng an toàn tại công viên trước khi vào ga để phục vụ quay về sau này
            Vector3 safeParkPos = xrOriginObject.transform.position;
            Quaternion safeParkRot = xrOriginObject.transform.rotation;
            if (seatSwitcher != null)
            {
                seatSwitcher.SetSavedParkPoint(safeParkPos, safeParkRot);
            }

            CharacterController cc = xrOriginObject.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            XRFallbackWalkController walkCtrl = xrOriginObject.GetComponent<XRFallbackWalkController>();
            if (walkCtrl != null) walkCtrl.enabled = false;

            xrOriginObject.transform.SetParent(null);
            xrOriginObject.transform.localScale = Vector3.one;

            Vector3 spawnPos = SeatSwitcher.FindSolidGroundPosition(stationEntryPoint.position);
            xrOriginObject.transform.SetPositionAndRotation(spawnPos, stationEntryPoint.rotation);

            // Chuyển scale của NPC đại diện người chơi lên 1.22x theo tỷ lệ ga tàu lượn
            PlayerNPCBodyController playerBody = xrOriginObject.GetComponent<PlayerNPCBodyController>();
            if (playerBody == null) playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
            if (playerBody != null)
            {
                playerBody.SetStationScale();
            }

            // Đọc chiều cao mắt từ PlayerNPCBodyController để đảm bảo Camera đứng đúng tầm mắt (Scale 1.22x)
            float eyeHeight = (playerBody != null) ? playerBody.GetPlayerStandingEyeHeight() : (1.45f * 1.22f);

            Camera mainCam = xrOriginObject.GetComponentInChildren<Camera>();
            if (mainCam != null)
            {
                Transform camParent = mainCam.transform.parent;
                if (camParent != null && camParent != xrOriginObject.transform)
                {
                    camParent.localPosition = new Vector3(0f, eyeHeight, 0.06f);
                    mainCam.transform.localPosition = Vector3.zero;
                }
                else
                {
                    mainCam.transform.localPosition = new Vector3(0f, eyeHeight, 0.06f);
                }
                mainCam.transform.localRotation = Quaternion.identity;
                mainCam.nearClipPlane = 0.05f;
            }

            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();

            if (walkCtrl != null)
            {
                walkCtrl.FindAndCacheWalkZones(true);
                walkCtrl.enabled = true;
                walkCtrl.InitCameraAngles();
            }

            Debug.Log($"<color=#00FF66>[RollerCoasterInteraction] Đã dịch chuyển thành công đến tọa độ: {spawnPos}, Camera Eye Height: {eyeHeight:F2}m</color>");
        }
        else
        {
            Debug.LogError($"[RollerCoasterInteraction] Không thể dịch chuyển! xrOriginObject={(xrOriginObject != null ? xrOriginObject.name : "NULL")}, stationEntryPoint={(stationEntryPoint != null ? stationEntryPoint.name : "NULL")}");
        }

        // 2. Kích hoạt SeatSwitcher vào chế độ chọn ghế tại sảnh ga
        if (seatSwitcher != null)
        {
            seatSwitcher.ForceBoardingMode();
        }

        // 3. Mở con trỏ chuột để người chơi tương tác với bảng chọn ghế
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);
    }
}