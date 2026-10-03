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
    [Tooltip("Bật để chặn người chơi không đi ra ngoài các BoxCollider (tường vô hình)")]
    public bool restrictToWalkZones = true;
    public BoxCollider[] walkZoneColliders;
    [Tooltip("Khoảng cách mép biên an toàn (để 0 để đi qua các Box liền kề mượt mà không bị kẹt)")]
    public float boundaryMargin = 0f;

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
        FindAndCacheWalkZones(true);
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

    public void FindAndCacheWalkZones(bool forceRefresh = true)
    {
        if (!forceRefresh && walkZoneColliders != null && walkZoneColliders.Length > 0) return;

        List<BoxCollider> foundZones = new List<BoxCollider>();
        BoxCollider[] allBoxes = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var box in allBoxes)
        {
            if (box == null) continue;
            string objName = box.gameObject.name.ToLower();
            bool isUnderZone = (box.transform.parent != null && box.transform.parent.name.Equals("Zone", System.StringComparison.OrdinalIgnoreCase))
                            || (box.transform.root != null && box.transform.root.name.Equals("Zone", System.StringComparison.OrdinalIgnoreCase));

            bool nameMatches = objName.Contains("walkzone") || objName.Contains("walk_zone") || objName.Contains("playzone") || objName.Contains("ticketzone") || objName.Contains("station") || objName.Contains("zone");

            if (isUnderZone || nameMatches)
            {
                if (objName.Contains("solidfloor") || objName.Contains("platform")) continue;

                box.isTrigger = true;
                if (!foundZones.Contains(box))
                {
                    foundZones.Add(box);
                }
            }
        }

        if (foundZones.Count > 0)
        {
            walkZoneColliders = foundZones.ToArray();
            string names = string.Join(", ", System.Array.ConvertAll(walkZoneColliders, b => b.gameObject.name));
            Debug.Log($"<color=cyan>[PlayerMovement] Đã nạp thành công {walkZoneColliders.Length} WalkZones: {names}</color>");
        }
        else
        {
            Debug.LogError("<color=red>[PlayerMovement] CẢNH BÁO: Không tìm thấy bất kỳ WalkZone nào trong Scene!</color>");
        }
    }

    private void SnapInsideNearestZone()
    {
        if (!restrictToWalkZones || walkZoneColliders == null || walkZoneColliders.Length == 0) return;

        if (!IsPointInsideAnyZone(transform.position, 0f))
        {
            Vector3 clampedPos = GetClampedPositionInsideZones(transform.position);
            controller.enabled = false;
            transform.position = clampedPos;
            controller.enabled = true;
        }
    }

    private float lastLogTime = 0f;

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
                    if (Time.time - lastLogTime > 0.8f)
                    {
                        lastLogTime = Time.time;
                        Debug.LogWarning($"<color=yellow>[SCRIPT BOUNDARY BLOCK] Bị script chặn bước chân tại Pos: {transform.position}, muốn đi tới: {nextPos}. Tổng số Box đang xét: {walkZoneColliders.Length}</color>");
                    }
                    horizontalMove = Vector3.zero;
                }
            }
        }

        Vector3 posBefore = transform.position;
        Vector3 finalVelocity = (horizontalMove / Time.deltaTime) + new Vector3(0, currentVelocityY, 0);
        controller.Move(finalVelocity * Time.deltaTime);

        // Phát hiện nếu lệnh di chuyển đã gửi nhưng nhân vật bị vật cản vật lý chặn cứng
        if (horizontalMove.sqrMagnitude > 0.0001f && Vector3.Distance(new Vector3(posBefore.x, 0, posBefore.z), new Vector3(transform.position.x, 0, transform.position.z)) < 0.0001f)
        {
            if (Time.time - lastLogTime > 0.8f)
            {
                lastLogTime = Time.time;
                Debug.LogError($"<color=red>[PHYSICS COLLISION BLOCK] Lệnh di chuyển hợp lệ nhưng CharacterController bị 1 vật cản vật lý (Collider cứng) trong Scene chặn lại tại: {transform.position}</color>");
            }
        }

        // 5. Cập nhật tham số Speed vào Animator
        if (animator != null)
        {
            animator.SetFloat(speedHash, targetSpeedValue, animationDampTime, Time.deltaTime);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Bỏ qua mặt sàn khi đang đứng trên sàn
        if (hit.normal.y > 0.6f) return;

        if (Time.time - lastLogTime > 0.8f)
        {
            lastLogTime = Time.time;
            Debug.LogError($"<color=orange>[VẬT CẢN VẬT LÝ] Người chơi vừa đâm vào: '{hit.gameObject.name}' (Loại Collider: {hit.collider.GetType().Name}, IsTrigger: {hit.collider.isTrigger}, Layer: {LayerMask.LayerToName(hit.gameObject.layer)}) tại tọa độ: {hit.point}</color>", hit.gameObject);
        }
    }

    public bool IsPointInsideAnyZone(Vector3 worldPoint, float margin = 0f)
    {
        if (walkZoneColliders == null || walkZoneColliders.Length == 0)
        {
            FindAndCacheWalkZones(true);
        }

        if (walkZoneColliders == null || walkZoneColliders.Length == 0) return true;

        for (int i = 0; i < walkZoneColliders.Length; i++)
        {
            BoxCollider box = walkZoneColliders[i];
            if (box != null && box.gameObject.activeInHierarchy)
            {
                if (IsPointInsideBox(worldPoint, box, margin))
                    return true;
            }
        }
        return false;
    }

    public static bool IsPointInsideBox(Vector3 worldPoint, BoxCollider box, float margin = 0f)
    {
        if (box == null) return false;

        // 1. Kiểm tra bằng Local Transform (tự động xử lý chính xác góc xoay và vị trí Box)
        Vector3 local = box.transform.InverseTransformPoint(worldPoint);
        float halfX = box.size.x * 0.5f;
        float halfZ = box.size.z * 0.5f;

        if (margin > 0f)
        {
            halfX = Mathf.Max(0.01f, halfX - margin);
            halfZ = Mathf.Max(0.01f, halfZ - margin);
        }

        bool inX = Mathf.Abs(local.x - box.center.x) <= halfX;
        bool inZ = Mathf.Abs(local.z - box.center.z) <= halfZ;
        // Dung sai chiều cao cực lớn để không bao giờ bị lệch độ cao Y giữa các BoxCollider giao nhau
        bool inY = Mathf.Abs(local.y - box.center.y) <= (box.size.y * 0.5f + 100.0f);

        if (inX && inZ && inY) return true;

        // 2. Fallback bằng PhysX ClosestPoint trên mặt phẳng ngang X-Z
        Vector3 centerWorld = box.transform.TransformPoint(box.center);
        Vector3 testPoint = new Vector3(worldPoint.x, centerWorld.y, worldPoint.z);
        Vector3 closest = box.ClosestPoint(testPoint);
        float distSq = (closest.x - testPoint.x) * (closest.x - testPoint.x) + (closest.z - testPoint.z) * (closest.z - testPoint.z);
        if (distSq <= 0.05f) return true;

        return false;
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
