using System.Collections;
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

    [Header("Respawn")]
    [Tooltip("Where the ball comes back. If empty, it returns to where it started.")]
    public Transform respawnPoint;
    [Tooltip("Seconds the ball may lie outside the court before it respawns.")]
    public float outOfBoundsTime = 3f;
    [Tooltip("Court lines (world X/Z). Outside this area counts as out of bounds.")]
    public float courtMinX = -7.5f, courtMaxX = 7.5f, courtMinZ = -23.3f, courtMaxZ = 4.7f;
    [Tooltip("If the ball falls below this height it respawns at once.")]
    public float respawnBelowY = -2f;

    [Header("Throwing")]
    [Tooltip("Caps the throw speed so a quick mouse flick doesn't launch the ball across the hall.")]
    public float maxThrowSpeed = 9f;
    [Tooltip("Point the aim assist aims for (centre of the rim).")]
    public Transform aimTarget;
    [Range(0f, 1f), Tooltip("0 = real physics, 1 = every throw roughly towards the hoop goes in.")]
    public float aimAssist = 0.6f;
    [Tooltip("Aim assist only helps if you throw within this many degrees of the hoop.")]
    public float aimAssistAngle = 45f;

    public bool IsHeld { get; private set; }
    public bool WasThrown { get; private set; }

    Rigidbody rb;
    Vector3 startPosition;
    Quaternion startRotation;
    float outsideTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Update()
    {
        if (IsHeld) { outsideTimer = 0f; return; }

        Vector3 p = transform.position;
        if (p.y < respawnBelowY) { Respawn(); return; }

        bool outside = p.x < courtMinX || p.x > courtMaxX || p.z < courtMinZ || p.z > courtMaxZ;
        outsideTimer = outside ? outsideTimer + Time.deltaTime : 0f;
        if (outsideTimer > outOfBoundsTime)
        {
            if (scoreManager != null) scoreManager.ShowMessage("Out of bounds!");
            Respawn();
        }
    }

    // XR Grab Interactable -> Interactable Events -> Select Entered
    public void OnGrabbed()
    {
        IsHeld = true;
        WasThrown = false;
        CancelInvoke(nameof(Respawn));
    }

    // XR Grab Interactable -> Interactable Events -> Select Exited
    public void OnReleased()
    {
        IsHeld = false;
        WasThrown = true;
        if (scoreManager != null) scoreManager.RegisterThrow();
        StartCoroutine(AdjustThrow());
    }

    // Runs one physics step after release, when the XR Grab Interactable has applied the throw velocity.
    IEnumerator AdjustThrow()
    {
        yield return new WaitForFixedUpdate();

        Vector3 v = rb.linearVelocity;
        if (v.magnitude < 1f) yield break; // just dropped, not thrown

        // 1) cap the speed
        if (v.magnitude > maxThrowSpeed) v = v.normalized * maxThrowSpeed;

        // 2) aim assist: blend towards a throw that lands in the rim
        if (aimTarget != null && aimAssist > 0f)
        {
            Vector3 toTarget = aimTarget.position - transform.position;
            Vector3 flatTo = new Vector3(toTarget.x, 0f, toTarget.z);
            Vector3 flatV = new Vector3(v.x, 0f, v.z);
            if (flatV.sqrMagnitude > 0.01f && Vector3.Angle(flatV, flatTo) < aimAssistAngle && v.y > -1f)
            {
                if (TryGetIdealVelocity(transform.position, aimTarget.position, out Vector3 ideal))
                    v = Vector3.Lerp(v, ideal, aimAssist);
            }
        }

        rb.linearVelocity = v;
    }

    // Velocity for a ball arc from 'from' that drops through 'to' (tries a few launch angles).
    static bool TryGetIdealVelocity(Vector3 from, Vector3 to, out Vector3 velocity)
    {
        float g = -Physics.gravity.y;
        Vector3 d = to - from;
        Vector3 flat = new Vector3(d.x, 0f, d.z);
        float dist = flat.magnitude;
        float h = d.y;
        foreach (float deg in new[] { 52f, 58f, 64f, 70f })
        {
            float a = deg * Mathf.Deg2Rad;
            float denom = 2f * Mathf.Cos(a) * Mathf.Cos(a) * (dist * Mathf.Tan(a) - h);
            if (denom <= 0f) continue;
            float speed = Mathf.Sqrt(g * dist * dist / denom);
            velocity = flat.normalized * speed * Mathf.Cos(a) + Vector3.up * speed * Mathf.Sin(a);
            return true;
        }
        velocity = Vector3.zero;
        return false;
    }

    // Called by GoalTrigger after a score, so the same throw can't count twice.
    public void MarkScored()
    {
        WasThrown = false;
    }

    public void Respawn()
    {
        CancelInvoke(nameof(Respawn));
        if (IsHeld) return;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        if (respawnPoint != null)
            transform.SetPositionAndRotation(respawnPoint.position, respawnPoint.rotation);
        else
            transform.SetPositionAndRotation(startPosition, startRotation);
        WasThrown = false;
        outsideTimer = 0f;
    }
}
