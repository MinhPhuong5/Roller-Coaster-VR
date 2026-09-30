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
    [Tooltip("Kích thước khi đi dạo ở công viên (1:1 chuẩn người thật)")]
    public Vector3 standingScale = Vector3.one;
    [Tooltip("Kích thước cục bộ khi ngồi trong SitPoint đệm ghế tàu")]
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
    }

    void Update()
    {
        if (isSeated) return;
        if (currentNPCBody == null || npcAnimator == null) return;

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

        npcAnimator.SetBool(IsSittingParam, false);
        npcAnimator.SetBool(IsWalkingParam, isWalking);
        npcAnimator.SetFloat(SpeedParam, isWalking ? moveSpeed : 0f);

        // Giữ vị trí model luôn bám sát chân XR Origin
        if (currentNPCBody.transform.parent == transform)
        {
            currentNPCBody.transform.localPosition = Vector3.zero;
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
        currentNPCBody.name = "Player_Representative_NPC";
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
            if (masterAnimatorController != null)
            {
                npcAnimator.runtimeAnimatorController = masterAnimatorController;
            }
            npcAnimator.applyRootMotion = false;
            npcAnimator.SetBool(IsSittingParam, false);
            npcAnimator.SetBool(IsWalkingParam, false);
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
            currentNPCBody.transform.SetParent(sitPoint);
            currentNPCBody.transform.localPosition = offset;
            currentNPCBody.transform.localRotation = Quaternion.Euler(rotOffset);
            currentNPCBody.transform.localScale = (scale.sqrMagnitude > 0.01f) ? scale : seatedScale;

            SetLayerRecursively(currentNPCBody, playerBodyLayer);

            if (npcAnimator != null)
            {
                npcAnimator.applyRootMotion = false;
                npcAnimator.SetBool(IsSittingParam, true);
                npcAnimator.SetBool(IsWalkingParam, false);
                npcAnimator.SetFloat(SpeedParam, 0f);
            }
        }
    }

    public void SetThrilled(bool thrilled)
    {
        if (npcAnimator != null)
        {
            npcAnimator.SetBool(IsThrilledParam, thrilled);
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
            }
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
        ParkNPCWanderer wanderer = npc.GetComponent<ParkNPCWanderer>();
        if (wanderer != null) Destroy(wanderer);

        CharacterController cc = npc.GetComponent<CharacterController>();
        if (cc != null) Destroy(cc);

        MonoBehaviour cp = npc.GetComponent("CityPeople") as MonoBehaviour;
        if (cp != null) Destroy(cp);

        Collider[] cols = npc.GetComponentsInChildren<Collider>();
        foreach (var col in cols)
        {
            col.enabled = false;
        }
    }
}
