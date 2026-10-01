using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quản lý 4 hành khách (1 người chơi + 3 NPC ngẫu nhiên độc nhất) trên tàu lượn siêu tốc:
/// - Khi ở sảnh ga: Các ghế hoàn toàn trống, KHÔNG sinh trước NPC để tránh xung đột/lỗi.
/// - Khi người chơi bấm chọn ghế:
///   + Đặt NPC đại diện của người chơi vào ghế đã chọn.
///   + Sinh đúng 3 NPC ngẫu nhiên hoàn toàn không trùng lặp và không trùng với người chơi.
///   + Tăng Scale lên 1.22x chuẩn tỷ lệ khoang tàu lượn.
///   + Kích hoạt tư thế ngồi (IsSitting = true).
/// - Khi tàu chạy qua các đoạn biến thiên độ cao / đổ dốc / tốc độ cao:
///   + Cả 3 NPC và người chơi sẽ tự động giơ tay / vẫy tay (IsThrilled = true) tạo cảm giác mạnh chân thực.
/// - Khi kết thúc chuyến tàu: Đưa hành khách ra sảnh ga đi dạo an toàn.
/// </summary>
public class CoasterPassengerManager : MonoBehaviour
{
    [Header("1. DANH SÁCH PREFAB NPC")]
    [Tooltip("Danh sách các mẫu NPC để chọn ngẫu nhiên 3 người không trùng lặp và không trùng với người chơi")]
    public GameObject[] npcPrefabs;

    [Tooltip("Animator Controller chuẩn có sẵn các layer Sitting và Thrill Reaction")]
    public RuntimeAnimatorController masterAnimatorController;

    [Header("2. VỊ TRÍ GHẾ GỐC TRÊN TÀU")]
    public Transform[] seats;

    [Header("3. 4 ĐIỂM ĐỆM GHẾ CHUYÊN DỤNG (SIT POINTS)")]
    [Tooltip("4 điểm đệm ghế: SitPoint_FrontLeft, SitPoint_FrontRight, SitPoint_BackLeft, SitPoint_BackRight")]
    public Transform[] sitPoints = new Transform[4];

    [Header("4. SCALE & CĂN CHỈNH TƯ THẾ NGỒI")]
    [Tooltip("Hệ số Scale tăng kích thước NPC khi ngồi trên tàu lượn (Mặc định 1.22x)")]
    public float scaleMultiplier = 1.22f;

    [Tooltip("Tỷ lệ Scale cục bộ của NPC khi ngồi trong tàu (1.22x)")]
    public Vector3 npcScale = new Vector3(1.22f, 1.22f, 1.22f);

    [Tooltip("Tỷ lệ Scale khi đứng ngoài sảnh ga (1:1 chuẩn người thật)")]
    public Vector3 stationStandingScale = Vector3.one;

    [Tooltip("Độ lệch vị trí so với SitPoint (Mặc định Vector3.zero để bám chuẩn 100% SitPoint)")]
    public Vector3 hipToSitPointOffset = Vector3.zero;

    [Tooltip("Độ lệch góc xoay ghế")]
    public Vector3 seatRotationOffset = Vector3.zero;

    [Header("5. TẦM MẮT NGƯỜI CHƠI (SEATED EYE ALIGNMENT)")]
    public float eyeForwardOffset = 0.08f;
    public float eyeHeightOffset = 0.03f;

    [Header("6. CẢM GIÁC MẠNH KHI BIẾN THIÊN ĐỘ CAO (THRILL REACTION)")]
    [Tooltip("Vận tốc rơi thẳng đứng tối thiểu để kích hoạt giơ tay (m/s)")]
    public float verticalDropVelocityThreshold = -1.5f;
    [Tooltip("Góc chúi đầu dốc tối thiểu (Dot product với Vector3.down)")]
    public float slopePitchDropThreshold = 0.35f;
    [Tooltip("Tụt độ cao cực mạnh (m/s)")]
    public float suddenAltitudeChangeThreshold = -2.5f;
    [Tooltip("Thời gian giữ tư thế giơ tay sau mỗi cú rơi (giây)")]
    public float thrillHoldDuration = 1.2f;

    [Header("7. LIÊN KẾT RIDE CONTROLLER")]
    public RideController rideController;

    [Header("8. ĐIỂM ĐỨNG NGOÀI SẢNH GA (STATION EXIT POINTS)")]
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

