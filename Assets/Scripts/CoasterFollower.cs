using UnityEngine;

[DefaultExecutionOrder(-50)]
public class CoasterFollower : MonoBehaviour
{
    [Header("1. Ray tĩnh & Thế giới ngầm")]
    public Transform staticTrackRC;
    public Transform ghostShow;

    [Header("2. Tàu thật")]
    public Transform realCart;

    [Header("3. Hướng")]
    public bool reverseDirection = false;

    [Header("4. Căn chỉnh vị trí bám ray (Tâm ray)")]
    public float verticalOffset = -0.8f;
    public float horizontalOffset = 0f;
    public float forwardOffset = 0f;

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

        // 1. TỌA ĐỘ VÀ GÓC RAY CHUẨN TỪ BLUFFTITLER (KHÔNG TỰ TÍNH LẠI HƯỚNG)
        Vector3 invPos = -(Quaternion.Inverse(ghostShow.localRotation) * ghostShow.localPosition);
        Vector3 rawBasePos = staticTrackRC.TransformPoint(invPos);

        Quaternion rawTrackRot = staticTrackRC.rotation * Quaternion.Inverse(ghostShow.localRotation);
        if (reverseDirection)
        {
            rawTrackRot = rawTrackRot * Quaternion.Euler(0f, 180f, 0f);
        }

        // 2. OFFSET BÁM THEO ĐÚNG HỆ TRỤC CỦA THANH RAY (KHÔNG BAO GIỜ BỊ VĂNG KHỎI RAY)
        Vector3 targetPos = rawBasePos
                          + (rawTrackRot * Vector3.up * verticalOffset)
                          + (rawTrackRot * Vector3.right * horizontalOffset)
                          + (rawTrackRot * Vector3.forward * forwardOffset);

        bool isRiding = (rideController != null && rideController.currentState == RideController.RideState.Riding);

        if (!isInitialized || !isRiding)
        {
            smoothedPos = targetPos;
            smoothedRot = rawTrackRot;
            realCart.position = targetPos;
            realCart.rotation = rawTrackRot;
            isInitialized = isRiding;
        }
        else
        {
            // 3. KHỬ RUNG ĐỈNH DỐC BẰNG BỘ LỌC TẦN SỐ CAO
            // Bám sát vị trí và góc ray gốc nhưng làm phẳng các vi chấn micro-step
            smoothedPos = Vector3.Lerp(smoothedPos, targetPos, Time.deltaTime * 35f);
            smoothedRot = Quaternion.Slerp(smoothedRot, rawTrackRot, Time.deltaTime * 35f);

            realCart.position = smoothedPos;
            realCart.rotation = smoothedRot;
        }

        realCart.localScale = initialScale;
    }
}