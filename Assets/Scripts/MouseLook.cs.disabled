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

    void OnEnable()
    {
        // Tự tắt nếu đang cắm kính VR thật
        if (UnityEngine.XR.XRSettings.isDeviceActive)
        {
            enabled = false;
            return;
        }

        ResetLook(Quaternion.identity);
    }

    void LateUpdate()
    {
        bool isLocked = (Cursor.lockState == CursorLockMode.Locked);
        bool isHoldingRightClick = Input.GetMouseButton(1);

        if (!isLocked && !isHoldingRightClick) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

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