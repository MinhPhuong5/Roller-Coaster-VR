using UnityEngine;
using UnityEngine.InputSystem;

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

    void OnEnable()
    {
        ResetLook(Quaternion.identity);
    }

    void LateUpdate()
    {
        // Khi build độc lập lên kính Meta Quest (không gắn chuột), tự động bỏ qua để tránh lỗi
        if (Mouse.current == null) return;

        bool isLocked = (Cursor.lockState == CursorLockMode.Locked);
        bool isHoldingRightClick = Mouse.current.rightButton.isPressed;

        if (!isLocked && !isHoldingRightClick) return;

        // Đọc delta chuột qua New Input System
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float mouseX = mouseDelta.x * mouseSensitivity * 0.05f * Time.deltaTime;
        float mouseY = mouseDelta.y * mouseSensitivity * 0.05f * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Giữ góc nhìn tương đối theo khoang ghế tàu
        transform.localRotation = baseLocalRotation * Quaternion.Euler(pitch, yaw, 0f);
    }

    public void ResetLook(Quaternion newBaseLocalRotation)
    {
        baseLocalRotation = newBaseLocalRotation;
        yaw = 0f;
        pitch = 0f;
        transform.localRotation = baseLocalRotation;
    }
}