using System.Collections;
using UnityEngine;

public class StationGateController : MonoBehaviour
{
    [Header("Khớp Xoay")]
    public Transform barPivot; // Kéo Bar_Pivot vào đây

    [Header("Góc Xoay (Trục Z)")]
    public float closedAngle = 0f;    // Đóng: nằm ngang
    public float openAngle = -85f;    // Mở: dựng đứng lên
    public float rotateSpeed = 2f;    // Tốc độ nâng hạ

    [Header("Âm Thanh Rào Chắn")]
    public AudioSource gateAudioSource;
    public AudioClip gateOpenClip;
    public AudioClip gateCloseClip;
    [Range(0f, 1f)]
    [Tooltip("Âm lượng âm thanh nâng/hạ rào chắn (Mặc định 0.35 dịu nhẹ)")]
    public float gateVolume = 0.35f;

    [Header("Thời Gian Nâng/Hạ")]
    [Tooltip("Thời gian nâng/hạ thanh chắn (Nếu <= 0 sẽ tự động khớp 100% theo độ dài file âm thanh AudioClip)")]
    public float customDuration = 0f;

    private Coroutine currentRotateRoutine;

    private void Awake()
    {
        // Tự động đảm bảo barPivot trỏ đúng vào con của chính GameObject này (tránh lỗi khi Duplicate vẫn trỏ về con của bản gốc)
        if (barPivot == null || !barPivot.IsChildOf(transform))
        {
            Transform found = transform.Find("Bar_Pivot");
            if (found == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.ToLower().Contains("pivot") || child.name.ToLower().Contains("bar"))
                    {
                        found = child;
                        break;
                    }
                }
            }
            if (found != null) barPivot = found;
        }

        if (gateVolume <= 0.001f) gateVolume = 0.35f;

        if (gateAudioSource == null)
        {
            gateAudioSource = GetComponent<AudioSource>();
        }

        if (gateAudioSource != null)
        {
            gateAudioSource.volume = gateVolume;
            gateAudioSource.spatialBlend = 1.0f;
        }
    }

    public bool IsRotating()
    {
        return currentRotateRoutine != null;
    }

    public bool IsFullyClosed()
    {
        if (barPivot == null) return true;
        if (currentRotateRoutine != null) return false;
        Quaternion targetRotation = Quaternion.Euler(0, 0, closedAngle);
        return Quaternion.Angle(barPivot.localRotation, targetRotation) <= 1.0f;
    }

    public void OpenGate()
    {
        if (gateAudioSource != null && gateOpenClip != null)
        {
            gateAudioSource.volume = gateVolume;
            gateAudioSource.PlayOneShot(gateOpenClip, gateVolume);
        }

        if (barPivot == null) return;

        float clipDur = (gateOpenClip != null && gateOpenClip.length > 0.1f) ? gateOpenClip.length : 1.5f;
        float duration = (customDuration > 0.05f) ? customDuration : Mathf.Max(0.5f, clipDur * 0.95f);

        if (currentRotateRoutine != null) StopCoroutine(currentRotateRoutine);
        currentRotateRoutine = StartCoroutine(RotateGateRoutine(openAngle, duration));
    }

    public void CloseGate()
    {
        if (gateAudioSource != null && gateCloseClip != null)
        {
            gateAudioSource.volume = gateVolume;
            gateAudioSource.PlayOneShot(gateCloseClip, gateVolume);
        }

        if (barPivot == null) return;

        // Thanh chắn đóng khớp chuẩn theo độ dài âm thanh (hạ chạm đáy ngay khi tiếng khóa kết thúc)
        float clipDur = (gateCloseClip != null && gateCloseClip.length > 0.1f) ? gateCloseClip.length : 1.5f;
        float duration = (customDuration > 0.05f) ? customDuration : Mathf.Max(0.5f, clipDur * 0.95f);

        if (currentRotateRoutine != null) StopCoroutine(currentRotateRoutine);
        currentRotateRoutine = StartCoroutine(RotateGateRoutine(closedAngle, duration));
    }

    private IEnumerator RotateGateRoutine(float targetZAngle, float duration)
    {
        if (barPivot == null) yield break;

        Quaternion startRotation = barPivot.localRotation;
        Quaternion targetRotation = Quaternion.Euler(0, 0, targetZAngle);

        if (duration <= 0.05f) duration = 1.2f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // SmoothStep giúp thanh chắn chuyển động mượt mà tự nhiên (khởi động êm và tiếp đất êm)
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            barPivot.localRotation = Quaternion.Lerp(startRotation, targetRotation, smoothT);
            yield return null;
        }

        barPivot.localRotation = targetRotation;
        currentRotateRoutine = null;
    }
}