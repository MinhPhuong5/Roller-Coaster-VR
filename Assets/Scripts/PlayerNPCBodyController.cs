using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Quản lý nhân vật NPC đại diện cho người chơi (XR Player Body):
/// - Khi Play game: Chọn ngẫu nhiên 1 NPC đại diện gắn vào chân XR Origin.
/// - Đồng bộ bước đi (WASD) và góc quay người chơi.
/// - Ẩn hoàn toàn khỏi góc nhìn XR Main Camera (không thấy tay, chân, thân, đầu) bằng Camera CullingMask.
/// - VẪN HIỂN THỊ ĐẦY ĐỦ 100% (cả đầu và cơ thể) trong cửa sổ Scene View để quan sát.
/// - Khi lên tàu lượn: Ngồi vào ghế được chọn, giơ tay mạo hiểm cùng các NPC khác mà không cần ẩn đầu.
/// </summary>
public class PlayerNPCBodyController : MonoBehaviour
{
    [Header("1. Tham Chiếu Rig & Camera")]
    public GameObject xrOriginRig;
    public Camera mainCamera;

    [Header("2. Layer Ẩn Khỏi Camera Người Chơi")]
    public string playerBodyLayerName = "PlayerBody";
    public int playerBodyLayer = 6;

    [Header("3. Danh Sách Prefabs & Scale NPC")]
    public GameObject[] npcPrefabs;
    [Tooltip("Kích thước khi đi dạo ở công viên (Scale 1.22x đồng bộ với NPC trong tàu)")]
    public Vector3 standingScale = new Vector3(1.22f, 1.22f, 1.22f);
    [Tooltip("Kích thước cục bộ khi ngồi trong SitPoint đệm ghế tàu (Scale 1.22x)")]
    public Vector3 seatedScale = new Vector3(1.22f, 1.22f, 1.22f);

    [HideInInspector] public int chosenPlayerPrefabIndex = -1;
    [HideInInspector] public GameObject chosenPlayerPrefab;

    [Header("4. Animator Controller Chuẩn")]
    public RuntimeAnimatorController masterAnimatorController;

    [Header("5. NPC Đang Hoạt Động")]
    public GameObject currentNPCBody;
    private Animator npcAnimator;
    private bool isSeated = false;

    private static readonly int IsSittingParam = Animator.StringToHash("IsSitting");
    private static readonly int IsWalkingParam = Animator.StringToHash("IsWalking");
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int IsThrilledParam = Animator.StringToHash("IsThrilled");

    /// <summary>
    /// Tìm xương đầu (hoặc mắt) của NPC đại diện người chơi theo chuẩn Humanoid hoặc rig xương cụ thể (bip Head, mixamo, ...)
    /// </summary>
    public Transform GetHeadTransform()
    {
        if (currentNPCBody == null) return null;

        Animator anim = currentNPCBody.GetComponent<Animator>();
        if (anim != null && anim.isHuman)
        {
            Transform h = anim.GetBoneTransform(HumanBodyBones.Head);
            if (h != null) return h;
        }

        string[] headNames = new string[] { "bip Head", "bip head", "Head", "head", "mixamorig:Head", "Character1_Head" };
        foreach (var hName in headNames)
        {
            Transform h = CoasterPassengerManager.FindChildRecursive(currentNPCBody.transform, hName);
            if (h != null) return h;
        }

        Transform[] allChildren = currentNPCBody.GetComponentsInChildren<Transform>(true);
        foreach (var t in allChildren)
        {
            string n = t.name.ToLower();
            if (n.Contains("head") && !n.Contains("top") && !n.Contains("wear") && !n.Contains("gear"))
            {
                return t;
            }
        }

        return null;
    }

