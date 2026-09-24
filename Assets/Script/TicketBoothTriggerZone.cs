using UnityEngine;

/// <summary>
/// Quét cự ly tự động: Người chơi lại gần -> Hiện bảng vé. Đi xa -> Tự tắt bảng.
/// Không lo trượt Collider, không cần Rigidbody.
/// </summary>
public class TicketBoothTriggerZone : MonoBehaviour
{
    [Header("Bảng Vé Cần Điều Khiển")]
    public GameObject ticketShopUI;

    [Header("Khoảng Cách Bật Bảng (Mét)")]
    [Tooltip("Khoảng cách đứng trước quầy để bảng tự bật lên")]
    public float activationDistance = 4.5f;

    [Header("Âm Thanh (Tùy chọn)")]
    public AudioSource audioSource;
    public AudioClip openSound;

    private Transform playerTransform;
    private bool isPlayerNearby = false;

    private void Start()
    {
        if (ticketShopUI == null)
        {
            ticketShopUI = GameObject.Find("Canvas_TicketSystem");
        }

        // Mặc định ẩn bảng đi khi mới vào game
        if (ticketShopUI != null)
        {
            ticketShopUI.SetActive(false);
        }

        FindPlayer();
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        // Tính khoảng cách giữa người chơi và tâm điểm cảm biến (bỏ qua độ cao Y)
        Vector3 playerPos = playerTransform.position;
        Vector3 zonePos = transform.position;
        playerPos.y = zonePos.y = 0f;

        float distance = Vector3.Distance(playerPos, zonePos);

        // Lại gần vùng bán kính quy định
        if (distance <= activationDistance)
        {
            if (!isPlayerNearby)
            {
                isPlayerNearby = true;
                Debug.Log("[TicketBooth] Người chơi đã đến gần quầy vé -> Bật UI.");

                if (ticketShopUI != null)
                {
                    ticketShopUI.SetActive(true);
                }

                if (audioSource != null && openSound != null)
                {
                    audioSource.PlayOneShot(openSound);
                }

                if (TicketShopUIManager.Instance != null)
                {
                    TicketShopUIManager.Instance.OpenShop();
                }
            }
        }
        else // Đi lùi ra xa
        {
            if (isPlayerNearby)
            {
                isPlayerNearby = false;
                Debug.Log("[TicketBooth] Người chơi đã rời xa quầy vé -> Ẩn UI.");

                if (ticketShopUI != null)
                {
                    ticketShopUI.SetActive(false);
                }

                if (TicketShopUIManager.Instance != null)
                {
                    TicketShopUIManager.Instance.CloseShopPanel();
                }
            }
        }
    }

    private void FindPlayer()
    {
        // 1. Tìm cụm XR Origin
        GameObject xrRig = GameObject.Find("XR Origin (XR Rig)");
        if (xrRig != null)
        {
            playerTransform = xrRig.transform;
            return;
        }

        // 2. Tìm qua script đi bộ XRFallbackWalkController
        XRFallbackWalkController walk = Object.FindAnyObjectByType<XRFallbackWalkController>();
        if (walk != null)
        {
            playerTransform = walk.transform;
            return;
        }

        // 3. Tìm qua Tag Player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            return;
        }

        // 4. Tìm qua Camera chính
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            playerTransform = mainCam.transform;
        }
    }

    private void OnDrawGizmos()
    {
        // Vẽ vòng tròn bán kính kích hoạt màu vàng trong cửa sổ Scene
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationDistance);
    }
}