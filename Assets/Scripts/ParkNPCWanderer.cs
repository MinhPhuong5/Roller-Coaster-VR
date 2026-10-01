using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Điều khiển NPC đi dạo công viên chuẩn Scale 1:1.
/// - Giới hạn chặt chẽ 100% bên trong các BoxCollider (WalkZone).
/// - Chống đi chéo cắt góc: Kiểm tra toàn bộ đường đi (segment sampling) để đảm bảo không đi xiên ra ngoài bãi cỏ/vật cản.
/// - Chống vượt ranh giới: Mỗi frame đều kiểm tra bước chân tiếp theo, nếu chạm mép BoxCollider sẽ tự động dừng lại và quay đầu.
/// - Bám mặt đất & Tự động tránh vật cản.
/// </summary>
public class ParkNPCWanderer : MonoBehaviour
{
    public enum BoundaryMode
    {
        BoxZoneCollider,     // Giới hạn chặt chẽ bên trong các BoxCollider vùng đường dạo (Mặc định)
        RadiusAroundCenter,  // Giới hạn trong bán kính quanh tâm
        Waypoints            // Đi tuần tra lần lượt qua các điểm mốc
    }

    [Header("1. GIỚI HẠN KHÔNG GIAN (WALK ZONES)")]
    public BoundaryMode boundaryMode = BoundaryMode.BoxZoneCollider;

    [Tooltip("Danh sách các BoxCollider vùng đường dạo (Nếu để trống script tự động tìm toàn bộ WalkZone trong cảnh)")]
    public BoxCollider[] walkZoneColliders;

    [Tooltip("Khoảng cách an toàn tính từ mép BoxCollider (mét) để NPC không cọ người vào tường")]
    public float boundaryMargin = 0.35f;

    [Tooltip("Tâm vùng giới hạn (nếu dùng chế độ RadiusAroundCenter)")]
    public Transform centerPoint;

    [Tooltip("Bán kính tối đa được phép đi dạo (mét) nếu dùng chế độ Radius")]
    public float maxRadius = 8.0f;

    [Tooltip("Kéo các mốc điểm nếu dùng chế độ Waypoints")]
    public Transform[] waypoints;

    [Header("2. CHỐNG ĐI XUYÊN VẬT THỂ (OBSTACLES)")]
    public bool enableObstacleAvoidance = true;
    [Tooltip("Khoảng cách dò vật cản phía trước (mét)")]
    public float obstacleCheckDistance = 0.8f;
    [Tooltip("Độ cao của tia dò vật cản tính từ chân (ngang bụng/ngực)")]
    public float sensorHeight = 0.8f;
    [Tooltip("Layer của các vật thể cản (tường, nhà, hàng rào, v.v.). Mặc định quét tất cả")]
    public LayerMask obstacleLayer = ~0;

    [Header("3. TỐC ĐỘ & THỜI GIAN NGHỈ")]
    [Tooltip("Tốc độ bước đi người thật (1.2 đến 1.5 m/s)")]
    public float walkSpeed = 1.35f;
    public float rotationSpeed = 5.0f;
    public float minWaitTime = 2.0f;
    public float maxWaitTime = 5.0f;

    [Header("4. BÁM MẶT ĐẤT")]
    public bool snapToGround = true;
    public LayerMask groundLayer = ~0;

    private Animator animator;
    private CharacterController characterController;
    private Vector3 originCenterPos;
    private Vector3 targetDestination;
    private int currentWaypointIndex = 0;
    private bool isWaiting = false;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int IsWalkingParam = Animator.StringToHash("IsWalking");

    void Awake()
    {
        MonoBehaviour cp = GetComponent("CityPeople") as MonoBehaviour;
        if (cp != null) cp.enabled = false;

        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();

        if (animator != null)
        {
            if (animator.runtimeAnimatorController == null)
            {
#if UNITY_EDITOR
                animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3D Model/NPC/NPC_Master_Animator.controller");
#endif
            }
            animator.applyRootMotion = false;
        }

        originCenterPos = centerPoint != null ? centerPoint.position : transform.position;

        // Tự động tìm tất cả các WalkZone trong Scene nếu chưa được gán thủ công
        FindAndCacheWalkZones();
    }

    void Start()
    {
        // Đảm bảo NPC ban đầu nằm đúng trong BoxCollider
        SnapInsideNearestZone();

        PickNextDestination();
    }

    void Update()
    {
        if (isWaiting) return;

        MoveTowardsTarget();
    }

    /// <summary>
    /// Tìm tất cả các BoxCollider thuộc các WalkZone trong Scene
    /// </summary>
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

