using UnityEngine;

/// <summary>Gives a cloud a slow wind drift and gentle vertical sway.</summary>
public class CloudDrift : MonoBehaviour
{
    public Vector3 windVelocity = new Vector3(2f, 0f, 0.7f);
    [Min(0f)] public float swayHeight = 2f;
    [Min(0f)] public float swaySpeed = 0.12f;

    private Vector3 basePosition;
    private float phase;

    private void OnEnable()
    {
        basePosition = transform.position;
        phase = Mathf.Repeat(GetInstanceID() * 0.618f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (!IntroDialogueController.GameStarted) return;

        basePosition += windVelocity * Time.deltaTime;
        float sway = Mathf.Sin(Time.time * swaySpeed + phase) * swayHeight;
        transform.position = basePosition + Vector3.up * sway;
    }
}
