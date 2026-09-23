using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR;

public class DesktopUIInputFallback : MonoBehaviour
{
    private InputSystemUIInputModule desktopModule;

    void Awake()
    {
        desktopModule = GetComponent<InputSystemUIInputModule>();
        if (desktopModule == null)
        {
            desktopModule = gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    void LateUpdate()
    {
        // Kiểm tra xem có thiết bị kính VR thật gửi vị trí đầu hay không
        bool hasHMD = false;
        var inputDevices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, inputDevices);
        if (inputDevices.Count > 0 && inputDevices[0].isValid)
        {
            hasHMD = true;
        }

        // Nếu KHÔNG có kính VR thật (đang test trên Laptop/PC)
        if (!hasHMD)
        {
            // Tắt tất cả các module khác của XR
            BaseInputModule[] allModules = GetComponents<BaseInputModule>();
            foreach (var mod in allModules)
            {
                if (mod != desktopModule && mod.enabled)
                {
                    mod.enabled = false;
                }
            }

            // Ép module chuột luôn luôn hoạt động
            if (desktopModule != null && !desktopModule.enabled)
            {
                desktopModule.enabled = true;
            }
        }
    }
}