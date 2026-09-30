using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quản lý 4 NPC ngẫu nhiên (hoàn toàn không trùng lặp) ngồi trong 4 ghế tàu lượn:
/// - Sử dụng 4 điểm đệm ghế SitPoints độc lập: GIỮ NGUYÊN 100% tọa độ do bạn chỉnh sửa trong Inspector/Scene, KHÔNG BAO GIỜ tự ý ghi đè/reset về mặc định!
/// - Khi bấm Play: NPC sẽ được đặt CHÍNH XÁC tại vị trí SitPoint của bạn (hipToSitPointOffset = (0,0,0)).
/// - Không sinh NPC xem trước trong Editor để tránh lỗi animation đứng.
/// - Giữ nguyên Scale 1.22x.
/// - Đồng bộ XR Camera rơi đúng tầm mắt người ngồi và ẩn đầu NPC đại diện.
/// - Tự động giơ 2 tay khi gặp biến thiên độ cao (đổ dốc / lượn dốc).
/// </summary>
public class CoasterPassengerManager : MonoBehaviour
{
    [Header("1. DANH SÁCH PREFAB NPC")]
    [Tooltip("Danh sách các mẫu NPC để chọn ngẫu nhiên 4 người không trùng lặp")]
    public GameObject[] npcPrefabs;

    [Tooltip("Animator Controller chuẩn có sẵn các layer Sitting và Thrill Reaction")]
    public RuntimeAnimatorController masterAnimatorController;

    [Header("2. VỊ TRÍ GHẾ GỐC TRÊN TÀU")]
    public Transform[] seats;

    [Header("3. 4 ĐIỂM ĐỆM GHẾ CHUYÊN DỤNG (SIT POINTS)")]
    [Tooltip("4 điểm đại diện cho đáy lòng đệm ghế do bạn tùy chỉnh vị trí: SitPoint_FrontLeft, SitPoint_FrontRight, SitPoint_BackLeft, SitPoint_BackRight")]
    public Transform[] sitPoints = new Transform[4];

    [Header("4. CĂN CHỈNH TƯ THẾ NGỒI THEO SIT POINT")]
    [Tooltip("Tỷ lệ Scale NPC khi ngồi trong tàu")]
    public Vector3 npcScale = new Vector3(1.22f, 1.22f, 1.22f);

    [Tooltip("Tỷ lệ Scale khi đứng ngoài sảnh ga (2.93x tương ứng với scale đoàn tàu và nhà ga)")]
    public Vector3 stationStandingScale = new Vector3(2.928f, 2.928f, 2.928f);

    [Tooltip("Độ lệch vị trí gốc model so với SitPoint (Mặc định Vector3.zero để NPC bám chính xác 100% vào vị trí SitPoint bạn kéo trong Scene)")]
    public Vector3 hipToSitPointOffset = Vector3.zero;

    [Tooltip("Vị trí mặc định ban đầu khi TẠO MỚI SitPoint lần đầu (nếu chưa có trong Scene)")]
    public Vector3 defaultNewSitPointOffset = new Vector3(0f, -0.85f, 0.12f);

    public Vector3 seatRotationOffset = Vector3.zero;

    [Header("5. TẦM MẮT NGƯỜI CHƠI (SEATED EYE ALIGNMENT)")]
    [Tooltip("Độ lệch mắt so với xương đầu Head Bone")]
    public float eyeForwardOffset = 0.08f;
    public float eyeHeightOffset = 0.03f;

    [Header("6. CẢM GIÁC MẠNH KHI BIẾN THIÊN ĐỘ CAO (THRILL REACTION)")]
    public float verticalDropVelocityThreshold = -0.8f;
    public float slopePitchDropThreshold = 0.12f;
    public float suddenAltitudeChangeThreshold = 2.2f;
    public float thrillHoldDuration = 0.6f;

    [Header("7. LIÊN KẾT RIDE CONTROLLER")]
    public RideController rideController;

