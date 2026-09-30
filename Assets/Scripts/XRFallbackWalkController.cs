using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// <summary>
/// Điều khiển XR Origin đi bộ WASD + Giữ chuột phải để xoay góc nhìn trên Laptop/PC.
/// Sử dụng hoàn toàn New Input System. Tự động bỏ qua khi cắm kính VR thật hoặc không có chuột/phím.
/// Tích hợp giới hạn di chuyển chặt chẽ trong các BoxCollider (WalkZone), nếu đi ra ngoài sẽ bị chặn lại.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class XRFallbackWalkController : MonoBehaviour
{
    [Header("Tốc độ di chuyển")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 7.0f;
    public float gravity = -9.81f;

    [Header("Độ nhạy xoay chuột")]
    public float mouseSensitivity = 2.0f;
    public float minPitch = -75f;
    public float maxPitch = 75f;

    [Header("Giới hạn vùng đi dạo (WalkZone Colliders)")]
    public bool restrictToWalkZones = true;
    public BoxCollider[] walkZoneColliders;
    public float boundaryMargin = 0.35f;

    private CharacterController characterController;
    private Transform cameraTransform;
    private float pitch = 0f;
    private float yaw = 0f;
    private float verticalVelocity = 0f;
    private bool isVRActive = false;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        FindAndCacheWalkZones();
    }

    void Start()
    {
        isVRActive = XRSettings.isDeviceActive;
        if (isVRActive)
        {
            enabled = false;
            return;
        }

        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null) cameraTransform = cam.transform;

        yaw = transform.eulerAngles.y;
        if (cameraTransform != null)
        {
            pitch = cameraTransform.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SnapInsideNearestZone();
    }

    void Update()
    {
        if (isVRActive) return;

        // Xử lý chuột qua New Input System (PC testing)
        if (Mouse.current != null)
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                RotateView();
            }
        }

        // Đi bộ bằng WASD qua New Input System
        MovePlayer();
    }

    public void FindAndCacheWalkZones()
    {
        if (walkZoneColliders != null && walkZoneColliders.Length > 0) return;

        List<BoxCollider> foundZones = new List<BoxCollider>();
        BoxCollider[] allBoxes = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);

        foreach (var box in allBoxes)
        {
            if (box == null) continue;
            string objName = box.gameObject.name.ToLower();
            if (objName.Contains("walkzone") || objName.Contains("walk_zone") || objName.Contains("playzone"))
            {
                foundZones.Add(box);
            }
        }

        walkZoneColliders = foundZones.ToArray();
    }

    private void SnapInsideNearestZone()
    {
        if (!restrictToWalkZones || walkZoneColliders == null || walkZoneColliders.Length == 0) return;

        if (!IsPointInsideAnyZone(transform.position, 0.05f))
        {
            Vector3 clampedPos = GetClampedPositionInsideZones(transform.position);
            characterController.enabled = false;
            transform.position = clampedPos;
            characterController.enabled = true;
        }
    }

    private void RotateView()
    {
        if (cameraTransform == null || Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        yaw += mouseDelta.x * mouseSensitivity * 0.1f;
        pitch -= mouseDelta.y * mouseSensitivity * 0.1f;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void MovePlayer()
    {
        if (characterController == null || cameraTransform == null) return;

        float h = 0f;
        float v = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
        }

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * v + right * h).normalized;
        bool isRunning = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        float speed = isRunning ? runSpeed : walkSpeed;

        if (characterController.isGrounded)
        {
            verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 horizontalMove = moveDir * speed * Time.deltaTime;

        // GIỚI HẠN DI CHUYỂN TRONG BOX COLLIDER (CHẶN ĐỨNG NẾU ĐI RA NGOÀI)
        if (restrictToWalkZones && walkZoneColliders != null && walkZoneColliders.Length > 0 && horizontalMove.sqrMagnitude > 0f)
        {
            Vector3 nextPos = transform.position + horizontalMove;
            if (!IsPointInsideAnyZone(nextPos, boundaryMargin))
            {
                // Thử trượt theo trục X
                Vector3 nextPosX = transform.position + new Vector3(horizontalMove.x, 0f, 0f);
                // Thử trượt theo trục Z
                Vector3 nextPosZ = transform.position + new Vector3(0f, 0f, horizontalMove.z);

                if (IsPointInsideAnyZone(nextPosX, boundaryMargin))
                {
                    horizontalMove = new Vector3(horizontalMove.x, 0f, 0f);
                }
                else if (IsPointInsideAnyZone(nextPosZ, boundaryMargin))
                {
                    horizontalMove = new Vector3(0f, 0f, horizontalMove.z);
                }
                else
                {
                    // Chặn hoàn toàn không cho bước ra ngoài
                    horizontalMove = Vector3.zero;
                }
            }
        }

        Vector3 velocity = (horizontalMove / Time.deltaTime) + (Vector3.up * verticalVelocity);
        characterController.Move(velocity * Time.deltaTime);
    }

    public bool IsPointInsideAnyZone(Vector3 worldPoint, float margin = 0.2f)
    {
        if (walkZoneColliders == null || walkZoneColliders.Length == 0) return true;

        for (int i = 0; i < walkZoneColliders.Length; i++)
        {
            BoxCollider box = walkZoneColliders[i];
            if (box != null && box.enabled && box.gameObject.activeInHierarchy)
            {
                if (IsPointInsideBox(worldPoint, box, margin))
                    return true;
            }
        }
        return false;
    }

    public static bool IsPointInsideBox(Vector3 worldPoint, BoxCollider box, float margin = 0.2f)
    {
        if (box == null) return false;

        Vector3 local = box.transform.InverseTransformPoint(worldPoint);
        Vector3 half = (box.size * 0.5f) - new Vector3(margin, 0, margin);
        if (half.x < 0.1f) half.x = box.size.x * 0.5f;
        if (half.z < 0.1f) half.z = box.size.z * 0.5f;

        bool inX = Mathf.Abs(local.x - box.center.x) <= half.x;
        bool inZ = Mathf.Abs(local.z - box.center.z) <= half.z;
        bool inY = Mathf.Abs(local.y - box.center.y) <= (box.size.y * 0.5f + 3.0f);

        return inX && inZ && inY;
    }

    public Vector3 GetClampedPositionInsideZones(Vector3 worldPoint)
    {
        if (walkZoneColliders == null || walkZoneColliders.Length == 0) return worldPoint;

        Vector3 bestPoint = worldPoint;
        float minDistance = float.MaxValue;

        foreach (var box in walkZoneColliders)
        {
            if (box == null) continue;
            Vector3 closest = box.ClosestPoint(worldPoint);
            float d = Vector3.Distance(worldPoint, closest);
            if (d < minDistance)
            {
                minDistance = d;
                bestPoint = closest;
            }
        }

        bestPoint.y = worldPoint.y;
        return bestPoint;
    }
}