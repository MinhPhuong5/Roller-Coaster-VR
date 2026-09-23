using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Điều khiển XR Origin đi bộ WASD + Giữ chuột phải để xoay góc nhìn trên Laptop/PC.
/// Chuột luôn tự do để tương tác Kiosk UI, không cần bấm ESC.
/// Tự động tắt khi cắm kính VR thật.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class XRFallbackWalkController : MonoBehaviour
{
    [Header("Tốc độ di chuyển")]
    public float walkSpeed = 5.0f;
    public float runSpeed = 10.0f;
    public float gravity = -9.81f;

    [Header("Độ nhạy xoay chuột")]
    public float mouseSensitivity = 2.0f;
    public float minPitch = -75f;
    public float maxPitch = 75f;

    private CharacterController characterController;
    private Transform cameraTransform;
    private float pitch = 0f;
    private float yaw = 0f;
    private float verticalVelocity = 0f;
    private bool isVRActive = false;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Start()
    {
        isVRActive = XRSettings.isDeviceActive;
        if (isVRActive)
        {
            enabled = false;
            return;
        }

        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null) cameraTransform = cam.transform;

        yaw = transform.eulerAngles.y;
        if (cameraTransform != null)
        {
            pitch = cameraTransform.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
        }

        // Mặc định luôn để chuột tự do, không giấu con trỏ chuột
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (isVRActive) return;

        // Bấm giữ chuột phải để lia camera ngắm nhìn công viên
        if (Input.GetMouseButtonDown(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (Input.GetMouseButtonUp(1))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Chỉ xoay góc nhìn khi đang giữ chuột phải
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            RotateView();
        }

        // Đi bộ bằng WASD luôn hoạt động
        MovePlayer();
    }

    private void RotateView()
    {
        if (cameraTransform == null) return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Xoay thân người theo phương ngang (Y)
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        // Xoay camera ngước lên / cúi xuống (X)
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void MovePlayer()
    {
        if (characterController == null || cameraTransform == null) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * v + right * h).normalized;
        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        if (characterController.isGrounded)
        {
            verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 velocity = (moveDir * speed) + (Vector3.up * verticalVelocity);
        characterController.Move(velocity * Time.deltaTime);
    }
}