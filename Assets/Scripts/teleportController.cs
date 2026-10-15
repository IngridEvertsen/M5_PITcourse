using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;


public class teleportController : MonoBehaviour
{
    public InputActionProperty teleportationActivationAction;
    public XRRayInteractor teleportInteractor;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        teleportInteractor.gameObject.SetActive(false);

        teleportationActivationAction.action.Enable();
        teleportationActivationAction.action.performed += Action_performed;
        teleportationActivationAction.action.canceled += Action_canceled;

    }
    
    void Action_performed(InputAction.CallbackContext obj)
    {
        teleportInteractor.gameObject.SetActive(true);
    }

    void Action_canceled(InputAction.CallbackContext obj)
    {
        teleportInteractor.gameObject.SetActive(false);
        StartCoroutine(JumpOneFrame());
    }

    System.Collections.IEnumerator JumpOneFrame()
    {
        yield return null;
        teleportInteractor.gameObject.SetActive(false);
    }
    
}
