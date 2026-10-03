using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// <summary>
/// Điều khiển XR Origin đi bộ WASD + Giữ chuột phải để xoay góc nhìn trên Laptop/PC.
/// Sử dụng New Input System. Hoàn toàn mượt mà, triệt tiêu 100% rung lắc (jitter-free).
/// Di chuyển thuần túy dựa trên vật lý mặt sàn (MeshCollider / BoxCollider / Terrain) giống hệt như ở công viên,
/// không dùng các BoxCollider ảo cưỡng bức kéo tọa độ gây rơi sàn.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class XRFallbackWalkController : MonoBehaviour
{
    [Header("Tốc độ di chuyển")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 7.0f;
    public float gravity = -9.81f;

    [Header("Độ nhạy xoay chuột")]
    public float mouseSensitivity = 2.0f;
    public float minPitch = -75f;
    public float maxPitch = 75f;

    [HideInInspector] public BoxCollider[] walkZoneColliders;
    [HideInInspector] public bool restrictToWalkZones = false;

    private CharacterController characterController;
    private Transform cameraTransform;
    private float pitch = 0f;
    private float yaw = 0f;
    private float verticalVelocity = 0f;
    private bool isVRActive = false;

    void Awake()
    {
        ConfigureCharacterController();
        EnsureWalkZonesAreTriggers();
    }

    public void ConfigureCharacterController()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (characterController != null)
        {
            characterController.skinWidth = 0.015f;
            characterController.minMoveDistance = 0f;
            characterController.stepOffset = 0.35f;
            characterController.slopeLimit = 60f;
            characterController.height = 1.6f;
            characterController.center = new Vector3(0f, 0.8f, 0f);
            characterController.radius = 0.25f;
        }
    }

    private float lastHitLogTime = 0f;

    public static void EnsureWalkZonesAreTriggers()
    {
        BoxCollider[] allBoxes = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int convertedCount = 0;
        foreach (var b in allBoxes)
        {
            if (b == null) continue;
            string n = b.gameObject.name.ToLower();
            bool isZone = n.Contains("walk") || n.Contains("zone") || n.Contains("play") || n.Contains("ticket")
                       || (b.transform.parent != null && b.transform.parent.name.Equals("Zone", System.StringComparison.OrdinalIgnoreCase))
                       || (b.transform.root != null && b.transform.root.name.Equals("Zone", System.StringComparison.OrdinalIgnoreCase));

            // Không can thiệp vào sàn vật lý của nhà ga
            if (n.Contains("solidfloor") || n.Contains("platform")) continue;

            if (isZone && !b.isTrigger)
            {
                b.isTrigger = true;
                convertedCount++;
                Debug.Log($"<color=yellow>[XRFallbackWalkController] Đã tự động chuyển '{b.gameObject.name}' thành IsTrigger=true để không chặn vật lý!</color>");
            }
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Bỏ qua mặt đất/sàn
        if (hit.normal.y > 0.5f) return;

        if (Time.time - lastHitLogTime > 0.8f)
        {
            lastHitLogTime = Time.time;
            Debug.LogError($"<color=red><b>[VẬT CẢN CHẶN ĐƯỜNG] Bạn đang đâm vào: '{hit.gameObject.name}' (Loại: {hit.collider.GetType().Name}, isTrigger: {hit.collider.isTrigger}, Layer: {LayerMask.LayerToName(hit.gameObject.layer)}) tại tọa độ: {hit.point}</b></color>", hit.gameObject);
        }
    }

    void OnEnable()
    {
        ConfigureCharacterController();
        EnsureWalkZonesAreTriggers();

        // Tắt MouseLook đi kèm nếu có để tránh xung đột kép góc quay
        MouseLook ml = GetComponentInChildren<MouseLook>();
        if (ml != null) ml.enabled = false;

        InitCameraAngles();
    }

    void Start()
    {
        isVRActive = XRSettings.isDeviceActive;
        if (isVRActive)
        {
            enabled = false;
            return;
        }

        ConfigureCharacterController();
        InitCameraAngles();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void InitCameraAngles()
    {
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        yaw = transform.eulerAngles.y;
        if (cameraTransform != null)
        {
            pitch = cameraTransform.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
        }
    }

    void Update()
    {
        if (isVRActive) return;

        // 1. Quản lý trạng thái khóa chuột (giữ chuột phải để lia góc nhìn)
        if (Mouse.current != null)
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // 2. Di chuyển người chơi (WASD)
        MovePlayer();
    }

    void LateUpdate()
    {
        if (isVRActive) return;

        // Xoay góc nhìn trong LateUpdate để đảm bảo camera bám mượt mà sau chuyển động
        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
        {
            RotateView();
        }
    }

    private void RotateView()
    {
        if (cameraTransform == null || Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        yaw += mouseDelta.x * mouseSensitivity * 0.1f;
        pitch -= mouseDelta.y * mouseSensitivity * 0.1f;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void MovePlayer()
    {
        if (characterController == null || !characterController.enabled) return;
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
            if (cameraTransform == null) return;
        }

        float h = 0f;
        float v = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
        }

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * v + right * h).normalized;
        bool isMoving = moveDir.sqrMagnitude > 0.0001f;
        bool isRunning = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        float speed = isRunning ? runSpeed : walkSpeed;
        bool isGrounded = characterController.isGrounded;

        if (isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -1.5f; // Giữ chân bám sát mặt sàn và leo bậc thềm
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 horizontalMove = moveDir * speed * Time.deltaTime;

        // Di chuyển thuần túy theo vật lý mặt sàn và tường va chạm MeshCollider (giống hệt công viên)
        if (isMoving || !isGrounded)
        {
            Vector3 velocity = (horizontalMove / Time.deltaTime) + (Vector3.up * verticalVelocity);
            characterController.Move(velocity * Time.deltaTime);
        }
    }

    public void FindAndCacheWalkZones(bool forceRefresh = false) { }
}