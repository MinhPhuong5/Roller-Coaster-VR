using UnityEngine;

[DefaultExecutionOrder(-50)]
public class CoasterFollower : MonoBehaviour
{
    [Header("1. Ray tĩnh & Thế giới ngầm")]
    public Transform staticTrackRC;
    public Transform ghostShow;

    [Header("2. Tàu thật")]
    public Transform realCart;

    [Header("3. Thiết lập Reverse & Bám Ray")]
    public bool reverseDirection = true;

    [Tooltip("Khoảng cách thời gian dò đầu tàu (Khuyên dùng: 0.04 đến 0.08)")]
    public float leadTime = 0.06f;

    public float rotationDamping = 35f;
    public float positionDamping = 45f;

    [Header("4. Căn chỉnh vị trí bám ray")]
    public float verticalOffset = -0.8f;
    public float horizontalOffset = 0f;

    [Header("5. Phối hợp với trạng thái chạy")]
    public RideController rideController;

    private Vector3 initialScale;
    private Vector3 smoothedPos;
    private Quaternion smoothedRot;
    private bool isInitialized = false;

    void Awake()
    {
        if (realCart != null)
            initialScale = realCart.localScale;
    }

    void OnEnable()
    {
        isInitialized = false;
    }

    void LateUpdate()
    {
        if (staticTrackRC == null || ghostShow == null || realCart == null) return;

        // 1. TỌA ĐỘ VÀ GÓC RAY CHUẨN TẠI VỊ TRÍ HIỆN TẠI
        Vector3 invPos = -(Quaternion.Inverse(ghostShow.localRotation) * ghostShow.localPosition);
        Vector3 trackPos = staticTrackRC.TransformPoint(invPos);

        Quaternion rawTrackRot = staticTrackRC.rotation * Quaternion.Inverse(ghostShow.localRotation);
        if (reverseDirection)
        {
            rawTrackRot = rawTrackRot * Quaternion.Euler(0f, 180f, 0f);
        }

        // 2. TÍNH VỊ TRÍ ĐÍCH TỰA TRÊN MẶT RAY (DÙNG CHUẨN VERTICAL OFFSET)
        Vector3 targetPos = trackPos
                          + (rawTrackRot * Vector3.up * verticalOffset)
                          + (rawTrackRot * Vector3.right * horizontalOffset);

        bool isRiding = (rideController != null && rideController.currentState == RideController.RideState.Riding);

        if (!isInitialized || !isRiding)
        {
            smoothedPos = targetPos;
            smoothedRot = rawTrackRot;
            realCart.position = targetPos;
            realCart.rotation = rawTrackRot;
            isInitialized = isRiding;
            return;
        }

        // 3. KHÓA GÓC QUAY VÀ VỊ TRÍ THEO RAY
        smoothedPos = Vector3.Lerp(smoothedPos, targetPos, Time.deltaTime * positionDamping);
        smoothedRot = Quaternion.Slerp(smoothedRot, rawTrackRot, Time.deltaTime * rotationDamping);

        realCart.position = smoothedPos;
        realCart.rotation = smoothedRot;
        realCart.localScale = initialScale;
    }
}