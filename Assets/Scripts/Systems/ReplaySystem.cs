using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Records vehicle state every fixed frame and plays it back.
/// Used to replay burnout runs, best laps, and drag passes.
/// Attach to the vehicle root. Playback spawns a ghost car.
/// </summary>
public class ReplaySystem : MonoBehaviour
{
    // ─── Settings ─────────────────────────────────────────────────────────────
    [Header("Recording")]
    [Tooltip("Frames per second to record (lower = less memory, coarser playback)")]
    public int    recordFPS         = 30;
    public float  maxRecordDuration = 120f;  // Max seconds stored (rolling buffer)

    [Header("Ghost Car")]
    [Tooltip("Semi-transparent ghost prefab spawned during playback")]
    public GameObject ghostCarPrefab;
    [Range(0f, 1f)]
    public float      ghostAlpha = 0.35f;

    [Header("Playback")]
    public float playbackSpeed = 1f;   // 0.25 for slow-mo, 2 for fast-forward

    // ─── Events ───────────────────────────────────────────────────────────────
    public System.Action OnRecordingStarted;
    public System.Action OnRecordingStopped;
    public System.Action OnPlaybackStarted;
    public System.Action OnPlaybackFinished;

    // ─── Runtime State ────────────────────────────────────────────────────────
    public bool IsRecording  { get; private set; }
    public bool IsPlayingBack { get; private set; }
    public float RecordingDuration => frames.Count / (float)recordFPS;

    // ─── Private ──────────────────────────────────────────────────────────────
    private List<ReplayFrame>    frames        = new List<ReplayFrame>();
    private List<ReplayFrame>    savedReplay   = new List<ReplayFrame>();
    private float                recordTimer;
    private float                recordInterval;
    private int                  playbackIndex;
    private float                playbackTimer;
    private GameObject           ghostInstance;
    private Renderer[]           ghostRenderers;
    private VehicleController    vehicle;
    private Rigidbody            rb;
    private Coroutine            playbackCoroutine;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Awake()
    {
        vehicle         = GetComponent<VehicleController>();
        rb              = GetComponent<Rigidbody>();
        recordInterval  = 1f / recordFPS;
    }

    void FixedUpdate()
    {
        if (!IsRecording) return;

        recordTimer += Time.fixedDeltaTime;
        if (recordTimer < recordInterval) return;
        recordTimer = 0f;

        CaptureFrame();

        // Rolling buffer — discard oldest frames beyond max duration
        int maxFrames = Mathf.RoundToInt(maxRecordDuration * recordFPS);
        while (frames.Count > maxFrames)
            frames.RemoveAt(0);
    }

    // ─── Recording ────────────────────────────────────────────────────────────
    public void StartRecording()
    {
        if (IsRecording) return;
        frames.Clear();
        IsRecording = true;
        recordTimer = 0f;
        OnRecordingStarted?.Invoke();
        Debug.Log("[Replay] Recording started.");
    }

    public void StopRecording()
    {
        if (!IsRecording) return;
        IsRecording = false;
        savedReplay = new List<ReplayFrame>(frames);
        OnRecordingStopped?.Invoke();
        Debug.Log($"[Replay] Recording stopped — {savedReplay.Count} frames ({RecordingDuration:F1}s)");
    }

    public void SaveBestReplay()
    {
        // Call this when a new best lap/score is set — freezes current buffer
        savedReplay = new List<ReplayFrame>(frames);
    }

    // ─── Frame Capture ────────────────────────────────────────────────────────
    void CaptureFrame()
    {
        var frame = new ReplayFrame
        {
            timestamp        = Time.time,
            position         = transform.position,
            rotation         = transform.rotation,
            velocity         = rb.linearVelocity,
            angularVelocity  = rb.angularVelocity,
            speedKPH         = vehicle?.speedKPH ?? 0f,
            rpm              = vehicle?.currentRPM ?? 0f,
            gear             = vehicle?.currentGear ?? 0,
            throttle         = vehicle?.throttleInput ?? 0f,
            brake            = vehicle?.brakeInput ?? 0f,
            steer            = vehicle?.steerInput ?? 0f,
            isBurningOut     = false,  // Set by BurnoutSystem if present
        };

        var burnout = GetComponent<BurnoutSystem>();
        if (burnout != null) frame.isBurningOut = burnout.isBurningOut;

        frames.Add(frame);
    }

    // ─── Playback ─────────────────────────────────────────────────────────────
    public void PlayReplay(bool useSaved = true)
    {
        var source = useSaved ? savedReplay : frames;
        if (source == null || source.Count < 2)
        {
            Debug.LogWarning("[Replay] Not enough frames to play back.");
            return;
        }

        if (playbackCoroutine != null) StopCoroutine(playbackCoroutine);
        playbackCoroutine = StartCoroutine(PlaybackRoutine(source));
    }