    /// <summary>
    /// Tìm xương mông/hông (Hips/Pelvis) của NPC theo chuẩn Humanoid hoặc rig xương cụ thể (bip Pelvis, Hips, mixamo...)
    /// </summary>
    public Transform GetHipsTransform()
    {
        if (currentNPCBody == null) return null;

        Animator anim = currentNPCBody.GetComponent<Animator>();
        if (anim != null && anim.isHuman)
        {
            Transform h = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (h != null) return h;
        }

        string[] hipNames = new string[] { "bip Pelvis", "bip pelvis", "Pelvis", "pelvis", "Hips", "hips", "mixamorig:Hips" };
        foreach (var hName in hipNames)
        {
            Transform h = CoasterPassengerManager.FindChildRecursive(currentNPCBody.transform, hName);
            if (h != null) return h;
        }

        Transform[] allChildren = currentNPCBody.GetComponentsInChildren<Transform>(true);
        foreach (var t in allChildren)
        {
            string n = t.name.ToLower();
            if ((n.Contains("pelvis") || n.Contains("hip")) && !n.Contains("cloth") && !n.Contains("mesh"))
            {
                return t;
            }
        }

        return null;
    }

    /// <summary>
    /// Đọc chiều cao mắt thực tế của NPC đại diện người chơi theo đúng xương đầu của từng model riêng biệt (bỏ qua giá trị mặc định trong Inspector)
    /// </summary>
    public float GetPlayerStandingEyeHeight()
    {
        if (currentNPCBody != null)
        {
            Animator anim = currentNPCBody.GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                Transform leftEye = anim.GetBoneTransform(HumanBodyBones.LeftEye);
                Transform rightEye = anim.GetBoneTransform(HumanBodyBones.RightEye);
                if (leftEye != null && rightEye != null)
                {
                    float eyeY = (leftEye.position.y + rightEye.position.y) * 0.5f - currentNPCBody.transform.position.y;
                    if (eyeY > 0.4f) return eyeY;
                }
                else if (leftEye != null)
                {
                    float eyeY = leftEye.position.y - currentNPCBody.transform.position.y;
                    if (eyeY > 0.4f) return eyeY;
                }
            }

            Transform head = GetHeadTransform();
            if (head != null)
            {
                float measuredHeight = head.position.y - currentNPCBody.transform.position.y;
                if (measuredHeight > 0.4f)
                {
                    // Tầm mắt nằm ngang trên xương đầu một chút (+0.03m theo scale)
                    return measuredHeight + (0.03f * (standingScale.y / 1.0f));
                }
            }
        }

