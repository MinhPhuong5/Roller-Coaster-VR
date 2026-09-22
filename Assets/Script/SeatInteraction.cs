using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SeatInteraction : MonoBehaviour
{
    public static bool IsPlayerSitting { get; private set; }
    private static SeatInteraction activeSeat;
    [Header("Điểm đặt nhân vật")]
    public Transform seatPoint;
    public Transform exitPoint;
    [Tooltip("Tùy chọn: điểm đặt camera khi nhân vật ngồi để camera không chui vào ghế hoặc bụi cây.")]
    public Transform seatCameraPoint;

    [Header("Nhân vật")]
    public Transform playerTransform;
    public AnimatorChihiro playerController;
    public Animator playerAnimator;

    [Header("Giao diện")]
    public GameObject promptUI;
    public TextMeshProUGUI promptText;
    public string sitPrompt = "Nhấn F để ngồi";
    public string leavePrompt = "Nhấn F để rời khỏi ghế";
    [Min(0f)] public float leaveDelay = 0.2f;

    private bool playerNear;
    private bool isSitting;
    private bool isLeaving;
    private ShiftOrbitCamera playerCamera;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        FindPlayer();
        SetPrompt(false);
    }

    private void Update()
    {
        if (playerTransform == null) FindPlayer();
        if (playerCamera == null && playerTransform != null)
            playerCamera = playerTransform.GetComponentInChildren<ShiftOrbitCamera>(true);

        // Several benches share one prompt UI. Only the trigger containing the player may use it or receive F.
        if (!isSitting && activeSeat != this) return;

        if (isSitting)
        {
            SetPrompt(true, leavePrompt);
            if (!isLeaving && Input.GetKeyDown(KeyCode.F)) StartCoroutine(LeaveSeat());
            return;
        }

        SetPrompt(playerNear, sitPrompt);
        if (playerNear && Input.GetKeyDown(KeyCode.F)) SitDown();
    }

    private void SitDown()
    {
        if (playerTransform == null || seatPoint == null || playerAnimator == null) return;

        playerController?.SetControlsLocked(true);
        Quaternion uprightSeatRotation = Quaternion.Euler(0f, seatPoint.eulerAngles.y, 0f);
        playerTransform.SetPositionAndRotation(seatPoint.position, uprightSeatRotation);
        playerCamera?.SetPositionOverride(seatCameraPoint);
        playerAnimator.SetBool("IsSitting", true);
        isSitting = true;
        IsPlayerSitting = true;
    }

    private IEnumerator LeaveSeat()
    {
        if (exitPoint == null || playerAnimator == null) yield break;

        isLeaving = true;
        SetPrompt(false);
        playerAnimator.SetBool("IsSitting", false);
        playerCamera?.SetPositionOverride(null);
        yield return new WaitForSeconds(leaveDelay);

        // Use the safe exit position, but keep the player upright even if the empty exit marker is rotated.
        Quaternion uprightExitRotation = Quaternion.Euler(0f, exitPoint.eulerAngles.y, 0f);
        playerTransform.SetPositionAndRotation(exitPoint.position, uprightExitRotation);
        playerController?.SetControlsLocked(false);
        isSitting = false;
        IsPlayerSitting = false;
        isLeaving = false;
    }

    private void OnDisable()
    {
        if (isSitting) IsPlayerSitting = false;
        if (activeSeat == this)
        {
            activeSeat = null;
            SetPrompt(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerNear = true;
        if (!isSitting) activeSeat = this;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other) || isSitting) return;

        playerNear = false;
        if (activeSeat == this)
        {
            activeSeat = null;
            SetPrompt(false);
        }
    }

    private bool IsPlayer(Collider other) =>
        other != null && (other.CompareTag("Player") || other.GetComponentInParent<AnimatorChihiro>() != null);

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        playerTransform = player.transform;
        playerController = player.GetComponent<AnimatorChihiro>();
        playerAnimator = playerController != null ? playerController.CharacterAnimator : player.GetComponent<Animator>();
        playerCamera = player.GetComponentInChildren<ShiftOrbitCamera>(true);
    }

    private void SetPrompt(bool visible, string message = "")
    {
        if (promptText != null && visible) promptText.text = message;
        if (promptUI != null && promptUI.activeSelf != visible) promptUI.SetActive(visible);
    }
}
