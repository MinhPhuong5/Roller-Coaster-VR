using UnityEngine;

[DefaultExecutionOrder(10000)]
public class FPSCameraController : MonoBehaviour
{
    [Header("Mục tiêu theo dõi")]
    public Transform target; // Kéo Chihiro vào đây

    [Header("Thiết lập khoảng cách")]
    [Tooltip("Khoảng cách nhìn từ sau lưng (TPS)")]
    public float tpsDistance = 6.5f;
    [Tooltip("Chiều cao tâm ngắm (đã tính theo scale 2.5 của Chihiro). HybridPlayerManager cũng dùng làm chiều cao mắt của góc XR")]
    public float targetHeight = 3.6f;

    [Header("Độ nhạy chuột")]
    public float mouseSensitivity = 2.5f;
    public float minPitch = -30f;
    public float maxPitch = 60f;

    private float yaw;
    private float pitch = 15f;

    void Start()
    {
        transform.localScale = Vector3.one;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (target != null)
            yaw = target.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // HybridPlayerManager gọi khi quay lại từ góc XR để giữ nguyên hướng nhìn
    public void SetYaw(float newYaw)
    {
        yaw = newYaw;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetPivot = target.position + Vector3.up * targetHeight;

        // Camera lùi ra sau lưng và xoay quanh Chihiro
        transform.position = targetPivot - (rotation * Vector3.forward * tpsDistance);
        transform.rotation = rotation;
    }
}