    /// <summary>
    /// Nếu NPC khởi đầu nằm ngoài vùng WalkZone, kéo NPC vào điểm hợp lệ gần nhất
    /// </summary>
    private void SnapInsideNearestZone()
    {
        if (boundaryMode != BoundaryMode.BoxZoneCollider || walkZoneColliders == null || walkZoneColliders.Length == 0) return;

        if (!IsPointInsideAnyZone(transform.position, 0.05f))
        {
            Vector3 clampedPos = GetClampedPositionInsideZones(transform.position);
            transform.position = clampedPos;
            originCenterPos = clampedPos;
        }
    }

    private void MoveTowardsTarget()
    {
        Vector3 targetFlat = new Vector3(targetDestination.x, transform.position.y, targetDestination.z);
        Vector3 moveDir = (targetFlat - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, targetFlat);

        // 1. Kiểm tra vật cản phía trước
        if (enableObstacleAvoidance && IsObstacleAhead(moveDir))
        {
            StartCoroutine(WaitAtPointRoutine());
            return;
        }

        // 2. Kiểm tra nếu đã đến gần điểm đích
        if (distance <= 0.45f)
        {
            StartCoroutine(WaitAtPointRoutine());
            return;
        }

        // 3. Xoay người mượt mà theo hướng đi
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }

        // 4. KIỂM TRA BƯỚC CHÂN TIẾP THEO (CHỐNG VƯỢT BOX COLLIDER TRIỆT ĐỂ)
        Vector3 velocity = transform.forward * walkSpeed;
        Vector3 nextStepPos = transform.position + velocity * Time.deltaTime;

        if (boundaryMode == BoundaryMode.BoxZoneCollider && walkZoneColliders != null && walkZoneColliders.Length > 0)
        {
            if (!IsPointInsideAnyZone(nextStepPos, boundaryMargin))
            {
                // Bước tiếp theo sẽ vượt ranh giới -> Dừng lại ngay lập tức và đổi hướng khác bên trong BoxCollider
                StartCoroutine(WaitAtPointRoutine());
                return;
            }
        }

        // 5. Áp dụng di chuyển
        if (characterController != null && characterController.enabled)
        {
            characterController.Move(velocity * Time.deltaTime);
        }
        else
        {
            transform.position += velocity * Time.deltaTime;
        }

