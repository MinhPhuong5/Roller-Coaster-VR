using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class AnimatorChihiro : MonoBehaviour
{
    [Header("Di chuyển")]
    public float moveSpeed = 5f;
    [Tooltip("Tốc độ tối đa khi giữ Shift và di chuyển tiến")]
    public float sprintSpeed = 100f;
    [Tooltip("Tốc độ tăng khi chạy nước rút, tính theo đơn vị mỗi giây")]
    public float sprintAcceleration = 80f;
    [Tooltip("Tốc độ rẽ trái/phải, tính theo độ mỗi giây")]
    public float turnSpeed = 540f;
    [Tooltip("Bỏ qua nhiễu nhỏ từ joystick để nhân vật không tự xoay")]
    [Range(0f, 1f)] public float turnDeadZone = 0.2f;

    [Header("Nhảy")]
    public string groundTag = "Ground";
    [Tooltip("Khoảng dò thêm dưới chân để nhận diện mặt đất")]
    public float groundCheckDistance = 0.2f;
    [Tooltip("Thời lượng bật animation IsJumping")]
    public float jumpAnimationDuration = 0.65f;
    [Tooltip("Vận tốc bật lên khi nhảy")]
    public float jumpVelocity = 6f;
    [Tooltip("Tốc độ lao về trước khi nhấn Space cùng W")]
    public float forwardJumpSpeed = 4f;

    [Header("Animator")]
    [Tooltip("Phải trùng hoàn toàn với tên Bool trong Animator")]
    public string walkingParameter = "IsWalking";
    [Tooltip("Bool điều khiển state Jumping trong Animator")]
    public string jumpingParameter = "IsJumping";

    private Rigidbody rb;
    private Animator animator;
    [SerializeField] private Animator characterAnimator;
    public Animator CharacterAnimator => characterAnimator != null ? characterAnimator : animator;
    private Collider characterCollider;
    private bool isGrounded;
    private bool jumpRequested;
    private float moveInput;
    private float turnInput;
    private bool isSprinting;
    private float currentMoveSpeed;
    private bool isJumping;
    private bool isForwardJump;
    private float jumpTimeRemaining;
    private bool controlsLocked;
    private readonly HashSet<Collider> groundContacts = new HashSet<Collider>();

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = characterAnimator != null ? characterAnimator : GetComponent<Animator>();
        characterCollider = GetComponent<Collider>();
        currentMoveSpeed = moveSpeed;

        // Nhân vật phải là Rigidbody động thì MovePosition và AddForce mới hoạt động.
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints &= ~(RigidbodyConstraints.FreezePositionX |
                            RigidbodyConstraints.FreezePositionY |
                            RigidbodyConstraints.FreezePositionZ);
        rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        Camera playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera != null && playerCamera.GetComponent<ShiftOrbitCamera>() == null)
        {
            playerCamera.gameObject.AddComponent<ShiftOrbitCamera>();
        }
    }

    private void Update()
    {
        if (controlsLocked)
        {
            moveInput = 0f;
            turnInput = 0f;
            jumpRequested = false;
            return;
        }

        // Tank controls: W/S tiến-lùi, A/D chỉ rẽ.
        moveInput = Input.GetAxisRaw("Vertical");
        turnInput = Input.GetAxisRaw("Horizontal");
        isSprinting = moveInput > 0.001f &&
                      (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        if (Mathf.Abs(turnInput) < turnDeadZone)
        {
            turnInput = 0f;
        }

        // Luôn ghi nhận phím tại đây; trạng thái chạm đất được kiểm tra ở FixedUpdate.
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isJumping)
        {
            jumpRequested = true;
        }
    }

    private void FixedUpdate()
    {
        if (controlsLocked)
        {
            if (animator != null)
            {
                animator.SetBool(walkingParameter, false);
                animator.SetBool(jumpingParameter, false);
            }
            return;
        }

        bool groundedNow = IsStandingOnGround() || groundContacts.Count > 0;
        if (groundedNow && !isGrounded)
        {
            SetJumping(false);
        }
        isGrounded = groundedNow;

        // A/D quay quanh trục Y. Khi giữ W/S cùng lúc, quỹ đạo sẽ là đường cong.
        float turnDegrees = turnInput * turnSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turnDegrees, 0f));

        if (isJumping)
        {
            if (isForwardJump)
            {
                rb.MovePosition(rb.position + rb.rotation * Vector3.forward * forwardJumpSpeed * Time.fixedDeltaTime);
            }

            jumpTimeRemaining -= Time.fixedDeltaTime;
            if (jumpTimeRemaining <= 0f)
            {
                isJumping = false;
                isForwardJump = false;
                SetJumping(false);
            }
        }

        bool isWalking = !isJumping && Mathf.Abs(moveInput) > 0.001f;
        currentMoveSpeed = isSprinting
            ? Mathf.MoveTowards(currentMoveSpeed, sprintSpeed, sprintAcceleration * Time.fixedDeltaTime)
            : moveSpeed;
        if (isWalking)
        {
            Vector3 direction = rb.rotation * Vector3.forward;
            rb.MovePosition(rb.position + direction * (moveInput * currentMoveSpeed * Time.fixedDeltaTime));
        }

        if (animator != null)
        {
            animator.SetBool(walkingParameter, isWalking);
        }

        if (jumpRequested && isGrounded)
        {
            isJumping = true;
            isForwardJump = moveInput > 0.001f;
            jumpTimeRemaining = jumpAnimationDuration;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpVelocity, rb.linearVelocity.z);
            SetJumping(true);
        }

        jumpRequested = false;
    }

    private bool IsStandingOnGround()
    {
        Bounds bounds = characterCollider.bounds;
        Vector3 origin = bounds.center;
        float distance = bounds.extents.y + groundCheckDistance;

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != characterCollider && hit.collider.CompareTag(groundTag))
            {
                return true;
            }
        }

        return false;
    }

    private void OnCollisionEnter(Collision collision) => TrackGroundContact(collision);

    private void OnCollisionStay(Collision collision) => TrackGroundContact(collision);

    private void OnCollisionExit(Collision collision)
    {
        groundContacts.Remove(collision.collider);
    }

    private void TrackGroundContact(Collision collision)
    {
        if (!collision.collider.CompareTag(groundTag)) return;

        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                groundContacts.Add(collision.collider);
                return;
            }
        }
    }

    private void SetJumping(bool value)
    {
        if (animator != null)
        {
            animator.SetBool(jumpingParameter, value);
        }
    }

    /// <summary>Khóa di chuyển khi nhân vật đang dùng một tương tác tại chỗ, ví dụ ngồi ghế.</summary>
    public void SetControlsLocked(bool locked)
    {
        SetInputLocked(locked);

        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = locked;
    }

    /// <summary>Khóa phím điều khiển cho UI, nhưng vẫn để physics hoạt động bình thường.</summary>
    public void SetInputLocked(bool locked)
    {
        controlsLocked = locked;
        moveInput = 0f;
        turnInput = 0f;
        jumpRequested = false;
        isJumping = false;
        isForwardJump = false;

        if (animator != null)
        {
            animator.SetBool(walkingParameter, false);
            animator.SetBool(jumpingParameter, false);
        }
    }
}