    [Header("8. ĐIỂM ĐỨNG NGOÀI SẢNH GA (STATION EXIT POINTS)")]
    [Tooltip("4 điểm đứng của 4 hành khách ngoài sảnh ga khi kết thúc chuyến tàu (nếu để trống script sẽ tự tính tỏa đều ra 2 bên sàn ga)")]
    public Transform[] stationExitPoints = new Transform[4];

    // Danh sách 4 đối tượng NPC thực tế đang ngồi trên 4 ghế
    [SerializeField] private GameObject[] spawnedPassengerObjects = new GameObject[4];
    private Animator[] passengerAnimators = new Animator[4];
    private Transform[] passengerHeadBones = new Transform[4];

    private int currentPlayerSeatIndex = -1;
    private bool isPlayerSeated = false;
    private bool isCurrentlyThrilled = false;
    private float thrillHoldTimer = 0f;
    private float lastCartPosY = 0f;
    private bool hasRecordedLastPos = false;

    private static readonly int IsSittingParam = Animator.StringToHash("IsSitting");
    private static readonly int IsWalkingParam = Animator.StringToHash("IsWalking");
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int IsThrilledParam = Animator.StringToHash("IsThrilled");

    void Awake()
    {
        InitializeSeatReferences();
        EnsureSitPoints();
        LoadDefaultAssetsIfEmpty();

        if (rideController == null)
        {
            rideController = GetComponent<RideController>();
            if (rideController == null) rideController = GetComponentInParent<RideController>();
            if (rideController == null) rideController = Object.FindAnyObjectByType<RideController>();
        }
    }

    void Start()
    {
        EnsureSitPoints();

        // Không sinh trước NPC khi mới bắt đầu game ở công viên.
        // Chỉ sinh 3 NPC vào 3 ghế còn lại khi người chơi đến ga chọn ghế ngồi!

        Vector3 currentPos = GetCurrentCartPosition();
        lastCartPosY = currentPos.y;
        hasRecordedLastPos = true;
    }

    void Update()
    {
        DetectHeightVariationAndThrill();
    }

    /// <summary>
    /// Vẽ Gizmo trực quan 4 điểm đệm ghế SitPoints và 4 điểm đứng ngoài sàn ga
    /// </summary>
    void OnDrawGizmos()
    {
        if (sitPoints != null)
        {
            for (int i = 0; i < sitPoints.Length; i++)
            {
                Transform sp = sitPoints[i];
                if (sp != null)
                {
                    // Vẽ quả cầu màu Cyan tại mặt đệm ghế
                    Gizmos.color = new Color(0f, 0.9f, 1f, 0.8f);
                    Gizmos.DrawWireSphere(sp.position, 0.12f);
                    Gizmos.DrawRay(sp.position, sp.forward * 0.35f);

                    // Vẽ hộp đệm ngồi màu xanh lá mờ
                    Gizmos.color = new Color(0f, 1f, 0.4f, 0.3f);
                    Gizmos.DrawCube(sp.position, new Vector3(0.45f, 0.08f, 0.45f));
                }
            }
        }

        if (stationExitPoints != null)
        {
            for (int i = 0; i < stationExitPoints.Length; i++)
            {
                Transform ep = stationExitPoints[i];
                if (ep != null)
                {
                    Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
                    Gizmos.DrawWireSphere(ep.position, 0.2f);
                    Gizmos.DrawRay(ep.position, ep.forward * 0.5f);
                }
            }
        }
    }

