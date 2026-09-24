using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class PigeonFlockZone : MonoBehaviour
{
    [SerializeField] private PigeonScare[] pigeons;
    [Min(0f)][SerializeField] private float reactionDelay = 0.25f;
    [Min(0f)][SerializeField] private float individualDelayRange = 0.4f;

    private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();

    private void Awake()
    {
        BoxCollider zone = GetComponent<BoxCollider>();
        zone.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        bool wasEmpty = playerColliders.Count == 0;
        playerColliders.Add(other);
        if (!wasEmpty) return;

        foreach (PigeonScare pigeon in pigeons)
            if (pigeon != null) pigeon.FlyAway(other.transform.position, reactionDelay + Random.Range(0f, individualDelayRange));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!playerColliders.Remove(other) || playerColliders.Count != 0) return;
        foreach (PigeonScare pigeon in pigeons)
            if (pigeon != null) pigeon.ReturnHome();
    }

    private static bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") || other.transform.root.CompareTag("Player");
    }
}
