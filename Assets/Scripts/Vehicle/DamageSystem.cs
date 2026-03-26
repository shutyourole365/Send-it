using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Realistic collision and mechanical damage system.
/// Tracks body panel damage, deforms meshes, breaks components,
/// and feeds damage state to VehicleController and EngineSimulator.
/// Attach to vehicle root.
/// </summary>
[RequireComponent(typeof(VehicleController))]
public class DamageSystem : MonoBehaviour
{
    // ─── Body Panel References ────────────────────────────────────────────────
    [System.Serializable]
    public class BodyPanel
    {
        public string       name;
        public MeshFilter   meshFilter;       // Panel mesh to deform
        public Collider     collider;         // Panel's collider
        public float        healthPercent = 100f;
        public bool         isDetachable  = false;
        public GameObject   destroyedVariant;  // Swapped in when panel breaks
        [HideInInspector]
        public Vector3[]    originalVertices;
        [HideInInspector]
        public bool         isDamaged;
    }

    [Header("Body Panels")]
    public BodyPanel bonnet;
    public BodyPanel frontBumper;
    public BodyPanel rearBumper;
    public BodyPanel leftFront;
    public BodyPanel rightFront;
    public BodyPanel leftRear;
    public BodyPanel rightRear;
    public BodyPanel bootLid;
    public BodyPanel roof;

    // ─── Mechanical Components ────────────────────────────────────────────────
    [Header("Mechanical Health (%)")]
    [Range(0f, 100f)] public float radiatorHealth    = 100f;
    [Range(0f, 100f)] public float steeringHealth    = 100f;
    [Range(0f, 100f)] public float suspensionFLHealth = 100f;
    [Range(0f, 100f)] public float suspensionFRHealth = 100f;
    [Range(0f, 100f)] public float suspensionRLHealth = 100f;
    [Range(0f, 100f)] public float suspensionRRHealth = 100f;
    [Range(0f, 100f)] public float transmissionHealth = 100f;
    [Range(0f, 100f)] public float exhaustHealth      = 100f;

    // ─── Damage Thresholds ───────────────────────────────────────────────────
    [Header("Damage Settings")]
    [Tooltip("Collision impulse (N·s) below which no damage occurs")]
    public float minDamageImpulse = 500f;

    [Tooltip("Collision impulse that causes maximum damage to a panel")]
    public float maxDamageImpulse = 20000f;

    [Tooltip("How deeply the mesh deforms per unit of damage")]
    public float deformStrength = 0.005f;

    [Tooltip("Radius of mesh deformation around the impact point")]
    public float deformRadius = 0.4f;

    // ─── Sparks / Smoke ───────────────────────────────────────────────────────
    [Header("Effects")]
    public ParticleSystem sparkParticles;
    public ParticleSystem coolantLeakParticles;  // Radiator damage
    public ParticleSystem exhaustSmokeParticles; // Mechanical damage smoke
    public AudioSource    crashAudio;
    public AudioClip[]    impactClips;           // Random impact sounds by severity
    public AudioClip      metalScrapClip;

    // ─── Runtime State ────────────────────────────────────────────────────────
    [HideInInspector] public bool isTotalLoss = false;
    [HideInInspector] public float overallBodyHealth;

    private VehicleController vehicle;
    private EngineSimulator   engine;
    private List<BodyPanel>   allPanels;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Awake()
    {
        vehicle = GetComponent<VehicleController>();
        engine  = GetComponent<EngineSimulator>();

        allPanels = new List<BodyPanel>
        {
            bonnet, frontBumper, rearBumper,
            leftFront, rightFront, leftRear, rightRear,
            bootLid, roof,
        };

        // Snapshot original vertices for each panel
        foreach (var panel in allPanels)
        {
            if (panel?.meshFilter != null)
                panel.originalVertices = panel.meshFilter.mesh.vertices.Clone() as Vector3[];
        }
    }

    void Update()
    {
        CalculateOverallHealth();
        UpdateEffects();
    }

