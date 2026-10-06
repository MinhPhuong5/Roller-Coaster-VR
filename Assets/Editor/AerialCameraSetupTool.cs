using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Công cụ Editor cài đặt và căn chỉnh góc nhìn Flycam toàn cảnh (Aerial Camera) trực tiếp trong Unity Editor:
/// - Menu 'Roller Coaster' trên thanh công cụ Unity: 1-Click tạo sẵn 2 điểm mốc trong Hierarchy.
/// - Căn chỉnh góc nhìn tức thời: Đang ngắm ở tab Scene chỉ cần bấm nút là tự động gán vào mốc.
/// </summary>
public class AerialCameraSetupTool : EditorWindow
{
    [MenuItem("Roller Coaster/1. Tạo 2 Mốc Quan Sát Flycam Vào Scene", false, 10)]
    public static void SetupAerialCameraInScene()
    {
        // 1. Tìm hoặc thêm CoasterAerialCameraController vào CoasterRig
        GameObject coasterRig = GameObject.Find("CoasterRig");
        if (coasterRig == null)
        {
            coasterRig = GameObject.Find("Ghost_Tracker");
        }

        if (coasterRig == null)
        {
            RideController rc = Object.FindAnyObjectByType<RideController>();
            if (rc != null) coasterRig = rc.gameObject;
        }

        if (coasterRig == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy CoasterRig hoặc RideController trong Scene!", "OK");
            return;
        }

        CoasterAerialCameraController camCtrl = coasterRig.GetComponent<CoasterAerialCameraController>();
        if (camCtrl == null)
        {
            camCtrl = coasterRig.AddComponent<CoasterAerialCameraController>();
        }

        Vector3 basePos = coasterRig.transform.position;

        // 2. Tạo Mốc 1: Static_Aerial_Point_Start (Đầu chặng - Lao dốc dài)
        GameObject point1 = GameObject.Find("Static_Aerial_Point_Start");
        if (point1 == null)
        {
            point1 = new GameObject("Static_Aerial_Point_Start");
            point1.transform.SetParent(null);
            point1.transform.position = basePos + new Vector3(-20.0f, 30.0f, 22.0f);
            point1.transform.rotation = Quaternion.Euler(30f, 130f, 0f);
            Undo.RegisterCreatedObjectUndo(point1, "Create Static_Aerial_Point_Start");
        }

        // 3. Tạo Mốc 2: Static_Aerial_Point_End (Cuối chặng - Tháp xoắn ốc)
        GameObject point2 = GameObject.Find("Static_Aerial_Point_End");
        if (point2 == null)
        {
            point2 = new GameObject("Static_Aerial_Point_End");
            point2.transform.SetParent(null);
            point2.transform.position = basePos + new Vector3(25.0f, 35.0f, -28.0f);
            point2.transform.rotation = Quaternion.Euler(40f, 315f, 0f);
            Undo.RegisterCreatedObjectUndo(point2, "Create Static_Aerial_Point_End");
        }

        // 4. Cấu hình vào CoasterAerialCameraController
        camCtrl.ResolveReferences();
        if (camCtrl.aerialSegments == null || camCtrl.aerialSegments.Count < 2)
        {
            camCtrl.aerialSegments = new System.Collections.Generic.List<CoasterAerialCameraController.AerialShotSegment>
            {
                new CoasterAerialCameraController.AerialShotSegment
                {
                    segmentName = "1. Đại dốc 1 (Đầu chặng - Bám tàu)",
                    aerialPoint = point1.transform,
                    startLoopTime = 7.0f,
                    endLoopTime = 23.5f,
                    transitionDuration = 1.5f,
                    targetLap = -1,
                    trackCoasterTarget = true,
                    trackingDamping = 3.5f
                },
                new CoasterAerialCameraController.AerialShotSegment
                {
                    segmentName = "2. Tháp xoắn ốc (Cuối chặng - Camera tĩnh)",
                    aerialPoint = point2.transform,
                    startLoopTime = 72.0f,
                    endLoopTime = 86.0f,
                    transitionDuration = 1.5f,
                    targetLap = -1,
                    trackCoasterTarget = false, // Tĩnh 100%
                    trackingDamping = 0f
                }
            };
        }
        else
        {
            camCtrl.aerialSegments[0].aerialPoint = point1.transform;
            camCtrl.aerialSegments[0].trackCoasterTarget = true;

            camCtrl.aerialSegments[1].aerialPoint = point2.transform;
            camCtrl.aerialSegments[1].trackCoasterTarget = false;
        }

        EditorUtility.SetDirty(camCtrl);
        EditorUtility.SetDirty(coasterRig);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        // Chọn mốc 1 để người dùng thấy ngay trên Scene View
        Selection.activeGameObject = point1;
        EditorGUIUtility.PingObject(point1);

        EditorUtility.DisplayDialog("Thành Công", "Đã tạo thành công 2 GameObject trong Hierarchy:\n- Static_Aerial_Point_Start (Đoạn 1)\n- Static_Aerial_Point_End (Đoạn 2)\n\nBạn có thể bay trong tab Scene ngắm góc đẹp rồi dùng Menu 'Roller Coaster' để gán tức thì!", "OK");
    }

