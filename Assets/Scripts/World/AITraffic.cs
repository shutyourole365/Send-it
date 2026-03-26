using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Realistic free-roam traffic system.
/// Spawns Australian vehicles, follows waypoint paths, obeys speed limits,
/// brakes for the player, reacts to burnouts (horn, panic stop, swerve),
/// and despawns when too far from the player.
/// Place one AITrafficManager in the scene and assign spawn points + waypoint paths.
/// </summary>

// ─── Traffic Manager ──────────────────────────────────────────────────────────

public class AITrafficManager : MonoBehaviour
{
    public static AITrafficManager Instance { get; private set; }

    [Header("Spawning")]
    public GameObject[]   trafficCarPrefabs;     // Different car models
    public Transform[]    spawnPoints;
    public int            maxTrafficCars   = 20;
    public float          spawnCheckInterval = 5f;
    public float          despawnDistance  = 300f;

    [Header("Player Reference")]
    public Transform playerTransform;

    [Header("Paths")]
    public AIWaypointPath[] trafficPaths;

    [Header("Speed Limits (km/h)")]
    public float defaultSpeedLimit = 60f;

    private List<AITrafficVehicle> activeVehicles = new List<AITrafficVehicle>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnCheckInterval);
            CleanupDespawned();
            if (activeVehicles.Count < maxTrafficCars)
                TrySpawn();
        }
    }

    void TrySpawn()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        if (trafficCarPrefabs == null || trafficCarPrefabs.Length == 0) return;

        // Find a spawn point far enough from player
        List<Transform> candidates = new List<Transform>();
        foreach (var sp in spawnPoints)
        {
            if (sp == null) continue;
            float dist = playerTransform
                ? Vector3.Distance(sp.position, playerTransform.position)
                : float.MaxValue;
            if (dist > 80f && dist < despawnDistance)
                candidates.Add(sp);
        }

        if (candidates.Count == 0) return;

        Transform spawn = candidates[Random.Range(0, candidates.Count)];
        GameObject prefab = trafficCarPrefabs[Random.Range(0, trafficCarPrefabs.Length)];
        GameObject go = Instantiate(prefab, spawn.position, spawn.rotation);

        var vehicle = go.GetComponent<AITrafficVehicle>();
        if (vehicle == null) vehicle = go.AddComponent<AITrafficVehicle>();

        // Assign a random path
        if (trafficPaths != null && trafficPaths.Length > 0)
            vehicle.path = trafficPaths[Random.Range(0, trafficPaths.Length)];

        vehicle.speedLimitKPH = defaultSpeedLimit * Random.Range(0.85f, 1.1f);
        activeVehicles.Add(vehicle);
    }

    void CleanupDespawned()
    {
        for (int i = activeVehicles.Count - 1; i >= 0; i--)
        {
            var v = activeVehicles[i];
            if (v == null) { activeVehicles.RemoveAt(i); continue; }

            float dist = playerTransform
                ? Vector3.Distance(v.transform.position, playerTransform.position)
                : 0f;

            if (dist > despawnDistance)
            {
                Destroy(v.gameObject);
                activeVehicles.RemoveAt(i);
            }
        }
    }

    /// <summary>Notify all traffic of a nearby burnout — triggers reactions.</summary>
    public void NotifyBurnout(Vector3 origin, float radius)
    {
        foreach (var v in activeVehicles)
        {
            if (v == null) continue;
            float dist = Vector3.Distance(v.transform.position, origin);
            if (dist < radius)
                v.ReactToBurnout(dist);
        }
    }
}

// ─── Per-Vehicle AI ───────────────────────────────────────────────────────────

[RequireComponent(typeof(Rigidbody))]
public class AITrafficVehicle : MonoBehaviour
{
    // ─── Config ───────────────────────────────────────────────────────────────
    [Header("Navigation")]
    public AIWaypointPath path;
    public float          waypointReachDistance = 5f;

    [Header("Driving")]
    public float speedLimitKPH = 60f;
    public float accelerationForce = 3000f;
    public float brakingForce      = 5000f;
    public float steeringSpeed     = 3f;
    public float maxSteerAngle     = 30f;

