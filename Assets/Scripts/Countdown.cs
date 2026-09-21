using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class Countdown : MonoBehaviour
{
    [Header("UI Text & Âm thanh")]
    [Tooltip("Kéo TextMeshPro hiển thị số đếm ngược vào đây")]
    public TextMeshProUGUI countdownText;
    [Tooltip("Kéo AudioSource chứa file Countdown.mp3 vào đây")]
    public AudioSource countdownAudio;

    [Header("Thời gian")]
    public int countdownSeconds = 3;

    void Awake()
    {
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }

    public void StartCountdown(Action onComplete)
    {
        StartCoroutine(CountdownRoutine(onComplete));
    }

    private IEnumerator CountdownRoutine(Action onComplete)
    {
        // 1. Phát file âm thanh đếm ngược gộp
        if (countdownAudio != null)
        {
            countdownAudio.Play();
        }

        // 2. Hiển thị chữ 3... 2... 1... GO!
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);

            for (int i = countdownSeconds; i > 0; i--)
            {
                countdownText.text = i.ToString();
                yield return new WaitForSeconds(1.0f);
            }

            countdownText.text = "GO!";
            yield return new WaitForSeconds(0.6f);
            countdownText.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(countdownSeconds + 0.6f);
        }

        // 3. Gọi lệnh cho tàu xuất phát
        onComplete?.Invoke();
    }
}