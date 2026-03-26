using UnityEngine;
using System.Collections;

/// <summary>
/// Realistic engine simulation: coolant temp, oil temp, overrev damage,
/// turbo/supercharger boost, intercooler, fuel, exhaust pops, and audio.
/// Attach to the vehicle root alongside VehicleController.
/// </summary>
[RequireComponent(typeof(VehicleController))]
public class EngineSimulator : MonoBehaviour
{
    // ─── Temperature ─────────────────────────────────────────────────────────
    [Header("Temperatures (°C)")]
    public float coolantTempC     = 25f;
    public float oilTempC         = 25f;
    public float ambientTempC     = 28f;   // Hot Australian day

    [Tooltip("Normal operating coolant temp")]
    public float optimalCoolantC  = 90f;

    [Tooltip("Overheating begins")]
    public float overheatCoolantC = 110f;

    [Tooltip("Engine seizure if reached")]
    public float seizureCoolantC  = 135f;

    [Tooltip("Coolant temp rise rate per unit throttle")]
    public float heatRiseRate     = 4f;

    [Tooltip("Cooling rate when fans/airflow are active")]
    public float coolRate         = 2f;

    // ─── Oil ─────────────────────────────────────────────────────────────────
    [Header("Oil")]
    [Range(0f, 1f)]
    public float oilLevel         = 1f;    // 1 = full, 0 = empty → seizure
    public float oilBurnRate      = 0.00002f;  // per second at high RPM

    [Tooltip("Oil temp lags coolant slightly")]
    public float oilThermalLag    = 0.3f;

    // ─── Forced Induction ────────────────────────────────────────────────────
    [Header("Forced Induction")]
    public ForcedInductionType inductionType = ForcedInductionType.None;
    public float maxBoostPSI    = 12f;
    public float boostBuildRate = 8f;     // PSI/s ramp up
    public float boostBleedRate = 20f;    // PSI/s ramp down (wastegate)
    public float spoolRPM       = 2800f;  // Turbo spools above this RPM
    public float intercoolerEfficiency = 0.75f;  // 0=no cooling, 1=perfect

    [HideInInspector] public float currentBoostPSI;
    [HideInInspector] public float intercoolerTempC = 30f;

    public enum ForcedInductionType { None, Turbo, Supercharger, TwinTurbo }

    // ─── Fuel ─────────────────────────────────────────────────────────────────
    [Header("Fuel")]
    [Range(0f, 1f)]
    public float fuelLevel        = 1f;   // 1 = full tank
    public float tankCapacityL    = 70f;
    public float fuelConsumptionL_per100km = 14f;  // L/100km base
    [HideInInspector] public bool isRunningRich;
    [HideInInspector] public bool isRunningLean;

    // ─── Engine State ─────────────────────────────────────────────────────────
    [Header("Engine State")]
    public float engineHealthPercent = 100f;  // 100 = perfect, 0 = blown
    [HideInInspector] public bool isOverheating;
    [HideInInspector] public bool isSeized;
    [HideInInspector] public bool isRunning = true;

    // ─── Audio ────────────────────────────────────────────────────────────────
    [Header("Audio")]
    public AudioSource engineAudio;
    public AudioSource exhaustAudio;
    public AudioSource turboAudio;

    [Tooltip("Engine sound clips — blend between them by RPM")]
    public AudioClip[] engineRPMLayers;      // 0=idle, 1=mid, 2=high, 3=redline

    public AudioClip turboSpoolClip;
    public AudioClip turboFlutterClip;
    public AudioClip exhaustPopClip;
    public AudioClip engineKnockClip;

    public float enginePitchMin = 0.5f;
    public float enginePitchMax = 2.5f;

    // ─── Private ─────────────────────────────────────────────────────────────
    private VehicleController vehicle;
    private float lastThrottle;
    private float timeSinceLastPop;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Awake()
    {
        vehicle = GetComponent<VehicleController>();
    }

    void FixedUpdate()
    {
        if (isSeized) return;

        UpdateTemperatures();
        UpdateForcedInduction();
        UpdateFuel();
        UpdateEngineHealth();
        CheckDamageEvents();
    }

    void Update()
    {
        if (isSeized) return;
        UpdateAudio();
    }

