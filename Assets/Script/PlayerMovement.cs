using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Speeds")]
    [Tooltip("Tốc độ đi bộ")]
    public float walkSpeed = 6.0f;
    [Tooltip("Tốc độ chạy nhanh (khi giữ Shift)")]
    public float runSpeed = 16.0f;
    [Tooltip("Độ mượt khi xoay người theo hướng đi")]
    public float rotationSmoothTime = 0.1f;
    [Tooltip("Độ mượt chuyển đổi animation (tránh giật cục)")]
    public float animationDampTime = 0.15f;

    [Header("Animation Thresholds")]
    [Tooltip("Giá trị Speed trong Animator khi đi bộ")]
    public float walkAnimValue = 0.5f;
    [Tooltip("Giá trị Speed trong Animator khi chạy")]
    public float runAnimValue = 1.0f;

    [Header("Physics")]
    public float gravity = -9.81f;

    [Header("References")]
    [Tooltip("Kéo Main Camera vào đây để nhân vật đi theo hướng nhìn của camera (nếu để trống script tự tìm)")]
    public Transform cameraTransform;

    private CharacterController controller;
    private Animator animator;
    private float currentVelocityY;
    private float turnSmoothVelocity;
    private int speedHash;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        speedHash = Animator.StringToHash("Speed");
    }

    void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        // 1. Đọc phím điều khiển (W/A/S/D hoặc phím mũi tên)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        // 2. Kiểm tra trạng thái: Đứng yên (0), Đi bộ, Chạy nhanh
        bool isMoving = direction.magnitude >= 0.1f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        float targetSpeedValue = 0f;
        float currentMoveSpeed = 0f;
        Vector3 moveDir = Vector3.zero;

        if (isMoving)
        {
            if (isRunning)
            {
                targetSpeedValue = runAnimValue;
                currentMoveSpeed = runSpeed;
            }
            else
            {
                targetSpeedValue = walkAnimValue;
                currentMoveSpeed = walkSpeed;
            }

            // 3. Tính toán góc quay mượt mà theo góc nhìn Camera
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            if (cameraTransform != null)
            {
                targetAngle += cameraTransform.eulerAngles.y;
            }

            float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);

            // Hướng di chuyển phẳng trên mặt sàn
            moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        }

        // 4. Xử lý trọng lực (Chống lún sàn triệt để)
        if (controller.isGrounded)
        {
            // Khi đã chạm đất thì triệt tiêu lực rơi, giữ lực ép nhẹ -0.5f để chân dính sát sàn
            currentVelocityY = -0.5f;
        }
        else
        {
            // Khi ở trên không thì mới áp dụng gia tốc rơi
            currentVelocityY += gravity * Time.deltaTime;
        }

        // Kết hợp di chuyển phẳng và trọng lực vào 1 lệnh Move duy nhất
        Vector3 finalVelocity = (moveDir.normalized * currentMoveSpeed) + new Vector3(0, currentVelocityY, 0);
        controller.Move(finalVelocity * Time.deltaTime);

        // 5. Cập nhật tham số Speed vào Animator
        if (animator != null)
        {
            animator.SetFloat(speedHash, targetSpeedValue, animationDampTime, Time.deltaTime);
        }
    }
}
