using UnityEngine;

/// <summary>Moves an animated whale in one circle across the sky, then hides it.</summary>
public class SkyWhaleFlyby : MonoBehaviour
{
    [Header("Flight")]
    [Tooltip("Huong tu diem bat dau den tam vong tron. Vi du (1, 0, 0) dat tam vong tron ben phai ca voi.")]
    public Vector3 travelDirection = Vector3.right;
    [Min(0.1f)] public float circleRadius = 450f;
    [Min(0.1f)] public float duration = 20f;
    public bool clockwise = true;
    [Tooltip("Do cao thap nhe khi boi giua may.")]
    [Min(0f)] public float floatHeight = 8f;
    [Min(0f)] public float floatCycles = 1.5f;
    public bool faceTravelDirection = true;
    public bool hideAtEnd = true;

    [Header("3D Sound")]
    public AudioClip whaleCall;
    public AudioClip windLoop;
    [Range(0f, 1f)] public float callVolume = 0.7f;
    [Range(0f, 1f)] public float windVolume = 0.25f;
    [Min(0f)] public float callInterval = 6f;
    [Min(1f)] public float minSoundDistance = 400f;
    [Min(1f)] public float maxSoundDistance = 2500f;

    private Vector3 startPosition;
    private Vector3 direction;
    private float elapsed;
    private float nextCallTime;
    private bool flybyStarted;
    private AudioSource soundSource;
    private AudioSource windSource;

    private void OnEnable()
    {
        flybyStarted = false;
    }

    private void BeginFlyby()
    {
        flybyStarted = true;
        startPosition = transform.position;
        direction = travelDirection.sqrMagnitude > 0.0001f ? travelDirection.normalized : transform.forward;
        elapsed = 0f;
        nextCallTime = 0f;

        SetupAudio();
    }

    private void Update()
    {
        if (!IntroDialogueController.GameStarted) return;
        if (!flybyStarted) BeginFlyby();

        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        float bob = Mathf.Sin(progress * floatCycles * Mathf.PI * 2f) * floatHeight;
        float angle = progress * 360f * (clockwise ? 1f : -1f);
        Vector3 center = startPosition + direction * circleRadius;
        Vector3 radial = Quaternion.AngleAxis(angle, Vector3.up) * (-direction * circleRadius);
        transform.position = center + radial + Vector3.up * bob;

        if (faceTravelDirection)
        {
            Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized * (clockwise ? 1f : -1f);
            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }

        if (whaleCall != null && Time.time >= nextCallTime)
        {
            soundSource.PlayOneShot(whaleCall, callVolume);
            nextCallTime = Time.time + callInterval;
        }

        if (progress >= 1f && hideAtEnd) gameObject.SetActive(false);
    }

    private void SetupAudio()
    {
        if (soundSource == null)
        {
            soundSource = gameObject.AddComponent<AudioSource>();
            soundSource.spatialBlend = 1f;
            soundSource.rolloffMode = AudioRolloffMode.Logarithmic;
            soundSource.minDistance = minSoundDistance;
            soundSource.maxDistance = maxSoundDistance;

            windSource = gameObject.AddComponent<AudioSource>();
            windSource.spatialBlend = 1f;
            windSource.rolloffMode = AudioRolloffMode.Logarithmic;
            windSource.minDistance = minSoundDistance;
            windSource.maxDistance = maxSoundDistance;
            windSource.loop = true;
        }

        soundSource.minDistance = minSoundDistance;
        soundSource.maxDistance = maxSoundDistance;
        windSource.minDistance = minSoundDistance;
        windSource.maxDistance = maxSoundDistance;
        windSource.clip = windLoop;
        windSource.volume = windVolume;
        if (windLoop != null) windSource.Play();
    }
}
