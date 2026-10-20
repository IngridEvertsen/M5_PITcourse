using UnityEngine;

/// <summary>
/// Put this on the ball (next to Rigidbody + XR Grab Interactable).
/// Hook OnGrabbed / OnReleased up in the XR Grab Interactable's "Interactable Events"
/// (Select Entered -> Ball.OnGrabbed, Select Exited -> Ball.OnReleased).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    public ScoreManager scoreManager;

    [Tooltip("If the ball falls below this height it is sent back to the start.")]
    public float respawnBelowY = -2f;

    public bool IsHeld { get; private set; }
    public bool WasThrown { get; private set; }

    Rigidbody rb;
    Vector3 startPosition;
    Quaternion startRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Update()
    {
        if (!IsHeld && transform.position.y < respawnBelowY)
            Respawn();
    }

    // XR Grab Interactable -> Interactable Events -> Select Entered
    public void OnGrabbed()
    {
        IsHeld = true;
        WasThrown = false;
    }

    // XR Grab Interactable -> Interactable Events -> Select Exited
    public void OnReleased()
    {
        IsHeld = false;
        WasThrown = true;
        if (scoreManager != null) scoreManager.RegisterThrow();
    }

    // Called by GoalTrigger after a score, so the same throw can't count twice.
    public void MarkScored()
    {
        WasThrown = false;
        Invoke(nameof(Respawn), 1.5f);
    }

    public void Respawn()
    {
        CancelInvoke(nameof(Respawn));
        if (IsHeld) return;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(startPosition, startRotation);
        WasThrown = false;
    }
}
