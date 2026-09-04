using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Header("Sensitivity")]
    public float mouseSensitivity = 150f;

    [Header("Pitch Clamp")]
    public float minPitch = -80f;
    public float maxPitch = 80f;

    private float yaw = 0f;
    private float pitch = 0f;
    private Quaternion baseLocalRotation = Quaternion.identity;

    void Start()
    {
        baseLocalRotation = transform.localRotation;
    }

    void Update()
    {
        // 1. Kiểm tra xem có được phép xoay không:
        // - Hoặc khi tàu đã chạy (chuột bị khóa cứng: CursorLockMode.Locked)
        // - Hoặc khi đang ngồi chờ ở ghế trước khi bấm Bắt đầu (nhấn giữ Chuột Phải)
        bool isLocked = (Cursor.lockState == CursorLockMode.Locked);
        bool isHoldingRightClick = Input.GetMouseButton(1);

        if (!isLocked && !isHoldingRightClick) return;

        // 2. Nhận tín hiệu di chuột
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Giữ góc nhìn chuẩn đồng bộ với hướng ghế
        transform.localRotation = baseLocalRotation * Quaternion.Euler(pitch, yaw, 0f);
    }

    // Reset lại góc quay khi chuyển đổi giữa các ghế
    public void ResetLook(Quaternion newBaseLocalRotation)
    {
        baseLocalRotation = newBaseLocalRotation;
        yaw = 0f;
        pitch = 0f;
        transform.localRotation = baseLocalRotation;
    }
}