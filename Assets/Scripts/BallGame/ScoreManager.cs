using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Keeps track of the score and plays feedback (text, sound, particles) when a goal is scored.
/// Put this on an empty "GameManager" object in the scene.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text scoreText;      // shows "Score: X"
    public TMP_Text messageText;    // short messages like "SCORE!" (optional)
    public TMP_Text bannerScoreText; // big score number on the team banner (optional)

    [Header("Feedback")]
    public AudioSource scoreSound;  // sound played when scoring (optional)
    public ParticleSystem scoreEffect; // confetti/sparkles in the goal (optional)

    [Header("Events")]
    [Tooltip("Extra things that should happen on a score, e.g. Crowd.Cheer")]
    public UnityEvent onScore;

    int score;
    int throws;

    void Start()
    {
        UpdateText();
        ShowMessage("Grab the ball and throw it in the basket!");
    }

    // Called by Ball when the player lets go of the ball.
    public void RegisterThrow()
    {
        throws++;
        UpdateText();
    }

    // Called by GoalTrigger when a thrown ball enters the goal.
    public void AddPoint()
    {
        score++;
        UpdateText();
        ShowMessage("SCORE! +1");

        if (scoreSound != null) scoreSound.Play();
        if (scoreEffect != null) scoreEffect.Play();
        onScore?.Invoke();
    }

    public void ShowMessage(string text)
    {
        if (messageText == null) return;
        messageText.text = text;
        CancelInvoke(nameof(ClearMessage));
        Invoke(nameof(ClearMessage), 2f);
    }

    void ClearMessage()
    {
        if (messageText != null) messageText.text = "";
    }

    void UpdateText()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}\nThrows: {throws}";
        if (bannerScoreText != null)
            bannerScoreText.text = score.ToString();
    }
}
