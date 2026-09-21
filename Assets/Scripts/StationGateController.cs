using System.Collections;
using UnityEngine;

public class StationGateController : MonoBehaviour
{
    [Header("Khớp Xoay")]
    public Transform barPivot; // Kéo Bar_Pivot vào đây

    [Header("Góc Xoay (Trục Z)")]
    public float closedAngle = 0f;    // Đóng: nằm ngang
    public float openAngle = -85f;    // Mở: dựng đứng lên
    public float rotateSpeed = 2f;    // Tốc độ nâng hạ

    [Header("Âm Thanh Rào Chắn")]
    public AudioSource gateAudioSource;
    public AudioClip gateOpenClip;
    public AudioClip gateCloseClip;

    private Coroutine currentRotateRoutine;

    public void OpenGate()
    {
        if (gateAudioSource != null && gateOpenClip != null)
        {
            gateAudioSource.PlayOneShot(gateOpenClip);
        }

        if (currentRotateRoutine != null) StopCoroutine(currentRotateRoutine);
        currentRotateRoutine = StartCoroutine(RotateGateRoutine(openAngle));
    }

    public void CloseGate()
    {
        if (gateAudioSource != null && gateCloseClip != null)
        {
            gateAudioSource.PlayOneShot(gateCloseClip);
        }

        if (currentRotateRoutine != null) StopCoroutine(currentRotateRoutine);
        currentRotateRoutine = StartCoroutine(RotateGateRoutine(closedAngle));
    }

    private IEnumerator RotateGateRoutine(float targetZAngle)
    {
        Quaternion targetRotation = Quaternion.Euler(0, 0, targetZAngle);
        while (Quaternion.Angle(barPivot.localRotation, targetRotation) > 0.5f)
        {
            barPivot.localRotation = Quaternion.Lerp(barPivot.localRotation, targetRotation, Time.deltaTime * rotateSpeed);
            yield return null;
        }
        barPivot.localRotation = targetRotation;
    }
}