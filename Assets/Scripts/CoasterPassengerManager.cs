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

    [Header("7. LIÊN KẾT RIDE CONTROLLER & VOICE MANAGER")]
    public RideController rideController;
    public CoasterPassengerVoiceManager voiceManager;

    [Header("8. ĐIỂM ĐỨNG NGOÀI SẢNH GA (STATION EXIT POINTS)")]
    public Transform[] stationExitPoints = new Transform[4];

    // 3 NPC đang đứng chờ sẵn tại sảnh ga
    [SerializeField] private GameObject[] waitingStationNPCs = new GameObject[3];

    // Danh sách 4 đối tượng NPC thực tế đang ngồi trên 4 ghế trong tàu
    [SerializeField] private GameObject[] spawnedPassengerObjects = new GameObject[4];
    private Animator[] passengerAnimators = new Animator[4];
    [HideInInspector] public Transform[] passengerHeadBones = new Transform[4];
    [SerializeField] private int[] passengerThrillTypes = new int[4]; // 0: Giơ 2 tay, 1: Vẫy tay, 2: Bám rung lắc, 3: Chới với

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
    private static readonly int ThrillTypeParam = Animator.StringToHash("ThrillType");

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

        if (voiceManager == null)
        {
            voiceManager = GetComponent<CoasterPassengerVoiceManager>();
            if (voiceManager == null) voiceManager = GetComponentInParent<CoasterPassengerVoiceManager>();
            if (voiceManager == null) voiceManager = Object.FindAnyObjectByType<CoasterPassengerVoiceManager>();
            if (voiceManager == null)
            {
                voiceManager = gameObject.AddComponent<CoasterPassengerVoiceManager>();
            }
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

        hipToSitPointOffset = Vector3.zero;
        seatRotationOffset = Vector3.zero;

        verticalDropVelocityThreshold = -1.5f;
        slopePitchDropThreshold = 0.35f;
        suddenAltitudeChangeThreshold = -2.5f;
    }

    void Start()
    {
        EnsureSitPoints();
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
    /// Sinh ngẫu nhiên 3 NPC đứng chờ sẵn tại sảnh ga (chân chạm sàn 100%, dáng đứng Idle, không di chuyển)
    /// </summary>
    public void SpawnWaitingStationNPCs(Transform stationRef)
    {
        LoadDefaultAssetsIfEmpty();
        ClearWaitingStationNPCs();

        if (stationRef == null)
        {
            if (stationExitPoints != null && stationExitPoints.Length > 0 && stationExitPoints[0] != null)
                stationRef = stationExitPoints[0];
            else
            {
                GameObject vrFloor = GameObject.Find("VR_FloorPoint");
                if (vrFloor != null) stationRef = vrFloor.transform;
                else stationRef = transform;
            }
        }

        // 1. Tính toán Scale chuẩn sảnh ga
        Transform sp0 = GetSitPoint(0);
        float cartLossyScale = (sp0 != null && sp0.lossyScale.y > 0.01f) ? sp0.lossyScale.y : 2.4f;
        Vector3 stationScale = new Vector3(cartLossyScale * scaleMultiplier, cartLossyScale * scaleMultiplier, cartLossyScale * scaleMultiplier);
        if (stationScale.x < 1.0f) stationScale = new Vector3(1.22f, 1.22f, 1.22f);

        // 2. Lấy model người chơi để LOẠI TRỪ 100%
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        GameObject playerModelPrefab = playerBody != null ? playerBody.chosenPlayerPrefab : null;

        // 3. Chuẩn bị danh sách pool NPC
        List<GameObject> pool = new List<GameObject>();
        if (npcPrefabs != null)
        {
            foreach (var p in npcPrefabs)
            {
                if (p == null) continue;
                if (playerModelPrefab != null && (p == playerModelPrefab || p.name.Equals(playerModelPrefab.name, System.StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                if (!pool.Contains(p)) pool.Add(p);
            }
        }

        // 4. Xáo trộn ngẫu nhiên (Fisher-Yates Shuffle)
        for (int i = 0; i < pool.Count; i++)
        {
            int r = Random.Range(i, pool.Count);
            GameObject tmp = pool[i];
            pool[i] = pool[r];
            pool[r] = tmp;
        }

        // 5. Tính 3 vị trí đứng chờ tự nhiên quanh sảnh ga
        Vector3 basePos = stationRef.position;
        Quaternion baseRot = stationRef.rotation;
        Vector3 right = stationRef.right;
        Vector3 fwd = stationRef.forward;

        Vector3[] waitOffsets = new Vector3[]
        {
            -right * 1.5f - fwd * 0.3f, // Bên trái người chơi
            right * 1.5f - fwd * 0.3f,  // Bên phải người chơi
            right * 2.3f - fwd * 1.4f   // Phía sau bên phải
        };

        waitingStationNPCs = new GameObject[3];

        for (int i = 0; i < 3; i++)
        {
            GameObject prefab = (pool.Count > i) ? pool[i] : (pool.Count > 0 ? pool[i % pool.Count] : null);
            if (prefab == null) continue;

            Vector3 targetPos = basePos + waitOffsets[i];
            Quaternion targetRot = baseRot * Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f);

            // DÙNG BỘ LỌC TÌM SÀN CHUẨN (tránh va trúng mái nhà ga, trần nhà hoặc tường vô hình)
            targetPos = SeatSwitcher.FindSolidGroundPosition(targetPos);

            GameObject npcInstance = Instantiate(prefab, targetPos, targetRot);
            npcInstance.name = $"StationWaiting_{i}_{prefab.name}";
            npcInstance.transform.localScale = stationScale;

            // Vô hiệu hóa triệt để các script di chuyển / physics để đứng yên tại chỗ
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
                anim.SetBool(IsSittingParam, false);
                anim.SetBool(IsWalkingParam, false);
                anim.SetFloat(SpeedParam, 0f);
                anim.SetBool(IsThrilledParam, false);
                anim.SetLayerWeight(1, 0f);
                anim.Play("Idle", 0, 0f);
                anim.Update(0f);
            }

            waitingStationNPCs[i] = npcInstance;
        }

        Debug.Log($"<color=#00FF88><b>[CoasterPassengerManager] Đã sinh 3 NPC mới đứng chờ sẵn tại sảnh ga (chân chạm sàn, dáng đứng Idle)!</b></color>");
    }

    /// <summary>
    /// Gán người chơi vào ghế đã chọn và đưa 3 NPC đang đứng chờ ở ga vào 3 ghế còn lại trên tàu
    /// </summary>
    public void SetPlayerPassenger(int seatIndex, Transform xrOriginParent)
    {
        EnsureSitPoints();
        LoadDefaultAssetsIfEmpty();

        currentPlayerSeatIndex = Mathf.Clamp(seatIndex, 0, 3);
        isPlayerSeated = true;

        // 1. Dọn các NPC cũ trên khoang tàu (nếu có)
        ClearCarSeatPassengersOnly();

        // 2. Tính Scale 1.22x chuẩn cho khoang tàu
        if (scaleMultiplier > 0.01f)
        {
            npcScale = new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier);
        }

        Transform sp = GetSitPoint(currentPlayerSeatIndex);

        // 3. Chuẩn bị 4 kiểu Thrill khác nhau cho 4 ghế
        int[] thrillPool = new int[] { 0, 1, 2, 3 };
        for (int t = 0; t < thrillPool.Length; t++)
        {
            int r = Random.Range(t, thrillPool.Length);
            int temp = thrillPool[t];
            thrillPool[t] = thrillPool[r];
            thrillPool[r] = temp;
        }
        for (int i = 0; i < 4; i++)
        {
            passengerThrillTypes[i] = thrillPool[i % thrillPool.Length];
        }

        // 4. Đặt avatar người chơi vào ghế đã chọn
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        if (playerBody != null)
        {
            playerBody.SetSeatedInCoaster(sp, hipToSitPointOffset, npcScale, seatRotationOffset, passengerThrillTypes[currentPlayerSeatIndex]);
        }

        // 5. Nếu chưa có 3 NPC chờ tại ga (hoặc bị thiếu), tự động sinh đủ 3 NPC
        if (waitingStationNPCs == null || waitingStationNPCs.Length != 3 || waitingStationNPCs[0] == null)
        {
            SpawnWaitingStationNPCs(stationExitPoints != null && stationExitPoints.Length > 0 ? stationExitPoints[0] : transform);
        }

        // 6. Đưa 3 NPC từ sảnh ga vào 3 ghế còn lại trong tàu
        int waitingIdx = 0;
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

            GameObject npcInstance = (waitingStationNPCs != null && waitingIdx < waitingStationNPCs.Length) ? waitingStationNPCs[waitingIdx] : null;
            waitingIdx++;

            if (npcInstance != null)
            {
                npcInstance.name = $"Passenger_{i}_{npcInstance.name.Replace("StationWaiting_", "")}";
                npcInstance.transform.SetParent(seatTransform, false);
                npcInstance.transform.localPosition = Vector3.zero;
                npcInstance.transform.localRotation = Quaternion.Euler(seatRotationOffset);
                npcInstance.transform.localScale = npcScale;

                DisableUnneededComponents(npcInstance, true);

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
                    anim.SetInteger(ThrillTypeParam, passengerThrillTypes[i]);
                    anim.SetBool(IsThrilledParam, false);
                    anim.SetLayerWeight(1, 0f);
                    anim.Play("Sitting", 0, 0f);
                    anim.Update(0f); // Ép Animator tính toán ngay tư thế ngồi để lấy vị trí xương mông
                }

                // Căn chỉnh mông ngồi khớp 100% SitPoint
                Transform hips = FindHipsBone(npcInstance);
                if (hips != null)
                {
                    Vector3 hipWorldOffset = hips.position - seatTransform.position;
                    npcInstance.transform.position -= hipWorldOffset;
                }
                else
                {
                    npcInstance.transform.localPosition = new Vector3(0f, -0.58f * npcScale.y, 0f);
                }

                if (hipToSitPointOffset != Vector3.zero)
                {
                    npcInstance.transform.position += seatTransform.TransformDirection(hipToSitPointOffset);
                }

                Transform head = null;
                if (anim != null && anim.isHuman) head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (head == null) head = FindChildRecursive(npcInstance.transform, "bip Head");
                if (head == null) head = FindChildRecursive(npcInstance.transform, "Head");

                spawnedPassengerObjects[i] = npcInstance;
                passengerAnimators[i] = anim;
                passengerHeadBones[i] = head;
            }
        }

        // Xóa rỗng tham chiếu waitingStationNPCs vì các NPC đã lên tàu
        waitingStationNPCs = new GameObject[3];

        // 7. Đồng bộ nhóm giọng và nguồn phát 3D cho 4 ghế
        if (voiceManager != null)
        {
            voiceManager.playerSeatIndex = currentPlayerSeatIndex;
            for (int s = 0; s < 4; s++)
            {
                if (s == currentPlayerSeatIndex)
                {
                    GameObject pObj = playerBody != null ? playerBody.currentNPCBody : null;
                    if (pObj == null && playerBody != null) pObj = playerBody.chosenPlayerPrefab;
                    voiceManager.SetPassengerVoiceType(s, pObj);
                }
                else
                {
                    voiceManager.SetPassengerVoiceType(s, spawnedPassengerObjects[s]);
                }
            }
            voiceManager.AttachAudioSourcesToSeats(sitPoints, passengerHeadBones);
        }

        Debug.Log($"<color=#00FF88><b>[CoasterPassengerManager] Đã đưa 3 NPC từ sảnh ga vào 3 ghế trong tàu và xếp người chơi vào ghế {seatIndex}!</b></color>");
    }

    public void ReleasePlayerPassenger()
    {
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        if (playerBody != null)
        {
            playerBody.ReturnToPlayerRig();
        }

        if (voiceManager != null && !voiceManager.IsReliefActive())
        {
            voiceManager.StopAllScreamsImmediate();
        }

        isPlayerSeated = false;
        currentPlayerSeatIndex = -1;
    }

    /// <summary>
    /// Đưa 3 NPC hành khách ra đứng yên tại sảnh ga (chân chạm sàn, dáng đứng Idle, tuyệt đối không tách người chơi)
    /// </summary>
    public void UnseatAllPassengersToStation(Transform stationRef)
    {
        // 1. Phục hồi người chơi về XR Rig (không tách ra)
        ReleasePlayerPassenger();

        if (stationRef == null)
        {
            GameObject vrFloor = GameObject.Find("VR_FloorPoint");
            if (vrFloor != null) stationRef = vrFloor.transform;
            else stationRef = transform;
        }

        Vector3 basePos = stationRef.position;
        Quaternion baseRot = stationRef.rotation;
        Vector3 rightDir = stationRef.right;
        Vector3 fwdDir = stationRef.forward;

        Vector3[] waitOffsets = new Vector3[]
        {
            -rightDir * 1.5f - fwdDir * 0.3f,
            rightDir * 1.5f - fwdDir * 0.3f,
            rightDir * 2.3f - fwdDir * 1.4f
        };

        Transform sp0 = GetSitPoint(0);
        float cartLossyScale = (sp0 != null && sp0.lossyScale.y > 0.01f) ? sp0.lossyScale.y : 2.4f;
        Vector3 stationScale = new Vector3(cartLossyScale * scaleMultiplier, cartLossyScale * scaleMultiplier, cartLossyScale * scaleMultiplier);
        if (stationScale.x < 1.0f) stationScale = new Vector3(1.22f, 1.22f, 1.22f);

        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        Transform playerBodyTransform = (playerBody != null && playerBody.currentNPCBody != null) ? playerBody.currentNPCBody.transform : null;

        waitingStationNPCs = new GameObject[3];
        int waitIdx = 0;

        for (int i = 0; i < 4; i++)
        {
            if (i == currentPlayerSeatIndex) continue; // Bỏ qua người chơi

            GameObject npcObj = spawnedPassengerObjects[i];
            if (npcObj == null) continue;
            if (playerBodyTransform != null && npcObj.transform == playerBodyTransform) continue;

            npcObj.transform.SetParent(null);

            Vector3 targetPos = basePos + (waitIdx < waitOffsets.Length ? waitOffsets[waitIdx] : Vector3.zero);
            Quaternion targetRot = baseRot * Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f);

            // DÙNG BỘ LỌC TÌM SÀN CHUẨN (tránh va trúng mái nhà ga, trần nhà hoặc tường vô hình)
            targetPos = SeatSwitcher.FindSolidGroundPosition(targetPos);

            npcObj.transform.SetPositionAndRotation(targetPos, targetRot);
            npcObj.transform.localScale = stationScale;

            // Vô hiệu hóa Wanderer để NPC ĐỨNG YÊN TẠI CHỖ
            DisableUnneededComponents(npcObj);

            Animator anim = passengerAnimators[i];
            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.SetBool(IsSittingParam, false);
                anim.SetBool(IsThrilledParam, false);
                anim.SetBool(IsWalkingParam, false);
                anim.SetFloat(SpeedParam, 0f);
                anim.SetLayerWeight(1, 0f);
                anim.Play("Idle", 0, 0f);
                anim.Update(0f);
            }

            if (waitIdx < waitingStationNPCs.Length)
            {
                waitingStationNPCs[waitIdx] = npcObj;
                waitIdx++;
            }

            spawnedPassengerObjects[i] = null;
        }

        Debug.Log("<color=#00FF99><b>[CoasterPassengerManager] Đã đưa 3 NPC ra đứng yên tại sảnh ga (chân chạm sàn, dáng đứng Idle)!</b></color>");
    }

    private Vector3 lastCartPos = Vector3.zero;

    public void TriggerThrillReaction(float duration = 1.2f)
    {
        if (rideController != null)
        {
            float currentProgress = rideController.CurrentTraveledTime;
            float clipLength = rideController.ClipLength;
            float loopTime = currentProgress % clipLength;
            if (loopTime < 11.0f || loopTime > 86.5f) return;
        }

        thrillHoldTimer = Mathf.Max(thrillHoldTimer, duration);
        SetAllPassengersThrilled(true);
    }

    /// <summary>
    /// Tự động phát hiện chính xác 100% theo chuyển động vật lý thực tế 3D của toa tàu:
    /// - Khi Đổ dốc / Lao dốc / Lộn vòng / Bo cua tốc độ cao -> Giơ tay / vẫy tay / hò reo cảm giác mạnh!
    /// - Khi Leo dốc chậm / Giảm tốc / Vào ga -> Hạ tay xuống bám thanh an toàn / ngồi bình thường.
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
        float clipLength = (rideController != null) ? rideController.ClipLength : 90f;
        float loopTime = progress % clipLength;

        // 1. ĐOẠN KHỞI HÀNH & KÉO XÍCH LÊN DỐC ĐẦU (0s -> 11.5s) hoặc VÀO GA ĐỖ PHANH CUỐI CHUYẾN (> 86.5s)
        // -> Cả 4 người ngồi yên bình thường, hai tay đặt trên đùi / ôm thanh chắn
        if (loopTime < 11.5f || loopTime > 86.5f)
        {
            thrillHoldTimer = 0f;
            SetAllPassengersThrilled(false);
            if (voiceManager != null) voiceManager.UpdateThrillState(false, false);
            Vector3 p = GetCurrentCartPosition();
            lastCartPos = p;
            lastCartPosY = p.y;
            hasRecordedLastPos = true;
            return;
        }

        // 2. CÁC MỐC THỜI GIAN LEO DỐC CHẬM RÕ RỆT TRÊN RAY (SLOW SECTIONS):
        // 24s -> 31s (Đoạn quay xe lên dốc 2), 69s -> 73.0s (Đoạn đỉnh dốc chậm trước khi lao xoắn ốc)
        bool inSlowTrackSection = (loopTime >= 24.0f && loopTime <= 31.0f) || (loopTime >= 69.0f && loopTime <= 73.0f);

        // 3. TÍNH TOÁN CHUYỂN ĐỘNG VẬT LÝ THỰC TẾ 3D CỦA TOA TÀU:
        Vector3 currentPos = GetCurrentCartPosition();
        float deltaTime = Time.deltaTime;

        if (!hasRecordedLastPos || deltaTime <= 0f)
        {
            lastCartPos = currentPos;
            lastCartPosY = currentPos.y;
            hasRecordedLastPos = true;
            return;
        }

        Vector3 velocity = (currentPos - lastCartPos) / deltaTime;
        float actualSpeed = velocity.magnitude;
        float verticalVelocity = velocity.y;
        lastCartPos = currentPos;
        lastCartPosY = currentPos.y;

        Transform cartTransform = GetCartTransform();
        float pitchDot = (cartTransform != null) ? Vector3.Dot(cartTransform.forward, Vector3.down) : 0f;
        float bankDot = (cartTransform != null) ? Mathf.Abs(Vector3.Dot(cartTransform.right, Vector3.up)) : 0f;

        // Leo dốc chậm thực sự (tốc độ chậm và ngửa đầu leo lên hoặc nằm trong slow section)
        bool isClimbingSlow = inSlowTrackSection || ((pitchDot < -0.15f || verticalVelocity > 1.2f) && actualSpeed < 10.0f);

        // Các đoạn tốc độ cao & cảm giác mạnh liên tục:
        // 11.5s-23.5s (Đại dốc 1), 32s-48.5s (Đại dốc 2 & lộn vòng), 50s-68.5s (Uốn lượn tốc độ cao), 73.5s-86.5s (Tháp xoắn ốc & Lao dốc về ga)
        bool isFastSection = (loopTime >= 11.5f && loopTime <= 23.5f) || (loopTime >= 32.0f && loopTime <= 48.5f) || (loopTime >= 50.0f && loopTime <= 68.5f) || (loopTime >= 73.5f && loopTime <= 86.5f);
        bool isDropping = (pitchDot > 0.08f || verticalVelocity < -0.5f);
        bool isHighGTurnOrLoop = (bankDot > 0.25f && actualSpeed > 5.0f);

        bool shouldBeThrilled = (isFastSection || isDropping || isHighGTurnOrLoop || actualSpeed > 8.0f) && !isClimbingSlow;
        bool isMajorDrop = (pitchDot > 0.2f || verticalVelocity < -2.0f || actualSpeed > 14.0f || (loopTime >= 11.5f && loopTime <= 18.0f) || (loopTime >= 32.0f && loopTime <= 40.0f) || (loopTime >= 73.5f && loopTime <= 86.0f));

        if (shouldBeThrilled)
        {
            thrillHoldTimer = 1.2f; // Giữ trạng thái mượt mà chống giật cục
            SetAllPassengersThrilled(true);
            if (voiceManager != null) voiceManager.UpdateThrillState(true, isMajorDrop);
        }
        else if (isClimbingSlow)
        {
            thrillHoldTimer = 0f;
            SetAllPassengersThrilled(false);
            if (voiceManager != null) voiceManager.UpdateThrillState(false, false);
        }
        else if (thrillHoldTimer > 0f)
        {
            thrillHoldTimer -= deltaTime;
            SetAllPassengersThrilled(true);
            if (voiceManager != null) voiceManager.UpdateThrillState(true, isMajorDrop);
        }
        else
        {
            SetAllPassengersThrilled(false);
            if (voiceManager != null) voiceManager.UpdateThrillState(false, false);
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

    public bool HasWaitingStationNPCs()
    {
        if (waitingStationNPCs == null || waitingStationNPCs.Length == 0) return false;
        foreach (var npc in waitingStationNPCs)
        {
            if (npc != null) return true;
        }
        return false;
    }

    public void ClearWaitingStationNPCs()
    {
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        Transform playerBodyTransform = (playerBody != null && playerBody.currentNPCBody != null) ? playerBody.currentNPCBody.transform : null;

        if (waitingStationNPCs != null)
        {
            for (int i = 0; i < waitingStationNPCs.Length; i++)
            {
                if (waitingStationNPCs[i] != null)
                {
                    if (playerBodyTransform != null && waitingStationNPCs[i].transform == playerBodyTransform)
                    {
                        waitingStationNPCs[i] = null;
                        continue;
                    }
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(waitingStationNPCs[i]);
                    else Destroy(waitingStationNPCs[i]);
#else
                    Destroy(waitingStationNPCs[i]);
#endif
                    waitingStationNPCs[i] = null;
                }
            }
        }
    }

    public void ClearCarSeatPassengersOnly()
    {
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        Transform playerBodyTransform = (playerBody != null && playerBody.currentNPCBody != null) ? playerBody.currentNPCBody.transform : null;

        for (int i = 0; i < spawnedPassengerObjects.Length; i++)
        {
            if (i != currentPlayerSeatIndex && spawnedPassengerObjects[i] != null)
            {
                if (playerBodyTransform != null && spawnedPassengerObjects[i].transform == playerBodyTransform)
                {
                    spawnedPassengerObjects[i] = null;
                    continue;
                }

                bool isWaiting = false;
                if (waitingStationNPCs != null)
                {
                    foreach (var w in waitingStationNPCs)
                    {
                        if (w != null && w == spawnedPassengerObjects[i]) { isWaiting = true; break; }
                    }
                }

                if (!isWaiting)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(spawnedPassengerObjects[i]);
                    else Destroy(spawnedPassengerObjects[i]);
#else
                    Destroy(spawnedPassengerObjects[i]);
#endif
                }
                spawnedPassengerObjects[i] = null;
            }
        }
    }

    public void ClearOldPassengers()
    {
        ClearWaitingStationNPCs();
        ClearCarSeatPassengersOnly();

        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        Transform playerBodyTransform = (playerBody != null && playerBody.currentNPCBody != null) ? playerBody.currentNPCBody.transform : null;

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
                if ((go.name.StartsWith("Passenger_") || go.name.StartsWith("StationWaiting_")) && (go.transform.parent == null || go.transform.parent.name.Contains("Station")))
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

    private void DisableUnneededComponents(GameObject npc, bool isSeated = false)
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

        if (isSeated)
        {
            // Khi ngồi trên tàu: Tắt toàn bộ Collider và Physics để không cản trở chuyển động của toa tàu và ray
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
        else
        {
            // Khi đứng chờ ở sảnh ga: Cung cấp CapsuleCollider và Kinematic Rigidbody để người chơi không thể đi xuyên qua NPC
            SetupStationStandingCollider(npc);
        }
    }

    /// <summary>
    /// Thiết lập CapsuleCollider và Kinematic Rigidbody cho NPC đứng chờ tại sảnh ga
    /// </summary>
    private void SetupStationStandingCollider(GameObject npc)
    {
        if (npc == null) return;

        CapsuleCollider col = npc.GetComponent<CapsuleCollider>();
        if (col == null) col = npc.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f, 0f);
        col.radius = 0.28f;
        col.height = 1.8f;
        col.isTrigger = false;
        col.enabled = true;

        Rigidbody rb = npc.GetComponent<Rigidbody>();
        if (rb == null) rb = npc.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = true;
        rb.interpolation = RigidbodyInterpolation.None;
    }

    public void SetAllPassengersThrilled(bool isThrilled)
    {
        if (isCurrentlyThrilled == isThrilled) return;
        isCurrentlyThrilled = isThrilled;

        // Mỗi lần bắt đầu một khúc đổ dốc / cảm giác mạnh mới (sau khi vừa qua đoạn leo dốc / đi chậm):
        // TỰ ĐỘNG XÁO TRỘN NGẪU NHIÊN kiểu giơ tay mới cho từng ghế, giúp 1 NPC có nhiều động tác phong phú khác nhau trong cùng 1 chuyến đi!
        if (isThrilled)
        {
            int[] thrillPool = new int[] { 0, 1, 2, 3 };
            for (int t = 0; t < thrillPool.Length; t++)
            {
                int r = Random.Range(t, thrillPool.Length);
                int temp = thrillPool[t];
                thrillPool[t] = thrillPool[r];
                thrillPool[r] = temp;
            }
            for (int i = 0; i < 4; i++)
            {
                passengerThrillTypes[i] = thrillPool[i % thrillPool.Length];
            }
        }

        // Cập nhật cho 3 NPC hành khách
        for (int i = 0; i < passengerAnimators.Length; i++)
        {
            if (passengerAnimators[i] != null)
            {
                passengerAnimators[i].SetInteger(ThrillTypeParam, passengerThrillTypes[i]);
                passengerAnimators[i].SetBool(IsThrilledParam, isThrilled);
                passengerAnimators[i].SetLayerWeight(1, isThrilled ? 1f : 0f);
            }
        }

        // Cập nhật cho NPC đại diện của người chơi
        PlayerNPCBodyController playerBody = Object.FindAnyObjectByType<PlayerNPCBodyController>();
        if (playerBody != null)
        {
            int playerThrillType = (currentPlayerSeatIndex >= 0 && currentPlayerSeatIndex < passengerThrillTypes.Length) ? passengerThrillTypes[currentPlayerSeatIndex] : 0;
            playerBody.SetThrilled(isThrilled, playerThrillType);
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
