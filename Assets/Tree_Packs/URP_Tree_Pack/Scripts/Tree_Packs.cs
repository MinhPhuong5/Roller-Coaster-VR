#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class Startup
{
    static Startup()    
    {
        EditorPrefs.SetInt("showCounts_URPTreeModels10", EditorPrefs.GetInt("showCounts_URPTreeModels10") + 1);

        if (EditorPrefs.GetInt("showCounts_URPTreeModels10") == 1)       
        {
            Application.OpenURL("https://assetstore.unity.com/packages/slug/340244");
            // System.IO.File.Delete("Assets/SportCar/Racing_Game.cs");
        }
    }     
}
#endif
