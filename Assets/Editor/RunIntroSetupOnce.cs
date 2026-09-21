#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RunIntroSetupOnce
{
    static RunIntroSetupOnce()
    {
        EditorApplication.delayCall += RunOnce;
    }

    private static void RunOnce()
    {
        GameObject canvas = GameObject.Find("Canvas_TicketSystem");
        if (canvas != null)
        {
            var intro = canvas.GetComponentInChildren<IntroDialogueController>(true);
            if (intro == null)
            {
                TicketSystemBuilder.BuildTicketSystemUI(silent: true);
                Debug.Log("<color=#00FF88>[IntroDialogue] Tự động cập nhật thành công cảnh hội thoại Haku vào ParkScene!</color>");
            }
        }
    }
}
#endif
