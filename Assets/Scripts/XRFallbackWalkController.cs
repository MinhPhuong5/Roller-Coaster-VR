using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// <summary>
/// Điều khiển XR Origin đi bộ WASD + Giữ chuột phải để xoay góc nhìn trên Laptop/PC.
/// Sử dụng hoàn toàn New Input System. Tự động bỏ qua khi cắm kính VR thật hoặc không có chuột/phím.
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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (isVRActive) return;

        // Xử lý chuột qua New Input System (PC testing)
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

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                RotateView();
            }
        }

        // Đi bộ bằng WASD qua New Input System
        MovePlayer();
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
        if (characterController == null || cameraTransform == null) return;

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
        bool isRunning = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        float speed = isRunning ? runSpeed : walkSpeed;

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