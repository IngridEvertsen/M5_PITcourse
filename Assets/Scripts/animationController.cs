using UnityEngine;
using UnityEngine.InputSystem;

public class animationController : MonoBehaviour
{
    public InputActionProperty grabAction;
    public InputActionProperty triggerAction;

    public Animator myAnimator;

    [Tooltip("Print grab/trigger values to the Console when they change (for testing).")]
    public bool debugLog = true;

    float lastGrab = -1f, lastTrigger = -1f;

    void Awake()
    {
        // If the Animator wasn't dragged in, look for one on this object or its children.
        if (myAnimator == null)
            myAnimator = GetComponentInChildren<Animator>();

        if (myAnimator == null)
            Debug.LogWarning($"[Hand] '{name}' has no Animator assigned or in its children.", this);
        else if (myAnimator.runtimeAnimatorController == null)
            Debug.LogWarning($"[Hand] '{name}': Animator on '{myAnimator.name}' has no Controller assigned.", this);
    }

    void OnEnable()
    {
        // Make sure the actions are listening even if no Input Action Manager enables them.
        grabAction.action?.Enable();
        triggerAction.action?.Enable();

        if (debugLog)
            Debug.Log($"[Hand] '{name}' grab='{grabAction.action?.name}' ({grabAction.action?.actionMap?.name}), " +
                      $"trigger='{triggerAction.action?.name}' ({triggerAction.action?.actionMap?.name}), animator='{myAnimator?.name}'", this);
    }

    void Update()
    {
        if (myAnimator == null) return;

        float grab = grabAction.action != null ? grabAction.action.ReadValue<float>() : 0f;
        float trigger = triggerAction.action != null ? triggerAction.action.ReadValue<float>() : 0f;

        myAnimator.SetFloat("grab", grab);
        myAnimator.SetFloat("trigger", trigger);

        if (debugLog && (Mathf.Abs(grab - lastGrab) > 0.05f || Mathf.Abs(trigger - lastTrigger) > 0.05f))
        {
            Debug.Log($"[Hand] {name}: grab={grab:0.00} trigger={trigger:0.00} " +
                      $"(grab from: {grabAction.action?.activeControl?.path ?? "-"}, trigger from: {triggerAction.action?.activeControl?.path ?? "-"})", this);
            lastGrab = grab; lastTrigger = trigger;
        }
    }
}
