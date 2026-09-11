using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ThirdPersonMouseLook : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Transform firstPersonAnchor;
    [SerializeField] Vector3 firstPersonLocalOffset = new Vector3(0f, 7.5f, 0.15f);
    [SerializeField] KeyCode switchViewKey = KeyCode.V;
    [SerializeField] float mouseSensitivity = 0.12f;
    [SerializeField] float minPitch = -35f;
    [SerializeField] float maxPitch = 70f;

    float yaw;
    float pitch;
    Vector3 offset;
    bool firstPerson;

    void Start()
    {
        offset = transform.position - target.position;
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x > 180f ? transform.eulerAngles.x - 360f : transform.eulerAngles.x;
        LockCursor();
    }

    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) UnlockCursor();
        if (Input.GetMouseButtonDown(0)) LockCursor();
        if (Input.GetKeyDown(switchViewKey)) firstPerson = !firstPerson;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 mouseDelta = GetMouseDelta();
        yaw += mouseDelta.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, minPitch, maxPitch);
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        if (firstPerson)
        {
            transform.position = firstPersonAnchor != null
                ? firstPersonAnchor.position
                : target.TransformPoint(firstPersonLocalOffset);
            transform.rotation = rotation;
        }
        else
        {
            transform.position = target.position + rotation * offset;
            transform.LookAt(target);
        }
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    Vector2 GetMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * Time.deltaTime;
#endif
    }
}