    [MenuItem("Roller Coaster/2. Gán Góc Nhìn Scene Hiện Tại Cho Điểm 1 (Đầu Chặng)", false, 20)]
    public static void AlignSceneViewToPoint1()
    {
        if (SceneView.lastActiveSceneView == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Vui lòng mở tab Scene View trước!", "OK");
            return;
        }

        GameObject point1 = GameObject.Find("Static_Aerial_Point_Start");
        if (point1 == null)
        {
            SetupAerialCameraInScene();
            point1 = GameObject.Find("Static_Aerial_Point_Start");
        }

        if (point1 != null)
        {
            Camera scCam = SceneView.lastActiveSceneView.camera;
            Undo.RecordObject(point1.transform, "Align Point 1 to Scene View");
            point1.transform.position = scCam.transform.position;
            point1.transform.rotation = scCam.transform.rotation;
            EditorUtility.SetDirty(point1);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = point1;
            EditorGUIUtility.PingObject(point1);

            Debug.Log($"<color=#00FF88><b>[AerialCameraSetupTool] ĐÃ GÁN GÓC NHÌN CHO ĐOẠN 1 (Đầu chặng): Tọa độ = {point1.transform.position}, Góc xoay = {point1.transform.eulerAngles}</b></color>");
        }
    }

    [MenuItem("Roller Coaster/3. Gán Góc Nhìn Scene Hiện Tại Cho Điểm 2 (Cuối Chặng)", false, 21)]
    public static void AlignSceneViewToPoint2()
    {
        if (SceneView.lastActiveSceneView == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Vui lòng mở tab Scene View trước!", "OK");
            return;
        }

        GameObject point2 = GameObject.Find("Static_Aerial_Point_End");
        if (point2 == null)
        {
            SetupAerialCameraInScene();
            point2 = GameObject.Find("Static_Aerial_Point_End");
        }

        if (point2 != null)
        {
            Camera scCam = SceneView.lastActiveSceneView.camera;
            Undo.RecordObject(point2.transform, "Align Point 2 to Scene View");
            point2.transform.position = scCam.transform.position;
            point2.transform.rotation = scCam.transform.rotation;
            EditorUtility.SetDirty(point2);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = point2;
            EditorGUIUtility.PingObject(point2);

            Debug.Log($"<color=#00FF88><b>[AerialCameraSetupTool] ĐÃ GÁN GÓC NHÌN CHO ĐOẠN 2 (Cuối chặng): Tọa độ = {point2.transform.position}, Góc xoay = {point2.transform.eulerAngles}</b></color>");
        }
    }
}

/// <summary>
/// Tùy biến giao diện Inspector cho CoasterAerialCameraController với các nút bấm thao tác 1 chạm cực kỳ tiện lợi
/// </summary>
[CustomEditor(typeof(CoasterAerialCameraController))]
public class CoasterAerialCameraControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CoasterAerialCameraController ctrl = (CoasterAerialCameraController)target;

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("CHỨC NĂNG FLYCAM TOÀN CẢNH (STATIC AERIAL OVERVIEW)\n- Đoạn 1: Bám theo tàu (trackCoasterTarget = true)\n- Đoạn 2: Tĩnh nhìn tháp xoắn ốc (trackCoasterTarget = false)", MessageType.Info);
        EditorGUILayout.Space(5);

        // Nút tạo 2 mốc
        GUI.backgroundColor = new Color(0.2f, 0.8f, 1f);
        if (GUILayout.Button("📍 1. Tạo / Tìm 2 Mốc Quan Sát Trong Hierarchy", GUILayout.Height(32)))
        {
            AerialCameraSetupTool.SetupAerialCameraInScene();
        }

        EditorGUILayout.Space(4);

        // Nút gán góc nhìn Scene cho mốc 1
        GUI.backgroundColor = new Color(0.3f, 1f, 0.5f);
        if (GUILayout.Button("📸 2. Gán Góc Nhìn Scene Hiện Tại Cho Đoạn 1 (Start)", GUILayout.Height(28)))
        {
            AerialCameraSetupTool.AlignSceneViewToPoint1();
        }

        // Nút gán góc nhìn Scene cho mốc 2
        GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
        if (GUILayout.Button("📸 3. Gán Góc Nhìn Scene Hiện Tại Cho Đoạn 2 (End)", GUILayout.Height(28)))
        {
            AerialCameraSetupTool.AlignSceneViewToPoint2();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(10);

        // Vẽ Inspector mặc định
        DrawDefaultInspector();
    }
}
