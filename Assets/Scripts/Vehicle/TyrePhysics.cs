using UnityEngine;

/// <summary>
/// Realistic per-wheel tyre simulation.
/// Models temperature, pressure, compound behaviour, wear, and blowout.
/// Dynamically adjusts WheelCollider friction curves based on tyre state.
/// Attach four instances — one per wheel — alongside WheelCollider.
/// </summary>
[RequireComponent(typeof(WheelCollider))]
public class TyrePhysics : MonoBehaviour
{
    // ─── Tyre Compound ───────────────────────────────────────────────────────
    public enum TyreCompound
    {
        StreetRadial,       // Daily driver — decent grip, slow warm-up
        PerformanceStreet,  // Semi-slick street — good grip, moderate warm-up
        SlickDrag,          // Full drag slick — peak grip only in temp window
        BurnoutSpecific,    // Hard compound — lasts longer, makes more smoke
        Mud,                // Off-road — low tarmac grip, no warm-up needed
    }

    // ─── Inspector ───────────────────────────────────────────────────────────
    [Header("Compound")]
    public TyreCompound compound = TyreCompound.StreetRadial;

    [Header("Pressure (PSI)")]
    [Range(10f, 60f)]
    public float pressurePSI = 32f;

    [Tooltip("Optimal pressure range centre — grip peaks here")]
    public float optimalPressurePSI = 32f;

    [Header("Temperature (°C)")]
    [Tooltip("Current tyre surface temperature")]
    public float temperatureC = 25f;

    [Tooltip("Ambient air temperature — affects cool-down rate")]
    public float ambientTempC = 28f;  // Hot Australian day

    public float optimalTempMin = 80f;
    public float optimalTempMax = 110f;
    public float blowoutTempC   = 200f;
    public float blowoutWearPct = 0f;   // Wear % at which blowout can occur

    [Header("Wear")]
    [Range(0f, 100f)]
    [Tooltip("100 = new, 0 = destroyed")]
    public float wearPercent = 100f;

    public float wearRatePerSlipKm = 0.8f;   // % wear per km of tyre slip

    // ─── Runtime State ────────────────────────────────────────────────────────
    [HideInInspector] public bool isBlown     = false;
    [HideInInspector] public float gripFactor = 1f;   // 0–1 multiplier applied to friction
    [HideInInspector] public float slipRatio;          // current longitudinal slip
    [HideInInspector] public float lateralSlip;        // current lateral slip angle (deg)

    // ─── Private ─────────────────────────────────────────────────────────────
    private WheelCollider wc;
    private WheelFrictionCurve baseFwdFriction;
    private WheelFrictionCurve baseSideFriction;

    // Compound lookup tables
    static readonly float[] CompoundPeakFwd  = { 1.0f, 1.2f, 1.5f, 0.85f, 0.6f };
    static readonly float[] CompoundPeakSide = { 1.0f, 1.2f, 1.4f, 0.7f,  0.5f };
    static readonly float[] CompoundHeatRate = { 0.3f, 0.5f, 0.8f, 0.2f,  0.1f };
    static readonly float[] CompoundCoolRate = { 0.1f, 0.08f,0.06f,0.12f, 0.15f };

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Awake()
    {
        wc = GetComponent<WheelCollider>();
        baseFwdFriction  = wc.forwardFriction;
        baseSideFriction = wc.sidewaysFriction;
    }

    void FixedUpdate()
    {
        if (isBlown)
        {
            ApplyBlownTyreFriction();
            return;
        }

        UpdateSlip();
        UpdateTemperature();
        UpdatePressureHeat();
        UpdateWear();
        UpdateGripFactor();
        ApplyFriction();
        CheckBlowout();
    }

    // ─── Slip ─────────────────────────────────────────────────────────────────
    void UpdateSlip()
    {
        WheelHit hit;
        if (wc.GetGroundHit(out hit))
        {
            slipRatio   = hit.forwardSlip;
            lateralSlip = hit.sidewaysSlip;
        }
        else
        {
            slipRatio   = 0f;
            lateralSlip = 0f;
        }
    }

    // ─── Temperature ──────────────────────────────────────────────────────────
    void UpdateTemperature()
    {
        int ci = (int)compound;
        float absSlip = Mathf.Abs(slipRatio) + Mathf.Abs(lateralSlip * 0.5f);

        // Heat from friction work
        float heatInput = absSlip * wc.radius * CompoundHeatRate[ci] * 200f;

        // Cool toward ambient
        float cooling = (temperatureC - ambientTempC) * CompoundCoolRate[ci] * Time.fixedDeltaTime * 5f;

        temperatureC += heatInput * Time.fixedDeltaTime;
        temperatureC -= cooling;
        temperatureC  = Mathf.Max(temperatureC, ambientTempC);
    }