        // Cập nhật scale vector dựa theo multiplier (1.22x)
        if (scaleMultiplier > 0.01f)
        {
            npcScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);
            stationStandingScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);
        }
        else
        {
            scaleMultiplier = 1.22f;
            npcScale = new Vector3(1.22f, 1.22f, 1.22f);
            stationStandingScale = new Vector3(1.22f, 1.22f, 1.22f);
        }

        // SitPoint là điểm đặt mông trên đệm ghế -> Offset = Vector3.zero để mông nhân vật đặt vừa vặn 100%
        hipToSitPointOffset = Vector3.zero;
        seatRotationOffset = Vector3.zero;

        // Đảm bảo các ngưỡng phát hiện cảm giác mạnh chuẩn xác (chỉ kích hoạt khi rơi/lao dốc)
        verticalDropVelocityThreshold = -1.5f;
        slopePitchDropThreshold = 0.35f;
        suddenAltitudeChangeThreshold = -2.5f;
    }

    void Start()
    {
        EnsureSitPoints();

        // 1. Dọn dẹp sạch sẽ toàn bộ ghế khi mới vào sảnh ga
        // NPC sẽ CHỈ xuất hiện khi người chơi bấm chọn ghế ngồi!
        ClearOldPassengers();

        Vector3 currentPos = GetCurrentCartPosition();
        lastCartPosY = currentPos.y;
        hasRecordedLastPos = true;
    }

    void Update()
    {
        DetectHeightVariationAndThrill();
    }

    /// <summary>
    /// Đảm bảo có đủ 4 điểm đệm ghế SitPoint
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

            if (sp == null)
            {
                Transform parentSeat = (seats != null && i < seats.Length && seats[i] != null) ? seats[i] : transform;
                GameObject spObj = new GameObject(sitPointNames[i]);
                spObj.transform.SetParent(parentSeat, false);
                spObj.transform.localPosition = new Vector3(0f, -0.85f, 0.12f);
                spObj.transform.localRotation = Quaternion.identity;
                spObj.transform.localScale = Vector3.one;
                sp = spObj.transform;
            }

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

    public Vector3 GetPlayerHeadEyePosition(int seatIndex)
    {
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        if (seatIndex == currentPlayerSeatIndex && playerBody != null)
        {
            return playerBody.GetSeatedEyePosition(GetSitPoint(seatIndex));
        }

        if (seatIndex >= 0 && seatIndex < passengerHeadBones.Length && passengerHeadBones[seatIndex] != null)
        {
            Transform head = passengerHeadBones[seatIndex];
            return head.position + (head.forward * eyeForwardOffset) + (Vector3.up * eyeHeightOffset);
        }

        Transform sp = GetSitPoint(seatIndex);
        if (sp != null)
        {
            return sp.position + (sp.up * 0.85f * scaleMultiplier) + (sp.forward * 0.08f);
        }
        return transform.position + Vector3.up * 0.85f;
    }

    /// <summary>
    /// Gán người chơi vào ghế được chọn và sinh 3 NPC ngẫu nhiên (hoàn toàn không trùng lặp, không trùng người chơi) vào 3 ghế còn lại
    /// </summary>
    public void SetPlayerPassenger(int seatIndex, Transform xrOriginParent)
    {
        EnsureSitPoints();
        LoadDefaultAssetsIfEmpty();

        currentPlayerSeatIndex = Mathf.Clamp(seatIndex, 0, 3);
        isPlayerSeated = true;

        // 1. Dọn dẹp triệt để các NPC cũ hoặc rác thừa trên tàu TRƯỚC KHI xếp hành khách mới
        ClearOldPassengers();

        // 2. Tính toán Scale 1.22x chuẩn cho khoang tàu
        if (scaleMultiplier > 0.01f)
        {
            npcScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);
        }

        Transform sp = GetSitPoint(currentPlayerSeatIndex);

        // 3. Đặt avatar người chơi vào ghế đã chọn với Scale 1.22x và tư thế ngồi chuẩn SitPoint
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        GameObject playerModelPrefab = null;
        if (playerBody != null)
        {
            playerBody.SetSeatedInCoaster(sp, hipToSitPointOffset, npcScale, seatRotationOffset);
            playerModelPrefab = playerBody.chosenPlayerPrefab;
        }

        // 4. Chuẩn bị danh sách Pool NPC LOẠI TRỪ hoàn toàn model của người chơi
        List<GameObject> pool = new List<GameObject>();
        if (npcPrefabs != null)
        {
            foreach (var p in npcPrefabs)
            {
                if (p == null) continue;

                // Loại trừ model của người chơi (so sánh cả reference lẫn tên prefab)
                if (playerModelPrefab != null)
                {
                    if (p == playerModelPrefab || p.name.Equals(playerModelPrefab.name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                if (!pool.Contains(p))
                {
                    pool.Add(p);
                }
            }
        }

        // 5. Trộn ngẫu nhiên hoàn toàn (Fisher-Yates Shuffle)
        for (int i = 0; i < pool.Count; i++)
        {
            int r = Random.Range(i, pool.Count);
            GameObject tmp = pool[i];
            pool[i] = pool[r];
            pool[r] = tmp;
        }

        // 6. Sinh đúng 3 NPC độc nhất vào 3 ghế còn lại
        int poolIdx = 0;
        for (int i = 0; i < 4; i++)
        {
            if (i == currentPlayerSeatIndex)
            {
                spawnedPassengerObjects[i] = playerBody != null ? playerBody.currentNPCBody : null;
                passengerAnimators[i] = playerBody != null ? playerBody.currentNPCBody?.GetComponent<Animator>() : null;
                passengerHeadBones[i] = playerBody != null ? playerBody.GetHeadTransform() : null;
                continue;
            }

            Transform seatTransform = GetSitPoint(i);
            if (seatTransform == null) continue;

            // Đảm bảo lấy từng NPC riêng biệt, không trùng lặp
            GameObject prefabToSpawn = (pool.Count > poolIdx) ? pool[poolIdx] : (pool.Count > 0 ? pool[poolIdx % pool.Count] : null);
            poolIdx++;

            if (prefabToSpawn != null)
            {
                GameObject npcInstance = Instantiate(prefabToSpawn, seatTransform);
                npcInstance.name = $"Passenger_{i}_{prefabToSpawn.name}";
                npcInstance.transform.localPosition = Vector3.zero;
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
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    anim.SetBool(IsSittingParam, true);
                    anim.SetBool(IsWalkingParam, false);
                    anim.SetFloat(SpeedParam, 0f);
                    anim.SetBool(IsThrilledParam, false);
                    anim.SetLayerWeight(1, 0f);
                    anim.Play("Sitting", 0, 0f);
                    anim.Update(0f); // Ép Animator tính toán ngay tư thế ngồi để lấy vị trí xương mông
                }

                // CĂN CHỈNH TÍNH CHỖ NGỒI BẰNG MÔNG (HIPS/PELVIS):
                // Dịch chuyển vị trí root sao cho xương Mông (Hips) trùng khớp 100% với SitPoint của đệm ghế
                Transform hips = FindHipsBone(npcInstance);
                if (hips != null)
                {
                    Vector3 hipWorldOffset = hips.position - seatTransform.position;
                    npcInstance.transform.position -= hipWorldOffset;
                }
                else
                {
                    // Fallback nếu không đo được xương: hạ root xuống theo chiều cao mông ngồi
                    npcInstance.transform.localPosition = new Vector3(0f, -0.58f * npcScale.y, 0f);
                }

                if (hipToSitPointOffset != Vector3.zero)
                {
                    npcInstance.transform.position += seatTransform.TransformDirection(hipToSitPointOffset);
                }

                Transform head = null;
                if (anim != null && anim.isHuman)
                {
                    head = anim.GetBoneTransform(HumanBodyBones.Head);
                }
                if (head == null) head = FindChildRecursive(npcInstance.transform, "bip Head");
                if (head == null) head = FindChildRecursive(npcInstance.transform, "bip head");
                if (head == null) head = FindChildRecursive(npcInstance.transform, "Head");
                if (head == null) head = FindChildRecursive(npcInstance.transform, "head");
                if (head == null) head = FindChildRecursive(npcInstance.transform, "mixamorig:Head");

                spawnedPassengerObjects[i] = npcInstance;
                passengerAnimators[i] = anim;
                passengerHeadBones[i] = head;
            }
        }

        Debug.Log($"<color=#00FF88><b>[CoasterPassengerManager] Đã xếp người chơi vào ghế {seatIndex} và sinh 3 NPC độc nhất vào 3 ghế còn lại (Scale {npcScale.x})!</b></color>");
    }

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
    /// Đưa toàn bộ hành khách ra khỏi tàu và kích hoạt đi dạo tự nhiên trong sảnh ga
    /// </summary>
    public void UnseatAllPassengersToStation(Transform stationRef)
    {
        ReleasePlayerPassenger();

        Vector3 basePos = (stationRef != null) ? stationRef.position : transform.position;
        Quaternion baseRot = (stationRef != null) ? stationRef.rotation : transform.rotation;
        Vector3 rightDir = (stationRef != null) ? stationRef.right : transform.right;
        Vector3 fwdDir = (stationRef != null) ? stationRef.forward : transform.forward;

        Vector3[] safeStationSpawnOffsets = new Vector3[]
        {
            rightDir * 3.2f - fwdDir * 1.5f,
            rightDir * 4.2f - fwdDir * 3.0f,
            -rightDir * 3.2f - fwdDir * 1.5f,
            -rightDir * 4.2f - fwdDir * 3.0f
        };

        Transform sp0 = GetSitPoint(0);
        float cartLossyScale = (sp0 != null && sp0.lossyScale.y > 0.01f) ? sp0.lossyScale.y : 2.4f;
        Vector3 dynamicStationScale = new Vector3(cartLossyScale * scaleMultiplier, cartLossyScale * scaleMultiplier, cartLossyScale * scaleMultiplier);

        for (int i = 0; i < 4; i++)
        {
            GameObject npcObj = spawnedPassengerObjects[i];
            if (npcObj == null) continue;

            npcObj.transform.SetParent(null);

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

            npcObj.transform.localScale = dynamicStationScale;

            if (passengerHeadBones[i] != null)
            {
                passengerHeadBones[i].localScale = Vector3.one;
            }

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

            ParkNPCWanderer wanderer = npcObj.GetComponent<ParkNPCWanderer>();
            if (wanderer == null) wanderer = npcObj.AddComponent<ParkNPCWanderer>();

            wanderer.boundaryMode = ParkNPCWanderer.BoundaryMode.RadiusAroundCenter;
            wanderer.centerPoint = stationRef;
            wanderer.maxRadius = 3.5f;
            wanderer.walkSpeed = Random.Range(1.15f, 1.4f);
            wanderer.minWaitTime = 2.0f;
            wanderer.maxWaitTime = 5.0f;
            wanderer.enabled = true;

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

        Debug.Log("<color=#00FF99><b>[CoasterPassengerManager] Đã đưa 4 NPC ra sảnh ga và kích hoạt đi dạo tự do!</b></color>");
    }

    public void TriggerThrillReaction(float duration = 1.2f)
    {
        // Không kích hoạt khi đang ở đoạn kéo xích lên dốc (lift hill)
        if (rideController != null)
        {
            float currentProgress = rideController.CurrentTraveledTime;
            if (currentProgress < 11.0f) return;
        }

        thrillHoldTimer = Mathf.Max(thrillHoldTimer, duration);
        SetAllPassengersThrilled(true);
    }

    /// <summary>
    /// Tự động phát hiện biến thiên độ cao và tốc độ để kích hoạt giơ 2 tay cảm giác mạnh (chỉ khi đổ dốc/lao dốc, không giơ khi đang kéo xích lên dốc)
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

        float progress = (rideController != null) ? rideController.CurrentTraveledTime : 0f;

        // TUYỆT ĐỐI KHÔNG GIƠ TAY KHI ĐANG KÉO XÍCH LÊN DỐC (0s -> 11s)
        if (progress < 11.0f)
        {
            thrillHoldTimer = 0f;
            SetAllPassengersThrilled(false);
            Vector3 p = GetCurrentCartPosition();
            lastCartPosY = p.y;
            hasRecordedLastPos = true;
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

            // 1. Góc chúi đầu xuống dốc (Pitch Down Angle >= 20 độ)
            float pitchDot = (cartTransform != null) ? Vector3.Dot(cartTransform.forward, Vector3.down) : 0f;
            bool isDippingDown = (pitchDot > slopePitchDropThreshold);

            // 2. Lao dốc tụt độ cao nhanh xuống phía dưới (< -1.5 m/s)
            bool isFallingDown = (verticalVelocity < verticalDropVelocityThreshold);

            // 3. Rơi tự do hoặc tụt dốc cực mạnh (< -2.5 m/s)
            bool isSuddenDrop = (verticalVelocity < suddenAltitudeChangeThreshold);

            // 4. Các đoạn đường ray tốc độ cao (không tính đoạn vào ga cuối)
            bool isFastSection = (progress > 11.5f && progress < 86.0f && (isFallingDown || isDippingDown));

            if (isFallingDown || isDippingDown || isSuddenDrop || isFastSection)
            {
                thrillHoldTimer = Mathf.Max(thrillHoldTimer, thrillHoldDuration);
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

    public void ClearOldPassengers()
    {
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        Transform playerBodyTransform = (playerBody != null && playerBody.currentNPCBody != null) ? playerBody.currentNPCBody.transform : null;

        for (int i = 0; i < spawnedPassengerObjects.Length; i++)
        {
            if (i != currentPlayerSeatIndex && spawnedPassengerObjects[i] != null)
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
                if (playerBodyTransform != null && child == playerBodyTransform) continue;

                // Xóa các passenger cũ hoặc mesh/animator rác nằm dưới ghế
                if (child.name.StartsWith("Passenger_") || child.GetComponent<Animator>() != null || child.GetComponent<SkinnedMeshRenderer>() != null)
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

        if (Application.isPlaying)
        {
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allObjects)
            {
                if (go == null) continue;
                if (playerBodyTransform != null && go.transform == playerBodyTransform) continue;

                // Xóa các bản Passenger clone bị rơi ra ngoài sảnh
                if (go.name.StartsWith("Passenger_") && (go.transform.parent == null || go.transform.parent.name.Contains("Station")))
                {
                    Destroy(go);
                }
                // Xóa NPC tĩnh dư thừa bị đặt nhầm vào tọa độ khoang tàu trong scene
                else if (go.name.Contains("casual_Male_G (1)") && go.transform.parent == null)
                {
                    Destroy(go);
                }
            }
        }
    }

    private void DisableUnneededComponents(GameObject npc)
    {
        if (npc == null) return;

        ParkNPCWanderer wanderer = npc.GetComponent<ParkNPCWanderer>();
        if (wanderer != null) DestroyImmediate(wanderer);

        CharacterController cc = npc.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Xóa hoàn toàn script CityPeople để không bị CityPeople.Start() chạy ShuffleClips / CrossFade đè lên tư thế ngồi
        MonoBehaviour[] scripts = npc.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in scripts)
        {
            if (mb != null && (mb.GetType().Name == "CityPeople" || mb.GetType().FullName.Contains("CityPeople")))
            {
                DestroyImmediate(mb);
            }
        }

        Rigidbody rb = npc.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        Collider[] colliders = npc.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
    }

    public void SetAllPassengersThrilled(bool isThrilled)
    {
        if (isCurrentlyThrilled == isThrilled) return;
        isCurrentlyThrilled = isThrilled;

        // Cập nhật cho 3 NPC hành khách
        for (int i = 0; i < passengerAnimators.Length; i++)
        {
            if (passengerAnimators[i] != null)
            {
                passengerAnimators[i].SetBool(IsThrilledParam, isThrilled);
                passengerAnimators[i].SetLayerWeight(1, isThrilled ? 1f : 0f);
            }
        }

        // Cập nhật cho NPC đại diện của người chơi
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

    /// <summary>
    /// Tìm xương mông/hông (Hips/Pelvis) của NPC theo chuẩn Humanoid hoặc rig xương cụ thể (bip Pelvis, Hips, mixamo...)
    /// </summary>
    public static Transform FindHipsBone(GameObject npc)
    {
        if (npc == null) return null;

        Animator anim = npc.GetComponent<Animator>();
        if (anim != null && anim.isHuman)
        {
            Transform h = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (h != null) return h;
        }

        string[] hipNames = new string[] { "bip Pelvis", "bip pelvis", "Pelvis", "pelvis", "Hips", "hips", "mixamorig:Hips" };
        foreach (var hName in hipNames)
        {
            Transform h = FindChildRecursive(npc.transform, hName);
            if (h != null) return h;
        }

        Transform[] allChildren = npc.GetComponentsInChildren<Transform>(true);
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
}
