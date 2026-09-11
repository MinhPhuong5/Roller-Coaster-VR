using UnityEngine;
using TMPro;

/// <summary>
/// Hiệu ứng nhấp nháy phát sáng nhẹ nhàng (breathing/pulsing) cho dòng chữ "TAP TO BEGIN" / "START GAME" giống Genshin Impact.
/// </summary>
public class MenuPulseAnimation : MonoBehaviour
{
    [Header("Tốc độ & Biên độ mờ (Alpha)")]
    [Tooltip("Tốc độ nhịp thở")]
    public float pulseSpeed = 2.8f;

    [Tooltip("Độ mờ nhỏ nhất")]
    [Range(0f, 1f)]
    public float minAlpha = 0.25f;

    [Tooltip("Độ mờ lớn nhất")]
    [Range(0f, 1f)]
    public float maxAlpha = 1.0f;

    [Header("Hiệu ứng co giãn nhẹ (Scale Pulse)")]
    public bool enableScalePulse = true;
    public float scaleAmplitude = 0.03f; // Tăng giảm 3%

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI tmpText;
    private Vector3 initialScale;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        tmpText = GetComponent<TextMeshProUGUI>();
        initialScale = transform.localScale;
    }

    void Update()
    {
        // Tính toán dao động dạng sóng Sin mượt mà trong khoảng [0, 1]
        float sinWave = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;

        // Cập nhật Alpha
        float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, sinWave);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = currentAlpha;
        }
        else if (tmpText != null)
        {
            Color c = tmpText.color;
            c.a = currentAlpha;
            tmpText.color = c;
        }

        // Cập nhật Scale nhẹ nhàng nếu bật
        if (enableScalePulse)
        {
            float scaleFactor = 1f + (sinWave * scaleAmplitude);
            transform.localScale = initialScale * scaleFactor;
        }
    }
}
