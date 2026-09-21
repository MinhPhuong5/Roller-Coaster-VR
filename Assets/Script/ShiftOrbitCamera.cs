using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Camera điều khiển góc nhìn tự do bằng chuột theo chuẩn FPS/TPS.
/// Không còn tình trạng tự động xoay hay trôi màn hình khi đứng yên.
/// </summary>
public class ShiftOrbitCamera : MonoBehaviour
{
    [Header("Mục tiêu theo dõi")]
    [SerializeField] private Transform target;

    [Header("Độ nhạy chuột")]
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 75f;

    [Header("Khoảng cách Camera")]
    [Tooltip("Khoảng cách lùi ra sau lưng (TPS). Đặt bằng 0 nếu muốn góc nhìn thứ nhất (FPS)")]
    public float distance = 3.5f;
    [Tooltip("Chiều cao camera so với chân")]
    public float height = 1.6f;

    private float yaw;
    private float pitch = 10f;
    private Transform positionOverride;

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
            else if (transform.parent != null) target = transform.parent;
        }

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x > 180f ? angles.x - 360f : angles.x;

        LockCursor();
    }

    private void LateUpdate()
    {
        // Khi tương tác ngồi ghế hoặc xem cinematic
        if (positionOverride != null)
        {
            transform.SetPositionAndRotation(positionOverride.position, positionOverride.rotation);
            return;
        }

        if (target == null) return;

        // Đọc di chuyển chuột trực tiếp
        Vector2 mouse = GetMouseDelta();
        yaw += mouse.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - mouse.y * mouseSensitivity, minPitch, maxPitch);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Tính vị trí camera: nằm sau lưng và trên cao so với target
        Vector3 targetPivot = target.position + Vector3.up * height;
        Vector3 desiredPosition = targetPivot - (rotation * Vector3.forward * distance);

        transform.position = desiredPosition;
        transform.rotation = rotation;
    }

    public void SetPositionOverride(Transform overrideTransform)
    {
        positionOverride = overrideTransform;
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static Vector2 GetMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero;
#else
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
    }
}