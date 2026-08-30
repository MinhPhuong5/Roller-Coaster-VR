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
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Toggle khóa/mở chuột (để bấm UI chọn ghế, menu, v.v.)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.localRotation = baseLocalRotation * Quaternion.Euler(pitch, yaw, 0f);
    }

    // Gọi hàm này từ SeatSwitcher ngay sau khi đổi ghế xong,
    // truyền vào localRotation của ghế mới để làm mốc "nhìn thẳng"
    public void ResetLook(Quaternion newBaseLocalRotation)
    {
        baseLocalRotation = newBaseLocalRotation;
        yaw = 0f;
        pitch = 0f;
        transform.localRotation = baseLocalRotation;
    }
}