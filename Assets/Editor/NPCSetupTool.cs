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

        // 1. Tạo AvatarMask chỉ lấy phần thân trên
        AvatarMask upperBodyMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (upperBodyMask == null)
        {
            upperBodyMask = new AvatarMask();
            upperBodyMask.name = "UpperBody_Mask";

            // Bật thân trên, đầu, 2 tay. Tắt hông và 2 chân
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
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

            AssetDatabase.CreateAsset(upperBodyMask, MaskPath);
            Debug.Log("[NPCSetupTool] Đã tạo thành công: " + MaskPath);
        }

        // 2. Tìm các Animation Clips
        AnimationClip walkClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/DenysAlmaral/CityPeople/Animations/locom_m_basicWalk_30f.fbx");
        AnimationClip idleClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/DenysAlmaral/CityPeople/Animations/idle_m_1_200f.fbx");
        AnimationClip sitClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/X Bot@Sitting Idle.fbx");
        
        // Hoạt ảnh mạo hiểm / giơ vẫy tay sống động
        AnimationClip thrillClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/DenysAlmaral/CityPeople/Animations/dance_hype_100f.fbx");
        if (thrillClip == null) thrillClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/X Bot@Falling.fbx");
        if (thrillClip == null) thrillClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/X Bot@Hanging Idle.fbx");

        // 3. Tạo hoặc nạp AnimatorController
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);

        // Thêm Parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsSitting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsThrilled", AnimatorControllerParameterType.Bool);

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

        var sitToIdle = sitState.AddTransition(idleState);
        sitToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSitting");
        sitToIdle.hasExitTime = false;
        sitToIdle.duration = 0.25f;

        // ==========================================
        // UPPER BODY LAYER: Động tác chới với / Giơ tay linh hoạt khi tàu lao dốc
        // ==========================================
        AnimatorControllerLayer upperLayer = new AnimatorControllerLayer
        {
            name = "UpperBody_Reaction",
            defaultWeight = 1.0f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = upperBodyMask,
            stateMachine = new AnimatorStateMachine()
        };
        upperLayer.stateMachine.name = upperLayer.name;
        upperLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(upperLayer.stateMachine, AnimatorPath);

        // State mặc định: Empty (motion = null). Giúp Base Layer hoàn toàn tự do (tay vung tự nhiên khi đi bộ/đứng ở công viên)
        AnimatorState emptyState = upperLayer.stateMachine.AddState("Empty", new Vector3(250, 100, 0));
        emptyState.motion = null;

        // State phản ứng lao dốc / tốc độ cao (Vẫy tay & Giơ tay sống động)
        AnimatorState thrillReactionState = upperLayer.stateMachine.AddState("Thrill_Reaction", new Vector3(250, 220, 0));
        thrillReactionState.motion = thrillClip;

        var toReact = emptyState.AddTransition(thrillReactionState);
        toReact.AddCondition(AnimatorConditionMode.If, 0, "IsThrilled");
        toReact.hasExitTime = false;
        toReact.duration = 0.2f;

        var toEmpty = thrillReactionState.AddTransition(emptyState);
        toEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, "IsThrilled");
        toEmpty.hasExitTime = false;
        toEmpty.duration = 0.3f;

        controller.AddLayer(upperLayer);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[NPCSetupTool] Đã tạo thành công AnimatorController tích hợp UpperBody Mask tại: " + AnimatorPath);

        // 4. Tự động gán vào các NPC đang có trong Scene
        ApplyToSceneNPCs(controller);

        // 5. Thiết lập hệ thống 4 hành khách ngẫu nhiên trên CoasterRig
        SetupCoasterPassengers();

        // 6. Thiết lập Player NPC Body Controller
        SetupPlayerNPCBody();

        // 7. Thiết lập sàn nhà ga vững chắc chống rơi sàn
        EnsureStationSolidColliders();
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
        bodyCtrl.standingScale = Vector3.one;
        bodyCtrl.seatedScale = new Vector3(1.22f, 1.22f, 1.22f);

        EditorUtility.SetDirty(bodyCtrl);
        Debug.Log("<color=#00FFCC><b>[NPCSetupTool] Đã thiết lập xong PlayerNPCBodyController cho người chơi!</b></color>");
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
        passengerMgr.stationStandingScale = Vector3.one;
        passengerMgr.EnsureSitPoints();
        EditorUtility.SetDirty(passengerMgr);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("<color=#00FF66><b>[NPCSetupTool] Đã cấu hình xong hệ thống hành khách trên tàu!</b></color>");
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
