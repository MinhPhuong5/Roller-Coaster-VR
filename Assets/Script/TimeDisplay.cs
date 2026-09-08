using UnityEngine;
using TMPro;

public class TimeDisplay : MonoBehaviour
{
    public TMP_Text timeText;

    void Update()
    {
        timeText.text = "Time: " + Time.time.ToString("F2") + " s";
    }
}