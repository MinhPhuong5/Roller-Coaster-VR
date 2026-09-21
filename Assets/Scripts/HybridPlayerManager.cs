using UnityEngine;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;

[DefaultExecutionOrder(10000)]
public class HybridPlayerManager : MonoBehaviour
{
    [Header("=== CỤM THIẾT BỊ VR ===")]
    public GameObject xrOriginRig;

    [Header("=== CỤM NHÂN VẬT PC (CHIHIRO) ===")]
    public GameObject chihiroObject;
    public Camera tpsCamera;
    [Tooltip("Object chứa mesh của Chihiro (Sketchfab_model). Để trống sẽ tự tìm")]
    public GameObject characterVisualModel;
    public AnimatorChihiro chihiroScript;

    [Header("=== VỊ TRÍ MẮT (GÓC XR) ===")]
    [Tooltip("Để trống sẽ tự tìm xương Head của Animator Humanoid")]
    public Transform headBone;
    [Tooltip("Nâng từ xương Head lên tầm mắt (đơn vị world)")]
    public float eyeUpOffset = 0.25f;
    [Tooltip("Chỉ dùng nếu không tìm thấy xương Head (đơn vị world)")]
    public float fallbackEyeHeight = 3.6f;

    [Header("=== NHÌN BẰNG CHUỘT (chỉ khi test PC ở góc XR) ===")]
    public float mouseSensitivity = 2.5f;
    public float minPitch = -60f;
    public float maxPitch = 60f;
    public bool invertLookY = false;

    [Header("=== PHÍM TEST PC ===")]
    public KeyCode toggleViewKey = KeyCode.V;

    private bool isVRActive = false;
    private bool isXRView = false;
    private XROrigin xrOrigin;
    private FPSCameraController tpsController;
    private Renderer[] modelRenderers;
    private float xrYaw;
    private float xrPitch;

    void Awake()
    {
        if (xrOriginRig != null)
            xrOrigin = xrOriginRig.GetComponent<XROrigin>();

        if (tpsCamera != null)
            tpsController = tpsCamera.GetComponent<FPSCameraController>();

        if (characterVisualModel == null && chihiroObject != null)
        {
            Transform model = chihiroObject.transform.Find("Sketchfab_model");
            if (model != null) characterVisualModel = model.gameObject;
        }

        GameObject modelRoot = characterVisualModel != null ? characterVisualModel : chihiroObject;
        if (modelRoot != null)
            modelRenderers = modelRoot.GetComponentsInChildren<Renderer>(true);

        if (chihiroObject != null)
        {
            Animator anim = chihiroObject.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; // xương đầu luôn cập nhật
                if (headBone == null && anim.isHuman)
                    headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            }
        }
    }

    void Start()
    {
        isVRActive = IsXRDeviceRunning();
        SetXRView(isVRActive);
    }

    void Update()
    {
        if (!isVRActive && Input.GetKeyDown(toggleViewKey))
            SetXRView(!isXRView);
    }

    void LateUpdate()
    {
        if (!isXRView || xrOrigin == null || xrOrigin.Camera == null) return;

        Transform rig = xrOriginRig.transform;

        if (isVRActive)
        {
            // KÍNH THẬT: locomotion di chuyển rig, Chihiro (ẩn) đi theo rig
            chihiroObject.transform.position = rig.position;
            return;
        }

        // ===== TEST PC =====
        xrYaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        float dy = Input.GetAxis("Mouse Y") * mouseSensitivity * (invertLookY ? -1f : 1f);
        xrPitch = Mathf.Clamp(xrPitch - dy, minPitch, maxPitch);

        // Camera luôn nằm ngay gốc rig -> đặt rig thẳng vào vị trí mắt, không cộng bù
        if (xrOrigin.CameraFloorOffsetObject != null)
        {
            Transform offset = xrOrigin.CameraFloorOffsetObject.transform;
            offset.localPosition = Vector3.zero;
            offset.localRotation = Quaternion.identity;
        }

        Transform cam = xrOrigin.Camera.transform;
        cam.localPosition = Vector3.zero;
        cam.localRotation = Quaternion.Euler(xrPitch, 0f, 0f);

        rig.rotation = Quaternion.Euler(0f, xrYaw, 0f);
        rig.position = GetEyeWorldPosition();
    }

    private Vector3 GetEyeWorldPosition()
    {
        if (headBone != null)
            return headBone.position + Vector3.up * eyeUpOffset;
        return chihiroObject.transform.position + Vector3.up * fallbackEyeHeight;
    }

    private bool IsXRDeviceRunning()
    {
        var s = XRGeneralSettings.Instance;
        return s != null && s.Manager != null && s.Manager.activeLoader != null;
    }

    private void SetModelVisible(bool visible)
    {
        if (modelRenderers == null) return;
        foreach (Renderer r in modelRenderers)
            if (r != null) r.enabled = visible;
    }

    // Test PC: tắt TrackedPoseDriver để không ai ghi đè camera. Kính thật: bật lại.
    private void SetPoseDrivers(bool on)
    {
        if (xrOrigin == null || xrOrigin.Camera == null) return;
        foreach (Behaviour b in xrOrigin.Camera.GetComponents<Behaviour>())
            if (b != null && b.GetType().Name.Contains("TrackedPoseDriver"))
                b.enabled = on;
    }

    private void SetXRView(bool xr)
    {
        isXRView = xr;

        if (xr)
        {
            xrOriginRig.SetActive(true);
            Transform rig = xrOriginRig.transform;

            xrYaw = isVRActive ? chihiroObject.transform.eulerAngles.y
                               : (tpsCamera != null ? tpsCamera.transform.eulerAngles.y
                                                    : chihiroObject.transform.eulerAngles.y);
            xrPitch = 0f;
            rig.rotation = Quaternion.Euler(0f, xrYaw, 0f);

            if (xrOrigin != null)
            {
                xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
                if (isVRActive)
                {
                    rig.position = chihiroObject.transform.position;
                    float eyeH = GetEyeWorldPosition().y - chihiroObject.transform.position.y;
                    xrOrigin.CameraYOffset = eyeH / rig.lossyScale.y;
                }
                else
                {
                    xrOrigin.CameraYOffset = 0f; // PC: rig đặt thẳng vào vị trí mắt
                    rig.position = GetEyeWorldPosition();
                }
            }

            SetPoseDrivers(isVRActive);

            if (tpsCamera != null) tpsCamera.gameObject.SetActive(false);
            SetModelVisible(false);

            if (chihiroScript != null)
            {
                if (isVRActive) chihiroScript.SetControlsLocked(true);   // khóa vật lý, rig dẫn đường
                else
                {
                    if (xrOrigin != null && xrOrigin.Camera != null)
                        chihiroScript.cameraTransform = xrOrigin.Camera.transform;
                    chihiroScript.SetInputLocked(false);                 // PC: vẫn đi bằng WASD
                }
            }
        }
        else
        {
            if (tpsController != null) tpsController.SetYaw(xrYaw);
            if (xrOriginRig != null) xrOriginRig.SetActive(false);
            if (chihiroObject != null) chihiroObject.SetActive(true);
            if (tpsCamera != null) tpsCamera.gameObject.SetActive(true);
            SetModelVisible(true);

            if (chihiroScript != null)
            {
                if (tpsCamera != null) chihiroScript.cameraTransform = tpsCamera.transform;
                chihiroScript.SetInputLocked(false);
            }
        }
    }
}