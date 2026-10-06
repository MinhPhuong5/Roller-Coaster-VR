using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Công cụ 1-Click tạo và cấu hình Hệ thống Cột Loa 3D Spatial Nhạc Công Viên (3D Park Speakers System)
/// </summary>
public class ParkMusicSetupTool : Editor
{
    [MenuItem("Roller Coaster/Tạo Trình Phát Nhạc Công Viên (Park BGM 3D)", false, 20)]
    public static void CreateParkMusicPlayer()
    {
        // 1. Tìm hoặc tạo GameObject cha [PARK_BGM_PLAYER] trong Scene
        GameObject bgmObj = GameObject.Find("[PARK_BGM_PLAYER]");
        if (bgmObj == null)
        {
            bgmObj = GameObject.Find("ParkMusicPlayer");
        }
        if (bgmObj == null)
        {
            bgmObj = GameObject.Find("ParkPlaylist");
        }

        if (bgmObj == null)
        {
            bgmObj = new GameObject("[PARK_BGM_PLAYER]");
            bgmObj.transform.position = new Vector3(0f, 0f, 40f);
            Undo.RegisterCreatedObjectUndo(bgmObj, "Create Park BGM Player");
        }

        // 2. Thêm component ParkPlaylist
        ParkPlaylist playlist = bgmObj.GetComponent<ParkPlaylist>();
        if (playlist == null)
        {
            playlist = Undo.AddComponent<ParkPlaylist>(bgmObj);
        }

        // 3. Tạo 3 Cột Loa 3D đặt cố định tại các vị trí trong công viên
        List<Transform> speakers = new List<Transform>();

        // Cột loa 1: Quảng trường trung tâm công viên
        Transform spk1 = bgmObj.transform.Find("Speaker_Plaza_Center");
        if (spk1 == null)
        {
            GameObject spkObj1 = new GameObject("Speaker_Plaza_Center");
            spkObj1.transform.SetParent(bgmObj.transform, false);
            spkObj1.transform.position = new Vector3(0f, 2.5f, 42.0f);
            spk1 = spkObj1.transform;
            Undo.RegisterCreatedObjectUndo(spkObj1, "Create Speaker_Plaza_Center");
        }
        speakers.Add(spk1);

        // Cột loa 2: Gần Quầy vé & Kiosk
        Transform spk2 = bgmObj.transform.Find("Speaker_Ticket_Kiosk");
        if (spk2 == null)
        {
            GameObject spkObj2 = new GameObject("Speaker_Ticket_Kiosk");
            spkObj2.transform.SetParent(bgmObj.transform, false);
            spkObj2.transform.position = new Vector3(1.85f, 2.5f, 26.0f);
            spk2 = spkObj2.transform;
            Undo.RegisterCreatedObjectUndo(spkObj2, "Create Speaker_Ticket_Kiosk");
        }
        speakers.Add(spk2);

        // Cột loa 3: Lối đi dạo công viên
        Transform spk3 = bgmObj.transform.Find("Speaker_Garden_Walkway");
        if (spk3 == null)
        {
            GameObject spkObj3 = new GameObject("Speaker_Garden_Walkway");
            spkObj3.transform.SetParent(bgmObj.transform, false);
            spkObj3.transform.position = new Vector3(-16.0f, 2.5f, 48.0f);
            spk3 = spkObj3.transform;
            Undo.RegisterCreatedObjectUndo(spkObj3, "Create Speaker_Garden_Walkway");
        }
        speakers.Add(spk3);

        playlist.speakerPositions = speakers;
        playlist.Setup3DSpeakers();

        // 4. Tự động nạp toàn bộ file nhạc từ thư mục Assets/Sound/Park
        playlist.AutoLoadClipsIfEmpty();

        // 5. Chọn Object trong Hierarchy và lưu Scene
        Selection.activeGameObject = bgmObj;
        EditorUtility.SetDirty(bgmObj);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        int count = playlist.playlist != null ? playlist.playlist.Length : 0;
        EditorUtility.DisplayDialog(
            "Hệ Thống Loa Nhạc 3D Công Viên",
            $"Đã tạo thành công đối tượng '[PARK_BGM_PLAYER]' cùng 3 Cột Loa 3D cố định trong công viên:\n\n" +
            $"🔊 1. Speaker_Plaza_Center (Quảng trường)\n" +
            $"🔊 2. Speaker_Ticket_Kiosk (Quầy vé & Kiosk)\n" +
            $"🔊 3. Speaker_Garden_Walkway (Lối đi dạo)\n\n" +
            $"✅ TÍNH NĂNG 3D SPATIAL:\n" +
            $"- Âm thanh phát 100% từ vị trí thực của loa trong thế giới 3D.\n" +
            $"- Ở gần loa nghe to, đi xa nhỏ dần và tự động tắt hẳn khi bước sang nhà ga / lên tàu lượn!\n" +
            $"- Bạn có thể tự do kéo thả vị trí 3 cột loa trong tab Scene theo ý muốn.\n\n" +
            $"Số bài nhạc hiện có trong Assets/Sound/Park: {count} bài.",
            "OK"
        );
    }
}
