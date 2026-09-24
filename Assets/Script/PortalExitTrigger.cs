using UnityEngine;

/// <summary>Gan len Box Collider Is Trigger o mat sau cua cong dich chuyen.</summary>
[RequireComponent(typeof(Collider))]
public class PortalExitTrigger : MonoBehaviour
{
    [SerializeField] private PortalNpcController portalController;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (portalController != null) portalController.BeginTeleport(other);
    }
}
