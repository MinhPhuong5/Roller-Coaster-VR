using UnityEngine;

/// <summary>
/// Script dieu khien may troi muot ma cho Cloud_Sky
/// Tuong thich hoan toan voi URP (Universal RP) va Built-in Pipeline.
/// </summary>
public class SmoothCloudMover : MonoBehaviour
{
    [Header("Toc do gio thoi may (X, Y)")]
    [Tooltip("Dieu chinh toc do may bay theo truc X va Y")]
    public Vector2 windSpeed = new Vector2(0.005f, 0.002f);

    [Header("Tuy chon")]
    [Tooltip("Tu dong go MeshCollider tren Cloud_Sky de tranh xung dot vat ly/raycast")]
    public bool autoRemoveCollider = true;

    private Material cloudMaterial;
    private Vector2 currentOffset = Vector2.zero;
    private int propertyId;

    void Start()
    {
        // 1. Tu dong tat Collider neu co de toi uu game va tranh va cham
        if (autoRemoveCollider)
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }
        }

        // 2. Lay Material dang duoc gan tren MeshRenderer
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            cloudMaterial = rend.material;

            // Kiem tra Shader dung _BaseMap (URP Lit/Unlit) hay _MainTex (Standard)
            if (cloudMaterial.HasProperty("_BaseMap"))
            {
                propertyId = Shader.PropertyToID("_BaseMap");
                currentOffset = cloudMaterial.GetTextureOffset(propertyId);
            }
            else if (cloudMaterial.HasProperty("_MainTex"))
            {
                propertyId = Shader.PropertyToID("_MainTex");
                currentOffset = cloudMaterial.GetTextureOffset(propertyId);
            }
            else
            {
                Debug.LogWarning("[SmoothCloudMover] Khong tim thay thuoc tinh _BaseMap hoac _MainTex tren Material!");
            }
        }
        else
        {
            Debug.LogError("[SmoothCloudMover] Khong tim thay MeshRenderer tren GameObject!");
        }
    }

    void Update()
    {
        if (cloudMaterial == null || propertyId == 0) return;

        // Troi UV lien tuc theo thoi gian thuc
        currentOffset += windSpeed * Time.deltaTime;

        // Giu offset trong vong lap 0 -> 1 de tranh tran so float
        if (currentOffset.x > 1f) currentOffset.x -= 1f;
        else if (currentOffset.x < -1f) currentOffset.x += 1f;

        if (currentOffset.y > 1f) currentOffset.y -= 1f;
        else if (currentOffset.y < -1f) currentOffset.y += 1f;

        // Cap nhat toa do offset len Material
        cloudMaterial.SetTextureOffset(propertyId, currentOffset);
    }
}