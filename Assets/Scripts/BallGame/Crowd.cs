using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animates all spectators (the children of this object): a gentle idle sway,
/// and jumping/cheering for a few seconds when Cheer() is called (hooked to ScoreManager.onScore).
/// </summary>
public class Crowd : MonoBehaviour
{
    public float idleAmount = 0.02f;
    public float cheerHeight = 0.3f;
    public float cheerDuration = 3f;

    readonly List<Transform> people = new List<Transform>();
    readonly List<Vector3> basePositions = new List<Vector3>();
    readonly List<float> phases = new List<float>();
    float cheerTimer;

    void Start()
    {
        foreach (Transform child in transform)
        {
            people.Add(child);
            basePositions.Add(child.localPosition);
            phases.Add(Random.Range(0f, Mathf.PI * 2f));
        }
    }

    public void Cheer()
    {
        cheerTimer = cheerDuration;
    }

    void Update()
    {
        if (cheerTimer > 0f) cheerTimer -= Time.deltaTime;
        float cheer = Mathf.Clamp01(cheerTimer / 0.5f); // fades out at the end

        for (int i = 0; i < people.Count; i++)
        {
            float t = Time.time * 1.5f + phases[i];
            float y = Mathf.Sin(t) * idleAmount;
            if (cheer > 0f)
                y += Mathf.Abs(Mathf.Sin(Time.time * 8f + phases[i])) * cheerHeight * cheer;
            people[i].localPosition = basePositions[i] + new Vector3(0f, y, 0f);
        }
    }
}
