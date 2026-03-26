using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Full circuit race system: lap timing, sector splits, position tracking,
/// penalty system, best lap, race director messages, and AI opponents.
/// Place one instance in the circuit scene.
/// Requires checkpoint triggers placed around the track.
/// </summary>
public class CircuitRace : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────────────────────
    [Header("References")]
    public VehicleController playerVehicle;
    public Transform         startFinishLine;

    [Header("Checkpoints")]
    [Tooltip("Ordered list of checkpoint triggers around the circuit")]
    public CheckpointTrigger[] checkpoints;

    // ─── Race Settings ────────────────────────────────────────────────────────
    [Header("Race Settings")]
    public int   totalLaps           = 5;
    public int   aiOpponentCount     = 7;
    public float penaltySeconds      = 5f;
    public float shortcutPenaltySeconds = 10f;
    public float wrongWayWarningTime = 3f;

    [Header("AI")]
    public float[] aiLapTimeTargets;  // Seconds per lap for each AI, slowest first
    public float   aiLapTimeVariance = 2f;

    // ─── Runtime State ────────────────────────────────────────────────────────
    [HideInInspector] public CircuitPhase phase = CircuitPhase.WaitingToStart;
    [HideInInspector] public int          playerLap          = 0;
    [HideInInspector] public float        currentLapTime;
    [HideInInspector] public float        bestLapTime        = float.MaxValue;
    [HideInInspector] public float        totalRaceTime;
    [HideInInspector] public float        pendingPenalty;
    [HideInInspector] public int          playerPosition     = 1;
    [HideInInspector] public int          nextCheckpointIndex;
    [HideInInspector] public bool         isWrongWay;
    [HideInInspector] public string       lastDirectorMessage = "";

    private List<LapRecord>    playerLaps     = new List<LapRecord>();
    private List<AIRacer>      aiRacers       = new List<AIRacer>();
    private List<SectorSplit>  currentSectors = new List<SectorSplit>();
    private float              wrongWayTimer;
    private bool               raceFinished;
    private Coroutine          countdownCoroutine;

    // ─── Events ───────────────────────────────────────────────────────────────
    public System.Action<CircuitPhase>  OnPhaseChanged;
    public System.Action<int>           OnNewLap;            // lap number
    public System.Action<float>         OnNewBestLap;        // best lap time
    public System.Action<string>        OnDirectorMessage;   // e.g. "PENALTY +5s"
    public System.Action<List<RaceStanding>> OnStandingsUpdate;
    public System.Action<RaceResult>    OnRaceFinished;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Start()
    {
        InitialiseAI();
        RegisterCheckpointCallbacks();
    }

    void Update()
    {
        if (phase != CircuitPhase.Racing) return;

        currentLapTime += Time.deltaTime;
        totalRaceTime  += Time.deltaTime;

        if (pendingPenalty > 0f)
            pendingPenalty -= Time.deltaTime;

        CheckWrongWay();
        UpdateAIRacers();
        UpdateStandings();
    }

    // ─── Start Sequence ───────────────────────────────────────────────────────
    public void BeginRace()
    {
        if (phase != CircuitPhase.WaitingToStart) return;
        countdownCoroutine = StartCoroutine(RaceCountdown());
    }

    IEnumerator RaceCountdown()
    {
        SetPhase(CircuitPhase.Countdown);
        PostDirectorMessage("GET READY");
        yield return new WaitForSeconds(1f);
        PostDirectorMessage("3");
        yield return new WaitForSeconds(1f);
        PostDirectorMessage("2");
        yield return new WaitForSeconds(1f);
        PostDirectorMessage("1");
        yield return new WaitForSeconds(1f);
        PostDirectorMessage("GO!");

        playerLap = 1;
        currentLapTime = 0f;
        nextCheckpointIndex = 0;
        SetPhase(CircuitPhase.Racing);
        OnNewLap?.Invoke(playerLap);

        foreach (var ai in aiRacers) ai.StartRacing();
    }

    // ─── Checkpoint Logic ─────────────────────────────────────────────────────
    void RegisterCheckpointCallbacks()
    {
        for (int i = 0; i < checkpoints.Length; i++)
        {
            if (checkpoints[i] == null) continue;
            int idx = i;
            checkpoints[i].OnPlayerEnter = () => OnPlayerHitCheckpoint(idx);
        }
    }

    void OnPlayerHitCheckpoint(int index)
    {
        if (phase != CircuitPhase.Racing) return;

        // Wrong way check
        if (index != nextCheckpointIndex)
        {
            bool isShortcut = index > nextCheckpointIndex + 1;
            if (isShortcut)
            {
                AddPenalty(shortcutPenaltySeconds, "SHORTCUT PENALTY");
            }
            return;
        }

        // Record sector split
        currentSectors.Add(new SectorSplit { sectorIndex = index, splitTime = currentLapTime });

        nextCheckpointIndex++;

        // Crossed start/finish — new lap
        if (nextCheckpointIndex >= checkpoints.Length)
        {
            CompleteLap();
        }
    }

    void CompleteLap()
    {
        float lapTime = currentLapTime + Mathf.Max(0f, pendingPenalty);
        pendingPenalty = 0f;

        var record = new LapRecord
        {
            lapNumber = playerLap,
            lapTime   = lapTime,
            sectors   = new List<SectorSplit>(currentSectors),
        };
        playerLaps.Add(record);
        currentSectors.Clear();

        if (lapTime < bestLapTime)
        {
            bestLapTime = lapTime;
            OnNewBestLap?.Invoke(bestLapTime);
            PostDirectorMessage($"BEST LAP  {FormatTime(bestLapTime)}");
        }

        Debug.Log($"[Circuit] Lap {playerLap} — {FormatTime(lapTime)}  Best: {FormatTime(bestLapTime)}");

        playerLap++;
        nextCheckpointIndex = 0;
        currentLapTime = 0f;

        if (playerLap > totalLaps)
        {
            FinishRace();
        }
        else
        {
            OnNewLap?.Invoke(playerLap);
            PostDirectorMessage($"LAP {playerLap} / {totalLaps}");
        }
    }

    void FinishRace()
    {
        raceFinished = true;
        SetPhase(CircuitPhase.Finished);

        int rank = GetPlayerRank();
        float totalTime = 0f;
        foreach (var l in playerLaps) totalTime += l.lapTime;

        var result = new RaceResult
        {
            finishPosition = rank,
            totalLaps      = totalLaps,
            totalTime      = totalTime,
            bestLap        = bestLapTime,
            lapRecords     = playerLaps,
        };

        int reward = CalculateReward(rank);
        GameManager.Instance?.SubmitScore(reward, GameMode.Circuit);

        PostDirectorMessage(rank == 1 ? "WINNER!" : $"P{rank}  FINISH");
        OnRaceFinished?.Invoke(result);
    }

    // ─── Penalties ────────────────────────────────────────────────────────────
    void AddPenalty(float seconds, string reason)
    {
        pendingPenalty += seconds;
        PostDirectorMessage($"{reason}  +{seconds:F0}s");
        Debug.Log($"[Circuit] Penalty: {reason} +{seconds}s");
    }

    // ─── Wrong Way ────────────────────────────────────────────────────────────
    void CheckWrongWay()
    {
        if (checkpoints == null || nextCheckpointIndex >= checkpoints.Length) return;

        Vector3 toNext = checkpoints[nextCheckpointIndex].transform.position
                       - playerVehicle.transform.position;
        float dot = Vector3.Dot(playerVehicle.transform.forward, toNext.normalized);

        if (dot < -0.5f)
        {
            wrongWayTimer += Time.deltaTime;
            if (wrongWayTimer > wrongWayWarningTime && !isWrongWay)
            {
                isWrongWay = true;
                PostDirectorMessage("WRONG WAY!");
            }
        }
        else
        {
            wrongWayTimer = 0f;
            if (isWrongWay)
            {
                isWrongWay = false;
                PostDirectorMessage("");
            }
        }
    }

    // ─── AI Racers ────────────────────────────────────────────────────────────
    void InitialiseAI()
    {
        for (int i = 0; i < aiOpponentCount; i++)
        {
            float lapTarget = aiLapTimeTargets != null && i < aiLapTimeTargets.Length
                ? aiLapTimeTargets[i]
                : 90f + i * 5f;

            aiRacers.Add(new AIRacer
            {
                name         = AussieAINames[i % AussieAINames.Length],
                lapTimeTarget = lapTarget,
                variance      = aiLapTimeVariance,
            });
        }
    }

    void UpdateAIRacers()
    {
        foreach (var ai in aiRacers)
            ai.Update(Time.deltaTime, totalLaps);
    }

    int GetPlayerRank()
    {
        float playerTotal = 0f;
        foreach (var l in playerLaps) playerTotal += l.lapTime;
        playerTotal += (totalLaps - playerLaps.Count) * 9999f; // DNF penalty

        int rank = 1;
        foreach (var ai in aiRacers)
        {
            if (ai.totalRaceTime < playerTotal) rank++;
        }
        return rank;
    }

    void UpdateStandings()
    {
        var standings = new List<RaceStanding>();

        float playerTotal = totalRaceTime;
        standings.Add(new RaceStanding { name = "You", lapsDone = playerLap - 1, totalTime = playerTotal, isPlayer = true });

        foreach (var ai in aiRacers)
            standings.Add(new RaceStanding { name = ai.name, lapsDone = ai.lapsCompleted, totalTime = ai.totalRaceTime });

        standings.Sort((a, b) =>
        {
            if (a.lapsDone != b.lapsDone) return b.lapsDone.CompareTo(a.lapsDone);
            return a.totalTime.CompareTo(b.totalTime);
        });

        for (int i = 0; i < standings.Count; i++)
            if (standings[i].isPlayer) playerPosition = i + 1;

        OnStandingsUpdate?.Invoke(standings);
    }

    int CalculateReward(int rank)
    {
        int[] rewards = { 5000, 3000, 2000, 1500, 1000, 750, 500, 250 };
        return rank <= rewards.Length ? rewards[rank - 1] : 100;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    void SetPhase(CircuitPhase p)
    {
        phase = p;
        OnPhaseChanged?.Invoke(p);
    }

    void PostDirectorMessage(string msg)
    {
        lastDirectorMessage = msg;
        OnDirectorMessage?.Invoke(msg);
    }

    public static string FormatTime(float seconds)
    {
        int   mins = (int)(seconds / 60f);
        float secs = seconds % 60f;
        return $"{mins}:{secs:00.000}";
    }

    static readonly string[] AussieAINames =
    {
        "Dazza", "Shazza", "Bazza", "Thommo", "Johnno",
        "Gazza", "Stevo", "Macca", "Robbo", "Muzza"
    };
}

