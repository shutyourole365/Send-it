using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages burnout detection, scoring, smoke/audio feedback, and tyre wear.
/// Requires VehicleController on the same GameObject.
/// Assign particle systems and audio sources in the inspector.
/// </summary>
[RequireComponent(typeof(VehicleController))]
public class BurnoutSystem : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────────────────────
    [Header("Particles")]
    [Tooltip("Smoke particle system for left rear tyre")]
    public ParticleSystem smokeRL;

    [Tooltip("Smoke particle system for right rear tyre")]
    public ParticleSystem smokeRR;

    [Tooltip("Tyre mark/skid decal projector — assign a TrailRenderer or DecalProjector")]
    public TrailRenderer skidMarkRL;
    public TrailRenderer skidMarkRR;

    [Header("Audio")]
    public AudioSource tyreScreechAudio;
    public AudioSource engineAudio;
    public AudioClip   tyreScreechClip;

    [Tooltip("Engine audio pitch range (idle to redline)")]
    public float enginePitchMin = 0.6f;
    public float enginePitchMax = 2.2f;

    // ─── Burnout Tuning ───────────────────────────────────────────────────────
    [Header("Burnout Settings")]
    [Tooltip("Rear wheel spin speed (RPM) required to trigger a burnout")]
    public float spinThreshold = 300f;

    [Tooltip("Minimum time in seconds before a burnout is scored")]
    public float minScoreDuration = 1.5f;

    [Tooltip("Base score per second of active burnout")]
    public float scorePerSecond = 100f;

    [Tooltip("Bonus multiplier for longer burnouts")]
    public float durationBonusMultiplier = 1.5f;

    [Tooltip("Maximum tyre wear. At 0 the tyre blows.")]
    [Range(0f, 1f)]
    public float tyreWear = 1f;

    public float tyreWearRate    = 0.004f;
    public float tyreRepairRate  = 0.001f;

    [Tooltip("Smoke emission rate scales with this value")]
    public float maxSmokeEmission = 150f;

    // ─── Score Tracking ───────────────────────────────────────────────────────
    [Header("Score (Runtime)")]
    [HideInInspector] public float sessionScore;
    [HideInInspector] public float currentRunScore;
    [HideInInspector] public float currentRunDuration;
    [HideInInspector] public bool  isBurningOut;

    // Leaderboard of best runs this session
    public List<BurnoutRun> runHistory = new List<BurnoutRun>();

    // ─── Intensity (used by CameraController) ────────────────────────────────
    /// <summary>0–1 intensity of the current burnout. Used by camera for shake.</summary>
    public float CurrentIntensity { get; private set; }

    // ─── Private ─────────────────────────────────────────────────────────────
    private VehicleController vehicle;
    private bool wasLineLockPressed;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Awake()
    {
        vehicle = GetComponent<VehicleController>();
    }

    void Update()
    {
        HandleLineLockInput();
        UpdateEngineAudio();
    }

    void FixedUpdate()
    {
        DetectBurnout();
        UpdateSmoke();
        UpdateSkidMarks();
        UpdateTyreWear();
    }

    // ─── Line Lock ───────────────────────────────────────────────────────────
    void HandleLineLockInput()
    {
        // Hold Left Shift to engage line lock (clamps front wheels for burnout)
        bool lineLockPressed = Input.GetKey(KeyCode.LeftShift);
        vehicle.lineLockActive = lineLockPressed;

        if (!wasLineLockPressed && lineLockPressed)
            OnLineLockEngage();
        else if (wasLineLockPressed && !lineLockPressed)
            OnLineLockRelease();

        wasLineLockPressed = lineLockPressed;
    }

    void OnLineLockEngage()
    {
        currentRunScore    = 0f;
        currentRunDuration = 0f;
    }

    void OnLineLockRelease()
    {
        if (isBurningOut && currentRunDuration >= minScoreDuration)
            FinaliseRun();
        isBurningOut = false;
        CurrentIntensity = 0f;
    }

    // ─── Burnout Detection ────────────────────────────────────────────────────
    void DetectBurnout()
    {
        float rearRPM = (Mathf.Abs(vehicle.wheelRL.rpm) + Mathf.Abs(vehicle.wheelRR.rpm)) * 0.5f;
        bool rearSpinning = rearRPM > spinThreshold;
        bool frontLocked  = vehicle.lineLockActive;
        bool throttleOn   = vehicle.throttleInput > 0.3f;

        bool activeBurnout = rearSpinning && frontLocked && throttleOn && tyreWear > 0f;

        if (activeBurnout)
        {
            isBurningOut        = true;
            currentRunDuration += Time.fixedDeltaTime;

            // Intensity: higher RPM + less tyre grip = more chaos
            float rpmRatio = Mathf.Clamp01(rearRPM / (vehicle.redlineRPM * 0.8f));
            CurrentIntensity = Mathf.Lerp(CurrentIntensity,
                rpmRatio * vehicle.tyreTemperature, Time.fixedDeltaTime * 5f);

            // Score: base + duration bonus
            float durationBonus = currentRunDuration > 5f ? durationBonusMultiplier : 1f;
            currentRunScore    += scorePerSecond * durationBonus * Time.fixedDeltaTime;
        }
        else
        {
            CurrentIntensity = Mathf.Lerp(CurrentIntensity, 0f, Time.fixedDeltaTime * 4f);

            // If we were burning and stopped naturally (tyre blow etc.)
            if (isBurningOut)
            {
                if (currentRunDuration >= minScoreDuration)
                    FinaliseRun();
                isBurningOut = false;
            }
        }
    }

    // ─── Scoring ─────────────────────────────────────────────────────────────
    void FinaliseRun()
    {
        sessionScore += currentRunScore;

        var run = new BurnoutRun
        {
            score    = currentRunScore,
            duration = currentRunDuration,
            maxHeat  = vehicle.tyreTemperature
        };
        runHistory.Add(run);
        runHistory.Sort((a, b) => b.score.CompareTo(a.score));

        Debug.Log($"[Burnout] Run finished — Score: {currentRunScore:F0}  Duration: {currentRunDuration:F1}s  Session total: {sessionScore:F0}");

        currentRunScore    = 0f;
        currentRunDuration = 0f;
    }

    // ─── Smoke ───────────────────────────────────────────────────────────────
    void UpdateSmoke()
    {
        float emission = isBurningOut ? maxSmokeEmission * CurrentIntensity : 0f;
        SetEmission(smokeRL, emission);
        SetEmission(smokeRR, emission);
    }

    void SetEmission(ParticleSystem ps, float rate)
    {
        if (ps == null) return;
        var emission = ps.emission;
        emission.rateOverTime = rate;

        if (rate > 0f && !ps.isPlaying) ps.Play();
        else if (rate <= 0f && ps.isPlaying) ps.Stop();
    }

    // ─── Skid Marks ──────────────────────────────────────────────────────────
    void UpdateSkidMarks()
    {
        bool emitting = isBurningOut && tyreWear > 0f;
        if (skidMarkRL != null) skidMarkRL.emitting = emitting;
        if (skidMarkRR != null) skidMarkRR.emitting = emitting;
    }

    // ─── Tyre Wear ───────────────────────────────────────────────────────────
    void UpdateTyreWear()
    {
        if (isBurningOut)
        {
            tyreWear = Mathf.Clamp01(tyreWear - tyreWearRate * Time.fixedDeltaTime);

            if (tyreWear <= 0f)
                OnTyreBlow();
        }
        else
        {
            // Very slow passive repair (pitstop)
            tyreWear = Mathf.Clamp01(tyreWear + tyreRepairRate * Time.fixedDeltaTime);
        }
    }

    void OnTyreBlow()
    {
        Debug.Log("[Burnout] TYRE BLOWN!");
        // Hook: disable rear drive, play blow audio, trigger UI
        vehicle.lineLockActive = false;
    }

    /// <summary>Instantly repair tyres — call from pit stop / customisation menu.</summary>
    public void RepairTyres() => tyreWear = 1f;

    // ─── Audio ───────────────────────────────────────────────────────────────
    void UpdateEngineAudio()
    {
        if (engineAudio == null || vehicle == null) return;

        float rpmRatio = Mathf.InverseLerp(vehicle.idleRPM, vehicle.redlineRPM, vehicle.currentRPM);
        engineAudio.pitch = Mathf.Lerp(enginePitchMin, enginePitchMax, rpmRatio);

        if (!engineAudio.isPlaying) engineAudio.Play();

        // Tyre screech
        if (tyreScreechAudio != null && tyreScreechClip != null)
        {
            if (isBurningOut && !tyreScreechAudio.isPlaying)
                tyreScreechAudio.PlayOneShot(tyreScreechClip);
            else if (!isBurningOut)
                tyreScreechAudio.Stop();
        }
    }
}

/// <summary>Stores data for a single completed burnout run.</summary>
[System.Serializable]
public class BurnoutRun
{
    public float score;
    public float duration;
    public float maxHeat;
}
