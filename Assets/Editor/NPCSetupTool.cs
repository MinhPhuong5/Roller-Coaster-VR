#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class NPCSetupTool
{
    private const string MaskPath = "Assets/3D Model/NPC/UpperBody_Mask.mask";
    private const string AnimatorPath = "Assets/3D Model/NPC/NPC_Master_Animator.controller";

    [MenuItem("Tools/NPC System/1-Click Setup NPC Animator & Avatar Mask")]
    public static void SetupNPCSystem()
    {
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
        AnimationClip fallClip = LoadFirstClipFromFBX("Assets/3D Model/NPC/X Bot@Falling.fbx");

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
        // UPPER BODY LAYER: Động tác chới với / Giơ tay khi tàu lao dốc
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

        // State trống (mặc định để tay chân ngồi yên theo Base Layer)
        AnimatorState emptyUpperState = upperLayer.stateMachine.AddState("Sitting_Arm_Rest", new Vector3(250, 100, 0));

        // State phản ứng lao dốc (Falling chới với thân trên)
        AnimatorState fallReactionState = upperLayer.stateMachine.AddState("Thrill_Reaction", new Vector3(250, 220, 0));
        fallReactionState.motion = fallClip;

        var toReact = emptyUpperState.AddTransition(fallReactionState);
        toReact.AddCondition(AnimatorConditionMode.If, 0, "IsThrilled");
        toReact.hasExitTime = false;
        toReact.duration = 0.2f;

        var toEmpty = fallReactionState.AddTransition(emptyUpperState);
        toEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, "IsThrilled");
        toEmpty.hasExitTime = false;
        toEmpty.duration = 0.3f;

        controller.AddLayer(upperLayer);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[NPCSetupTool] Đã tạo thành công AnimatorController tích hợp UpperBody Mask tại: " + AnimatorPath);

        // 4. Tự động gán vào các NPC đang có trong Scene
        ApplyToSceneNPCs(controller);
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

    private static void ApplyToSceneNPCs(RuntimeAnimatorController controller)
    {
        GameObject npcRoot = GameObject.Find("NPC");
        if (npcRoot == null) return;

        int count = 0;
        foreach (Transform child in npcRoot.transform)
        {
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

            count++;
        }

        Debug.Log($"[NPCSetupTool] Đã tự động gắn Animator và ParkNPCWanderer cho {count} nhân vật trong 'NPC'!");
    }

    [MenuItem("Tools/NPC System/1-Click Restore & Auto-Spawn All NPCs")]
    public static void RestoreAndAutoSpawnNPCs()
    {
        // 1. Đảm bảo AnimatorController & AvatarMask đã có
        SetupNPCSystem();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath);

        // 2. Tìm hoặc tạo GameObject cha "NPC"
        GameObject npcRoot = GameObject.Find("NPC");
        if (npcRoot == null)
        {
            npcRoot = new GameObject("NPC");
            Undo.RegisterCreatedObjectUndo(npcRoot, "Create NPC Root");
        }

        // 3. Danh sách các Prefab nhân vật đầy đủ mọi thành phần
        string[] prefabPaths = new string[]
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
            "Assets/3D Model/NPC/CityPeople_Free/Prefabs/Female_Adult/Female_Adult_ColorA.prefab"
        };

        // Xác định vị trí trung tâm đường dạo công viên
        Vector3 baseSpawnPos = new Vector3(-1297f, 906.5f, 1121f);
        GameObject returnPoint = GameObject.Find("CoasterReturrnMapPoint");
        if (returnPoint != null)
        {
            baseSpawnPos = returnPoint.transform.position + returnPoint.transform.forward * 4f;
        }

        // Tự động nhận diện scale: Nếu Map_CongVien đã thu nhỏ (<0.5) thì NPC scale 1, nếu map cũ thì NPC scale 17
        GameObject mapParent = GameObject.Find("Map_CongVien");
        float npcScale = (mapParent != null && mapParent.transform.localScale.x < 0.5f) ? 1.0f : 17.0f;
        if (mapParent != null && mapParent.transform.localScale.x < 0.5f)
        {
            baseSpawnPos = mapParent.transform.TransformPoint(new Vector3(0, 0, 0));
        }

        // Xóa các con cũ nếu có trong NPC để tránh trùng lặp
        while (npcRoot.transform.childCount > 0)
        {
            Undo.DestroyObjectImmediate(npcRoot.transform.GetChild(0).gameObject);
        }

        // Spawn từng nhân vật và xếp vị trí rải đều
        int spawnedCount = 0;
        for (int i = 0; i < prefabPaths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
            if (prefab == null) continue;

            GameObject npcInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, npcRoot.transform);
            Undo.RegisterCreatedObjectUndo(npcInstance, "Spawn NPC");

            // Rải vị trí so le nhau tránh va chạm dính chụm
            float spacing = 3.5f * (npcScale > 1f ? npcScale * 0.25f : 1.2f);
            float offsetX = ((i % 4) - 1.5f) * spacing;
            float offsetZ = (i / 4) * (spacing * 1.3f);
            npcInstance.transform.position = baseSpawnPos + new Vector3(offsetX, 0f, offsetZ);
            npcInstance.transform.localScale = Vector3.one * npcScale;

            // Gán Animator & Wanderer
            Animator anim = npcInstance.GetComponent<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
            }

            ParkNPCWanderer wanderer = npcInstance.GetComponent<ParkNPCWanderer>();
            if (wanderer == null)
            {
                wanderer = npcInstance.AddComponent<ParkNPCWanderer>();
            }

            spawnedCount++;
        }

        // Đảm bảo XR Origin có tốc độ đi bộ chuẩn
        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)");
        if (xrOrigin != null)
        {
            XRFallbackWalkController walk = xrOrigin.GetComponent<XRFallbackWalkController>();
            if (walk != null)
            {
                walk.walkSpeed = (npcScale > 1f) ? 15f : 3.5f;
                walk.runSpeed = (npcScale > 1f) ? 30f : 7.0f;
                walk.gravity = -9.81f;
                EditorUtility.SetDirty(walk);
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log($"<color=#00FF66><b>[NPCSetupTool] Đã tự động khôi phục và spawn {spawnedCount} NPC vào công viên hoàn chỉnh!</b></color>");
        EditorUtility.DisplayDialog("Khôi phục NPC thành công", $"Đã tự động tạo và rải đều {spawnedCount} nhân vật vào đường dạo công viên!\nMọi Animator và Script di chuyển đã được thiết lập sẵn sàng.", "Tuyệt vời");
    }
}
#endif