// ─── Checkpoint Trigger ───────────────────────────────────────────────────────

public class CheckpointTrigger : MonoBehaviour
{
    public System.Action OnPlayerEnter;
    public int sectorIndex;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            OnPlayerEnter?.Invoke();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (TryGetComponent<BoxCollider>(out var bc))
            Gizmos.DrawCube(bc.center, bc.size);
    }
}

// ─── AI Racer (pure simulation) ───────────────────────────────────────────────

[System.Serializable]
public class AIRacer
{
    public string name;
    public float  lapTimeTarget;
    public float  variance;
    public int    lapsCompleted;
    public float  totalRaceTime;
    public bool   racing;

    private float currentLapTimer;

    public void StartRacing() => racing = true;

    public void Update(float dt, int totalLaps)
    {
        if (!racing || lapsCompleted >= totalLaps) return;
        currentLapTimer += dt;

        float thisLapTarget = lapTimeTarget + Random.Range(-variance, variance);
        if (currentLapTimer >= thisLapTarget)
        {
            lapsCompleted++;
            totalRaceTime  += currentLapTimer;
            currentLapTimer = 0f;
        }
    }
}

// ─── Data Types ───────────────────────────────────────────────────────────────

public enum CircuitPhase { WaitingToStart, Countdown, Racing, Finished }

[System.Serializable]
public class LapRecord
{
    public int             lapNumber;
    public float           lapTime;
    public List<SectorSplit> sectors;
}

[System.Serializable]
public class SectorSplit
{
    public int   sectorIndex;
    public float splitTime;
}

[System.Serializable]
public class RaceStanding
{
    public string name;
    public int    lapsDone;
    public float  totalTime;
    public bool   isPlayer;
}

[System.Serializable]
public class RaceResult
{
    public int            finishPosition;
    public int            totalLaps;
    public float          totalTime;
    public float          bestLap;
    public List<LapRecord> lapRecords;
}