        // Fallback theo tỷ lệ scale nếu không đo được xương
        float scaleY = (standingScale.y > 0.1f) ? standingScale.y : 1.0f;
        return 1.45f * scaleY;
    }

    /// <summary>
    /// Lấy vị trí tầm mắt chính xác của NPC người chơi khi đang ngồi trong khoang tàu
    /// </summary>
    public Vector3 GetSeatedEyePosition(Transform fallbackSitPoint)
    {
        Transform head = GetHeadTransform();
        if (head != null)
        {
            return head.position + (head.forward * 0.06f) + (head.up * 0.03f);
        }

        if (fallbackSitPoint != null)
        {
            return fallbackSitPoint.position + (fallbackSitPoint.up * (0.85f * seatedScale.y)) + (fallbackSitPoint.forward * 0.08f);
        }

        return transform.position + Vector3.up * 0.85f;
    }

    /// <summary>
    /// Căn chỉnh Camera và CharacterController chính xác theo tầm mắt thực của model NPC được chọn
    /// </summary>
    public void AlignCameraToHead()
    {
        ResolvePlayerReferences();
        float eyeHeight = GetPlayerStandingEyeHeight();

        if (mainCamera == null)
        {
            mainCamera = GetComponentInChildren<Camera>();
        }

        if (mainCamera != null)
        {
            Transform camParent = mainCamera.transform.parent;
            if (camParent != null && camParent != transform)
            {
                // Có Camera Offset (chuẩn XR Origin): đặt Camera Offset ở độ cao mắt của NPC
                camParent.localPosition = new Vector3(0f, eyeHeight, 0f);
                camParent.localRotation = Quaternion.identity;
                mainCamera.transform.localPosition = Vector3.zero;
            }
            else
            {
                // Camera gắn trực tiếp vào root
                mainCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
            }
            mainCamera.transform.localRotation = Quaternion.identity;
        }

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.height = Mathf.Max(0.8f, eyeHeight + 0.15f);
            cc.center = new Vector3(0f, cc.height / 2f, 0f);
        }

        Debug.Log($"<color=#00FF88><b>[PlayerNPCBodyController] Đã căn chỉnh Camera theo đầu NPC '{chosenPlayerPrefab?.name}': Chiều cao mắt = {eyeHeight:F3}m (Bỏ qua Inspector mặc định)</b></color>");
    }

    /// <summary>
    /// Chuyển scale NPC đại diện sang tỷ lệ ga tàu / tàu lượn (Đồng bộ tuyệt đối World Scale: Cart.lossyScale * 1.22)
    /// </summary>
    public void SetStationScale()
    {
        float scaleFactor = 1.22f;
        CoasterPassengerManager passengerMgr = Object.FindAnyObjectByType<CoasterPassengerManager>();
        if (passengerMgr != null)
        {
            Transform sp = passengerMgr.GetSitPoint(0);
            if (sp != null && sp.lossyScale.y > 0.01f)
            {
                scaleFactor = sp.lossyScale.y * (passengerMgr.scaleMultiplier > 0.01f ? passengerMgr.scaleMultiplier : 1.22f);
            }
        }

        // Nếu lossyScale đo được chưa đủ (do chưa load xong), dùng hệ số nhân tỷ lệ toa tàu chuẩn ~2.93x (2.4 * 1.22)
        if (scaleFactor < 1.3f)
        {
            scaleFactor = 1.22f * 2.4f;
        }

        standingScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
        seatedScale = new Vector3(1.22f, 1.22f, 1.22f);

        if (currentNPCBody != null)
        {
            currentNPCBody.transform.localScale = standingScale;
            Debug.Log($"<color=#00FF88><b>[PlayerNPCBodyController] ĐÃ ĐỒNG BỘ SCALE GA TÀU THÀNH CÔNG: {standingScale.x:F2}x (Tàu/Ghế: {scaleFactor / 1.22f:F2}x * NPC: 1.22x)</b></color>");
        }

        AlignCameraToHead();
    }

    /// <summary>
    /// Chuyển scale NPC đại diện về tỷ lệ công viên (Scale 1.0x)
    /// </summary>
    public void SetParkScale()
    {
        standingScale = Vector3.one;
        seatedScale = new Vector3(1.22f, 1.22f, 1.22f);
        if (currentNPCBody != null)
        {
            currentNPCBody.transform.localScale = standingScale;
            Debug.Log($"<color=#00CCFF><b>[PlayerNPCBodyController] Đã trả scale NPC đại diện về 1.0x cho công viên: {currentNPCBody.transform.localScale}</b></color>");
        }

        AlignCameraToHead();
    }

    void Awake()
    {
        ResolvePlayerReferences();
        SetupCameraCulling();
        LoadDefaultAssetsIfEmpty();
    }

    void Start()
    {
        if (currentNPCBody == null)
        {
            SpawnPlayerRepresentativeNPC();
        }

        AlignCameraToHead();
    }

    void Update()
    {
        if (isSeated)
        {
            // Khi đang ngồi trên tàu: duy trì trạng thái ngồi tuyệt đối, ngăn chặn bất kỳ sự kiện nào đè animation
            if (npcAnimator != null && npcAnimator.runtimeAnimatorController != null)
            {
                if (!npcAnimator.GetBool(IsSittingParam))
                {
                    npcAnimator.SetBool(IsSittingParam, true);
                    npcAnimator.SetBool(IsWalkingParam, false);
                    npcAnimator.SetFloat(SpeedParam, 0f);
                }
            }
            return;
        }

        if (currentNPCBody == null) return;

        // Đảm bảo scale của model luôn bám sát theo standingScale
        if (currentNPCBody.transform.parent == transform)
        {
            currentNPCBody.transform.localPosition = Vector3.zero;
            if (currentNPCBody.transform.localScale != standingScale)
            {
                currentNPCBody.transform.localScale = standingScale;
            }
        }

        // Đồng bộ bước đi khi người chơi di chuyển (WASD)
        bool isWalking = false;
        float moveSpeed = 0f;

        if (Keyboard.current != null)
        {
            isWalking = Keyboard.current.wKey.isPressed || Keyboard.current.sKey.isPressed ||
                        Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed ||
                        Keyboard.current.upArrowKey.isPressed || Keyboard.current.downArrowKey.isPressed ||
                        Keyboard.current.leftArrowKey.isPressed || Keyboard.current.rightArrowKey.isPressed;

            bool isRunning = Keyboard.current.leftShiftKey.isPressed;
            moveSpeed = isRunning ? 2.0f : 1.0f;
        }

        if (npcAnimator != null && npcAnimator.runtimeAnimatorController != null)
        {
            npcAnimator.SetBool(IsSittingParam, false);
            npcAnimator.SetBool(IsWalkingParam, isWalking);
            npcAnimator.SetFloat(SpeedParam, isWalking ? moveSpeed : 0f);
        }
    }

    void LateUpdate()
    {
        // Khi đang ngồi trong tàu lượn: Giữ chắc nhân vật tại SitPoint của ghế
        if (isSeated && currentNPCBody != null && currentNPCBody.transform.parent != null)
        {
            // Đảm bảo scale luôn là seatedScale (1.22x)
            if (currentNPCBody.transform.localScale != seatedScale)
            {
                currentNPCBody.transform.localScale = seatedScale;
            }
        }
    }

    public void ResolvePlayerReferences()
    {
        if (xrOriginRig == null)
        {
            xrOriginRig = gameObject;
        }

        if (mainCamera == null)
        {
            mainCamera = GetComponentInChildren<Camera>();
        }

        int layer = LayerMask.NameToLayer(playerBodyLayerName);
        if (layer >= 0)
        {
            playerBodyLayer = layer;
        }
    }

    public void SetupCameraCulling()
    {
        if (mainCamera == null)
        {
            mainCamera = GetComponentInChildren<Camera>();
        }

        if (mainCamera != null)
        {
            // Tắt render Layer của NPC đại diện trên Camera chính của người chơi
            // Giúp người chơi có tầm nhìn FPS/VR thông thoáng 100%, không bị vướng đầu/tay
            // Nhưng trong Scene View vẫn hiển thị đầy đủ mọi bộ phận!
            mainCamera.cullingMask &= ~(1 << playerBodyLayer);
        }
    }

    public void LoadDefaultAssetsIfEmpty()
    {
#if UNITY_EDITOR
        if (masterAnimatorController == null)
        {
            masterAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3D Model/NPC/NPC_Master_Animator.controller");
        }

        if (npcPrefabs == null || npcPrefabs.Length == 0)
        {
            string[] defaultPaths = new string[]
            {
                "Assets/3D Model/NPC/DenysAlmaral/CityPeople/Prefabs/city/casual_Male_G.prefab",
                "Assets/3D Model/NPC/DenysAlmaral/CityPeople/Prefabs/city/casual_Female_G.prefab",
                "Assets/3D Model/NPC/DenysAlmaral/CityPeople/Prefabs/downtown/casual_Male_K.prefab",
                "Assets/3D Model/NPC/DenysAlmaral/CityPeople/Prefabs/downtown/casual_Female_K.prefab",
                "Assets/3D Model/NPC/DenysAlmaral/CityPeople/Prefabs/professions/Doctor_Male_B.prefab",
                "Assets/3D Model/NPC/DenysAlmaral/CityPeople/Prefabs/professions/police_Female_A.prefab",
                "Assets/3D Model/NPC/DenysAlmaral/CityPeople/Prefabs/elder/elder_Female_A.prefab",
                "Assets/3D Model/NPC/DenysAlmaral/CityPeople/Prefabs/little_kids/little_boy_B.prefab",
                "Assets/3D Model/NPC/DenysAlmaral/CityPeople/Prefabs/worker_Male_constructor_B.prefab",
                "Assets/3D Model/NPC/DenysAlmaral/CityPeople/Prefabs/disabilities/prostheticLeg_girl.prefab",
                "Assets/3D Model/NPC/CityPeople_Free/Prefabs/Female_Adult/Female_Adult_ColorA.prefab",
                "Assets/3D Model/NPC/CityPeople_Free/Prefabs/Female_Adult/Female_Adult_ColorB.prefab"
            };

            System.Collections.Generic.List<GameObject> loadedList = new System.Collections.Generic.List<GameObject>();
            foreach (var path in defaultPaths)
            {
                GameObject p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null) loadedList.Add(p);
            }
            npcPrefabs = loadedList.ToArray();
        }
