using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;


public class teleportController : MonoBehaviour
{
    public InputActionProperty teleportationActivationAction;
    public XRRayInteractor teleportInteractor;

    [Tooltip("Print each teleport step to the Console (for testing).")]
    public bool debugLog = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        teleportInteractor.gameObject.SetActive(false);

        teleportationActivationAction.action.Enable();
        teleportationActivationAction.action.performed += Action_performed;
        teleportationActivationAction.action.canceled += Action_canceled;

        if (debugLog)
        {
            teleportInteractor.hoverEntered.AddListener(a => Debug.Log($"[Teleport] 2. ray is pointing at '{a.interactableObject.transform.name}'"));
            teleportInteractor.selectEntered.AddListener(a => Debug.Log($"[Teleport] 3. target selected: '{a.interactableObject.transform.name}'"));
            teleportInteractor.selectExited.AddListener(a => Debug.Log($"[Teleport] 4. released on '{a.interactableObject.transform.name}' (cancelled: {a.isCanceled})"));

            var provider = FindAnyObjectByType<TeleportationProvider>();
            if (provider == null) Debug.LogWarning("[Teleport] No TeleportationProvider found in the scene!");
            else provider.locomotionStarted += p => Debug.Log("[Teleport] 5. TELEPORTED");
        }
    }

    void Action_performed(InputAction.CallbackContext obj)
    {
        if (debugLog) Debug.Log("[Teleport] 1. Y pressed - ray on");
        teleportInteractor.gameObject.SetActive(true);
    }

    void Action_canceled(InputAction.CallbackContext obj)
    {
        if (debugLog) Debug.Log("[Teleport] Y released");
        // Don't switch the ray off right away: the teleport happens on release,
        // so wait one frame and then hide the ray.
        StartCoroutine(JumpOneFrame());
    }

    System.Collections.IEnumerator JumpOneFrame()
    {
        yield return null;
        teleportInteractor.gameObject.SetActive(false);
    }

}