    /// <summary>
    /// Tự động tạo hoặc lấy 4 điểm đệm ghế SitPoint. 
    /// TUYỆT ĐỐI KHÔNG GHI ĐÈ VỊ TRÍ ĐÃ ĐƯỢC NGƯỜI DÙNG TÙY CHỈNH THỦ CÔNG!
    /// </summary>
    public void EnsureSitPoints()
    {
        InitializeSeatReferences();

        if (sitPoints == null || sitPoints.Length != 4)
        {
            sitPoints = new Transform[4];
        }

        string[] sitPointNames = new string[] { "SitPoint_FrontLeft", "SitPoint_FrontRight", "SitPoint_BackLeft", "SitPoint_BackRight" };

        for (int i = 0; i < 4; i++)
        {
            // 1. Nếu đã có tham chiếu hợp lệ thì giữ nguyên, không đụng vào transform
            Transform sp = (sitPoints != null && i < sitPoints.Length) ? sitPoints[i] : null;

            if (sp == null)
            {
                Transform parentSeat = (seats != null && i < seats.Length && seats[i] != null) ? seats[i] : transform;
                sp = parentSeat.Find(sitPointNames[i]);
                if (sp == null)
                {
                    sp = FindChildRecursive(transform, sitPointNames[i]);
                }
            }

            // 2. Chỉ tạo mới khi trong Scene hoàn toàn chưa có GameObject này
            if (sp == null)
            {
                Transform parentSeat = (seats != null && i < seats.Length && seats[i] != null) ? seats[i] : transform;
                GameObject spObj = new GameObject(sitPointNames[i]);
                spObj.transform.SetParent(parentSeat, false);
                spObj.transform.localPosition = defaultNewSitPointOffset;
                spObj.transform.localRotation = Quaternion.identity;
                spObj.transform.localScale = Vector3.one;
                sp = spObj.transform;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Undo.RegisterCreatedObjectUndo(spObj, "Create SitPoint");
                }
#endif
            }

            // Gán vào mảng và TUYỆT ĐỐI KHÔNG RESET / GHI ĐÈ LOCALPOSITION ĐÃ CHỈNH SỬA
            sitPoints[i] = sp;
        }
    }

    public Transform GetSitPoint(int seatIndex)
    {
        EnsureSitPoints();
        if (seatIndex >= 0 && seatIndex < sitPoints.Length && sitPoints[seatIndex] != null)
        {
            return sitPoints[seatIndex];
        }
        if (seats != null && seatIndex >= 0 && seatIndex < seats.Length && seats[seatIndex] != null)
        {
            return seats[seatIndex];
        }
        return transform;
    }

    /// <summary>
    /// Lấy vị trí tầm mắt chuẩn thực tế từ xương đầu (Head Bone) của NPC tại ghế chỉ định
    /// </summary>
    public Vector3 GetPlayerHeadEyePosition(int seatIndex)
    {
        if (seatIndex >= 0 && seatIndex < passengerHeadBones.Length && passengerHeadBones[seatIndex] != null)
        {
            Transform head = passengerHeadBones[seatIndex];
            return head.position + (head.forward * eyeForwardOffset) + (Vector3.up * eyeHeightOffset);
        }

        Transform sp = GetSitPoint(seatIndex);
        if (sp != null)
        {
            return sp.position + (sp.up * 0.90f) + (sp.forward * 0.08f);
        }
        return transform.position + Vector3.up * 0.85f;
    }

    /// <summary>
    /// <summary>
    /// <summary>
    /// Gán người chơi vào ghế được chọn và sinh 3 NPC ngẫu nhiên (không trùng người chơi) vào 3 ghế còn lại
    /// </summary>
    public void SetPlayerPassenger(int seatIndex, Transform xrOriginParent)
    {
        EnsureSitPoints();
        LoadDefaultAssetsIfEmpty();

        currentPlayerSeatIndex = Mathf.Clamp(seatIndex, 0, 3);
        isPlayerSeated = true;

        Transform sp = GetSitPoint(currentPlayerSeatIndex);

        // 1. Đặt avatar người chơi vào ghế đã chọn
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        GameObject playerModelPrefab = null;
        if (playerBody != null)
        {
            playerBody.SetSeatedInCoaster(sp, hipToSitPointOffset, npcScale, seatRotationOffset);
            playerModelPrefab = playerBody.chosenPlayerPrefab;
        }

        // 2. Dọn dẹp các NPC hành khách cũ trên tàu
        ClearOldPassengers();

        // 3. Chuẩn bị pool 3 NPC khác biệt hoàn toàn với model của người chơi
        List<GameObject> pool = new List<GameObject>();
        if (npcPrefabs != null)
        {
            foreach (var p in npcPrefabs)
            {
                if (p == null) continue;
                if (playerModelPrefab != null && (p == playerModelPrefab || p.name == playerModelPrefab.name)) continue;
                if (!pool.Contains(p)) pool.Add(p);
            }
        }

        // Trộn ngẫu nhiên (Fisher-Yates Shuffle)
        for (int i = 0; i < pool.Count; i++)
        {
            int r = Random.Range(i, pool.Count);
            GameObject tmp = pool[i];
            pool[i] = pool[r];
            pool[r] = tmp;
        }

        // 4. Sinh 3 NPC lấp đầy 3 ghế còn lại
        int poolIdx = 0;
        for (int i = 0; i < 4; i++)
        {
            if (i == currentPlayerSeatIndex)
            {
                spawnedPassengerObjects[i] = null;
                passengerAnimators[i] = null;
                passengerHeadBones[i] = null;
                continue;
            }

            Transform seatTransform = GetSitPoint(i);
            if (seatTransform == null) continue;

            GameObject prefabToSpawn = (pool.Count > 0) ? pool[poolIdx % pool.Count] : null;
            poolIdx++;

            if (prefabToSpawn != null)
            {
                GameObject npcInstance = Instantiate(prefabToSpawn, seatTransform);
                npcInstance.name = $"Passenger_{i}_{prefabToSpawn.name}";
                npcInstance.transform.localPosition = hipToSitPointOffset;
                npcInstance.transform.localRotation = Quaternion.Euler(seatRotationOffset);
                npcInstance.transform.localScale = npcScale;

                DisableUnneededComponents(npcInstance);

                Animator anim = npcInstance.GetComponent<Animator>();
                if (anim != null)
                {
                    if (masterAnimatorController != null)
                    {
                        anim.runtimeAnimatorController = masterAnimatorController;
                    }
                    anim.applyRootMotion = false;
                    anim.SetBool(IsSittingParam, true);
                    anim.SetBool(IsWalkingParam, false);
                    anim.SetFloat(SpeedParam, 0f);
                    anim.SetBool(IsThrilledParam, false);
                }

                Transform head = null;
                if (anim != null && anim.isHuman)
                {
                    head = anim.GetBoneTransform(HumanBodyBones.Head);
                }
                if (head == null) head = FindChildRecursive(npcInstance.transform, "Head");
                if (head == null) head = FindChildRecursive(npcInstance.transform, "head");
                if (head == null) head = FindChildRecursive(npcInstance.transform, "mixamorig:Head");

                spawnedPassengerObjects[i] = npcInstance;
                passengerAnimators[i] = anim;
                passengerHeadBones[i] = head;
            }
        }

        Debug.Log($"<color=#00FF88><b>[CoasterPassengerManager] Đã xếp người chơi vào ghế {seatIndex} và sinh 3 NPC khác vào 3 ghế còn lại!</b></color>");
    }

    /// <summary>
    /// Trả NPC đại diện cho người chơi về lại ghế gốc khi rời tàu
    /// </summary>
    public void ReleasePlayerPassenger()
    {
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        if (playerBody != null)
        {
            playerBody.ReturnToPlayerRig();
        }

        isPlayerSeated = false;
        currentPlayerSeatIndex = -1;
    }

    /// <summary>
    /// Đưa toàn bộ hành khách ra khỏi tàu và kích hoạt cho họ đi dạo tự nhiên trong sảnh ga
    /// </summary>
    public void UnseatAllPassengersToStation(Transform stationRef)
    {
        // 1. Giải phóng người chơi trước để không bị kéo theo NPC
        ReleasePlayerPassenger();

        Vector3 basePos = (stationRef != null) ? stationRef.position : transform.position;
        Quaternion baseRot = (stationRef != null) ? stationRef.rotation : transform.rotation;
        Vector3 rightDir = (stationRef != null) ? stationRef.right : transform.right;
        Vector3 fwdDir = (stationRef != null) ? stationRef.forward : transform.forward;

        // Vị trí đứng xuất phát ở hành lang sảnh ga (lùi về phía sau và dạt sang hai bên sảnh, TUYỆT ĐỐI không đứng trước mặt bảng UI)
        Vector3[] safeStationSpawnOffsets = new Vector3[]
        {
            rightDir * 3.2f - fwdDir * 1.5f,
            rightDir * 4.2f - fwdDir * 3.0f,
            -rightDir * 3.2f - fwdDir * 1.5f,
            -rightDir * 4.2f - fwdDir * 3.0f
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject npcObj = spawnedPassengerObjects[i];
            if (npcObj == null) continue;

            // 1. Tháo NPC ra khỏi ghế tàu
            npcObj.transform.SetParent(null);

            // 2. Đặt vị trí ra sàn ga an toàn (không chắn tầm nhìn UI)
            if (stationExitPoints != null && i < stationExitPoints.Length && stationExitPoints[i] != null)
            {
                npcObj.transform.SetPositionAndRotation(stationExitPoints[i].position, stationExitPoints[i].rotation);
            }
            else
            {
                Vector3 targetPos = basePos + safeStationSpawnOffsets[i];
                Quaternion targetRot = baseRot * Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f);
                npcObj.transform.SetPositionAndRotation(targetPos, targetRot);
            }

            // 3. Đặt Scale đứng 1:1 chuẩn người thật
            npcObj.transform.localScale = Vector3.one;

            // 4. Bật lại đầu đầy đủ
            if (passengerHeadBones[i] != null)
            {
                passengerHeadBones[i].localScale = Vector3.one;
            }

            // 5. Bật CharacterController và Collider
            CharacterController cc = npcObj.GetComponent<CharacterController>();
            if (cc == null) cc = npcObj.AddComponent<CharacterController>();
            cc.radius = 0.28f;
            cc.height = 1.6f;
            cc.center = new Vector3(0, 0.8f, 0);
            cc.enabled = true;

            Collider[] colliders = npcObj.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col != cc) col.enabled = true;
            }

            // 6. Gán / Bật ParkNPCWanderer để NPC thực sự đi dạo trong sảnh ga
            ParkNPCWanderer wanderer = npcObj.GetComponent<ParkNPCWanderer>();
            if (wanderer == null) wanderer = npcObj.AddComponent<ParkNPCWanderer>();

            wanderer.boundaryMode = ParkNPCWanderer.BoundaryMode.RadiusAroundCenter;
            wanderer.centerPoint = stationRef;
            wanderer.maxRadius = 3.5f;
            wanderer.walkSpeed = Random.Range(1.15f, 1.4f);
            wanderer.minWaitTime = 2.0f;
            wanderer.maxWaitTime = 5.0f;
            wanderer.enabled = true;

            // 7. Cập nhật Animator
            Animator anim = passengerAnimators[i];
            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.SetBool(IsSittingParam, false);
                anim.SetBool(IsThrilledParam, false);
                anim.SetBool(IsWalkingParam, true);
                anim.SetFloat(SpeedParam, wanderer.walkSpeed);
            }
        }

        Debug.Log("<color=#00FF99><b>[CoasterPassengerManager] Đã đưa toàn bộ 4 NPC ra sảnh ga và kích hoạt đi dạo tự do!</b></color>");
    }

    private BoxCollider[] FindStationWalkZones()
    {
        List<BoxCollider> stationZones = new List<BoxCollider>();
        BoxCollider[] allBoxes = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
        foreach (var box in allBoxes)
        {
            if (box == null) continue;
            string objName = box.gameObject.name.ToLower();
            if (objName.Contains("walkzone") || objName.Contains("walk_zone") || objName.Contains("playzone"))
            {
                stationZones.Add(box);
            }
        }
        return stationZones.ToArray();
    }

    private void RestorePassengerToSeat(int index)
    {
        if (index < 0 || index >= 4) return;

        GameObject npcObj = spawnedPassengerObjects[index];
        Transform sp = GetSitPoint(index);

        if (npcObj != null && sp != null)
        {
            npcObj.transform.SetParent(sp);
            npcObj.transform.localPosition = hipToSitPointOffset;
            npcObj.transform.localRotation = Quaternion.Euler(seatRotationOffset);
            npcObj.transform.localScale = npcScale;
        }

        // Bật lại đầu bình thường
        if (passengerHeadBones[index] != null)
        {
            passengerHeadBones[index].localScale = Vector3.one;
        }
    }

    public void TriggerThrillReaction(float duration = 1.0f)
    {
        thrillHoldTimer = Mathf.Max(thrillHoldTimer, duration);
        SetAllPassengersThrilled(true);
    }

    /// <summary>
    /// Tự động phát hiện biến thiên độ cao (đổ dốc, góc chúi, lượn dốc nhanh) để kích hoạt giơ 2 tay
    /// </summary>
    private void DetectHeightVariationAndThrill()
    {
        bool isRiding = (rideController != null && rideController.currentState == RideController.RideState.Riding);

        if (!isRiding)
        {
            thrillHoldTimer = 0f;
            SetAllPassengersThrilled(false);
            return;
        }

        Vector3 currentPos = GetCurrentCartPosition();

        if (!hasRecordedLastPos)
        {
            lastCartPosY = currentPos.y;
            hasRecordedLastPos = true;
            return;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime > 0f)
        {
            float verticalVelocity = (currentPos.y - lastCartPosY) / deltaTime;
            lastCartPosY = currentPos.y;

            Transform cartTransform = GetCartTransform();

            // 1. Góc chúi đầu xuống dốc (Pitch Angle)
            float pitchDot = (cartTransform != null) ? Vector3.Dot(cartTransform.forward, Vector3.down) : 0f;
            bool isDippingDown = (pitchDot > 0.08f);

            // 2. Lao dốc tụt độ cao nhanh
            bool isFallingDown = (verticalVelocity < -0.25f);

            // 3. Biến thiên độ cao đột ngột (G-force / Lượn dốc mạnh)
            bool isSuddenChange = (Mathf.Abs(verticalVelocity) > 1.2f);

            if (isFallingDown || isDippingDown || isSuddenChange)
            {
                thrillHoldTimer = Mathf.Max(thrillHoldTimer, 1.0f);
            }
        }

        if (thrillHoldTimer > 0f)
        {
            thrillHoldTimer -= Time.deltaTime;
            SetAllPassengersThrilled(true);
        }
        else
        {
            SetAllPassengersThrilled(false);
        }
    }

    private Vector3 GetCurrentCartPosition()
    {
        if (sitPoints != null && sitPoints.Length > 0 && sitPoints[0] != null)
        {
            return sitPoints[0].position;
        }
        if (seats != null && seats.Length > 0 && seats[0] != null)
        {
            return seats[0].position;
        }
        return transform.position;
    }

    private Transform GetCartTransform()
    {
        if (sitPoints != null && sitPoints.Length > 0 && sitPoints[0] != null)
        {
            return sitPoints[0].parent != null ? sitPoints[0].parent : sitPoints[0];
        }
        return transform;
    }

    public void InitializeSeatReferences()
    {
        if (seats != null && seats.Length == 4 && seats[0] != null) return;

        SeatSwitcher switcher = GetComponent<SeatSwitcher>();
        if (switcher == null) switcher = GetComponentInParent<SeatSwitcher>();
        if (switcher == null) switcher = Object.FindAnyObjectByType<SeatSwitcher>();

        if (switcher != null && switcher.seats != null && switcher.seats.Length == 4 && switcher.seats[0] != null)
        {
            seats = switcher.seats;
            return;
        }

        List<Transform> foundSeats = new List<Transform>();
        string[] standardSeatNames = new string[] { "Seat_FrontLeft", "Seat_FrontRight", "Seat_BackLeft", "Seat_BackRight" };

        foreach (var seatName in standardSeatNames)
        {
            Transform s = FindChildRecursive(transform, seatName);
            if (s != null) foundSeats.Add(s);
        }

        if (foundSeats.Count == 4)
        {
            seats = foundSeats.ToArray();
        }
    }

    public void LoadDefaultAssetsIfEmpty()
    {
#if UNITY_EDITOR
        if (masterAnimatorController == null)
        {
            masterAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3D Model/NPC/NPC_Master_Animator.controller");
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
                "Assets/3D Model/NPC/CityPeople_Free/Prefabs/Female_Adult/Female_Adult_ColorB.prefab",
                "Assets/3D Model/NPC/CityPeople_Free/Prefabs/Female_Adult/Female_Adult_CustomSkinA.prefab",
                "Assets/3D Model/NPC/CityPeople_Free/Prefabs/Female_Adult/Female_Adult_CustomSkinB.prefab"
            };

            List<GameObject> loadedList = new List<GameObject>();
            foreach (var path in defaultPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) loadedList.Add(prefab);
            }

            if (loadedList.Count > 0)
            {
                npcPrefabs = loadedList.ToArray();
            }
        }
