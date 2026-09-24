using System.Collections;
using UnityEngine;

/// <summary>Behaviour for one pigeon, controlled by PigeonFlockZone.</summary>
public class PigeonScare : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;

    [Header("Sounds")]
    [SerializeField] private AudioClip idleSound;
    [SerializeField] private AudioClip wingFlapSound;
    [Min(0f)][SerializeField] private float soundMaxDistance = 12f;

    [Header("Animation")]
    [SerializeField] private string flyTrigger = "Fly";
    [SerializeField] private string landTrigger = "Land";
    [Min(0f)][SerializeField] private float takeOffAnimationTime = 0.6f;
    [Min(0f)][SerializeField] private float landAnimationTime = 0.6f;

    [Header("Flight")]
    [Min(0f)][SerializeField] private float flightSpeed = 6f;
    [Min(0f)][SerializeField] private float flightDistance = 10f;
    [Min(0f)][SerializeField] private float flightHeight = 4f;
    [Tooltip("Các điểm chim có thể đậu khi bay. Để trống để dùng hướng bay ngẫu nhiên hiện tại.")]
    [SerializeField] private Transform[] flightPerches;

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private Vector3 targetPosition;
    private bool flying;
    private bool returning;
    private bool busy;

    public bool HasAnimatorController => animator != null && animator.runtimeAnimatorController != null;

    private void Awake()
    {
        homePosition = transform.position;
        homeRotation = transform.rotation;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 1.5f;
            audioSource.maxDistance = soundMaxDistance;
        }
        PlayIdleSound();
    }

    private void Update()
    {
        if (!flying) return;

        Vector3 direction = targetPosition - transform.position;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, flightSpeed * Time.deltaTime);
        if (direction.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 8f * Time.deltaTime);

        if ((transform.position - targetPosition).sqrMagnitude < 0.001f && returning)
            StartCoroutine(Land());
    }

    public void FlyAway(Vector3 playerPosition, float reactionDelay)
    {
        if (busy || !HasAnimatorController) return;
        StartCoroutine(TakeOff(playerPosition, reactionDelay));
    }

    public void ReturnHome()
    {
        if (!busy || returning) return;
        returning = true;
        if (flying) targetPosition = homePosition;
    }

    private IEnumerator TakeOff(Vector3 playerPosition, float reactionDelay)
    {
        busy = true;
        yield return new WaitForSeconds(reactionDelay);

        if (audioSource != null)
        {
            audioSource.Stop();
            if (wingFlapSound != null) audioSource.PlayOneShot(wingFlapSound);
        }

        if (animator != null && !string.IsNullOrWhiteSpace(flyTrigger)) animator.SetTrigger(flyTrigger);

        yield return new WaitForSeconds(takeOffAnimationTime);

        Vector3 direction = transform.position - playerPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
        direction = direction.normalized + Random.insideUnitSphere * 0.35f;
        direction.y = 0f;

        targetPosition = ChooseFlightTarget(playerPosition, direction);
        flying = true;
        if (returning) targetPosition = homePosition;
    }

    private IEnumerator Land()
    {
        flying = false;
        returning = false;
        if (animator != null && !string.IsNullOrWhiteSpace(landTrigger)) animator.SetTrigger(landTrigger);
        yield return new WaitForSeconds(landAnimationTime);
        transform.position = homePosition;
        transform.rotation = homeRotation;
        PlayIdleSound();
        busy = false;
    }

    private void PlayIdleSound()
    {
        if (audioSource == null || idleSound == null) return;
        audioSource.clip = idleSound;
        audioSource.loop = true;
        audioSource.Play();
    }

    private Vector3 ChooseFlightTarget(Vector3 playerPosition, Vector3 fallbackDirection)
    {
        if (flightPerches != null)
        {
            Transform[] validPerches = System.Array.FindAll(flightPerches, perch => perch != null);
            if (validPerches.Length > 0)
                return validPerches[Random.Range(0, validPerches.Length)].position;
        }

        return homePosition + fallbackDirection.normalized * flightDistance + Vector3.up * flightHeight;
    }
}