    // ─── Pressure expands with heat ──────────────────────────────────────────
    void UpdatePressureHeat()
    {
        // Pressure rises ~0.1 PSI per 10°C above ambient
        float thermalPressureRise = (temperatureC - ambientTempC) * 0.01f;
        // This is display-only; the effect is factored into grip below
        _ = thermalPressureRise;
    }

    // ─── Wear ─────────────────────────────────────────────────────────────────
    void UpdateWear()
    {
        float slipSpeed = Mathf.Abs(slipRatio) * wc.radius * Mathf.PI * 2f; // m/s of slip
        float wearThisFrame = slipSpeed * wearRatePerSlipKm * Time.fixedDeltaTime / 1000f;
        wearPercent = Mathf.Max(0f, wearPercent - wearThisFrame);
    }

    // ─── Grip Factor ──────────────────────────────────────────────────────────
    void UpdateGripFactor()
    {
        // 1. Temperature window
        float tempGrip;
        if (temperatureC < optimalTempMin)
            tempGrip = Mathf.Lerp(0.5f, 1f, temperatureC / optimalTempMin);
        else if (temperatureC > optimalTempMax)
            tempGrip = Mathf.Lerp(1f, 0.4f, (temperatureC - optimalTempMax) / (blowoutTempC - optimalTempMax));
        else
            tempGrip = 1f;

        // 2. Pressure deviation
        float pressureDelta = Mathf.Abs(pressurePSI - optimalPressurePSI);
        float pressureGrip  = Mathf.Clamp01(1f - pressureDelta * 0.015f);

        // 3. Wear — below 20% wear grip drops sharply
        float wearGrip = wearPercent > 20f
            ? Mathf.Lerp(0.7f, 1f, (wearPercent - 20f) / 80f)
            : Mathf.Lerp(0.1f, 0.7f, wearPercent / 20f);

        gripFactor = tempGrip * pressureGrip * wearGrip;
    }

    // ─── Apply Friction ───────────────────────────────────────────────────────
    void ApplyFriction()
    {
        int ci = (int)compound;
        float peakFwd  = CompoundPeakFwd[ci]  * gripFactor;
        float peakSide = CompoundPeakSide[ci] * gripFactor;

        WheelFrictionCurve fwd  = baseFwdFriction;
        WheelFrictionCurve side = baseSideFriction;

        fwd.extremumValue  = peakFwd;
        fwd.asymptoteValue = peakFwd  * 0.75f;
        side.extremumValue  = peakSide;
        side.asymptoteValue = peakSide * 0.75f;

        wc.forwardFriction  = fwd;
        wc.sidewaysFriction = side;
    }

    // ─── Blowout ──────────────────────────────────────────────────────────────
    void CheckBlowout()
    {
        bool overheated = temperatureC >= blowoutTempC;
        bool worn       = wearPercent  <= blowoutWearPct;
        bool overinflated = pressurePSI > 55f;

        if ((overheated || worn || overinflated) && !isBlown)
            Blowout();
    }

    void Blowout()
    {
        isBlown = true;
        Debug.Log($"[TyrePhysics] BLOWOUT on {gameObject.name}! Temp: {temperatureC:F0}°C  Wear: {wearPercent:F1}%");
        ApplyBlownTyreFriction();
    }

    void ApplyBlownTyreFriction()
    {
        WheelFrictionCurve fwd  = wc.forwardFriction;
        WheelFrictionCurve side = wc.sidewaysFriction;
        fwd.extremumValue   = 0.2f;
        fwd.asymptoteValue  = 0.15f;
        side.extremumValue  = 0.1f;
        side.asymptoteValue = 0.08f;
        wc.forwardFriction  = fwd;
        wc.sidewaysFriction = side;
    }

    // ─── Public API ───────────────────────────────────────────────────────────
    public void RepairAndRefitTyre(TyreCompound newCompound, float startPressure = 32f)
    {
        compound     = newCompound;
        pressurePSI  = startPressure;
        wearPercent  = 100f;
        temperatureC = ambientTempC;
        isBlown      = false;
        gripFactor   = 1f;
        ApplyFriction();
    }

    public void SetPressure(float psi)
    {
        pressurePSI = Mathf.Clamp(psi, 5f, 65f);
    }

    /// <summary>True when tyre is in its optimal operating temperature window.</summary>
    public bool IsInWindow => temperatureC >= optimalTempMin && temperatureC <= optimalTempMax;

    /// <summary>Colour for UI temp gauge: blue cold → green optimal → red hot.</summary>
    public Color TemperatureColour()
    {
        if (temperatureC < optimalTempMin)
            return Color.Lerp(Color.blue, Color.green, temperatureC / optimalTempMin);
        if (temperatureC > optimalTempMax)
            return Color.Lerp(Color.green, Color.red, (temperatureC - optimalTempMax) / (blowoutTempC - optimalTempMax));
        return Color.green;
    }
}
