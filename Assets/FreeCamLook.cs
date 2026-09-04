using UnityEngine;

public class FreeCamLook : MonoBehaviour
{
    [Header("Tốc độ xoay chuột")]
    public float sensitivity = 120f;

    [Header("Giới hạn ngước/cúi đầu")]
    public float minPitch = -60f;
    public float maxPitch = 60f;

    private float pitch = 0f;
    private float yaw = 0f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x > 180 ? angles.x - 360 : angles.x;
        yaw = angles.y;
    }

    void Update()
    {
        // GIỮ CHUỘT PHẢI để xoay nhìn tự do xung quanh sân ga
        if (Input.GetMouseButton(1))
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        else if (Input.GetMouseButtonUp(1))
        {
            // THẢ CHUỘT PHẢI: Trả lại con trỏ chuột bình thường để click bấm nút UI chọn ghế
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}