    // ─── Temperatures ────────────────────────────────────────────────────────
    void UpdateTemperatures()
    {
        float throttle = vehicle.throttleInput;
        float rpmRatio = Mathf.InverseLerp(vehicle.idleRPM, vehicle.redlineRPM, vehicle.currentRPM);

        // Coolant heats with load and RPM
        float loadHeat   = throttle * rpmRatio * heatRiseRate;
        float airCooling = vehicle.speedKPH * 0.005f * (coolantTempC - ambientTempC);
        float fanCooling = coolantTempC > optimalCoolantC
            ? (coolantTempC - optimalCoolantC) * coolRate
            : 0f;

        coolantTempC += (loadHeat - airCooling - fanCooling) * Time.fixedDeltaTime;
        coolantTempC  = Mathf.Max(coolantTempC, ambientTempC);

        // Oil lags coolant
        oilTempC = Mathf.Lerp(oilTempC, coolantTempC + 15f, oilThermalLag * Time.fixedDeltaTime);

        // Overheat state
        isOverheating = coolantTempC >= overheatCoolantC;

        // Oil burn at high RPM
        if (rpmRatio > 0.7f)
            oilLevel = Mathf.Max(0f, oilLevel - oilBurnRate * Time.fixedDeltaTime);
    }

    // ─── Forced Induction ────────────────────────────────────────────────────
    void UpdateForcedInduction()
    {
        if (inductionType == ForcedInductionType.None)
        {
            currentBoostPSI = 0f;
            return;
        }

        bool throttleOpen = vehicle.throttleInput > 0.1f;
        float rpmRatio    = Mathf.InverseLerp(vehicle.idleRPM, vehicle.redlineRPM, vehicle.currentRPM);

        if (inductionType == ForcedInductionType.Supercharger)
        {
            // Supercharger: instant boost proportional to throttle
            currentBoostPSI = Mathf.Lerp(currentBoostPSI,
                vehicle.throttleInput * maxBoostPSI, Time.fixedDeltaTime * 10f);
        }
        else
        {
            // Turbo: needs RPM to spool
            bool spooling = vehicle.currentRPM > spoolRPM && throttleOpen;
            float target  = spooling
                ? Mathf.Lerp(0f, maxBoostPSI, (vehicle.currentRPM - spoolRPM) / (vehicle.redlineRPM - spoolRPM))
                : 0f;

            float rate = spooling ? boostBuildRate : boostBleedRate;
            currentBoostPSI = Mathf.MoveTowards(currentBoostPSI, target, rate * Time.fixedDeltaTime);

            // Turbo flutter on throttle lift
            if (lastThrottle > 0.6f && vehicle.throttleInput < 0.2f && currentBoostPSI > 4f)
                TriggerTurboFlutter();
        }

        // Intercooler: cools charge air, heat rises at high boost
        intercoolerTempC = Mathf.Lerp(intercoolerTempC,
            ambientTempC + (currentBoostPSI / maxBoostPSI) * 60f * (1f - intercoolerEfficiency),
            Time.fixedDeltaTime * 2f);

        // Apply boost torque multiplier to vehicle (detonation risk if too hot)
        float boostTorqueMult = 1f + (currentBoostPSI / 14.7f) * 0.8f;
        if (intercoolerTempC > 80f)
            boostTorqueMult *= Mathf.Lerp(1f, 0.7f, (intercoolerTempC - 80f) / 40f);

        vehicle.peakTorque = vehicle.peakTorque * boostTorqueMult;

        lastThrottle = vehicle.throttleInput;
    }

    // ─── Fuel ─────────────────────────────────────────────────────────────────
    void UpdateFuel()
    {
        if (fuelLevel <= 0f)
        {
            isRunning = false;
            return;
        }

        float rpmRatio       = Mathf.InverseLerp(vehicle.idleRPM, vehicle.redlineRPM, vehicle.currentRPM);
        float loadFactor     = vehicle.throttleInput * rpmRatio;
        float boostFactor    = 1f + (currentBoostPSI / 14.7f) * 0.4f;
        float consumptionRate = (fuelConsumptionL_per100km / 100f) * (vehicle.speedKPH / 3600f)
                              * loadFactor * boostFactor;

        fuelLevel = Mathf.Max(0f, fuelLevel - (consumptionRate / tankCapacityL) * Time.fixedDeltaTime);

        isRunningRich = loadFactor > 0.85f && currentBoostPSI > maxBoostPSI * 0.9f;
        isRunningLean = fuelLevel < 0.05f;
    }

