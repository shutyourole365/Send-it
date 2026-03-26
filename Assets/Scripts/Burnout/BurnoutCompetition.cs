using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages a Summernats-style burnout competition event.
/// Handles competitor turns, judge scoring, crowd reaction, and result tabulation.
/// Place one instance in the burnout pad scene.
/// </summary>
public class BurnoutCompetition : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────────────────────
    [Header("References")]
    public BurnoutSystem playerBurnout;
    public VehicleController playerVehicle;

    [Header("Pad Bounds")]
    [Tooltip("Trigger collider defining the burnout pad area")]
    public BoxCollider padBounds;

    // ─── Competition Settings ─────────────────────────────────────────────────
    [Header("Competition Settings")]
    [Tooltip("Time limit for each competitor's run in seconds")]
    public float runTimeLimitSeconds = 60f;

    [Tooltip("Number of judges scoring the run")]
    public int numJudges = 3;

    [Tooltip("Max score each judge can give per category")]
    public float maxScorePerCategory = 10f;

    public int competitorCount = 8;

    // ─── Scoring Weights ─────────────────────────────────────────────────────
    [Header("Judge Weights")]
    [Tooltip("Smoke volume and density (0–1 weight)")]
    public float smokeWeight      = 0.35f;

    [Tooltip("Duration of sustained burnout (0–1 weight)")]
    public float durationWeight   = 0.25f;

    [Tooltip("Style — donuts, figure-eights, proximity to judges (0–1 weight)")]
    public float styleWeight      = 0.25f;

    [Tooltip("Crowd reaction / noise level (0–1 weight)")]
    public float crowdWeight      = 0.15f;

    // ─── Runtime State ────────────────────────────────────────────────────────
    [HideInInspector] public CompetitionPhase phase = CompetitionPhase.WaitingToStart;
    [HideInInspector] public float runTimeRemaining;
    [HideInInspector] public float currentRunScore;
    [HideInInspector] public bool  playerIsOnPad;

    private List<CompetitorResult> results = new List<CompetitorResult>();
    private Coroutine runCoroutine;

    // ─── Events ───────────────────────────────────────────────────────────────
    public System.Action<float>             OnRunTimerTick;
    public System.Action<float>             OnScoreUpdate;
    public System.Action<List<CompetitorResult>> OnResultsReady;
    public System.Action<CompetitionPhase>  OnPhaseChanged;

    // ─── Unity ────────────────────────────────────────────────────────────────
    void Update()
    {
        if (phase == CompetitionPhase.PlayerRunning)
            UpdateLiveScore();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerIsOnPad = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsOnPad = false;
            // Leaving the pad mid-run ends the run early
            if (phase == CompetitionPhase.PlayerRunning)
                EndPlayerRun(earlyExit: true);
        }
    }

    // ─── Competition Flow ─────────────────────────────────────────────────────
    public void StartCompetition()
    {
        results.Clear();
        SimulateAICompetitors();
        BeginPlayerTurn();
    }

    void BeginPlayerTurn()
    {
        SetPhase(CompetitionPhase.PlayerPreparing);
        Debug.Log("[BurnoutComp] Your turn — get on the pad and hold Left Shift to line lock!");
    }

    public void StartPlayerRun()
    {
        if (phase != CompetitionPhase.PlayerPreparing || !playerIsOnPad) return;
        runCoroutine = StartCoroutine(RunTimerRoutine());
        SetPhase(CompetitionPhase.PlayerRunning);
    }

    IEnumerator RunTimerRoutine()
    {
        runTimeRemaining = runTimeLimitSeconds;

        while (runTimeRemaining > 0f)
        {
            runTimeRemaining -= Time.deltaTime;
            OnRunTimerTick?.Invoke(runTimeRemaining);
            yield return null;
        }

        EndPlayerRun(earlyExit: false);
    }

    void EndPlayerRun(bool earlyExit)
    {
        if (runCoroutine != null)
        {
            StopCoroutine(runCoroutine);
            runCoroutine = null;
        }

        float finalScore = CalculateFinalScore();
        currentRunScore  = finalScore;

        var playerResult = new CompetitorResult
        {
            competitorName = "Player",
            isPlayer       = true,
            finalScore     = finalScore,
            smokeScore     = ScoreSmoke(),
            durationScore  = ScoreDuration(),
            styleScore     = ScoreStyle(),
            crowdScore     = ScoreCrowd(),
            runDuration    = runTimeLimitSeconds - runTimeRemaining,
            earlyExit      = earlyExit,
        };

        results.Add(playerResult);
        results.Sort((a, b) => b.finalScore.CompareTo(a.finalScore));

        GameManager.Instance?.SubmitScore(finalScore, GameMode.BurnoutComp);

        SetPhase(CompetitionPhase.Results);
        OnResultsReady?.Invoke(results);

        Debug.Log($"[BurnoutComp] Run over — Score: {finalScore:F1}  Rank: {GetPlayerRank()}/{results.Count}");
    }

    // ─── Scoring ──────────────────────────────────────────────────────────────
    void UpdateLiveScore()
    {
        currentRunScore = CalculateFinalScore();
        OnScoreUpdate?.Invoke(currentRunScore);
    }

    float CalculateFinalScore()
    {
        float raw =
            ScoreSmoke()    * smokeWeight    +
            ScoreDuration() * durationWeight +
            ScoreStyle()    * styleWeight    +
            ScoreCrowd()    * crowdWeight;

        // Scale to 0–100
        return raw * (100f / maxScorePerCategory);
    }

    float ScoreSmoke()
    {
        if (playerBurnout == null) return 0f;
        // Higher tyre temp + longer burn = more smoke
        float heat     = playerBurnout.CurrentIntensity;
        float duration = playerBurnout.currentRunDuration;
        float raw      = Mathf.Clamp(heat * 8f + (duration / runTimeLimitSeconds) * 2f, 0f, maxScorePerCategory);
        return ApplyJudgeVariance(raw);
    }

    float ScoreDuration()
    {
        if (playerBurnout == null) return 0f;
        float durationRatio = Mathf.Clamp01(playerBurnout.currentRunDuration / runTimeLimitSeconds);
        float raw = durationRatio * maxScorePerCategory;
        return ApplyJudgeVariance(raw);
    }

    float ScoreStyle()
    {
        if (playerVehicle == null) return 0f;
        // Style: lateral movement while burning = figure-eights / donuts
        float lateralVel = playerVehicle.GetComponent<Rigidbody>()?.angularVelocity.magnitude ?? 0f;
        float raw = Mathf.Clamp(lateralVel * 2f, 0f, maxScorePerCategory);
        return ApplyJudgeVariance(raw);
    }

    float ScoreCrowd()
    {
        // Crowd reacts to smoke density + duration — simple proxy
        float combined = (ScoreSmoke() + ScoreDuration()) * 0.5f;
        return ApplyJudgeVariance(combined);
    }

    /// <summary>Adds a small random variance to simulate judge subjectivity.</summary>
    float ApplyJudgeVariance(float baseScore)
    {
        float variance = Random.Range(-0.5f, 0.5f);
        return Mathf.Clamp(baseScore + variance, 0f, maxScorePerCategory);
    }

    // ─── AI Competitors ───────────────────────────────────────────────────────
    void SimulateAICompetitors()
    {
        string[] names = {
            "Mick 'Smokey' Henderson", "Dazza from Darra", "Shazza's VE",
            "Bazza with the Barra",    "Johnno XR8",       "Gazza's Torana",
            "Thommo's Monaro",         "Stevo HSV",
        };

        for (int i = 0; i < Mathf.Min(competitorCount - 1, names.Length); i++)
        {
            float aiScore = Random.Range(35f, 90f);
            results.Add(new CompetitorResult
            {
                competitorName = names[i],
                isPlayer       = false,
                finalScore     = aiScore,
                smokeScore     = Random.Range(3f, 10f),
                durationScore  = Random.Range(3f, 10f),
                styleScore     = Random.Range(2f, 10f),
                crowdScore     = Random.Range(3f, 10f),
                runDuration    = Random.Range(20f, runTimeLimitSeconds),
            });
        }

        results.Sort((a, b) => b.finalScore.CompareTo(a.finalScore));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    int GetPlayerRank()
    {
        for (int i = 0; i < results.Count; i++)
            if (results[i].isPlayer) return i + 1;
        return -1;
    }

    void SetPhase(CompetitionPhase newPhase)
    {
        phase = newPhase;
        OnPhaseChanged?.Invoke(newPhase);
    }

    public List<CompetitorResult> GetResults() => results;
}

// ─── Supporting Types ─────────────────────────────────────────────────────────

public enum CompetitionPhase
{
    WaitingToStart,
    PlayerPreparing,
    PlayerRunning,
    Results,
}

[System.Serializable]
public class CompetitorResult
{
    public string competitorName;
    public bool   isPlayer;
    public float  finalScore;
    public float  smokeScore;
    public float  durationScore;
    public float  styleScore;
    public float  crowdScore;
    public float  runDuration;
    public bool   earlyExit;
}
