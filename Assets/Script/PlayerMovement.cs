using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Điều khiển người chơi di chuyển (W/A/S/D) và xoay theo hướng nhìn Camera.
/// Tích hợp giới hạn di chuyển chặt chẽ trong các BoxCollider (WalkZone).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Speeds")]
    [Tooltip("Tốc độ đi bộ")]
    public float walkSpeed = 3.5f;
    [Tooltip("Tốc độ chạy nhanh (khi giữ Shift)")]
    public float runSpeed = 7.0f;
    [Tooltip("Độ mượt khi xoay người theo hướng đi")]
    public float rotationSmoothTime = 0.1f;
    [Tooltip("Độ mượt chuyển đổi animation (tránh giật cục)")]
    public float animationDampTime = 0.15f;

    [Header("Animation Thresholds")]
    [Tooltip("Giá trị Speed trong Animator khi đi bộ")]
    public float walkAnimValue = 0.5f;
    [Tooltip("Giá trị Speed trong Animator khi chạy")]
    public float runAnimValue = 1.0f;

    [Header("Physics")]
    public float gravity = -9.81f;

    [Header("Giới hạn vùng đi dạo (WalkZone Colliders)")]
    public bool restrictToWalkZones = true;
    public BoxCollider[] walkZoneColliders;
    public float boundaryMargin = 0.35f;

    [Header("References")]
    [Tooltip("Kéo Main Camera vào đây để nhân vật đi theo hướng nhìn của camera (nếu để trống script tự tìm)")]
    public Transform cameraTransform;

    private CharacterController controller;
    private Animator animator;
    private float currentVelocityY;
    private float turnSmoothVelocity;
    private int speedHash;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        FindAndCacheWalkZones();
    }

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        speedHash = Animator.StringToHash("Speed");
        SnapInsideNearestZone();
    }

    void Update()
    {
        HandleMovement();
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
            controller.enabled = false;
            transform.position = clampedPos;
            controller.enabled = true;
        }
    }

    private void HandleMovement()
    {
        // 1. Đọc phím điều khiển (W/A/S/D hoặc phím mũi tên)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        // 2. Kiểm tra trạng thái: Đứng yên (0), Đi bộ, Chạy nhanh
        bool isMoving = direction.magnitude >= 0.1f;
        bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        float targetSpeedValue = 0f;
        float currentMoveSpeed = 0f;
        Vector3 moveDir = Vector3.zero;

        if (isMoving)
        {
            if (isRunning)
            {
                targetSpeedValue = runAnimValue;
                currentMoveSpeed = runSpeed;
            }
            else
            {
                targetSpeedValue = walkAnimValue;
                currentMoveSpeed = walkSpeed;
            }

            // 3. Tính toán góc quay mượt mà theo góc nhìn Camera
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            if (cameraTransform != null)
            {
                targetAngle += cameraTransform.eulerAngles.y;
            }

            float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);

            // Hướng di chuyển phẳng trên mặt sàn
            moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        }

        // 4. Xử lý trọng lực
        if (controller.isGrounded)
        {
            currentVelocityY = -0.5f;
        }
        else
        {
            currentVelocityY += gravity * Time.deltaTime;
        }

        Vector3 horizontalMove = moveDir.normalized * currentMoveSpeed * Time.deltaTime;

        // GIỚI HẠN DI CHUYỂN TRONG BOX COLLIDER (CHẶN ĐỨNG NẾU ĐI RA NGOÀI)
        if (restrictToWalkZones && walkZoneColliders != null && walkZoneColliders.Length > 0 && horizontalMove.sqrMagnitude > 0f)
        {
            Vector3 nextPos = transform.position + horizontalMove;
            if (!IsPointInsideAnyZone(nextPos, boundaryMargin))
            {
                Vector3 nextPosX = transform.position + new Vector3(horizontalMove.x, 0f, 0f);
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
                    horizontalMove = Vector3.zero;
                }
            }
        }

        Vector3 finalVelocity = (horizontalMove / Time.deltaTime) + new Vector3(0, currentVelocityY, 0);
        controller.Move(finalVelocity * Time.deltaTime);

        // 5. Cập nhật tham số Speed vào Animator
        if (animator != null)
        {
            animator.SetFloat(speedHash, targetSpeedValue, animationDampTime, Time.deltaTime);
        }
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
