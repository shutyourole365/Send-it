using UnityEngine;
using System.Collections;

/// <summary>
/// Australian weather system: scorching heat, sudden storms, dust, and night.
/// Affects tyre grip, engine cooling, visibility, and road surface.
/// Controls particle systems, lighting, and post-processing volume weights.
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    // ─── Weather State ────────────────────────────────────────────────────────
    public enum WeatherType
    {
        ClearHot,       // Typical Canberra summer — 38°C, shimmering road
        Overcast,       // Dull grey sky — moderate grip
        ThunderstormApproaching,
        HeavyRain,      // Wet road — dramatically reduced grip
        Hailstorm,      // Damage to exposed cars
        Dusty,          // Outback-style red dust, reduced visibility
        Night,          // Artificial lighting only
        NightRain,
    }

    // ─── Inspector ───────────────────────────────────────────────────────────
    [Header("Current Weather")]
    public WeatherType currentWeather = WeatherType.ClearHot;

    [Header("Transition")]
    public float transitionDuration = 30f;
    public bool  autoWeatherCycle   = false;
    public float cycleIntervalMin   = 120f;
    public float cycleIntervalMax   = 300f;

    [Header("Sun & Sky")]
    public Light sunLight;
    public Gradient sunColourByWeather;
    public AnimationCurve sunIntensityByWeather;

    [Header("Particles")]
    public ParticleSystem rainParticles;
    public ParticleSystem hailParticles;
    public ParticleSystem dustParticles;
    public ParticleSystem heatHazeParticles;  // Spawned on road surface

    [Header("Road Wetness (Material)")]
    [Tooltip("Shared material for road surfaces — set _Wetness property")]
    public Material roadMaterial;

    [Header("Post Processing (URP Volume Weights)")]
    public UnityEngine.Rendering.Volume clearVolume;
    public UnityEngine.Rendering.Volume rainVolume;
    public UnityEngine.Rendering.Volume nightVolume;
    public UnityEngine.Rendering.Volume heatHazeVolume;

    [Header("Audio")]
    public AudioSource weatherAmbientAudio;
    public AudioClip   rainClip;
    public AudioClip   thunderClip;
    public AudioClip   hailClip;
    public AudioClip   dustWindClip;

    // ─── Grip Modifiers (applied to TyrePhysics via WeatherManager) ──────────
    [Header("Grip Modifiers")]
    [Tooltip("Road grip multiplier per weather type (matches WeatherType order)")]
    public float[] roadGripMultipliers = { 1.0f, 0.95f, 0.7f, 0.45f, 0.5f, 0.85f, 1.0f, 0.4f };

    [Header("Ambient Temperature by Weather (°C)")]
    public float[] ambientTemperatures = { 38f, 24f, 28f, 18f, 12f, 32f, 22f, 16f };

    // ─── Runtime ─────────────────────────────────────────────────────────────
    [HideInInspector] public float currentRoadGrip   = 1f;
    [HideInInspector] public float currentAmbientTemp = 28f;
    [HideInInspector] public float roadWetness        = 0f;   // 0 dry, 1 fully wet

    private WeatherType targetWeather;
    private float       transitionProgress = 1f;
    private TyrePhysics[] sceneTyres;
    private Coroutine     cycleCoroutine;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Start()
    {
        sceneTyres = FindObjectsByType<TyrePhysics>(FindObjectsSortMode.None);
        ApplyWeatherImmediate(currentWeather);

        if (autoWeatherCycle)
            cycleCoroutine = StartCoroutine(AutoCycleWeather());
    }

    void Update()
    {
        if (transitionProgress < 1f)
        {
            transitionProgress += Time.deltaTime / transitionDuration;
            transitionProgress  = Mathf.Clamp01(transitionProgress);
            BlendWeather(currentWeather, targetWeather, transitionProgress);
        }

        UpdateRoadDrying();
        UpdateHeatHaze();
    }

    // ─── Weather Transition ───────────────────────────────────────────────────
    public void SetWeather(WeatherType weather, bool instant = false)
    {
        if (weather == currentWeather) return;

        targetWeather       = weather;
        transitionProgress  = instant ? 1f : 0f;

        if (instant)
            ApplyWeatherImmediate(weather);
    }

    void ApplyWeatherImmediate(WeatherType weather)
    {
        currentWeather    = weather;
        int wi            = (int)weather;
        currentRoadGrip   = roadGripMultipliers[Mathf.Clamp(wi, 0, roadGripMultipliers.Length - 1)];
        currentAmbientTemp = ambientTemperatures[Mathf.Clamp(wi, 0, ambientTemperatures.Length - 1)];

        UpdateParticles(weather, 1f);
        UpdateLighting(weather, 1f);
        UpdatePostProcessing(weather, 1f);
        UpdateAudio(weather);
        BroadcastGripToTyres();
        SetRoadWetness(IsWet(weather) ? 1f : 0f);
    }

    void BlendWeather(WeatherType from, WeatherType to, float t)
    {
        int fi = (int)from;
        int ti = (int)to;

        currentRoadGrip    = Mathf.Lerp(roadGripMultipliers[fi], roadGripMultipliers[ti], t);
        currentAmbientTemp = Mathf.Lerp(ambientTemperatures[fi], ambientTemperatures[ti], t);

        UpdateParticles(to, t);
        UpdateLighting(to, t);
        UpdatePostProcessing(to, t);
        BroadcastGripToTyres();
        SetRoadWetness(IsWet(to) ? t : Mathf.Max(0f, roadWetness - t));

        if (t >= 1f)
        {
            currentWeather = to;
            UpdateAudio(to);
        }
    }

    // ─── Particles ───────────────────────────────────────────────────────────
    void UpdateParticles(WeatherType weather, float intensity)
    {
        SetParticleEmission(rainParticles,     weather == WeatherType.HeavyRain || weather == WeatherType.NightRain, intensity);
        SetParticleEmission(hailParticles,     weather == WeatherType.Hailstorm, intensity);
        SetParticleEmission(dustParticles,     weather == WeatherType.Dusty, intensity);
        SetParticleEmission(heatHazeParticles, weather == WeatherType.ClearHot, intensity);
    }

    void SetParticleEmission(ParticleSystem ps, bool active, float intensity)
    {
        if (ps == null) return;
        var em = ps.emission;
        if (active)
        {
            em.rateOverTime = ps.main.maxParticles * intensity;
            if (!ps.isPlaying) ps.Play();
        }
        else
        {
            em.rateOverTime = 0f;
            if (ps.isPlaying) ps.Stop();
        }
    }

    // ─── Lighting ────────────────────────────────────────────────────────────
    void UpdateLighting(WeatherType weather, float intensity)
    {
        if (sunLight == null) return;

        bool isNight = weather == WeatherType.Night || weather == WeatherType.NightRain;
        bool isStorm = weather == WeatherType.HeavyRain || weather == WeatherType.Hailstorm
                    || weather == WeatherType.ThunderstormApproaching;

        float targetIntensity = isNight ? 0.05f : isStorm ? 0.5f : 1.2f;
        Color targetColour    = isNight ? new Color(0.1f, 0.1f, 0.3f)
                                : isStorm ? new Color(0.6f, 0.65f, 0.7f)
                                : new Color(1f, 0.95f, 0.8f);

        sunLight.intensity = Mathf.Lerp(sunLight.intensity, targetIntensity, Time.deltaTime * 2f);
        sunLight.color     = Color.Lerp(sunLight.color, targetColour, Time.deltaTime * 2f);
    }

    // ─── Post Processing ─────────────────────────────────────────────────────
    void UpdatePostProcessing(WeatherType weather, float t)
    {
        bool isRaining = weather == WeatherType.HeavyRain || weather == WeatherType.NightRain
                      || weather == WeatherType.Hailstorm;
        bool isHot     = weather == WeatherType.ClearHot;
        bool isNight   = weather == WeatherType.Night || weather == WeatherType.NightRain;

        if (clearVolume    != null) clearVolume.weight    = Mathf.Lerp(clearVolume.weight,    isHot && !isRaining ? 1f : 0f, t);
        if (rainVolume     != null) rainVolume.weight     = Mathf.Lerp(rainVolume.weight,     isRaining ? 1f : 0f, t);
        if (nightVolume    != null) nightVolume.weight    = Mathf.Lerp(nightVolume.weight,    isNight ? 1f : 0f, t);
        if (heatHazeVolume != null) heatHazeVolume.weight = Mathf.Lerp(heatHazeVolume.weight, isHot ? t : 0f, t);
    }

    // ─── Road Wetness ─────────────────────────────────────────────────────────
    void SetRoadWetness(float wetness)
    {
        roadWetness = Mathf.Clamp01(wetness);
        if (roadMaterial != null)
            roadMaterial.SetFloat("_Wetness", roadWetness);
    }

    void UpdateRoadDrying()
    {
        // Road dries slowly after rain
        if (!IsWet(currentWeather) && roadWetness > 0f)
        {
            float dryRate = currentAmbientTemp > 30f ? 0.02f : 0.005f;
            SetRoadWetness(roadWetness - dryRate * Time.deltaTime);
            BroadcastGripToTyres();
        }
    }

    bool IsWet(WeatherType w) =>
        w == WeatherType.HeavyRain || w == WeatherType.Hailstorm || w == WeatherType.NightRain;

    // ─── Heat Haze ────────────────────────────────────────────────────────────
    void UpdateHeatHaze()
    {
        if (currentWeather != WeatherType.ClearHot || heatHazeVolume == null) return;
        // Pulse haze intensity slightly for a shimmering effect
        float pulse = 0.8f + Mathf.Sin(Time.time * 0.5f) * 0.2f;
        heatHazeVolume.weight = pulse;
    }

    // ─── Tyre Grip Broadcast ──────────────────────────────────────────────────
    void BroadcastGripToTyres()
    {
        foreach (var tyre in sceneTyres)
        {
            if (tyre == null) continue;
            tyre.ambientTempC = currentAmbientTemp;

            // Wet road reduces the compound's effective grip ceiling
            // by reducing the WheelCollider friction multiplier globally
            // (TyrePhysics already applies per-tyre grip; this is road surface wetness)
            WheelCollider wc = tyre.GetComponent<WheelCollider>();
            if (wc == null) continue;

            float wetGripMultiplier = Mathf.Lerp(1f, 0.4f, roadWetness);
            WheelFrictionCurve fwd = wc.forwardFriction;
            fwd.stiffness = wetGripMultiplier;
            wc.forwardFriction = fwd;

            WheelFrictionCurve side = wc.sidewaysFriction;
            side.stiffness = wetGripMultiplier;
            wc.sidewaysFriction = side;
        }
    }

    // ─── Audio ────────────────────────────────────────────────────────────────
    void UpdateAudio(WeatherType weather)
    {
        if (weatherAmbientAudio == null) return;

        AudioClip clip = weather switch
        {
            WeatherType.HeavyRain  or WeatherType.NightRain => rainClip,
            WeatherType.Hailstorm  => hailClip,
            WeatherType.Dusty      => dustWindClip,
            WeatherType.ThunderstormApproaching => thunderClip,
            _ => null,
        };

        if (clip != null && weatherAmbientAudio.clip != clip)
        {
            weatherAmbientAudio.clip = clip;
            weatherAmbientAudio.loop = true;
            weatherAmbientAudio.Play();
        }
        else if (clip == null)
        {
            weatherAmbientAudio.Stop();
        }
    }

    // ─── Auto Cycle ───────────────────────────────────────────────────────────
    IEnumerator AutoCycleWeather()
    {
        while (true)
        {
            float wait = Random.Range(cycleIntervalMin, cycleIntervalMax);
            yield return new WaitForSeconds(wait);

            WeatherType next = PickNextWeather();
            SetWeather(next);
        }
    }

    WeatherType PickNextWeather()
    {
        // Biased toward Australian conditions
        float r = Random.value;
        if (r < 0.40f) return WeatherType.ClearHot;
        if (r < 0.55f) return WeatherType.Overcast;
        if (r < 0.65f) return WeatherType.ThunderstormApproaching;
        if (r < 0.75f) return WeatherType.HeavyRain;
        if (r < 0.80f) return WeatherType.Dusty;
        if (r < 0.88f) return WeatherType.Night;
        if (r < 0.94f) return WeatherType.NightRain;
        return WeatherType.Hailstorm;
    }

    // ─── Public ───────────────────────────────────────────────────────────────
    /// <summary>Is the track safe to race on right now?</summary>
    public bool IsRaceSafe() =>
        currentWeather != WeatherType.Hailstorm && roadWetness < 0.8f;

    /// <summary>Current road surface description for UI.</summary>
    public string RoadCondition()
    {
        if (roadWetness > 0.7f) return "WET";
        if (roadWetness > 0.3f) return "DAMP";
        if (currentWeather == WeatherType.ClearHot) return "HOT/DRY";
        return "DRY";
    }
}
