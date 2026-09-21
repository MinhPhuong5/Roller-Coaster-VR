using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class AnimatorChihiro : MonoBehaviour
{
    [Header("Tốc độ di chuyển")]
    public float moveSpeed = 15f;
    public float sprintSpeed = 30f;
    public float rotationSpeed = 15f;

    [Header("Camera định hướng")]
    public Transform cameraTransform;

    [Header("Nhảy")]
    public string groundTag = "Ground";
    public float groundCheckDistance = 0.5f;
    public float jumpForce = 8f;
    public float jumpAnimationDuration = 0.65f;

    [Header("Animator")]
    public string walkingParameter = "IsWalking";
    public string jumpingParameter = "IsJumping";

    private Rigidbody rb;
    private Animator animator;
    private Collider characterCollider;

    private float inputH;
    private float inputV;
    private bool isSprinting;
    private bool isGrounded;
    private bool jumpRequested;
    private bool isJumping;
    private float jumpTimer;
    private bool controlsLocked;

    private readonly HashSet<Collider> groundContacts = new HashSet<Collider>();

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        characterCollider = GetComponent<Collider>();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Khôi phục vị trí sau khi chơi tàu lượn về
        if (PlayerPrefs.GetInt("HasPlayedCoaster", 0) == 1)
        {
            float savedX = PlayerPrefs.GetFloat("SavedPos_X", transform.position.x);
            float savedY = PlayerPrefs.GetFloat("SavedPos_Y", transform.position.y);
            float savedZ = PlayerPrefs.GetFloat("SavedPos_Z", transform.position.z);
            float savedRotY = PlayerPrefs.GetFloat("SavedRot_Y", transform.eulerAngles.y);

            Vector3 targetPosition = new Vector3(savedX, savedY, savedZ);
            Quaternion targetRotation = Quaternion.Euler(0f, savedRotY, 0f);

            transform.SetPositionAndRotation(targetPosition, targetRotation);
            rb.position = targetPosition;
            rb.rotation = targetRotation;

            PlayerPrefs.SetInt("HasPlayedCoaster", 0);
            PlayerPrefs.Save();
        }

        rb.isKinematic = false;
        rb.useGravity = true;
        // KHÓA CỨNG TRỤC XOAY VẬT LÝ ĐỂ KHÔNG BỊ TỰ ĐỘNG LẬT HAY XOAY TRÒN
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    private void Update()
    {
        if (controlsLocked)
        {
            inputH = 0f;
            inputV = 0f;
            jumpRequested = false;
            return;
        }

        // Nhận 4 phím độc lập
        inputH = Input.GetAxisRaw("Horizontal"); // A (-1) và D (+1)
        inputV = Input.GetAxisRaw("Vertical");   // S (-1) và W (+1)

        isSprinting = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && (inputH != 0f || inputV != 0f);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isJumping)
        {
            jumpRequested = true;
        }

        if (isJumping)
        {
            jumpTimer -= Time.deltaTime;
            if (jumpTimer <= 0f)
            {
                isJumping = false;
                SetJumping(false);
            }
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

        bool groundedNow = CheckGrounded() || groundContacts.Count > 0;
        if (groundedNow && !isGrounded)
        {
            SetJumping(false);
            isJumping = false;
        }
        isGrounded = groundedNow;

        // Tính vector hướng mặt và hướng ngang dựa trên Camera
        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;

        if (cameraTransform != null)
        {
            camForward = cameraTransform.forward;
            camRight = cameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();
        }

        // Vector di chuyển phẳng kết hợp cả Tiến/Lùi (W/S) và Sang Ngang (A/D)
        Vector3 moveDirection = (camForward * inputV + camRight * inputH).normalized;
        bool isMoving = moveDirection.sqrMagnitude > 0.01f;

        float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;

        if (isMoving)
        {
            // Thân người xoay theo hướng di chuyển tổng thể
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

            // Tịnh tiến vị trí bằng Rigidbody
            Vector3 movement = moveDirection * (currentSpeed * Time.fixedDeltaTime);
            rb.MovePosition(rb.position + movement);
        }

        if (jumpRequested && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            isJumping = true;
            jumpTimer = jumpAnimationDuration;
            SetJumping(true);
            jumpRequested = false;
        }

        if (animator != null)
        {
            animator.SetBool(walkingParameter, isMoving);
        }
    }

    private bool CheckGrounded()
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

    private void OnCollisionEnter(Collision collision) => TrackContact(collision);
    private void OnCollisionStay(Collision collision) => TrackContact(collision);
    private void OnCollisionExit(Collision collision) => groundContacts.Remove(collision.collider);

    private void TrackContact(Collision collision)
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

    public void SetControlsLocked(bool locked)
    {
        SetInputLocked(locked);
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = locked;
        }
    }

    public void SetInputLocked(bool locked)
    {
        controlsLocked = locked;
        inputH = 0f;
        inputV = 0f;
        jumpRequested = false;
        isJumping = false;

        if (animator != null)
        {
            animator.SetBool(walkingParameter, false);
            animator.SetBool(jumpingParameter, false);
        }
    }

    public void SavePositionBeforeEnteringRide()
    {
        PlayerPrefs.SetFloat("SavedPos_X", transform.position.x);
        PlayerPrefs.SetFloat("SavedPos_Y", transform.position.y);
        PlayerPrefs.SetFloat("SavedPos_Z", transform.position.z);
        PlayerPrefs.SetFloat("SavedRot_Y", transform.eulerAngles.y);
        PlayerPrefs.Save();
    }
}