    // ─── Collision ───────────────────────────────────────────────────────────
    void OnCollisionEnter(Collision col)
    {
        float impulse = col.impulse.magnitude;
        if (impulse < minDamageImpulse) return;

        Vector3 contactPoint = col.GetContact(0).point;
        Vector3 contactNormal = col.GetContact(0).normal;

        float damageFraction = Mathf.InverseLerp(minDamageImpulse, maxDamageImpulse, impulse);

        // Find the closest panel to the contact point
        BodyPanel hitPanel = FindNearestPanel(contactPoint);
        if (hitPanel != null)
            DamagePanel(hitPanel, damageFraction, contactPoint, contactNormal);

        DamageMechanicals(damageFraction, contactPoint);
        PlayImpactAudio(damageFraction);
        SpawnSparks(contactPoint, impulse);
    }

    // ─── Panel Damage ─────────────────────────────────────────────────────────
    BodyPanel FindNearestPanel(Vector3 worldPoint)
    {
        BodyPanel nearest = null;
        float minDist = float.MaxValue;

        foreach (var panel in allPanels)
        {
            if (panel == null || panel.collider == null) continue;
            float dist = Vector3.Distance(panel.collider.ClosestPoint(worldPoint), worldPoint);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = panel;
            }
        }
        return nearest;
    }

    void DamagePanel(BodyPanel panel, float fraction, Vector3 worldImpact, Vector3 normal)
    {
        float damageAmount = fraction * 100f;
        panel.healthPercent = Mathf.Max(0f, panel.healthPercent - damageAmount);
        panel.isDamaged     = true;

        // Mesh deformation
        DeformMesh(panel, worldImpact, normal, fraction);

        // Swap to destroyed variant at 0% health
        if (panel.healthPercent <= 0f && panel.isDetachable && panel.destroyedVariant != null)
        {
            panel.destroyedVariant.SetActive(true);
            if (panel.meshFilter != null)
                panel.meshFilter.gameObject.SetActive(false);
        }
    }

    void DeformMesh(BodyPanel panel, Vector3 worldImpact, Vector3 impactNormal, float strength)
    {
        if (panel.meshFilter == null || panel.originalVertices == null) return;

        Mesh mesh = panel.meshFilter.mesh;
        Vector3[] verts = mesh.vertices;
        Vector3 localImpact = panel.meshFilter.transform.InverseTransformPoint(worldImpact);
        Vector3 localNormal = panel.meshFilter.transform.InverseTransformDirection(impactNormal);

        for (int i = 0; i < verts.Length; i++)
        {
            float dist = Vector3.Distance(verts[i], localImpact);
            if (dist < deformRadius)
            {
                float influence = 1f - (dist / deformRadius);
                influence = influence * influence;  // Squared falloff
                verts[i] += localNormal * (-deformStrength * strength * influence * 100f);

                // Add slight random micro-crumple
                verts[i] += Random.insideUnitSphere * deformStrength * strength * 0.3f;
            }
        }

        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // ─── Mechanical Damage ───────────────────────────────────────────────────
    void DamageMechanicals(float fraction, Vector3 contactPoint)
    {
        // Front hit damages radiator and steering
        Vector3 localContact = transform.InverseTransformPoint(contactPoint);

        if (localContact.z > 0f)  // Front impact
        {
            radiatorHealth  = Mathf.Max(0f, radiatorHealth  - fraction * 80f);
            steeringHealth  = Mathf.Max(0f, steeringHealth  - fraction * 40f);
            suspensionFLHealth = Mathf.Max(0f, suspensionFLHealth - fraction * 30f);
            suspensionFRHealth = Mathf.Max(0f, suspensionFRHealth - fraction * 30f);
        }
        else // Rear hit
        {
            transmissionHealth = Mathf.Max(0f, transmissionHealth - fraction * 50f);
            exhaustHealth      = Mathf.Max(0f, exhaustHealth      - fraction * 60f);
            suspensionRLHealth = Mathf.Max(0f, suspensionRLHealth - fraction * 30f);
            suspensionRRHealth = Mathf.Max(0f, suspensionRRHealth - fraction * 30f);
        }

        ApplyMechanicalEffects();
    }

    void ApplyMechanicalEffects()
    {
        // Radiator damage → rapid overheat
        if (radiatorHealth < 30f && engine != null)
            engine.coolRate *= radiatorHealth / 100f;

        // Steering damage → reduced max steer angle
        if (steeringHealth < 50f)
            vehicle.maxSteerAngle *= (steeringHealth / 100f);

        // Suspension damage → wheel toe/camber approximation (reduce grip on that corner)
        // In a full implementation, adjust WheelCollider suspension params per corner

        // Transmission damage → random miss-shifts
        if (transmissionHealth < 20f)
            vehicle.automaticGearbox = false;  // Gearbox won't auto shift properly
    }

    // ─── Overall Health ───────────────────────────────────────────────────────
    void CalculateOverallHealth()
    {
        float total = 0f;
        int count   = 0;

        foreach (var panel in allPanels)
        {
            if (panel == null) continue;
            total += panel.healthPercent;
            count++;
        }

        overallBodyHealth = count > 0 ? total / count : 100f;
        isTotalLoss = overallBodyHealth < 5f && radiatorHealth <= 0f;
    }

    // ─── Effects ─────────────────────────────────────────────────────────────
    void UpdateEffects()
    {
        // Coolant leak when radiator is damaged
        if (coolantLeakParticles != null)
        {
            bool shouldLeak = radiatorHealth < 40f;
            if (shouldLeak && !coolantLeakParticles.isPlaying) coolantLeakParticles.Play();
            if (!shouldLeak && coolantLeakParticles.isPlaying) coolantLeakParticles.Stop();
        }

        // Exhaust smoke when mechanical damage is high
        if (exhaustSmokeParticles != null)
        {
            bool shouldSmoke = exhaustHealth < 50f || (engine != null && engine.OverheatSeverity > 0.5f);
            if (shouldSmoke && !exhaustSmokeParticles.isPlaying) exhaustSmokeParticles.Play();
            if (!shouldSmoke && exhaustSmokeParticles.isPlaying) exhaustSmokeParticles.Stop();
        }
    }

    void SpawnSparks(Vector3 position, float impulse)
    {
        if (sparkParticles == null) return;
        if (impulse > minDamageImpulse * 2f)
        {
            sparkParticles.transform.position = position;
            sparkParticles.Emit(Mathf.RoundToInt(impulse / 1000f));
        }
    }

    void PlayImpactAudio(float fraction)
    {
        if (crashAudio == null || impactClips == null || impactClips.Length == 0) return;

        int clipIndex = Mathf.FloorToInt(fraction * (impactClips.Length - 1));
        clipIndex = Mathf.Clamp(clipIndex, 0, impactClips.Length - 1);
        crashAudio.PlayOneShot(impactClips[clipIndex], Mathf.Lerp(0.3f, 1f, fraction));
    }

    // ─── Repair ───────────────────────────────────────────────────────────────
    public void RepairBodywork()
    {
        foreach (var panel in allPanels)
        {
            if (panel == null) continue;
            panel.healthPercent = 100f;
            panel.isDamaged     = false;

            // Restore mesh to original
            if (panel.meshFilter != null && panel.originalVertices != null)
            {
                panel.meshFilter.mesh.vertices = panel.originalVertices.Clone() as Vector3[];
                panel.meshFilter.mesh.RecalculateNormals();
                panel.meshFilter.mesh.RecalculateBounds();
                panel.meshFilter.gameObject.SetActive(true);
            }

            if (panel.destroyedVariant != null)
                panel.destroyedVariant.SetActive(false);
        }
    }

    public void RepairMechanicals()
    {
        radiatorHealth  = suspensionFLHealth = suspensionFRHealth = 100f;
        suspensionRLHealth = suspensionRRHealth = steeringHealth  = 100f;
        transmissionHealth = exhaustHealth = 100f;

        vehicle.maxSteerAngle = 35f;
        if (engine != null) engine.RepairEngine();
    }

    public void FullRepair()
    {
        RepairBodywork();
        RepairMechanicals();
        isTotalLoss = false;
    }
}
