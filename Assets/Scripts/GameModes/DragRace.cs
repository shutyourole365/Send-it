using UnityEngine;
using System.Collections;

/// <summary>
/// Full drag race system: Christmas tree staging, reaction time, ET, trap speed,
/// dial-in bracket racing, and competitor AI.
/// Place one instance in the drag strip scene.
/// Assign the staging beams, finish line trigger, and player vehicle in the inspector.
/// </summary>
public class DragRace : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────────────────────
    [Header("References")]
    public VehicleController playerVehicle;
    public Transform         playerStartPosition;
    public Transform         finishLineTransform;

    [Header("Staging Beams (Triggers)")]
    [Tooltip("Pre-stage beam trigger — first yellow on tree")]
    public Collider preStagingBeam;

    [Tooltip("Stage beam trigger — second yellow on tree")]
    public Collider stagingBeam;

    [Tooltip("Finish line beam")]
    public Collider finishBeam;

    // ─── Christmas Tree Lights ────────────────────────────────────────────────
    [Header("Christmas Tree Lights")]
    [Tooltip("Pre-stage indicator light renderers (player, opponent)")]
    public Renderer[] preStageLight   = new Renderer[2];
    public Renderer[] stageLight      = new Renderer[2];
    public Renderer[] yellowLight1    = new Renderer[2];
    public Renderer[] yellowLight2    = new Renderer[2];
    public Renderer[] yellowLight3    = new Renderer[2];
    public Renderer[] greenLight      = new Renderer[2];
    public Renderer[] redLight        = new Renderer[2];  // Foul/red light

    public Color litColour  = Color.yellow;
    public Color dimColour  = new Color(0.15f, 0.15f, 0.05f);

    // ─── Race Settings ────────────────────────────────────────────────────────
    [Header("Race Settings")]
    public float trackLengthMetres = 402.3f;   // 1/4 mile

    [Tooltip("Full tree = 0.5s between each yellow. Pro tree = 0.4s.")]
    public float treeIntervalSeconds = 0.5f;

    [Tooltip("Bracket racing: player's predicted ET")]
    public float dialInSeconds = 0f;   // 0 = open competition

    public bool bracketRacing = false;

    // ─── AI Opponent ─────────────────────────────────────────────────────────
    [Header("AI Opponent")]
    public bool   hasOpponent     = true;
    public float  opponentDialIn  = 12.5f;   // AI's predicted ET
    public float  opponentVariance = 0.3f;   // Random ET spread

    // ─── Runtime State ────────────────────────────────────────────────────────
    [HideInInspector] public DragPhase phase = DragPhase.Idle;
    [HideInInspector] public bool  playerPreStaged;
    [HideInInspector] public bool  playerStaged;
    [HideInInspector] public bool  raceStarted;
    [HideInInspector] public float reactionTime;      // Seconds from green to launch
    [HideInInspector] public float elapsedTime;       // ET in seconds
    [HideInInspector] public float trapSpeed;         // KPH at finish line
    [HideInInspector] public bool  isFoul;            // Redlight

    private float   raceStartTime;
    private float   greenLightTime;
    private bool    playerFinished;
    private bool    opponentFinished;
    private float   opponentET;
    private float   opponentReaction;
    private Coroutine treeCoroutine;

    // ─── Events ───────────────────────────────────────────────────────────────
    public System.Action<DragPhase>  OnPhaseChanged;
    public System.Action<DragResult> OnRaceComplete;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Start()
    {
        DimAllLights();
    }

    void Update()
    {
        if (phase == DragPhase.Racing && !playerFinished)
        {
            elapsedTime = Time.time - raceStartTime;

            // Measure trap speed as player crosses finish
            if (playerVehicle != null)
            {
                float distToFinish = Vector3.Distance(
                    playerVehicle.transform.position, finishLineTransform.position);

                if (distToFinish < 2f && !playerFinished)
                    PlayerCrossedFinish();
            }
        }
    }

    // ─── Staging ─────────────────────────────────────────────────────────────
    public void OnPlayerEnterPreStage()
    {
        playerPreStaged = true;
        SetLight(preStageLight[0], true);
        Debug.Log("[Drag] Player pre-staged.");
        TryStartTree();
    }

    public void OnPlayerEnterStage()
    {
        playerStaged = true;
        SetLight(stageLight[0], true);
        Debug.Log("[Drag] Player staged. Get ready!");
        TryStartTree();
    }

    public void OnPlayerLeaveStagingArea()
    {
        // Creep over the beam — foul in some formats; here just abort
        if (phase == DragPhase.Tree)
        {
            StopAllCoroutines();
            SetPhase(DragPhase.Idle);
        }
    }

    void TryStartTree()
    {
        if (playerStaged && phase == DragPhase.Idle)
        {
            SetPhase(DragPhase.Staged);
            treeCoroutine = StartCoroutine(RunChristmasTree());
        }
    }

    // ─── Christmas Tree ───────────────────────────────────────────────────────
    IEnumerator RunChristmasTree()
    {
        SetPhase(DragPhase.Tree);
        DimAllLights();
        SetLight(preStageLight[0], true);
        SetLight(stageLight[0], true);
        if (hasOpponent)
        {
            SetLight(preStageLight[1], true);
            SetLight(stageLight[1], true);
        }

        yield return new WaitForSeconds(1.2f);

        // Yellow 1
        SetLightPair(yellowLight1, true);
        yield return new WaitForSeconds(treeIntervalSeconds);

        // Yellow 2
        SetLightPair(yellowLight2, true);
        yield return new WaitForSeconds(treeIntervalSeconds);

        // Yellow 3
        SetLightPair(yellowLight3, true);
        yield return new WaitForSeconds(treeIntervalSeconds);

        // Green — GO!
        DimAllLights();
        SetLightPair(greenLight, true);
        greenLightTime = Time.time;
        SetPhase(DragPhase.Racing);
        StartRace();

        yield return new WaitForSeconds(2f);
        SetLightPair(greenLight, false);
    }

    // ─── Race ─────────────────────────────────────────────────────────────────
    void StartRace()
    {
        raceStarted = true;
        raceStartTime = Time.time;
        playerFinished = false;
        opponentFinished = false;

        // AI opponent result
        if (hasOpponent)
        {
            opponentReaction = Random.Range(0.08f, 0.25f);
            opponentET       = opponentDialIn + Random.Range(-opponentVariance, opponentVariance);
            StartCoroutine(SimulateOpponent());
        }

        Debug.Log("[Drag] RACE STARTED!");
    }

    IEnumerator SimulateOpponent()
    {
        yield return new WaitForSeconds(opponentET);
        opponentFinished = true;
        Debug.Log($"[Drag] Opponent finished — ET: {opponentET:F3}s  RT: {opponentReaction:F3}s");
        CheckRaceOver();
    }

    // ─── Player Crosses Finish ────────────────────────────────────────────────
    void PlayerCrossedFinish()
    {
        playerFinished = true;
        elapsedTime    = Time.time - raceStartTime;
        reactionTime   = greenLightTime + 0.001f - raceStartTime;  // approximated
        trapSpeed      = playerVehicle.speedKPH;

        // Check foul — launched before green (reaction time < 0)
        isFoul = reactionTime < 0f;
        if (isFoul) SetLightPair(redLight, true);

        Debug.Log($"[Drag] Player finished — ET: {elapsedTime:F3}s  RT: {reactionTime:F3}s  Trap: {trapSpeed:F1} km/h");

        CheckRaceOver();
    }

    void CheckRaceOver()
    {
        if (!playerFinished && hasOpponent && !opponentFinished) return;
        if (!playerFinished && !hasOpponent) return;

        SetPhase(DragPhase.Results);

        DragResult result = BuildResult();
        GameManager.Instance?.SubmitScore(CalculateScore(result), GameMode.DragRace);
        OnRaceComplete?.Invoke(result);

        Debug.Log($"[Drag] Race complete — Winner: {result.winner}");
    }

    // ─── Scoring ──────────────────────────────────────────────────────────────
    DragResult BuildResult()
    {
        string winner;
        if (isFoul)
            winner = hasOpponent ? "Opponent" : "DQ";
        else if (!hasOpponent)
            winner = "Player";
        else if (!opponentFinished)
            winner = "Player";
        else
        {
            // Bracket: closest to dial-in wins (if bracket racing)
            if (bracketRacing && dialInSeconds > 0f)
            {
                float playerBreakout = elapsedTime - dialInSeconds;
                float oppBreakout    = opponentET  - opponentDialIn;
                // Negative breakout = ran quicker than dial = breakout (DQ in pure bracket)
                winner = playerBreakout >= 0 && playerBreakout < oppBreakout ? "Player" : "Opponent";
            }
            else
                winner = elapsedTime < opponentET ? "Player" : "Opponent";
        }

        return new DragResult
        {
            playerET       = elapsedTime,
            playerRT       = reactionTime,
            playerTrapKPH  = trapSpeed,
            opponentET     = opponentET,
            opponentRT     = opponentReaction,
            isFoul         = isFoul,
            winner         = winner,
        };
    }

    float CalculateScore(DragResult r)
    {
        if (r.winner != "Player") return 0f;
        float score = 10000f / Mathf.Max(r.playerET, 0.1f);  // Faster ET = higher score
        score += r.playerRT < 0.15f ? 500f : 0f;             // Reaction time bonus
        return score;
    }

    // ─── Light Helpers ────────────────────────────────────────────────────────
    void SetLight(Renderer r, bool on)
    {
        if (r == null) return;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetColor("_EmissionColor", on ? litColour * 2f : Color.black);
        r.SetPropertyBlock(block);
    }

    void SetLightPair(Renderer[] pair, bool on)
    {
        foreach (var r in pair) SetLight(r, on);
    }

    void DimAllLights()
    {
        Renderer[][] allLights = { preStageLight, stageLight, yellowLight1, yellowLight2, yellowLight3, greenLight, redLight };
        foreach (var group in allLights)
            SetLightPair(group, false);
    }

    void SetPhase(DragPhase p)
    {
        phase = p;
        OnPhaseChanged?.Invoke(p);
    }

    // ─── Public ───────────────────────────────────────────────────────────────
    public void ResetRace()
    {
        StopAllCoroutines();
        DimAllLights();
        playerPreStaged = playerStaged = raceStarted = playerFinished = opponentFinished = isFoul = false;
        elapsedTime = reactionTime = trapSpeed = 0f;
        SetPhase(DragPhase.Idle);

        if (playerVehicle != null && playerStartPosition != null)
        {
            playerVehicle.transform.position = playerStartPosition.position;
            playerVehicle.transform.rotation = playerStartPosition.rotation;
        }
    }
}

// ─── Supporting Types ─────────────────────────────────────────────────────────

public enum DragPhase { Idle, Staged, Tree, Racing, Results }

[System.Serializable]
public class DragResult
{
    public float  playerET;
    public float  playerRT;
    public float  playerTrapKPH;
    public float  opponentET;
    public float  opponentRT;
    public bool   isFoul;
    public string winner;
}