    IEnumerator PlaybackRoutine(List<ReplayFrame> source)
    {
        IsPlayingBack = true;
        SpawnGhost();
        OnPlaybackStarted?.Invoke();

        // Disable player vehicle during playback
        if (vehicle) vehicle.enabled = false;

        float startTime = source[0].timestamp;
        float playTime  = 0f;
        int   frameIdx  = 0;

        while (frameIdx < source.Count - 1)
        {
            playTime += Time.deltaTime * playbackSpeed;

            // Find the two frames to interpolate between
            while (frameIdx < source.Count - 2 &&
                   source[frameIdx + 1].timestamp - startTime <= playTime)
                frameIdx++;

            var a = source[frameIdx];
            var b = source[Mathf.Min(frameIdx + 1, source.Count - 1)];

            float span = b.timestamp - a.timestamp;
            float t    = span > 0f ? Mathf.Clamp01((playTime - (a.timestamp - startTime)) / span) : 1f;

            ApplyFrameToGhost(a, b, t);
            yield return null;
        }

        // Snap to final frame
        if (ghostInstance != null)
        {
            ghostInstance.transform.position = source[^1].position;
            ghostInstance.transform.rotation = source[^1].rotation;
        }

        yield return new WaitForSeconds(1f);
        StopPlayback();
    }

    void ApplyFrameToGhost(ReplayFrame a, ReplayFrame b, float t)
    {
        if (ghostInstance == null) return;

        ghostInstance.transform.position = Vector3.Lerp(a.position, b.position, t);
        ghostInstance.transform.rotation = Quaternion.Slerp(a.rotation, b.rotation, t);

        // Pulse ghost opacity during burnout
        bool burning = a.isBurningOut;
        float alpha  = burning ? ghostAlpha * (0.7f + Mathf.Sin(Time.time * 10f) * 0.3f) : ghostAlpha;
        SetGhostAlpha(alpha);
    }

    public void StopPlayback()
    {
        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }

        IsPlayingBack = false;
        DestroyGhost();

        if (vehicle) vehicle.enabled = true;
        OnPlaybackFinished?.Invoke();
        Debug.Log("[Replay] Playback finished.");
    }

    // ─── Ghost Car ────────────────────────────────────────────────────────────
    void SpawnGhost()
    {
        if (ghostCarPrefab == null)
        {
            // Fallback: duplicate this vehicle and make it transparent
            ghostInstance = Instantiate(gameObject);
            Destroy(ghostInstance.GetComponent<VehicleController>());
            Destroy(ghostInstance.GetComponent<Rigidbody>());
            Destroy(ghostInstance.GetComponent<BurnoutSystem>());
            Destroy(ghostInstance.GetComponent<ReplaySystem>());
            Destroy(ghostInstance.GetComponent<EngineSimulator>());
            Destroy(ghostInstance.GetComponent<DamageSystem>());
        }
        else
        {
            ghostInstance = Instantiate(ghostCarPrefab);
        }

        ghostRenderers = ghostInstance.GetComponentsInChildren<Renderer>();
        SetGhostAlpha(ghostAlpha);
        ghostInstance.name = "Ghost_Replay";
    }

    void SetGhostAlpha(float alpha)
    {
        if (ghostRenderers == null) return;
        foreach (var r in ghostRenderers)
        {
            foreach (var mat in r.materials)
            {
                Color c = mat.color;
                c.a = alpha;
                mat.color = c;

                // URP transparency
                mat.SetFloat("_Surface", 1f);  // Transparent
                mat.SetFloat("_Alpha",   alpha);
            }
        }
    }

    void DestroyGhost()
    {
        if (ghostInstance != null)
        {
            Destroy(ghostInstance);
            ghostInstance = null;
        }
    }

    // ─── Serialise / Export ──────────────────────────────────────────────────
    public string ExportToJson()
    {
        var export = new ReplayExport
        {
            carId       = GameManager.Instance?.currentCarId ?? "unknown",
            frameCount  = savedReplay.Count,
            durationSec = RecordingDuration,
            frames      = savedReplay,
        };
        return JsonUtility.ToJson(export, true);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    public float GetTotalDuration() => savedReplay.Count / (float)recordFPS;
    public bool  HasSavedReplay     => savedReplay != null && savedReplay.Count > 2;
}

// ─── Data ─────────────────────────────────────────────────────────────────────

[System.Serializable]
public class ReplayFrame
{
    public float      timestamp;
    public Vector3    position;
    public Quaternion rotation;
    public Vector3    velocity;
    public Vector3    angularVelocity;
    public float      speedKPH;
    public float      rpm;
    public int        gear;
    public float      throttle;
    public float      brake;
    public float      steer;
    public bool       isBurningOut;
}

[System.Serializable]
public class ReplayExport
{
    public string            carId;
    public int               frameCount;
    public float             durationSec;
    public List<ReplayFrame> frames;
}
