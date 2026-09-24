using UnityEngine;

/// <summary>
/// Gắn lên Box Collider (Is Trigger = true) của cổng dịch chuyển vàng CongDichChuyenMorty.
/// Khi người chơi bước qua cổng này, script gọi EvilMortyController để quay về Scene Menu (Trang chủ).
/// </summary>
[RequireComponent(typeof(Collider))]
public class MortyPortalExitTrigger : MonoBehaviour
{
    [Tooltip("Kéo object MortyPortalTrigger (có script EvilMortyController) vào đây.")]
    [SerializeField] private EvilMortyController mortyController;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (mortyController != null)
        {
            mortyController.BeginTeleport(other);
        }
    }
}
