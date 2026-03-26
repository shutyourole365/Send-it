using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// In-car HUD: analogue-style speedo and tacho needles, boost gauge,
/// coolant/oil temp, tyre temp indicators, gear display, fuel, burnout score,
/// damage warnings, and weather/road condition overlay.
///
/// Assign all UI references in the inspector.
/// Requires VehicleController, EngineSimulator, BurnoutSystem, DamageSystem, WeatherSystem.
/// </summary>
public class HUD : MonoBehaviour
{
    // ─── Vehicle References ──────────────────────────────────────────────────
    [Header("Vehicle")]
    public VehicleController vehicle;
    public EngineSimulator   engine;
    public BurnoutSystem     burnout;
    public DamageSystem      damage;
    public WeatherSystem     weather;

    // Per-wheel tyre physics (FL, FR, RL, RR)
    public TyrePhysics tyreFL;
    public TyrePhysics tyreFR;
    public TyrePhysics tyreRL;
    public TyrePhysics tyreRR;

    // ─── Speedo ───────────────────────────────────────────────────────────────
    [Header("Speedometer")]
    public RectTransform speedoNeedle;
    [Tooltip("Needle rotation at 0 km/h")]
    public float speedoMinAngle = -135f;
    [Tooltip("Needle rotation at max km/h")]
    public float speedoMaxAngle = 135f;
    public float speedoMaxKPH   = 320f;
    public TextMeshProUGUI speedText;

    // ─── Tacho ────────────────────────────────────────────────────────────────
    [Header("Tachometer")]
    public RectTransform tachoNeedle;
    public float tachoMinAngle  = -135f;
    public float tachoMaxAngle  = 135f;
    public TextMeshProUGUI rpmText;
    public Image           redlineFlash;  // Flashes red at redline

    // ─── Gear ─────────────────────────────────────────────────────────────────
    [Header("Gear Display")]
    public TextMeshProUGUI gearText;    // "1" "2" "R" "N"
    public Image           gearBackground;
    public Color           reverseColour = new Color(1f, 0.3f, 0.3f);
    public Color           normalColour  = new Color(0.1f, 0.1f, 0.1f, 0.7f);

    // ─── Boost ────────────────────────────────────────────────────────────────
    [Header("Boost Gauge")]
    public RectTransform boostNeedle;
    public float boostMinAngle  = -90f;
    public float boostMaxAngle  = 90f;
    public float boostMaxBar    = 2f;    // Bar (1 bar ≈ 14.5 PSI)
    public Image boostFill;
    public Gradient boostColourGradient;
    public GameObject boostGaugeRoot;   // Hidden when no forced induction

    // ─── Temperatures ────────────────────────────────────────────────────────
    [Header("Temperatures")]
    public Image           coolantTempFill;
    public TextMeshProUGUI coolantTempText;
    public Image           oilTempFill;
    public TextMeshProUGUI oilTempText;
    public Image           coolantWarningIcon;   // Blinks when overheating
    public Image           oilWarningIcon;

    // ─── Tyre Temp Indicators ────────────────────────────────────────────────
    [Header("Tyre Temperature")]
    public Image tyreTempFL;
    public Image tyreTempFR;
    public Image tyreTempRL;
    public Image tyreTempRR;
    public Image tyreWearFL;
    public Image tyreWearFR;
    public Image tyreWearRL;
    public Image tyreWearRR;
    public Image tyreBlownFL;
    public Image tyreBlownFR;
    public Image tyreBlownRL;
    public Image tyreBlownRR;

    // ─── Fuel ─────────────────────────────────────────────────────────────────
    [Header("Fuel")]
    public Image           fuelFill;
    public TextMeshProUGUI fuelText;
    public Image           lowFuelWarning;

    // ─── Burnout Score ────────────────────────────────────────────────────────
    [Header("Burnout HUD")]
    public GameObject          burnoutScoreRoot;
    public TextMeshProUGUI     burnoutScoreText;
    public TextMeshProUGUI     burnoutTimerText;
    public Image               burnoutScoreFlash;
    public TextMeshProUGUI     sessionScoreText;

    // ─── Damage Warnings ─────────────────────────────────────────────────────
    [Header("Damage Warnings")]
    public Image checkEngineLight;
    public Image radiatorWarning;
    public Image steeringWarning;
    public Image transmissionWarning;
    public Image totalLossOverlay;       // Full screen red flash

    // ─── Weather / Road ──────────────────────────────────────────────────────
    [Header("Weather")]
    public TextMeshProUGUI roadConditionText;
    public Image           wetRoadIcon;
    public TextMeshProUGUI ambientTempText;

    // ─── Drag Race Overlay ───────────────────────────────────────────────────
    [Header("Drag Race")]
    public GameObject          dragOverlayRoot;
    public TextMeshProUGUI     etText;
    public TextMeshProUGUI     reactionTimeText;
    public TextMeshProUGUI     trapSpeedText;
    public TextMeshProUGUI     dragResultText;