        // 6. Bám sát mặt đất nhẹ nhàng
        if (snapToGround)
        {
            Vector3 rayStart = transform.position + Vector3.up * 0.8f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 2.0f, groundLayer, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject != gameObject && !hit.collider.transform.IsChildOf(transform))
                {
                    transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
                }
            }
        }

        UpdateAnimator(1.0f, true);
    }

    private bool IsObstacleAhead(Vector3 direction)
    {
        Vector3 sensorPos = transform.position + Vector3.up * sensorHeight;
        float radius = 0.35f;

        if (Physics.SphereCast(sensorPos, radius, direction, out RaycastHit hit, obstacleCheckDistance, obstacleLayer, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                return false;

            if (hit.collider.isTrigger)
                return false;

            return true;
        }
        return false;
    }

    private IEnumerator WaitAtPointRoutine()
    {
        isWaiting = true;
        UpdateAnimator(0.0f, false);

        float waitTime = Random.Range(minWaitTime, maxWaitTime);
        yield return new WaitForSeconds(waitTime);

        PickNextDestination();
        isWaiting = false;
    }

    /// <summary>
    /// Chọn điểm đến tiếp theo.
    /// Đảm bảo đường đi thẳng tới điểm đích nằm 100% bên trong lòng BoxCollider, không đi chéo cắt góc.
    /// </summary>
    private void PickNextDestination()
    {
        Vector3 currentCenter = centerPoint != null ? centerPoint.position : originCenterPos;

        switch (boundaryMode)
        {
            case BoundaryMode.BoxZoneCollider:
                if (walkZoneColliders != null && walkZoneColliders.Length > 0)
                {
                    targetDestination = PickValidDestinationInsideWalkZones();
                }
                else
                {
                    PickRandomRadiusPoint(currentCenter);
                }
                break;

            case BoundaryMode.Waypoints:
                if (waypoints != null && waypoints.Length > 0)
                {
                    targetDestination = waypoints[currentWaypointIndex].position;
                    currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                }
                else
                {
                    PickRandomRadiusPoint(currentCenter);
                }
                break;

            case BoundaryMode.RadiusAroundCenter:
            default:
                PickRandomRadiusPoint(currentCenter);
                break;
        }
    }

    /// <summary>
    /// Chọn điểm đến đảm bảo không đi chéo ra ngoài BoxCollider
    /// </summary>
    private Vector3 PickValidDestinationInsideWalkZones()
    {
        BoxCollider currentZone = GetZoneContainingPoint(transform.position);
        if (currentZone == null)
        {
            currentZone = walkZoneColliders[Random.Range(0, walkZoneColliders.Length)];
        }

        Vector3 startPos = transform.position;

        // Thử tối đa 12 lần để tìm một điểm đến có đường đi thẳng 100% nằm trong lòng BoxCollider
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 candidate;

            // Chiến thuật 1: Đi dọc theo chiều dài hành lang của BoxCollider hiện tại (Ưu tiên đi thẳng, không đi chéo)
            if (attempt < 6 && currentZone != null)
            {
                candidate = GetCorridorAlignedPoint(currentZone, startPos);
            }
            // Chiến thuật 2: Chọn ngẫu nhiên trong vùng BoxCollider hiện tại
            else if (currentZone != null)
            {
                candidate = GetRandomPointInBox(currentZone, boundaryMargin);
            }
            // Chiến thuật 3: Chọn ngẫu nhiên trong bất kỳ BoxCollider nào
            else
            {
                BoxCollider randomZone = walkZoneColliders[Random.Range(0, walkZoneColliders.Length)];
                candidate = GetRandomPointInBox(randomZone, boundaryMargin);
            }

            candidate.y = transform.position.y;

            // KIỂM TRA ĐƯỜNG ĐI: Chia đoạn thẳng từ startPos -> candidate thành nhiều điểm mẫu
            // Nếu TẤT CẢ các điểm đều nằm trong BoxCollider (không bị lọt ra bãi cỏ/góc chéo) thì chấp nhận!
            if (IsPathInsideWalkZones(startPos, candidate, 10, boundaryMargin))
            {
                return candidate;
            }
        }

        // Fallback: Di chuyển khoảng ngắn dọc theo hướng mặt của NPC nếu phía trước còn an toàn
        Vector3 fallbackTarget = transform.position + transform.forward * Random.Range(2.0f, 5.0f);
        if (IsPointInsideAnyZone(fallbackTarget, boundaryMargin))
        {
            return fallbackTarget;
        }

        // Nếu chạm mép, quay ngược lại vào tâm của Zone
        if (currentZone != null)
        {
            Vector3 centerWorld = currentZone.transform.TransformPoint(currentZone.center);
            centerWorld.y = transform.position.y;
            return centerWorld;
        }

        return transform.position;
    }

    /// <summary>
    /// Lấy điểm nằm dọc theo trục dài của hành lang BoxCollider (chống đi chéo góc)
    /// </summary>
    private Vector3 GetCorridorAlignedPoint(BoxCollider box, Vector3 currentWorldPos)
    {
        Vector3 localPos = box.transform.InverseTransformPoint(currentWorldPos);
        Vector3 half = (box.size * 0.5f) - new Vector3(boundaryMargin, 0, boundaryMargin);
        if (half.x < 0.2f) half.x = box.size.x * 0.5f;
        if (half.z < 0.2f) half.z = box.size.z * 0.5f;

        // Xác định trục dài của hành lang (trục có kích thước lớn hơn)
        bool isZLonger = box.size.z >= box.size.x;

        float targetLocalX = localPos.x;
        float targetLocalZ = localPos.z;

        if (isZLonger)
        {
            // Đi dọc theo trục Z của hành lang
            float stepZ = Random.Range(-half.z, half.z);
            targetLocalZ = Mathf.Clamp(stepZ + box.center.z, -half.z + box.center.z, half.z + box.center.z);
            // Giữ nguyên X hoặc hơi dịch nhẹ trong lòng hành lang
            targetLocalX = Mathf.Clamp(localPos.x + Random.Range(-0.5f, 0.5f), -half.x + box.center.x, half.x + box.center.x);
        }
        else
        {
            // Đi dọc theo trục X của hành lang
            float stepX = Random.Range(-half.x, half.x);
            targetLocalX = Mathf.Clamp(stepX + box.center.x, -half.x + box.center.x, half.x + box.center.x);
            targetLocalZ = Mathf.Clamp(localPos.z + Random.Range(-0.5f, 0.5f), -half.z + box.center.z, half.z + box.center.z);
        }

        Vector3 localTarget = new Vector3(targetLocalX, box.center.y, targetLocalZ);
        return box.transform.TransformPoint(localTarget);
    }

    /// <summary>
    /// Lấy ngẫu nhiên 1 điểm trong lòng BoxCollider (đã tính xoay và scale theo Local Space)
    /// </summary>
    private Vector3 GetRandomPointInBox(BoxCollider box, float margin)
    {
        Vector3 half = (box.size * 0.5f) - new Vector3(margin, 0, margin);
        if (half.x < 0.2f) half.x = box.size.x * 0.5f;
        if (half.z < 0.2f) half.z = box.size.z * 0.5f;

        float rx = Random.Range(-half.x, half.x) + box.center.x;
        float rz = Random.Range(-half.z, half.z) + box.center.z;
        Vector3 localPoint = new Vector3(rx, box.center.y, rz);
        return box.transform.TransformPoint(localPoint);
    }

    /// <summary>
    /// Kiểm tra xem toàn bộ đoạn thẳng từ Start đến End có nằm hoàn toàn trong lòng các BoxCollider hay không.
    /// Nếu có bất kỳ điểm nào rơi ra ngoài bãi cỏ/góc chéo -> Trả về false.
    /// </summary>
    public bool IsPathInsideWalkZones(Vector3 start, Vector3 end, int sampleCount = 10, float margin = 0.3f)
    {
        if (walkZoneColliders == null || walkZoneColliders.Length == 0) return true;

        for (int i = 0; i <= sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            Vector3 samplePoint = Vector3.Lerp(start, end, t);
            if (!IsPointInsideAnyZone(samplePoint, margin))
            {
                return false; // Bị lọt ra ngoài ranh giới!
            }
        }
        return true;
    }

    /// <summary>
    /// Kiểm tra 1 điểm World Position có nằm trong bất kỳ BoxCollider WalkZone nào không
    /// </summary>
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

    /// <summary>
    /// Kiểm tra điểm nằm trong 1 BoxCollider cụ thể (chính xác theo góc xoay của Box)
    /// </summary>
    public static bool IsPointInsideBox(Vector3 worldPoint, BoxCollider box, float margin = 0.2f)
    {
        if (box == null) return false;

        Vector3 local = box.transform.InverseTransformPoint(worldPoint);
        Vector3 half = (box.size * 0.5f) - new Vector3(margin, 0, margin);
        if (half.x < 0.1f) half.x = box.size.x * 0.5f;
        if (half.z < 0.1f) half.z = box.size.z * 0.5f;

        bool inX = Mathf.Abs(local.x - box.center.x) <= half.x;
        bool inZ = Mathf.Abs(local.z - box.center.z) <= half.z;
        // Dung sai chiều cao Y để vượt qua độ dốc địa hình
        bool inY = Mathf.Abs(local.y - box.center.y) <= (box.size.y * 0.5f + 3.0f);

        return inX && inZ && inY;
    }

    private BoxCollider GetZoneContainingPoint(Vector3 worldPoint)
    {
        if (walkZoneColliders == null) return null;
        for (int i = 0; i < walkZoneColliders.Length; i++)
        {
            if (walkZoneColliders[i] != null && IsPointInsideBox(worldPoint, walkZoneColliders[i], 0.05f))
            {
                return walkZoneColliders[i];
            }
        }
        return null;
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

    private void PickRandomRadiusPoint(Vector3 center)
    {
        Vector2 randomCircle = Random.insideUnitCircle * maxRadius;
        targetDestination = center + new Vector3(randomCircle.x, 0f, randomCircle.y);
    }

    private void UpdateAnimator(float speedValue, bool isWalking)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetFloat(SpeedParam, speedValue);
        animator.SetBool(IsWalkingParam, isWalking);
    }

    public void StopWandering()
    {
        StopAllCoroutines();
        enabled = false;
        UpdateAnimator(0f, false);
    }

    public void ResumeWandering()
    {
        enabled = true;
        isWaiting = false;
        PickNextDestination();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 center = centerPoint != null ? centerPoint.position : (Application.isPlaying ? originCenterPos : transform.position);

        if (boundaryMode == BoundaryMode.RadiusAroundCenter)
        {
            Gizmos.DrawWireSphere(center, maxRadius);
        }

        // Vẽ đường đi tới đích
        if (Application.isPlaying && isWaiting == false)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, targetDestination);
            Gizmos.DrawWireSphere(targetDestination, 0.3f);
        }

        // Vẽ tia cảm biến vật cản phía trước
        Gizmos.color = Color.red;
        Vector3 sensorPos = transform.position + Vector3.up * sensorHeight;
        Gizmos.DrawLine(sensorPos, sensorPos + transform.forward * obstacleCheckDistance);
        Gizmos.DrawWireSphere(sensorPos + transform.forward * obstacleCheckDistance, 0.35f);
    }
}