    // ─── Engine Health ────────────────────────────────────────────────────────
    void UpdateEngineHealth()
    {
        float damage = 0f;

        // Overheating damage
        if (coolantTempC > overheatCoolantC)
            damage += (coolantTempC - overheatCoolantC) * 0.02f * Time.fixedDeltaTime;

        // Overrev damage
        if (vehicle.currentRPM > vehicle.redlineRPM)
            damage += (vehicle.currentRPM - vehicle.redlineRPM) * 0.001f * Time.fixedDeltaTime;

        // Low oil damage
        if (oilLevel < 0.2f)
            damage += (0.2f - oilLevel) * 5f * Time.fixedDeltaTime;

        // Lean mixture damage
        if (isRunningLean)
            damage += 0.5f * Time.fixedDeltaTime;

        engineHealthPercent = Mathf.Max(0f, engineHealthPercent - damage);

        if (engineHealthPercent <= 0f || coolantTempC >= seizureCoolantC || oilLevel <= 0f)
            SeizeEngine();
    }

    void CheckDamageEvents()
    {
        timeSinceLastPop += Time.fixedDeltaTime;

        // Exhaust pop on gear shift / throttle lift with boost
        bool throttleLift = lastThrottle > 0.5f && vehicle.throttleInput < 0.1f;
        bool hasBoost     = currentBoostPSI > 2f;

        if (throttleLift && hasBoost && timeSinceLastPop > 0.3f)
        {
            TriggerExhaustPop();
            timeSinceLastPop = 0f;
        }

        // Engine knock at high temp + high load
        if (coolantTempC > overheatCoolantC * 0.95f && vehicle.throttleInput > 0.8f)
            TriggerEngineKnock();
    }

    // ─── Engine Events ────────────────────────────────────────────────────────
    void SeizeEngine()
    {
        if (isSeized) return;
        isSeized  = true;
        isRunning = false;
        vehicle.enabled = false;
        Debug.LogWarning("[Engine] ENGINE SEIZED! Get to the workshop.");
    }

    void TriggerTurboFlutter()
    {
        if (turboAudio != null && turboFlutterClip != null)
            turboAudio.PlayOneShot(turboFlutterClip, 0.8f);
    }

    void TriggerExhaustPop()
    {
        if (exhaustAudio != null && exhaustPopClip != null)
            exhaustAudio.PlayOneShot(exhaustPopClip, Random.Range(0.6f, 1f));
    }

    void TriggerEngineKnock()
    {
        if (engineAudio != null && engineKnockClip != null && !engineAudio.isPlaying)
            engineAudio.PlayOneShot(engineKnockClip, 0.4f);
    }

    // ─── Audio ────────────────────────────────────────────────────────────────
    void UpdateAudio()
    {
        if (engineAudio == null || vehicle == null) return;

        float rpmRatio = Mathf.InverseLerp(vehicle.idleRPM, vehicle.redlineRPM, vehicle.currentRPM);
        engineAudio.pitch = Mathf.Lerp(enginePitchMin, enginePitchMax, rpmRatio);

        if (!engineAudio.isPlaying && isRunning) engineAudio.Play();
        if (engineAudio.isPlaying && !isRunning) engineAudio.Stop();

        // Turbo whine proportional to boost
        if (turboAudio != null && inductionType != ForcedInductionType.None)
        {
            turboAudio.volume = currentBoostPSI / maxBoostPSI * 0.6f;
            turboAudio.pitch  = 0.5f + (currentBoostPSI / maxBoostPSI) * 1.5f;
            if (!turboAudio.isPlaying && turboSpoolClip != null) turboAudio.Play();
        }
    }

    // ─── Public API ───────────────────────────────────────────────────────────
    public void Refuel()
    {
        fuelLevel = 1f;
    }

    public void TopUpOil()
    {
        oilLevel = Mathf.Min(1f, oilLevel + 0.5f);
    }

    public void RepairEngine()
    {
        engineHealthPercent = 100f;
        isSeized            = false;
        isRunning           = true;
        coolantTempC        = ambientTempC + 20f;
        vehicle.enabled     = true;
    }

    /// <summary>0–1 overheat severity for UI gauge colour.</summary>
    public float OverheatSeverity =>
        Mathf.Clamp01((coolantTempC - optimalCoolantC) / (seizureCoolantC - optimalCoolantC));

    /// <summary>Boost in bar for HUD (1 PSI = 0.0689 bar).</summary>
    public float BoostBar => currentBoostPSI * 0.0689f;
}