    // ─── Private ─────────────────────────────────────────────────────────────
    private float redlineFlashTimer;
    private float warningBlinkTimer;
    private bool  warningBlinkState;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void LateUpdate()
    {
        UpdateSpeedo();
        UpdateTacho();
        UpdateGear();
        UpdateBoost();
        UpdateTemperatures();
        UpdateTyreIndicators();
        UpdateFuel();
        UpdateBurnoutScore();
        UpdateDamageWarnings();
        UpdateWeather();

        warningBlinkTimer += Time.deltaTime;
        if (warningBlinkTimer > 0.5f)
        {
            warningBlinkTimer = 0f;
            warningBlinkState = !warningBlinkState;
        }
    }

    // ─── Speedo ───────────────────────────────────────────────────────────────
    void UpdateSpeedo()
    {
        if (vehicle == null) return;
        float angle = Mathf.Lerp(speedoMinAngle, speedoMaxAngle, vehicle.speedKPH / speedoMaxKPH);
        if (speedoNeedle) speedoNeedle.localEulerAngles = new Vector3(0f, 0f, -angle);
        if (speedText)    speedText.text = $"{vehicle.speedKPH:F0}";
    }

    // ─── Tacho ────────────────────────────────────────────────────────────────
    void UpdateTacho()
    {
        if (vehicle == null) return;
        float rpmRatio = vehicle.currentRPM / vehicle.redlineRPM;
        float angle    = Mathf.Lerp(tachoMinAngle, tachoMaxAngle, rpmRatio);
        if (tachoNeedle) tachoNeedle.localEulerAngles = new Vector3(0f, 0f, -angle);
        if (rpmText)     rpmText.text = $"{vehicle.currentRPM:F0}";

        // Redline flash
        if (redlineFlash != null)
        {
            bool atRedline = vehicle.currentRPM > vehicle.redlineRPM * 0.95f;
            redlineFlash.enabled = atRedline && warningBlinkState;
        }
    }

    // ─── Gear ─────────────────────────────────────────────────────────────────
    void UpdateGear()
    {
        if (vehicle == null || gearText == null) return;

        if (vehicle.isReversing)
        {
            gearText.text = "R";
            if (gearBackground) gearBackground.color = reverseColour;
        }
        else
        {
            gearText.text = (vehicle.currentGear + 1).ToString();
            if (gearBackground) gearBackground.color = normalColour;
        }
    }

    // ─── Boost ────────────────────────────────────────────────────────────────
    void UpdateBoost()
    {
        if (engine == null) return;

        bool hasInduction = engine.inductionType != EngineSimulator.ForcedInductionType.None;
        if (boostGaugeRoot) boostGaugeRoot.SetActive(hasInduction);
        if (!hasInduction) return;

        float boostRatio = engine.BoostBar / boostMaxBar;
        float angle      = Mathf.Lerp(boostMinAngle, boostMaxAngle, boostRatio);
        if (boostNeedle) boostNeedle.localEulerAngles = new Vector3(0f, 0f, -angle);

        if (boostFill)
        {
            boostFill.fillAmount = boostRatio;
            boostFill.color      = boostColourGradient.Evaluate(boostRatio);
        }
    }

    // ─── Temperatures ────────────────────────────────────────────────────────
    void UpdateTemperatures()
    {
        if (engine == null) return;

        float coolantRatio = engine.coolantTempC / engine.seizureCoolantC;
        float oilRatio     = engine.oilTempC     / engine.seizureCoolantC;

        SetFill(coolantTempFill, coolantRatio, TempColour(coolantRatio));
        SetFill(oilTempFill,     oilRatio,     TempColour(oilRatio));

        if (coolantTempText) coolantTempText.text = $"{engine.coolantTempC:F0}°";
        if (oilTempText)     oilTempText.text     = $"{engine.oilTempC:F0}°";

        // Warning blink
        if (coolantWarningIcon) coolantWarningIcon.enabled = engine.isOverheating && warningBlinkState;
        if (oilWarningIcon)     oilWarningIcon.enabled     = engine.oilLevel < 0.15f && warningBlinkState;
    }

    // ─── Tyre Indicators ──────────────────────────────────────────────────────
    void UpdateTyreIndicators()
    {
        UpdateTyreCorner(tyreFL, tyreWearFL, tyreBlownFL, tyreFL);
        UpdateTyreCorner(tyreFR, tyreWearFR, tyreBlownFR, tyreFR);
        UpdateTyreCorner(tyreRL, tyreWearRL, tyreBlownRL, tyreRL);
        UpdateTyreCorner(tyreRR, tyreWearRR, tyreBlownRR, tyreRR);
    }

