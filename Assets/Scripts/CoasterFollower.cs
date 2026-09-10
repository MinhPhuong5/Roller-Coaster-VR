using UnityEngine;

public class CoasterFollower : MonoBehaviour
{
    [Header("1. Ray tĩnh & Thế giới ngầm")]
    public Transform staticTrackRC;
    public Transform ghostShow;

    [Header("2. Tàu thật")]
    public Transform realCart;

    [Header("3. Hướng")]
    public bool reverseDirection = true;

    [Header("4. Căn chỉnh vị trí bám ray (Chỉnh khi STOP PLAY)")]
    [Tooltip("Số âm để hạ tàu xuống chạm ray, số dương để nâng lên")]
    public float verticalOffset = -0.8f;
    public float horizontalOffset = 0f;
    public float forwardOffset = 0f;

    private Vector3 initialScale;

    void Awake()
    {
        if (realCart != null)
        {
            initialScale = realCart.localScale;
        }
    }

    void LateUpdate()
    {
        if (staticTrackRC == null || ghostShow == null || realCart == null) return;

        // 1. Góc xoay
        Quaternion invRot = Quaternion.Inverse(ghostShow.localRotation);
        if (reverseDirection)
        {
            invRot = invRot * Quaternion.Euler(0f, 180f, 0f);
        }
        realCart.rotation = staticTrackRC.rotation * invRot;

        // 2. Toạ độ bám ray
        Vector3 invPos = -(Quaternion.Inverse(ghostShow.localRotation) * ghostShow.localPosition);
        Vector3 basePos = staticTrackRC.TransformPoint(invPos);

        // Áp dụng hạ độ cao theo trục Up của chính con tàu
        Vector3 offset = (realCart.up * verticalOffset)
                       + (realCart.right * horizontalOffset)
                       + (realCart.forward * forwardOffset);

        realCart.position = basePos + offset;

        // 3. Giữ nguyên scale đã đặt ngoài Inspector
        realCart.localScale = initialScale;
    }
}