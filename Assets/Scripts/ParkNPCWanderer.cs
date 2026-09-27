using System.Collections;
using UnityEngine;

/// <summary>
/// Điều khiển NPC đi dạo công viên chuẩn Scale 1:1.
/// - Giới hạn không gian: Chỉ đi trong vùng quy định (theo Bán kính hoặc BoxCollider vùng đường đi).
/// - Chống đi xuyên vật thể: Dò vật cản phía trước (tường, nhà, hàng rào, ghế đá) để tự động dừng và quay đầu.
/// - Bám mặt đất: Không bay lên trời, không tụt dốc.
/// </summary>
public class ParkNPCWanderer : MonoBehaviour
{
    public enum BoundaryMode
    {
        RadiusAroundCenter,  // Giới hạn trong bán kính quanh tâm
        BoxZoneCollider,     // Giới hạn chặt chẽ bên trong 1 BoxCollider vùng đường đi
        Waypoints            // Đi tuần tra lần lượt qua các điểm mốc
    }

    [Header("1. GIỚI HẠN KHÔNG GIAN")]
    public BoundaryMode boundaryMode = BoundaryMode.RadiusAroundCenter;

    [Tooltip("Tâm vùng giới hạn (nếu để trống script tự lấy vị trí xuất phát làm tâm)")]
    public Transform centerPoint;

    [Tooltip("Bán kính tối đa được phép đi dạo (mét) nếu dùng chế độ Radius")]
    public float maxRadius = 8.0f;

    [Tooltip("Kéo một GameObject có BoxCollider (ví dụ vùng đường dạo) vào đây nếu dùng chế độ BoxZoneCollider")]
    public Collider walkZoneCollider;

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
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();

        // Tắt Root Motion để code trực tiếp đẩy NPC di chuyển
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        originCenterPos = centerPoint != null ? centerPoint.position : transform.position;
    }

    void Start()
    {
        PickNextDestination();
    }

    void Update()
    {
        if (isWaiting) return;

        MoveTowardsTarget();
    }

    private void MoveTowardsTarget()
    {
        Vector3 targetFlat = new Vector3(targetDestination.x, transform.position.y, targetDestination.z);
        Vector3 moveDir = (targetFlat - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, targetFlat);

        // 1. Kiểm tra vật cản phía trước (Tường, Nhà, Cây, Ghế đá)
        if (enableObstacleAvoidance && IsObstacleAhead(moveDir))
        {
            // Gặp vật cản -> Dừng lại và đổi hướng sang điểm khác ngay
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

        // 4. Di chuyển về phía trước (Ưu tiên dùng CharacterController nếu có để trượt vật lý mượt)
        Vector3 velocity = transform.forward * walkSpeed;
        if (characterController != null && characterController.enabled)
        {
            characterController.Move(velocity * Time.deltaTime);
        }
        else
        {
            transform.position += velocity * Time.deltaTime;
        }

        // 5. Bám sát mặt đất nhẹ nhàng
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

    /// <summary>
    /// Bắn tia hình cầu (SphereCast) ngang ngực phía trước để phát hiện tường/vật cản
    /// </summary>
    private bool IsObstacleAhead(Vector3 direction)
    {
        Vector3 sensorPos = transform.position + Vector3.up * sensorHeight;
        float radius = 0.35f;

        if (Physics.SphereCast(sensorPos, radius, direction, out RaycastHit hit, obstacleCheckDistance, obstacleLayer, QueryTriggerInteraction.Ignore))
        {
            // Bỏ qua nếu vật cản chính là bản thân mình
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                return false;

            // Bỏ qua vùng trigger
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

    private void PickNextDestination()
    {
        Vector3 currentCenter = centerPoint != null ? centerPoint.position : originCenterPos;

        switch (boundaryMode)
        {
            case BoundaryMode.BoxZoneCollider:
                if (walkZoneCollider != null)
                {
                    // Lấy ngẫu nhiên 1 điểm nằm chặt chẽ bên trong Bounds của BoxCollider
                    Bounds bounds = walkZoneCollider.bounds;
                    float randomX = Random.Range(bounds.min.x, bounds.max.x);
                    float randomZ = Random.Range(bounds.min.z, bounds.max.z);
                    targetDestination = new Vector3(randomX, transform.position.y, randomZ);
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

    private void PickRandomRadiusPoint(Vector3 center)
    {
        Vector2 randomCircle = Random.insideUnitCircle * maxRadius;
        targetDestination = center + new Vector3(randomCircle.x, 0f, randomCircle.y);
    }

    private void UpdateAnimator(float speedValue, bool isWalking)
    {
        if (animator == null) return;
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

        // Vẽ tia cảm biến vật cản phía trước
        Gizmos.color = Color.red;
        Vector3 sensorPos = transform.position + Vector3.up * sensorHeight;
        Gizmos.DrawLine(sensorPos, sensorPos + transform.forward * obstacleCheckDistance);
        Gizmos.DrawWireSphere(sensorPos + transform.forward * obstacleCheckDistance, 0.35f);
    }
}