    [Header("Sensors")]
    public float frontSensorLength = 10f;
    public float sideSensorOffset  = 1.2f;
    public LayerMask obstacleMask;

    // ─── Audio ────────────────────────────────────────────────────────────────
    [Header("Audio")]
    public AudioSource engineAudioSource;
    public AudioSource hornAudioSource;
    public AudioClip   hornClip;
    public float enginePitchMin = 0.5f;
    public float enginePitchMax = 1.8f;

    // ─── Runtime ──────────────────────────────────────────────────────────────
    private Rigidbody   rb;
    private int         currentWaypointIndex;
    private float       currentSpeedKPH;
    private bool        isBraking;
    private bool        isAvoiding;
    private float       avoidSteering;
    private AIState     state = AIState.Driving;
    private float       panicTimer;
    private float       hornTimer;

    // Wheel colliders (optional — falls back to direct rigidbody force)
    private WheelCollider wFL, wFR, wRL, wRR;

    enum AIState { Driving, Braking, Panic, Stopped }

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Awake()
    {
        rb  = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.3f, 0f);

        wFL = transform.Find("WheelFL")?.GetComponent<WheelCollider>();
        wFR = transform.Find("WheelFR")?.GetComponent<WheelCollider>();
        wRL = transform.Find("WheelRL")?.GetComponent<WheelCollider>();
        wRR = transform.Find("WheelRR")?.GetComponent<WheelCollider>();
    }

    void FixedUpdate()
    {
        currentSpeedKPH = rb.linearVelocity.magnitude * 3.6f;

        switch (state)
        {
            case AIState.Driving: DrivingUpdate(); break;
            case AIState.Braking: BrakingUpdate(); break;
            case AIState.Panic:   PanicUpdate();   break;
            case AIState.Stopped: break;
        }

        CheckObstacles();
        UpdateAudio();
    }

    // ─── Navigation ──────────────────────────────────────────────────────────
    void DrivingUpdate()
    {
        if (path == null || path.waypoints == null || path.waypoints.Length == 0) return;

        Vector3 target = path.waypoints[currentWaypointIndex].position;
        Vector3 dir    = (target - transform.position).normalized;

        // Steering — cross product to determine left/right
        float steerInput = Vector3.SignedAngle(transform.forward, dir, Vector3.up) / maxSteerAngle;
        steerInput = Mathf.Clamp(steerInput, -1f, 1f);

        // Speed — slow down for sharp turns
        float turnSharpness = Mathf.Abs(steerInput);
        float targetKPH     = Mathf.Lerp(speedLimitKPH, speedLimitKPH * 0.4f, turnSharpness);

        float throttle = currentSpeedKPH < targetKPH ? 1f : 0f;
        float brake    = currentSpeedKPH > targetKPH + 5f ? 1f : 0f;

        ApplyDrive(throttle, brake, steerInput + (isAvoiding ? avoidSteering : 0f));

        // Waypoint advance
        if (Vector3.Distance(transform.position, target) < waypointReachDistance)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % path.waypoints.Length;
        }
    }

    void BrakingUpdate()
    {
        ApplyDrive(0f, 1f, 0f);
        if (currentSpeedKPH < 2f) state = AIState.Driving;
    }

    void PanicUpdate()
    {
        panicTimer -= Time.fixedDeltaTime;
        ApplyDrive(0f, 1f, Random.Range(-0.3f, 0.3f));  // Random slight swerve
        if (panicTimer <= 0f) state = AIState.Driving;
    }

    // ─── Drive Application ───────────────────────────────────────────────────
    void ApplyDrive(float throttle, float brake, float steer)
    {
        float steerAngle = steer * maxSteerAngle;

        if (wFL != null)
        {
            // WheelCollider mode
            wFL.steerAngle = steerAngle;
            wFR.steerAngle = steerAngle;

            float torque = throttle * accelerationForce;
            float brakeTq = brake * brakingForce;

            wRL.motorTorque  = torque * 0.5f;
            wRR.motorTorque  = torque * 0.5f;
            wFL.brakeTorque  = brakeTq;
            wFR.brakeTorque  = brakeTq;
            wRL.brakeTorque  = brakeTq;
            wRR.brakeTorque  = brakeTq;
        }
        else
        {
            // Direct rigidbody mode (no wheel colliders on this car)
            if (throttle > 0f)
                rb.AddForce(transform.forward * throttle * accelerationForce * Time.fixedDeltaTime, ForceMode.Force);
            if (brake > 0f)
                rb.AddForce(-rb.linearVelocity.normalized * brake * brakingForce * Time.fixedDeltaTime, ForceMode.Force);

            // Rotation
            float yaw = steer * steeringSpeed * Time.fixedDeltaTime * currentSpeedKPH;
            transform.Rotate(0f, yaw, 0f);
        }
    }

    // ─── Obstacle Sensors ────────────────────────────────────────────────────
    void CheckObstacles()
    {
        isAvoiding = false;

        // Front centre
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward,
            out RaycastHit hit, frontSensorLength, obstacleMask))
        {
            float proximity = 1f - (hit.distance / frontSensorLength);

            if (hit.collider.CompareTag("Player"))
            {
                state = AIState.Braking;
                PlayHorn();
            }
            else if (proximity > 0.6f)
            {
                state = AIState.Braking;
            }
        }

        // Front left sensor
        Vector3 leftOrigin = transform.position + transform.right * -sideSensorOffset + Vector3.up * 0.5f;
        if (Physics.Raycast(leftOrigin, transform.forward, frontSensorLength * 0.7f, obstacleMask))
        {
            isAvoiding   = true;
            avoidSteering = 0.5f;  // Steer right
        }

        // Front right sensor
        Vector3 rightOrigin = transform.position + transform.right * sideSensorOffset + Vector3.up * 0.5f;
        if (Physics.Raycast(rightOrigin, transform.forward, frontSensorLength * 0.7f, obstacleMask))
        {
            isAvoiding   = true;
            avoidSteering = -0.5f; // Steer left
        }
    }

    // ─── Burnout Reaction ────────────────────────────────────────────────────
    public void ReactToBurnout(float distance)
    {
        float severity = 1f - Mathf.Clamp01(distance / 60f);

        if (severity > 0.7f)
        {
            // Very close — panic stop and swerve
            state      = AIState.Panic;
            panicTimer = Random.Range(2f, 5f);
            PlayHorn();
        }
        else if (severity > 0.3f)
        {
            // Medium — brake
            state = AIState.Braking;
            StartCoroutine(ResumeAfterDelay(Random.Range(1f, 3f)));
            if (Random.value > 0.5f) PlayHorn();
        }
        // Else — carry on, too far away
    }

    IEnumerator ResumeAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (state == AIState.Braking) state = AIState.Driving;
    }

    // ─── Audio ───────────────────────────────────────────────────────────────
    void UpdateAudio()
    {
        if (engineAudioSource == null) return;
        float speedRatio  = Mathf.Clamp01(currentSpeedKPH / speedLimitKPH);
        engineAudioSource.pitch  = Mathf.Lerp(enginePitchMin, enginePitchMax, speedRatio);
        engineAudioSource.volume = 0.3f + speedRatio * 0.4f;
        if (!engineAudioSource.isPlaying) engineAudioSource.Play();
    }

    void PlayHorn()
    {
        hornTimer -= Time.deltaTime;
        if (hornTimer > 0f) return;
        if (hornAudioSource != null && hornClip != null)
            hornAudioSource.PlayOneShot(hornClip);
        hornTimer = Random.Range(2f, 5f);
    }

    void OnCollisionEnter(Collision col)
    {
        if (col.impulse.magnitude > 2000f)
        {
            state      = AIState.Panic;
            panicTimer = Random.Range(3f, 6f);
            PlayHorn();
        }
    }
}

// ─── Waypoint Path ────────────────────────────────────────────────────────────

public class AIWaypointPath : MonoBehaviour
{
    public Transform[] waypoints;
    public bool        loop = true;

    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null) continue;
            Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            Gizmos.DrawSphere(waypoints[i].position, 0.5f);
        }
        if (loop && waypoints[0] != null && waypoints[^1] != null)
            Gizmos.DrawLine(waypoints[^1].position, waypoints[0].position);
    }
}
