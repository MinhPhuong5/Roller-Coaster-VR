using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Hold Shift + left mouse for free look; release to return behind the player.</summary>
public class ShiftOrbitCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -75f;
    [SerializeField] private float maxPitch = 75f;
    [SerializeField] private float returnSpeed = 6f;

    private Vector3 defaultLocalOffset;
    private Quaternion defaultLocalRotation;
    private float yaw;
    private float pitch;
    private bool orbiting;
    private Transform positionOverride;

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (target == null)
        {
            Debug.LogError("[ShiftOrbitCamera] Khong tim thay Player.");
            enabled = false;
            return;
        }

        defaultLocalOffset = target.InverseTransformPoint(transform.position);
        defaultLocalRotation = Quaternion.Inverse(target.rotation) * transform.rotation;
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x > 180f ? angles.x - 360f : angles.x;
    }

    private void LateUpdate()
    {
        if (positionOverride != null)
        {
            transform.SetPositionAndRotation(positionOverride.position, positionOverride.rotation);
            return;
        }

        bool wantsOrbit = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetMouseButton(0);
        if (wantsOrbit)
        {
            if (!orbiting) LockCursor();
            orbiting = true;

            Vector2 mouse = GetMouseDelta();
            yaw += mouse.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - mouse.y * mouseSensitivity, minPitch, maxPitch);
            transform.position = target.TransformPoint(defaultLocalOffset);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            return;
        }

        if (orbiting) UnlockCursor();
        orbiting = false;
        transform.position = Vector3.Lerp(transform.position, target.TransformPoint(defaultLocalOffset), returnSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation * defaultLocalRotation, returnSpeed * Time.deltaTime);
    }

    /// <summary>Đặt camera tại một điểm cố định trong lúc tương tác, ví dụ ngồi ghế.</summary>
    public void SetPositionOverride(Transform overrideTransform)
    {
        positionOverride = overrideTransform;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static Vector2 GetMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * Time.deltaTime;
#endif
    }
}