    void UpdateTyreCorner(TyrePhysics tyre, Image wearFill, Image blownIcon, TyrePhysics tempSource)
    {
        if (tyre == null) return;

        // Temp colour indicator
        Image tempImg = tyre == tyreFL ? tyreTempFL
                      : tyre == tyreFR ? tyreTempFR
                      : tyre == tyreRL ? tyreTempRL
                      : tyreTempRR;

        if (tempImg != null) tempImg.color = tyre.TemperatureColour();

        // Wear fill
        if (wearFill != null)
        {
            wearFill.fillAmount = tyre.wearPercent / 100f;
            wearFill.color      = Color.Lerp(Color.red, Color.green, tyre.wearPercent / 100f);
        }

        // Blown icon
        if (blownIcon != null) blownIcon.enabled = tyre.isBlown;
    }

    // ─── Fuel ─────────────────────────────────────────────────────────────────
    void UpdateFuel()
    {
        if (engine == null) return;

        SetFill(fuelFill, engine.fuelLevel, FuelColour(engine.fuelLevel));
        if (fuelText) fuelText.text = $"{engine.fuelLevel * engine.tankCapacityL:F0}L";
        if (lowFuelWarning) lowFuelWarning.enabled = engine.fuelLevel < 0.1f && warningBlinkState;
    }

    // ─── Burnout Score ────────────────────────────────────────────────────────
    void UpdateBurnoutScore()
    {
        if (burnout == null) return;

        bool active = burnout.isBurningOut;
        if (burnoutScoreRoot) burnoutScoreRoot.SetActive(active || burnout.currentRunDuration > 0f);

        if (burnoutScoreText) burnoutScoreText.text = $"{burnout.currentRunScore:F0}";
        if (burnoutTimerText) burnoutTimerText.text = $"{burnout.currentRunDuration:F1}s";
        if (sessionScoreText) sessionScoreText.text = $"SESSION  {burnout.sessionScore:F0}";

        // Flash on active burnout
        if (burnoutScoreFlash)
            burnoutScoreFlash.enabled = active && warningBlinkState;
    }

    // ─── Damage Warnings ─────────────────────────────────────────────────────
    void UpdateDamageWarnings()
    {
        if (damage == null) return;

        if (checkEngineLight)     checkEngineLight.enabled     = (engine != null && engine.engineHealthPercent < 50f) && warningBlinkState;
        if (radiatorWarning)      radiatorWarning.enabled      = damage.radiatorHealth < 30f && warningBlinkState;
        if (steeringWarning)      steeringWarning.enabled      = damage.steeringHealth < 40f && warningBlinkState;
        if (transmissionWarning)  transmissionWarning.enabled  = damage.transmissionHealth < 30f && warningBlinkState;
        if (totalLossOverlay)     totalLossOverlay.enabled     = damage.isTotalLoss;
    }

    // ─── Weather ─────────────────────────────────────────────────────────────
    void UpdateWeather()
    {
        if (weather == null) return;

        if (roadConditionText) roadConditionText.text = weather.RoadCondition();
        if (ambientTempText)   ambientTempText.text   = $"{weather.currentAmbientTemp:F0}°C";
        if (wetRoadIcon)       wetRoadIcon.enabled    = weather.roadWetness > 0.2f;
    }

    // ─── Drag Race Results ────────────────────────────────────────────────────
    public void ShowDragResult(DragResult result)
    {
        if (dragOverlayRoot) dragOverlayRoot.SetActive(true);
        if (etText)           etText.text           = $"ET  {result.playerET:F3}s";
        if (reactionTimeText) reactionTimeText.text = $"RT  {result.playerRT:F4}s";
        if (trapSpeedText)    trapSpeedText.text     = $"{result.playerTrapKPH:F1} km/h";
        if (dragResultText)
        {
            dragResultText.text  = result.isFoul ? "RED LIGHT" : result.winner == "Player" ? "WIN" : "LOSS";
            dragResultText.color = result.isFoul ? Color.red : result.winner == "Player" ? Color.green : Color.white;
        }
    }

    public void HideDragResult()
    {
        if (dragOverlayRoot) dragOverlayRoot.SetActive(false);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    void SetFill(Image img, float amount, Color colour)
    {
        if (img == null) return;
        img.fillAmount = Mathf.Clamp01(amount);
        img.color      = colour;
    }

    Color TempColour(float ratio) =>
        ratio < 0.5f ? Color.Lerp(Color.blue, Color.green, ratio * 2f) :
        ratio < 0.8f ? Color.Lerp(Color.green, Color.yellow, (ratio - 0.5f) / 0.3f) :
        Color.Lerp(Color.yellow, Color.red, (ratio - 0.8f) / 0.2f);

    Color FuelColour(float level) =>
        level > 0.25f ? Color.green :
        level > 0.1f  ? Color.yellow :
        Color.red;
}
