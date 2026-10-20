using UnityEngine;

/// <summary>
/// Put this on an invisible trigger box inside the basket/goal.
/// The object needs a Collider with "Is Trigger" ticked.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    public ScoreManager scoreManager;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        Ball ball = other.GetComponentInParent<Ball>();
        if (ball == null) return;

        // Only count it if the ball was thrown (not just carried in by hand).
        if (ball.IsHeld || !ball.WasThrown) return;

        ball.MarkScored();
        if (scoreManager != null) scoreManager.AddPoint();
    }
}
