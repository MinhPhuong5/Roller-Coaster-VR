#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

public static class NPCSetupTool
{
    private const string MaskPath = "Assets/3D Model/NPC/UpperBody_Mask.mask";
    private const string AnimatorPath = "Assets/3D Model/NPC/NPC_Master_Animator.controller";

    [MenuItem("Tools/NPC System/1-Click Setup NPC Animator & Avatar Mask")]
    public static void SetupNPCSystem()
    {
        EnsurePlayerBodyLayerRegistered();

        // 1. Tạo / Cập nhật AvatarMask chỉ lấy duy nhất 2 cánh tay (KHÔNG đụng tới Body, Head, Root hay Chân)
        AvatarMask upperBodyMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (upperBodyMask == null)
        {
            upperBodyMask = new AvatarMask();
            upperBodyMask.name = "UpperBody_Mask";
            AssetDatabase.CreateAsset(upperBodyMask, MaskPath);
        }

        // TẮT hoàn toàn Root, Body (ngực/lưng/cột sống), Head (đầu/cổ), Hips, Legs. CHỈ BẬT 2 cánh tay và bàn tay
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, false);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, false);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
        upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
        EditorUtility.SetDirty(upperBodyMask);
        Debug.Log("[NPCSetupTool] Đã cấu hình UpperBody_Mask CHỈ tác động 2 cánh tay (Body & Head = FALSE) tại: " + MaskPath);

        // 2. Cấu hình LoopTime = true và ép Unity Reimport các FBX sang chuẩn Humanoid
        EnsureClipLooping("Assets/3D Model/NPC/X Bot@Sitting Idle.fbx");
        EnsureClipLooping("Assets/3D Model/NPC/Waving.fbx");
        EnsureClipLooping("Assets/3D Model/NPC/Braced Hang Shimmy.fbx");
        EnsureClipLooping("Assets/3D Model/NPC/X Bot@Hanging Idle.fbx");
        EnsureClipLooping("Assets/3D Model/NPC/X Bot@Hanging Idle (1).fbx");
        EnsureClipLooping("Assets/3D Model/NPC/X Bot@Falling.fbx");

        // Nạp các Animation Clips chuẩn Humanoid
        AnimationClip walkClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/DenysAlmaral/CityPeople/Animations/locom_m_basicWalk_30f.fbx");
        AnimationClip idleClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/DenysAlmaral/CityPeople/Animations/idle_m_1_200f.fbx");
        AnimationClip sitClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/X Bot@Sitting Idle.fbx");
        
        // 4 Hoạt ảnh mạo hiểm / cảm giác mạnh khác nhau khi tàu lao dốc / tốc độ cao
        AnimationClip hangingClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/X Bot@Hanging Idle.fbx");
        if (hangingClip == null) hangingClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/X Bot@Hanging Idle (1).fbx");

        AnimationClip wavingClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/Waving.fbx");
        AnimationClip bracedClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/Braced Hang Shimmy.fbx");
        AnimationClip fallingClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/X Bot@Falling.fbx");

        // Fallback an toàn nếu thiếu file
        if (wavingClip == null) wavingClip = hangingClip;
        if (bracedClip == null) bracedClip = hangingClip;
        if (fallingClip == null) fallingClip = hangingClip;

        // 3. Tạo hoặc nạp AnimatorController
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);

        // Thêm Parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsSitting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsThrilled", AnimatorControllerParameterType.Bool);
        controller.AddParameter("ThrillType", AnimatorControllerParameterType.Int);

        // ==========================================
        // BASE LAYER: Di chuyển ở công viên & Dáng ngồi trong tàu
        // ==========================================
        AnimatorStateMachine baseStateMachine = controller.layers[0].stateMachine;
        baseStateMachine.entryPosition = new Vector3(50, 100, 0);

        AnimatorState idleState = baseStateMachine.AddState("Idle", new Vector3(280, 100, 0));
        idleState.motion = idleClip;

        AnimatorState walkState = baseStateMachine.AddState("Walk", new Vector3(280, 200, 0));
        walkState.motion = walkClip;

        AnimatorState sitState = baseStateMachine.AddState("Sitting", new Vector3(540, 100, 0));
        sitState.motion = sitClip;

        // Transition: Idle <-> Walk
        var toWalk = idleState.AddTransition(walkState);
        toWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
        toWalk.hasExitTime = false;
        toWalk.duration = 0.2f;

        var toIdle = walkState.AddTransition(idleState);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
        toIdle.hasExitTime = false;
        toIdle.duration = 0.2f;

        // Transition: AnyState -> Sitting (Khi lên tàu lượn)
        var anyToSit = baseStateMachine.AddAnyStateTransition(sitState);
        anyToSit.AddCondition(AnimatorConditionMode.If, 0, "IsSitting");
        anyToSit.hasExitTime = false;
        anyToSit.duration = 0.25f;
        anyToSit.canTransitionToSelf = false;

        var sitToIdle = sitState.AddTransition(idleState);
        sitToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSitting");
        sitToIdle.hasExitTime = false;
        sitToIdle.duration = 0.25f;

        // ==========================================
        // UPPER BODY LAYER: Động tác mạo hiểm phong phú đa dạng cho từng NPC khi tàu lao dốc
        // (Chỉ tác động 2 cánh tay qua UpperBody_Mask, ngồi yên trên ghế khi đoạn bình thường)
        // ==========================================
        AnimatorControllerLayer upperLayer = new AnimatorControllerLayer
        {
            name = "UpperBody_Reaction",
            defaultWeight = 0.0f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = upperBodyMask,
            stateMachine = new AnimatorStateMachine()
        };
        upperLayer.stateMachine.name = upperLayer.name;
        upperLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(upperLayer.stateMachine, AnimatorPath);

        // State mặc định: Empty (motion = sitClip). Giữ tư thế tay ngồi tự nhiên và triệt tiêu hoàn toàn T-Pose khi chưa kích hoạt IsThrilled
        AnimatorState emptyState = upperLayer.stateMachine.AddState("Empty", new Vector3(300, 100, 0));
        emptyState.motion = sitClip;
        emptyState.writeDefaultValues = true;

        // 1. Thrill Type 0: Giơ 2 tay lên cao (High Hands Up)
        AnimatorState stateHandsUp = upperLayer.stateMachine.AddState("Thrill_HandsUp", new Vector3(80, 240, 0));
        stateHandsUp.motion = hangingClip;
        stateHandsUp.writeDefaultValues = true;

        var toHandsUp = emptyState.AddTransition(stateHandsUp);
        toHandsUp.AddCondition(AnimatorConditionMode.If, 0, "IsThrilled");
        toHandsUp.AddCondition(AnimatorConditionMode.Equals, 0, "ThrillType");
        toHandsUp.hasExitTime = false;
        toHandsUp.duration = 0.2f;

        var handsUpToEmpty = stateHandsUp.AddTransition(emptyState);
        handsUpToEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, "IsThrilled");
        handsUpToEmpty.hasExitTime = false;
        handsUpToEmpty.duration = 0.25f;

        // 2. Thrill Type 1: Vẫy tay phấn khích (Waving)
        AnimatorState stateWaving = upperLayer.stateMachine.AddState("Thrill_Waving", new Vector3(240, 240, 0));
        stateWaving.motion = wavingClip;
        stateWaving.writeDefaultValues = true;

        var toWaving = emptyState.AddTransition(stateWaving);
        toWaving.AddCondition(AnimatorConditionMode.If, 0, "IsThrilled");
        toWaving.AddCondition(AnimatorConditionMode.Equals, 1, "ThrillType");
        toWaving.hasExitTime = false;
        toWaving.duration = 0.2f;

        var wavingToEmpty = stateWaving.AddTransition(emptyState);
        wavingToEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, "IsThrilled");
        wavingToEmpty.hasExitTime = false;
        wavingToEmpty.duration = 0.25f;

        // 3. Thrill Type 2: Bám chặt rung lắc / Co tay mạo hiểm (Braced Hang Shimmy)
        AnimatorState stateBraced = upperLayer.stateMachine.AddState("Thrill_BracedHang", new Vector3(400, 240, 0));
        stateBraced.motion = bracedClip;
        stateBraced.writeDefaultValues = true;

        var toBraced = emptyState.AddTransition(stateBraced);
        toBraced.AddCondition(AnimatorConditionMode.If, 0, "IsThrilled");
        toBraced.AddCondition(AnimatorConditionMode.Equals, 2, "ThrillType");
        toBraced.hasExitTime = false;
        toBraced.duration = 0.2f;

        var bracedToEmpty = stateBraced.AddTransition(emptyState);
        bracedToEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, "IsThrilled");
        bracedToEmpty.hasExitTime = false;
        bracedToEmpty.duration = 0.25f;

        // 4. Thrill Type 3: Chới với rơi tự do (Falling Arms)
        AnimatorState stateFalling = upperLayer.stateMachine.AddState("Thrill_Falling", new Vector3(560, 240, 0));
        stateFalling.motion = fallingClip;
        stateFalling.writeDefaultValues = true;

        var toFalling = emptyState.AddTransition(stateFalling);
        toFalling.AddCondition(AnimatorConditionMode.If, 0, "IsThrilled");
        toFalling.AddCondition(AnimatorConditionMode.Equals, 3, "ThrillType");
        toFalling.hasExitTime = false;
        toFalling.duration = 0.2f;

        var fallingToEmpty = stateFalling.AddTransition(emptyState);
        fallingToEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, "IsThrilled");
        fallingToEmpty.hasExitTime = false;
        fallingToEmpty.duration = 0.25f;

        controller.AddLayer(upperLayer);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[NPCSetupTool] Đã tạo thành công AnimatorController tích hợp 4 kiểu Thrill Reactions tại: " + AnimatorPath);

        // 4. Tự động gán vào các NPC đang có trong Scene
        ApplyToSceneNPCs(controller);

        // 5. Thiết lập hệ thống 4 hành khách ngẫu nhiên trên CoasterRig
        SetupCoasterPassengers();

        // 6. Thiết lập Player NPC Body Controller
        SetupPlayerNPCBody();

        // 7. Thiết lập sàn nhà ga vững chắc chống rơi sàn
        EnsureStationSolidColliders();

        // 8. Đảm bảo toàn bộ WalkZone là Trigger để người chơi đi lại tự do không bị chặn
        EnsureAllWalkZonesAreTriggers();
    }

    public static void EnsurePlayerBodyLayerRegistered()
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        bool exists = false;
        for (int i = 6; i < 32; i++)
        {
            SerializedProperty sp = layers.GetArrayElementAtIndex(i);
            if (sp.stringValue == "PlayerBody")
            {
                exists = true;
                break;
            }
        }

        if (!exists)
        {
            for (int i = 6; i < 32; i++)
            {
                SerializedProperty sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = "PlayerBody";
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[NPCSetupTool] Đã tự động đăng ký Layer '{sp.stringValue}' tại index {i}");
                    break;
                }
            }
        }
    }

    [MenuItem("Tools/NPC System/1-Click Setup Player Representative NPC Body")]
    public static void SetupPlayerNPCBody()
    {
        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)");
        if (xrOrigin == null) xrOrigin = GameObject.FindGameObjectWithTag("Player");
        if (xrOrigin == null) return;

        PlayerNPCBodyController bodyCtrl = xrOrigin.GetComponent<PlayerNPCBodyController>();
        if (bodyCtrl == null)
        {
            bodyCtrl = xrOrigin.AddComponent<PlayerNPCBodyController>();
            Undo.RegisterCreatedObjectUndo(bodyCtrl, "Add PlayerNPCBodyController");
        }

        bodyCtrl.ResolvePlayerReferences();
        bodyCtrl.SetupCameraCulling();
        bodyCtrl.LoadDefaultAssetsIfEmpty();
        bodyCtrl.standingScale = new Vector3(1.22f, 1.22f, 1.22f);
        bodyCtrl.seatedScale = new Vector3(1.22f, 1.22f, 1.22f);

        EditorUtility.SetDirty(bodyCtrl);
        Debug.Log("<color=#00FFCC><b>[NPCSetupTool] Đã thiết lập xong PlayerNPCBodyController cho người chơi!</b></color>");
    }

    [MenuItem("Tools/NPC System/1-Click Ensure All WalkZones Are Triggers (Fix Blocked Walking)")]
    public static void EnsureAllWalkZonesAreTriggers()
    {
        BoxCollider[] allBoxes = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int fixedCount = 0;
        foreach (var b in allBoxes)
        {
            if (b == null) continue;
            string n = b.gameObject.name.ToLower();
            bool isZone = n.Contains("walkzone") || n.Contains("walk_zone") || n.Contains("playzone") || n.Contains("ticketzone") || (b.transform.parent != null && b.transform.parent.name.Equals("Zone", System.StringComparison.OrdinalIgnoreCase));

            // Không can thiệp vào sàn vật lý của nhà ga
            if (n.Contains("solidfloor") || n.Contains("platform")) continue;

            if (isZone)
            {
                if (!b.isTrigger)
                {
                    b.isTrigger = true;
                    EditorUtility.SetDirty(b);
                    fixedCount++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"<color=#00FF88><b>[NPCSetupTool] Đã chuyển {fixedCount} BoxCollider của WalkZone sang 'Is Trigger = TRUE' (Không còn bị tường tàng hình cản đường)!</b></color>");
    }

    [MenuItem("Tools/NPC System/1-Click Ensure Station Solid Floor Colliders")]
    public static void EnsureStationSolidColliders()
    {
        GameObject wtsMock = GameObject.Find("WTS_Mock");
        if (wtsMock == null) return;

        Transform floorRoot = wtsMock.transform.Find("Station_SolidFloors");
        if (floorRoot == null)
        {
            GameObject go = new GameObject("Station_SolidFloors");
            go.transform.SetParent(wtsMock.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            floorRoot = go.transform;
            Undo.RegisterCreatedObjectUndo(go, "Create Station_SolidFloors");
        }

        EnsureBoxColliderChild(floorRoot, "UpperPlatform_SolidFloor", new Vector3(0.06f, 0.04f, 0.12f), new Vector3(6.5f, 0.15f, 6.5f));
        EnsureBoxColliderChild(floorRoot, "LowerPlatform_SolidFloor", new Vector3(0.06f, 0.01f, 0.12f), new Vector3(6.5f, 0.15f, 6.5f));
        EnsureBoxColliderChild(floorRoot, "Platform_ConnectingRamp", new Vector3(0.06f, 0.025f, 0.12f), new Vector3(6.5f, 0.15f, 2.0f));

        // Căn chỉnh điểm đón khách VR_FloorPoint nằm sát trên mặt sàn nhà ga, không để lơ lửng trên không trung
        GameObject vrFloor = GameObject.Find("VR_FloorPoint");
        if (vrFloor != null)
        {
            if (vrFloor.transform.parent != null && vrFloor.transform.parent.name.Contains("WTS_Mock"))
            {
                vrFloor.transform.localPosition = new Vector3(0.06f, 0.042f, 0.12f);
                EditorUtility.SetDirty(vrFloor);
            }
        }

        EditorUtility.SetDirty(wtsMock);
        Debug.Log("<color=#00FF99><b>[NPCSetupTool] Đã củng cố sàn nhà ga bằng BoxCollider chuẩn chống rơi sàn và chỉnh VR_FloorPoint xuống mặt sàn!</b></color>");
    }

    private static void EnsureBoxColliderChild(Transform parent, string name, Vector3 localCenter, Vector3 size)
    {
        Transform child = parent.Find(name);
        GameObject go;
        if (child == null)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }
        else
        {
            go = child.gameObject;
        }

        BoxCollider box = go.GetComponent<BoxCollider>();
        if (box == null) box = go.AddComponent<BoxCollider>();
        box.center = localCenter;
        box.size = size;
        box.isTrigger = false; // Solid physical floor
    }

    [MenuItem("Tools/NPC System/1-Click Setup Coaster 4 Random Passengers")]
    public static void SetupCoasterPassengers()
    {
        string scenePath = "Assets/Scenes/ParkScene.unity";
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != scenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
            }
            else
            {
                return;
            }
        }

        GameObject coasterRig = GameObject.Find("CoasterRig");
        if (coasterRig == null)
        {
            SeatSwitcher sw = Object.FindAnyObjectByType<SeatSwitcher>();
            if (sw != null) coasterRig = sw.gameObject;
        }

        if (coasterRig == null)
        {
            Debug.LogWarning("[NPCSetupTool] Không tìm thấy CoasterRig trong Scene!");
            return;
        }

        CoasterPassengerManager passengerMgr = coasterRig.GetComponent<CoasterPassengerManager>();
        if (passengerMgr == null)
        {
            passengerMgr = coasterRig.AddComponent<CoasterPassengerManager>();
            Undo.RegisterCreatedObjectUndo(passengerMgr, "Add CoasterPassengerManager");
        }

        RuntimeAnimatorController animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath);
        if (animCtrl != null) passengerMgr.masterAnimatorController = animCtrl;

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

        List<GameObject> loadedList = new List<GameObject>();
        foreach (var path in defaultPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) loadedList.Add(prefab);
        }
        if (loadedList.Count > 0)
        {
            passengerMgr.npcPrefabs = loadedList.ToArray();
        }

        passengerMgr.npcScale = new Vector3(1.22f, 1.22f, 1.22f);
        passengerMgr.stationStandingScale = new Vector3(1.22f, 1.22f, 1.22f);
        passengerMgr.EnsureSitPoints();
        EditorUtility.SetDirty(passengerMgr);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("<color=#00FF66><b>[NPCSetupTool] Đã cấu hình xong hệ thống hành khách trên tàu!</b></color>");
    }

    private static void EnsureClipLooping(string fbxPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.clipAnimations;
            }
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].loopTime = true;
                    clips[i].loopPose = true;
                    clips[i].wrapMode = WrapMode.Loop;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            else
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static AnimationClip LoadFirstClipFromFBX(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object obj in assets)
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }

    private static BoxCollider[] FindAllWalkZones()
    {
        List<BoxCollider> list = new List<BoxCollider>();
        BoxCollider[] allBoxes = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var b in allBoxes)
        {
            if (b == null) continue;
            string n = b.gameObject.name.ToLower();
            if (n.Contains("walkzone") || n.Contains("walk_zone") || n.Contains("playzone"))
            {
                list.Add(b);
            }
        }
        return list.ToArray();
    }

    [MenuItem("Tools/NPC System/1-Click Remove Station Solid Floor Colliders")]
    public static void RemoveStationSolidColliders()
    {
        GameObject wtsMock = GameObject.Find("WTS_Mock");
        if (wtsMock != null)
        {
            Transform floorRoot = wtsMock.transform.Find("Station_SolidFloors");
            if (floorRoot != null)
            {
                Undo.DestroyObjectImmediate(floorRoot.gameObject);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("<color=#FF6600><b>[NPCSetupTool] Đã gỡ bỏ Station_SolidFloors khỏi nhà ga WTS_Mock!</b></color>");
                EditorUtility.DisplayDialog("Xóa Thành Công", "Đã xóa Station_SolidFloors khỏi nhà ga WTS_Mock.", "OK");
                return;
            }
        }
        EditorUtility.DisplayDialog("Thông Báo", "Không tìm thấy Station_SolidFloors trong WTS_Mock.", "OK");
    }

    [MenuItem("Tools/NPC System/1-Click Remove Player Representative NPC Body")]
    public static void RemovePlayerNPCBody()
    {
        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)");
        if (xrOrigin == null) xrOrigin = GameObject.FindGameObjectWithTag("Player");
        if (xrOrigin != null)
        {
            PlayerNPCBodyController bodyCtrl = xrOrigin.GetComponent<PlayerNPCBodyController>();
            if (bodyCtrl != null)
            {
                Undo.DestroyObjectImmediate(bodyCtrl);
            }

            Transform repBody = xrOrigin.transform.Find("PlayerRepresentativeBody");
            if (repBody != null)
            {
                Undo.DestroyObjectImmediate(repBody.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=#FF6600><b>[NPCSetupTool] Đã gỡ bỏ PlayerNPCBodyController khỏi XR Origin!</b></color>");
            EditorUtility.DisplayDialog("Xóa Thành Công", "Đã xóa PlayerNPCBodyController và Avatar đại diện khỏi XR Origin.", "OK");
            return;
        }
        EditorUtility.DisplayDialog("Thông Báo", "Không tìm thấy XR Origin trong Scene.", "OK");
    }

    [MenuItem("Tools/NPC System/1-Click Remove Coaster Passengers Setup")]
    public static void RemoveCoasterPassengers()
    {
        GameObject coasterRig = GameObject.Find("CoasterRig");
        if (coasterRig == null)
        {
            SeatSwitcher sw = Object.FindAnyObjectByType<SeatSwitcher>();
            if (sw != null) coasterRig = sw.gameObject;
        }

        if (coasterRig != null)
        {
            CoasterPassengerManager mgr = coasterRig.GetComponent<CoasterPassengerManager>();
            if (mgr != null)
            {
                Undo.DestroyObjectImmediate(mgr);
            }

            for (int i = coasterRig.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = coasterRig.transform.GetChild(i);
                if (child.name.StartsWith("Passenger_"))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=#FF6600><b>[NPCSetupTool] Đã gỡ bỏ CoasterPassengerManager và các hành khách tạm khỏi CoasterRig!</b></color>");
            EditorUtility.DisplayDialog("Xóa Thành Công", "Đã gỡ bỏ CoasterPassengerManager khỏi CoasterRig.", "OK");
            return;
        }
        EditorUtility.DisplayDialog("Thông Báo", "Không tìm thấy CoasterRig trong Scene.", "OK");
    }

    [MenuItem("Tools/NPC System/1-Click Cleanup All Generated NPC & Station Setup")]
    public static void CleanupAllNPCSetup()
    {
        bool confirm = EditorUtility.DisplayDialog("Xác Nhận Xóa Toàn Bộ NPC Setup", 
            "Bạn có chắc muốn xóa/gỡ bỏ toàn bộ sàn nhà ga phụ (Station_SolidFloors), PlayerNPCBodyController trên XR Rig, và CoasterPassengerManager trên tàu lượn không?", 
            "Xác Nhận Xóa", "Hủy");
        if (!confirm) return;

        // 1. Xóa Sàn nhà ga phụ
        GameObject wtsMock = GameObject.Find("WTS_Mock");
        if (wtsMock != null)
        {
            Transform floorRoot = wtsMock.transform.Find("Station_SolidFloors");
            if (floorRoot != null) Undo.DestroyObjectImmediate(floorRoot.gameObject);
        }

        // 2. Xóa Player Body Controller
        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)");
        if (xrOrigin == null) xrOrigin = GameObject.FindGameObjectWithTag("Player");
        if (xrOrigin != null)
        {
            PlayerNPCBodyController bodyCtrl = xrOrigin.GetComponent<PlayerNPCBodyController>();
            if (bodyCtrl != null) Undo.DestroyObjectImmediate(bodyCtrl);

            Transform repBody = xrOrigin.transform.Find("PlayerRepresentativeBody");
            if (repBody != null) Undo.DestroyObjectImmediate(repBody.gameObject);
        }

        // 3. Xóa Coaster Passengers
        GameObject coasterRig = GameObject.Find("CoasterRig");
        if (coasterRig == null)
        {
            SeatSwitcher sw = Object.FindAnyObjectByType<SeatSwitcher>();
            if (sw != null) coasterRig = sw.gameObject;
        }
        if (coasterRig != null)
        {
            CoasterPassengerManager mgr = coasterRig.GetComponent<CoasterPassengerManager>();
            if (mgr != null) Undo.DestroyObjectImmediate(mgr);

            for (int i = coasterRig.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = coasterRig.transform.GetChild(i);
                if (child.name.StartsWith("Passenger_"))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        // 4. Gỡ ParkNPCWanderer trên các NPC công viên nếu có
        GameObject npcRoot = GameObject.Find("NPC");
        if (npcRoot != null)
        {
            ParkNPCWanderer[] wanderers = npcRoot.GetComponentsInChildren<ParkNPCWanderer>(true);
            foreach (var w in wanderers)
            {
                if (w != null) Undo.DestroyObjectImmediate(w);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=#FF6600><b>[NPCSetupTool] Đã dọn dẹp sạch toàn bộ các thành phần NPC & Nhà Ga vừa tạo!</b></color>");
        EditorUtility.DisplayDialog("Dọn Dẹp Hoàn Tất", "Đã xóa sạch toàn bộ các thành phần NPC, Player Body, Coaster Passengers và Sàn nhà ga phụ!", "OK");
    }

    private static void ApplyToSceneNPCs(RuntimeAnimatorController controller)
    {
        GameObject npcRoot = GameObject.Find("NPC");
        if (npcRoot == null) return;

        BoxCollider[] walkZones = FindAllWalkZones();

        int count = 0;
        foreach (Transform child in npcRoot.transform)
        {
            // Tắt script CityPeople cũ tránh lỗi gọi PlayAnyClip / CrossFade
            MonoBehaviour cp = child.GetComponent("CityPeople") as MonoBehaviour;
            if (cp != null) cp.enabled = false;

            Animator anim = child.GetComponent<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
            }

            ParkNPCWanderer wanderer = child.GetComponent<ParkNPCWanderer>();
            if (wanderer == null)
            {
                wanderer = child.gameObject.AddComponent<ParkNPCWanderer>();
            }

            wanderer.boundaryMode = ParkNPCWanderer.BoundaryMode.BoxZoneCollider;
            wanderer.walkZoneColliders = walkZones;
            wanderer.boundaryMargin = 0.35f;

            EditorUtility.SetDirty(wanderer);
            count++;
        }

        Debug.Log($"[NPCSetupTool] Đã tự động gắn Animator và ParkNPCWanderer cho {count} nhân vật trong 'NPC'!");
    }
}
#endif
