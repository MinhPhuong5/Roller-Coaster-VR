using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class SeatSwitcher : MonoBehaviour
{
    [Header("VR Rig / Người Chơi")]
    [Tooltip("Kéo toàn bộ cụm XR Origin (XR Rig) vào đây")]
    public GameObject xrOriginRig;

    [Header("XR Controllers (Ẩn khi tàu chạy)")]
    public GameObject leftController;
    public GameObject rightController;

    [Header("Chế Độ Xoay Chuột")]
    [Tooltip("Kéo MouseLook trên Main Camera của XR Origin vào đây")]
    public MouseLook mouseLook;

    [Header("Ghế trong tàu")]
    public Transform[] seats;
    private int currentSeatIndex = 0;

    [Header("Ride Controller")]
    public RideController rideController;

    [Header("Rào Chắn Ga")]
    public StationGateController stationGate;

    [Header("UI Sảnh")]
    [Tooltip("Kéo RideUIPanel vào đây")]
    public GameObject uiPanel;
    [Tooltip("Kéo SelectSeatGroup vào đây")]
    public GameObject selectSeatGroup;
    [Tooltip("Kéo GameOverGroup vào đây (hỏi chơi tiếp)")]
    public GameObject gameOverPanel;
    public GameObject startButton;

    [Header("Tùy Chọn Đếm Ngược")]
    public bool useCountdownText = true;
    public bool useCountdownAudio = true;

    [Header("Đếm Ngược Bắt Đầu")]
    public TextMeshProUGUI countdownText;
    public int countdownSeconds = 3;

    [Header("Âm Thanh Đếm Ngược")]
    public AudioSource countdownAudio;

    [Header("Thời Gian Chờ Mở Rào (Giây)")]
    public float gateOpenDelay = 6.0f;

    [Header("Điểm Sàn Ga Tàu (VR_FloorPoint)")]
    [Tooltip("Kéo VR_FloorPoint ở sảnh ga vào đây")]
    public Transform stationFloorPoint;

    [Header("Điểm Thoát Về Map Công Viên")]
    [Tooltip("Kéo CoasterReturnMapPoint ở ngoài đường dạo công viên vào đây")]
    public Transform parkReturnPoint;

    private bool isRiding = false;
    private bool isHandlingExit = false;
    private bool isGateAlreadyClosed = false;

    private Transform mainCameraTransform;
    private Transform originalCamParent;
    private XRFallbackWalkController walkController;

    void Start()
    {
        CacheCameraReferences();
        HideUI();

        if (startButton != null) startButton.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (stationFloorPoint == null)
        {
            GameObject floor = GameObject.Find("VR_FloorPoint");
            if (floor != null) stationFloorPoint = floor.transform;
        }

        if (rideController == null)
        {
            rideController = Object.FindAnyObjectByType<RideController>();
        }
    }

    void Update()
    {
        if (rideController != null && rideController.currentState == RideController.RideState.WaitingAtStation)
        {
            if (!isRiding && !isHandlingExit)
            {
                // New Input System: Phím Tab để đổi ghế nhanh trên PC
                if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    NextSeat();
                }

                // New Input System: Giữ chuột phải để lia góc nhìn ngắm cảnh sảnh ga trên PC
                if (Mouse.current != null)
                {
                    if (Mouse.current.rightButton.wasPressedThisFrame)
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                    else if (Mouse.current.rightButton.wasReleasedThisFrame)
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                }
            }
        }
    }

    private void CacheCameraReferences()
    {
        if (xrOriginRig != null)
        {
            if (walkController == null)
            {
                walkController = xrOriginRig.GetComponent<XRFallbackWalkController>();
            }

            if (mainCameraTransform == null)
            {
                Camera cam = xrOriginRig.GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    mainCameraTransform = cam.transform;
                    originalCamParent = mainCameraTransform.parent;
                    if (mouseLook == null)
                    {
                        mouseLook = cam.GetComponent<MouseLook>();
                    }
                }
            }
        }
    }

    public void NextSeat()
    {
        if (seats == null || seats.Length == 0) return;
        currentSeatIndex = (currentSeatIndex + 1) % seats.Length;
        SelectSeatFromUI(currentSeatIndex);
    }

    public void SelectSeatFromUI(int index)
    {
        if (isRiding || isHandlingExit) return;
        SwitchSeat(index);
        StartRideProcess();
    }

    public void SwitchSeat(int index)
    {
        if (seats == null || seats.Length == 0) return;
        CacheCameraReferences();

        currentSeatIndex = index;

        if (walkController != null) walkController.enabled = false;

        Transform targetSeat = seats[index];

        // Ép mắt camera rơi chính xác vào tâm điểm 3 trục rotate của ghế
        if (xrOriginRig != null)
        {
            CharacterController cc = xrOriginRig.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            xrOriginRig.transform.SetParent(targetSeat);
            xrOriginRig.transform.localRotation = Quaternion.identity;
            xrOriginRig.transform.localScale = Vector3.one;

            if (mainCameraTransform != null)
            {
                Vector3 camLocalPos = xrOriginRig.transform.InverseTransformPoint(mainCameraTransform.position);
                xrOriginRig.transform.localPosition = -camLocalPos;
            }
            else
            {
                xrOriginRig.transform.localPosition = Vector3.zero;
            }

            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
        }
        else if (mainCameraTransform != null)
        {
            mainCameraTransform.SetParent(targetSeat);
            mainCameraTransform.localPosition = Vector3.zero;
            mainCameraTransform.localRotation = Quaternion.identity;
        }

        // Bật xoay góc nhìn chuột trong khoang tàu
        if (mouseLook != null)
        {
            mouseLook.enabled = true;
            mouseLook.ResetLook(Quaternion.identity);
        }

        EnterSeatedMode();
    }

    public void HideUI()
    {
        if (uiPanel != null) uiPanel.SetActive(false);
        if (selectSeatGroup != null) selectSeatGroup.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (startButton != null) startButton.SetActive(false);
    }

    public void StartRideProcess()
    {
        HideUI();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        StartCoroutine(StartRideSequence());
    }

    public void EnterBoardingMode()
    {
        if (isHandlingExit) return;
        ForceBoardingMode();
    }

    /// <summary>
    /// Đưa người chơi ra sàn ga và ép góc nhìn nhìn thẳng vào bảng chọn ghế
    /// </summary>
    public void ForceBoardingMode()
    {
        CacheCameraReferences();

        if (walkController != null) walkController.enabled = false;

        // 1. Tháo người chơi ra khỏi ghế và đưa ra đứng tại VR_FloorPoint
        if (xrOriginRig != null)
        {
            xrOriginRig.SetActive(true);
            xrOriginRig.transform.SetParent(null);
            xrOriginRig.transform.localScale = Vector3.one;

            if (stationFloorPoint != null)
            {
                CharacterController cc = xrOriginRig.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                xrOriginRig.transform.SetPositionAndRotation(stationFloorPoint.position, stationFloorPoint.rotation);

                if (cc != null) cc.enabled = true;
                Physics.SyncTransforms();
            }
        }

        // 2. Trả Main Camera về vị trí gốc và ép góc quay thẳng
        if (mainCameraTransform != null && originalCamParent != null)
        {
            if (mainCameraTransform.parent != originalCamParent)
            {
                mainCameraTransform.SetParent(originalCamParent);
            }
            mainCameraTransform.localPosition = Vector3.zero;
            mainCameraTransform.localRotation = Quaternion.identity;
        }

        // 3. Reset MouseLook để hướng nhìn khóa thẳng theo hướng của sàn ga (hướng vào UI)
        if (mouseLook != null)
        {
            mouseLook.enabled = true;
            Quaternion targetRotation = stationFloorPoint != null ? stationFloorPoint.rotation : Quaternion.identity;
            mouseLook.ResetLook(targetRotation);
        }

        SetControllersActive(true);

        // Mở chuột tự do
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Bật bảng chọn ghế, ẩn bảng kết thúc
        if (uiPanel != null) uiPanel.SetActive(true);
        if (selectSeatGroup != null) selectSeatGroup.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (startButton != null) startButton.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        if (rideController != null)
        {
            rideController.ResetToStation();
        }
    }

    public void EnterSeatedMode() { }

    public void TriggerEarlyGateClose()
    {
        if (!isGateAlreadyClosed && stationGate != null)
        {
            isGateAlreadyClosed = true;
            stationGate.CloseGate();
        }
    }

    public void ExitCar()
    {
        if (!isHandlingExit)
        {
            StartCoroutine(HandleRideEndSequence());
        }
    }

    private IEnumerator StartRideSequence()
    {
        isGateAlreadyClosed = false;

        if (stationGate != null)
        {
            stationGate.OpenGate();
        }

        bool hasCountdown = useCountdownText || useCountdownAudio;

        if (hasCountdown)
        {
            float waitBeforeCountdown = Mathf.Max(0f, gateOpenDelay - countdownSeconds);
            if (waitBeforeCountdown > 0f)
            {
                yield return new WaitForSeconds(waitBeforeCountdown);
            }

            if (useCountdownAudio && countdownAudio != null)
            {
                countdownAudio.Play();
            }

            if (useCountdownText && countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
                for (int i = countdownSeconds; i > 0; i--)
                {
                    countdownText.text = i.ToString();
                    yield return new WaitForSeconds(1.0f);
                }
                countdownText.text = "GO!";
                yield return new WaitForSeconds(0.6f);
                countdownText.gameObject.SetActive(false);
            }
            else
            {
                yield return new WaitForSeconds(Mathf.Min(gateOpenDelay, (float)countdownSeconds));
            }
        }
        else
        {
            yield return new WaitForSeconds(gateOpenDelay);
        }

        SetControllersActive(false);

        if (rideController != null)
        {
            rideController.StartRide();
        }

        isRiding = true;
        isHandlingExit = false;
    }

    /// <summary>
    /// Chờ tàu phanh đỗ hẳn vào bến rồi mới tháo người chơi ra sàn và hiện GameOverGroup
    /// </summary>
    private IEnumerator HandleRideEndSequence()
    {
        isHandlingExit = true;

        // Chờ đủ thời gian để tàu phanh từ từ về bến dừng hẳn
        yield return new WaitForSeconds(3.5f);

        if (stationGate != null && stationGate.gateAudioSource != null)
        {
            stationGate.gateAudioSource.Stop();
        }

        if (countdownAudio != null && countdownAudio.isPlaying)
        {
            countdownAudio.Stop();
        }

        isRiding = false;
        isHandlingExit = false;

        // 1. Tháo người chơi ra khỏi ghế, đưa ra đứng ở VR_FloorPoint
        if (xrOriginRig != null)
        {
            xrOriginRig.SetActive(true);
            xrOriginRig.transform.SetParent(null);
            xrOriginRig.transform.localScale = Vector3.one;

            if (stationFloorPoint != null)
            {
                CharacterController cc = xrOriginRig.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                xrOriginRig.transform.SetPositionAndRotation(stationFloorPoint.position, stationFloorPoint.rotation);

                if (cc != null) cc.enabled = true;
                Physics.SyncTransforms();
            }
        }

        // 2. Trả Main Camera về vị trí chuẩn và xoay thẳng
        if (mainCameraTransform != null && originalCamParent != null)
        {
            if (mainCameraTransform.parent != originalCamParent)
            {
                mainCameraTransform.SetParent(originalCamParent);
            }
            mainCameraTransform.localPosition = Vector3.zero;
            mainCameraTransform.localRotation = Quaternion.identity;
        }

        if (mouseLook != null)
        {
            mouseLook.enabled = true;
            Quaternion targetRotation = stationFloorPoint != null ? stationFloorPoint.rotation : Quaternion.identity;
            mouseLook.ResetLook(targetRotation);
        }

        SetControllersActive(true);

        // 3. Mở chuột tự do
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 4. Hiện bảng hỏi chơi lại
        if (uiPanel != null) uiPanel.SetActive(true);
        if (selectSeatGroup != null) selectSeatGroup.SetActive(false);
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            Debug.Log("[SeatSwitcher] Đã dừng hẳn tại ga -> Bật GameOverGroup thành công!");
        }
    }

    private void SetControllersActive(bool isActive)
    {
        if (leftController != null) leftController.SetActive(isActive);
        if (rightController != null) rightController.SetActive(isActive);
    }

    /// <summary>
    /// Thoát khỏi tàu lượn và dịch chuyển trở về đường dạo công viên
    /// </summary>
    public void ReturnToParkMap()
    {
        Time.timeScale = 1f;

        if (xrOriginRig != null)
        {
            xrOriginRig.transform.SetParent(null);

            if (parkReturnPoint != null)
            {
                CharacterController cc = xrOriginRig.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                xrOriginRig.transform.SetPositionAndRotation(parkReturnPoint.position, parkReturnPoint.rotation);

                if (cc != null) cc.enabled = true;
                Physics.SyncTransforms();
            }

            if (walkController != null) walkController.enabled = true;
        }

        if (mouseLook != null) mouseLook.enabled = false;

        HideUI();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}