#endif
    }

    /// <summary>
    /// Sinh ngẫu nhiên 4 NPC hoàn toàn khác nhau đặt vào 4 SitPoint của tàu (chỉ khi Play mode)
    /// </summary>
    public void SpawnRandomPassengers()
    {
        EnsureSitPoints();
        LoadDefaultAssetsIfEmpty();

        if (sitPoints == null || sitPoints.Length < 4 || sitPoints[0] == null)
        {
            Debug.LogWarning("[CoasterPassengerManager] Chưa tạo đủ 4 SitPoint trên tàu!");
            return;
        }

        // 1. Dọn dẹp các NPC cũ nếu có
        ClearOldPassengers();

        // 2. Tạo danh sách pool ngẫu nhiên không trùng lặp
        List<GameObject> pool = new List<GameObject>();
        if (npcPrefabs != null && npcPrefabs.Length > 0)
        {
            foreach (var p in npcPrefabs)
            {
                if (p != null && !pool.Contains(p))
                {
                    pool.Add(p);
                }
            }
        }

        if (pool.Count == 0)
        {
            Debug.LogWarning("[CoasterPassengerManager] Danh sách npcPrefabs đang trống!");
            return;
        }

        // Trộn ngẫu nhiên hoàn toàn (Fisher-Yates Shuffle)
        for (int i = 0; i < pool.Count; i++)
        {
            int randomIndex = Random.Range(i, pool.Count);
            GameObject temp = pool[i];
            pool[i] = pool[randomIndex];
            pool[randomIndex] = temp;
        }

        // 3. Gắn 4 NPC độc nhất vào 4 SitPoint
        for (int i = 0; i < 4; i++)
        {
            Transform sitPointTransform = sitPoints[i];
            if (sitPointTransform == null) continue;

            GameObject prefabToSpawn = pool[i % pool.Count];
            GameObject npcInstance = Instantiate(prefabToSpawn, sitPointTransform);
            npcInstance.name = $"Passenger_{i}_{prefabToSpawn.name}";

            // Cố định vị trí ngồi: Đặt chính xác 100% tại tọa độ SitPoint
            npcInstance.transform.localPosition = hipToSitPointOffset;
            npcInstance.transform.localRotation = Quaternion.Euler(seatRotationOffset);
            npcInstance.transform.localScale = npcScale;

            // Vô hiệu hóa các script di chuyển/vật lý để NPC ngồi yên theo tàu
            DisableUnneededComponents(npcInstance);

            // Cố định Animator tư thế ngồi
            Animator anim = npcInstance.GetComponent<Animator>();
            if (anim != null)
            {
                if (masterAnimatorController != null)
                {
                    anim.runtimeAnimatorController = masterAnimatorController;
                }
                anim.applyRootMotion = false;
                anim.SetBool(IsSittingParam, true);
                anim.SetBool(IsWalkingParam, false);
                anim.SetFloat(SpeedParam, 0f);
                anim.SetBool(IsThrilledParam, false);
            }

            // Tìm xương đầu (Head Bone)
            Transform head = null;
            if (anim != null && anim.isHuman)
            {
                head = anim.GetBoneTransform(HumanBodyBones.Head);
            }
            if (head == null) head = FindChildRecursive(npcInstance.transform, "Head");
            if (head == null) head = FindChildRecursive(npcInstance.transform, "head");
            if (head == null) head = FindChildRecursive(npcInstance.transform, "mixamorig:Head");

            spawnedPassengerObjects[i] = npcInstance;
            passengerAnimators[i] = anim;
            passengerHeadBones[i] = head;
        }

        Debug.Log($"<color=#00FF88><b>[CoasterPassengerManager] Đã gắn 4 NPC ngồi chuẩn theo các điểm SitPoints của bạn!</b></color>");
    }

    public void ClearOldPassengers()
    {
        for (int i = 0; i < spawnedPassengerObjects.Length; i++)
        {
            if (spawnedPassengerObjects[i] != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(spawnedPassengerObjects[i]);
                }
                else
                {
                    Destroy(spawnedPassengerObjects[i]);
                }
#else
                Destroy(spawnedPassengerObjects[i]);
#endif
                spawnedPassengerObjects[i] = null;
            }
        }

        List<Transform> parentsToCheck = new List<Transform>();
        if (sitPoints != null) parentsToCheck.AddRange(sitPoints);
        if (seats != null) parentsToCheck.AddRange(seats);

        foreach (var parent in parentsToCheck)
        {
            if (parent == null) continue;
            for (int c = parent.childCount - 1; c >= 0; c--)
            {
                Transform child = parent.GetChild(c);
                if (child.name.StartsWith("Passenger_"))
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(child.gameObject);
                    else Destroy(child.gameObject);
#else
                    Destroy(child.gameObject);
#endif
                }
            }
        }

        // Dọn dẹp cả những NPC đang đứng ngoài sảnh ga (parent == null)
        if (Application.isPlaying)
        {
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allObjects)
            {
                if (go != null && go.name.StartsWith("Passenger_") && (go.transform.parent == null || go.transform.parent.name.Contains("Station")))
                {
                    Destroy(go);
                }
            }
        }
    }

    private void CachePassengerComponents()
    {
        for (int i = 0; i < 4; i++)
        {
            if (spawnedPassengerObjects != null && i < spawnedPassengerObjects.Length && spawnedPassengerObjects[i] != null)
            {
                GameObject npc = spawnedPassengerObjects[i];
                Animator anim = npc.GetComponent<Animator>();
                passengerAnimators[i] = anim;

                Transform head = null;
                if (anim != null && anim.isHuman)
                {
                    head = anim.GetBoneTransform(HumanBodyBones.Head);
                }
                if (head == null) head = FindChildRecursive(npc.transform, "Head");
                if (head == null) head = FindChildRecursive(npc.transform, "head");
                if (head == null) head = FindChildRecursive(npc.transform, "mixamorig:Head");
                passengerHeadBones[i] = head;
            }
        }
    }

    private void DisableUnneededComponents(GameObject npc)
    {
        ParkNPCWanderer wanderer = npc.GetComponent<ParkNPCWanderer>();
        if (wanderer != null) wanderer.enabled = false;

        CharacterController cc = npc.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        MonoBehaviour cityPeople = npc.GetComponent("CityPeople") as MonoBehaviour;
        if (cityPeople != null) cityPeople.enabled = false;

        Rigidbody rb = npc.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        Collider[] colliders = npc.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
    }

    public Transform GetPassengerHead(int seatIndex)
    {
        if (seatIndex >= 0 && seatIndex < passengerHeadBones.Length)
        {
            return passengerHeadBones[seatIndex];
        }
        return null;
    }

    public GameObject GetPassengerObject(int seatIndex)
    {
        if (seatIndex >= 0 && seatIndex < spawnedPassengerObjects.Length)
        {
            return spawnedPassengerObjects[seatIndex];
        }
        return null;
    }

    public void SetAllPassengersThrilled(bool isThrilled)
    {
        if (isCurrentlyThrilled == isThrilled) return;
        isCurrentlyThrilled = isThrilled;

        for (int i = 0; i < passengerAnimators.Length; i++)
        {
            if (passengerAnimators[i] != null)
            {
                passengerAnimators[i].SetBool(IsThrilledParam, isThrilled);
            }
        }

        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        if (playerBody != null)
        {
            playerBody.SetThrilled(isThrilled);
        }
    }

    public static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null) return null;
        if (parent.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase)) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }
}