#endif
    }

    public void SpawnPlayerRepresentativeNPC()
    {
        if (npcPrefabs == null || npcPrefabs.Length == 0) return;

        // Chọn ngẫu nhiên 1 model
        int rand = Random.Range(0, npcPrefabs.Length);
        chosenPlayerPrefabIndex = rand;
        chosenPlayerPrefab = npcPrefabs[rand];
        if (chosenPlayerPrefab == null) return;

        currentNPCBody = Instantiate(chosenPlayerPrefab, transform);
        currentNPCBody.name = "[PLAYER_BODY] " + chosenPlayerPrefab.name;
        currentNPCBody.transform.localPosition = Vector3.zero;
        currentNPCBody.transform.localRotation = Quaternion.identity;
        currentNPCBody.transform.localScale = standingScale;

        // Đặt toàn bộ Mesh sang Layer ẩn khỏi Main Camera
        SetLayerRecursively(currentNPCBody, playerBodyLayer);

        // Vô hiệu hóa các script di chuyển/collider thừa trên model
        DisableNPCInternalMovement(currentNPCBody);

        npcAnimator = currentNPCBody.GetComponent<Animator>();
        if (npcAnimator != null)
        {
            if (masterAnimatorController == null)
            {
                LoadDefaultAssetsIfEmpty();
            }
            if (masterAnimatorController != null)
            {
                npcAnimator.runtimeAnimatorController = masterAnimatorController;
            }
            npcAnimator.applyRootMotion = false;
            if (npcAnimator.runtimeAnimatorController != null)
            {
                npcAnimator.SetBool(IsSittingParam, false);
                npcAnimator.SetBool(IsWalkingParam, false);
            }
        }

        isSeated = false;
        Debug.Log($"<color=#00FFCC><b>[PlayerNPCBodyController] Đã gắn NPC đại diện '{chosenPlayerPrefab.name}' vào người chơi với Standing Scale {standingScale.x}!</b></color>");
    }

    public void SetSeatedInCoaster(Transform sitPoint, Vector3 offset, Vector3 scale, Vector3 rotOffset)
    {
        if (currentNPCBody == null)
        {
            SpawnPlayerRepresentativeNPC();
        }

        if (currentNPCBody != null && sitPoint != null)
        {
            isSeated = true;

            // Xóa triệt để các script xung đột như CityPeople, Wanderer, Colliders
            DisableNPCInternalMovement(currentNPCBody);

            currentNPCBody.transform.SetParent(sitPoint, false);
            currentNPCBody.transform.localPosition = Vector3.zero;
            currentNPCBody.transform.localRotation = Quaternion.Euler(rotOffset);
            currentNPCBody.transform.localScale = (scale.sqrMagnitude > 0.01f) ? scale : seatedScale;

            SetLayerRecursively(currentNPCBody, playerBodyLayer);

            if (npcAnimator == null) npcAnimator = currentNPCBody.GetComponent<Animator>();
            if (npcAnimator != null)
            {
                if (masterAnimatorController != null)
                {
                    npcAnimator.runtimeAnimatorController = masterAnimatorController;
                }
                npcAnimator.applyRootMotion = false;
                npcAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                npcAnimator.SetBool(IsSittingParam, true);
                npcAnimator.SetBool(IsWalkingParam, false);
                npcAnimator.SetFloat(SpeedParam, 0f);
                npcAnimator.SetBool(IsThrilledParam, false);
                npcAnimator.SetLayerWeight(1, 0f);
                npcAnimator.Play("Sitting", 0, 0f);
                npcAnimator.Update(0f); // Ép Animator tính toán tư thế ngồi ngay lập tức để đọc vị trí xương mông
            }

            // TÍNH CHỖ NGỒI BẰNG MÔNG (HIPS/PELVIS):
            // Dịch chuyển vị trí root sao cho xương Mông (Hips) trùng khớp 100% với SitPoint của đệm ghế
            Transform hips = GetHipsTransform();
            if (hips != null)
            {
                Vector3 hipWorldOffset = hips.position - sitPoint.position;
                currentNPCBody.transform.position -= hipWorldOffset;
            }
            else
            {
                // Fallback nếu không đo được xương: hạ root xuống theo chiều cao mông ngồi
                currentNPCBody.transform.localPosition = new Vector3(0f, -0.58f * currentNPCBody.transform.localScale.y, 0f);
            }

            if (offset != Vector3.zero)
            {
                currentNPCBody.transform.position += sitPoint.TransformDirection(offset);
            }
        }
    }

    public void SetThrilled(bool thrilled)
    {
        if (npcAnimator != null)
        {
            npcAnimator.SetBool(IsThrilledParam, thrilled);
            npcAnimator.SetLayerWeight(1, thrilled ? 1f : 0f);
        }
    }

    public void ReturnToPlayerRig()
    {
        if (currentNPCBody != null)
        {
            isSeated = false;
            currentNPCBody.transform.SetParent(transform);
            currentNPCBody.transform.localPosition = Vector3.zero;
            currentNPCBody.transform.localRotation = Quaternion.identity;
            currentNPCBody.transform.localScale = standingScale;

            SetLayerRecursively(currentNPCBody, playerBodyLayer);

            if (npcAnimator != null)
            {
                npcAnimator.SetBool(IsSittingParam, false);
                npcAnimator.SetBool(IsWalkingParam, false);
                npcAnimator.SetBool(IsThrilledParam, false);
                npcAnimator.SetLayerWeight(1, 0f);
                npcAnimator.Play("Idle", 0, 0f);
                npcAnimator.Update(0f);
            }

            AlignCameraToHead();
        }
    }

    public static void SetLayerRecursively(GameObject go, int newLayer)
    {
        if (go == null) return;
        go.layer = newLayer;
        foreach (Transform child in go.transform)
        {
            if (child != null) SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    private void DisableNPCInternalMovement(GameObject npc)
    {
        if (npc == null) return;

        ParkNPCWanderer wanderer = npc.GetComponent<ParkNPCWanderer>();
        if (wanderer != null) DestroyImmediate(wanderer);

        CharacterController cc = npc.GetComponent<CharacterController>();
        if (cc != null) DestroyImmediate(cc);

        // Hủy triệt để script CityPeople để tránh việc CityPeople.Start() chạy ShuffleClips / CrossFade đè lên tư thế ngồi
        MonoBehaviour[] scripts = npc.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in scripts)
        {
            if (mb != null && (mb.GetType().Name == "CityPeople" || mb.GetType().FullName.Contains("CityPeople")))
            {
                DestroyImmediate(mb);
            }
        }

        Collider[] cols = npc.GetComponentsInChildren<Collider>(true);
        foreach (var col in cols)
        {
            DestroyImmediate(col);
        }
    }
}
