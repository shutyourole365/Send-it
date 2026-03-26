using UnityEngine;

/// <summary>
/// Core vehicle physics controller.
/// Handles engine, drivetrain, steering, braking and line lock for burnouts.
/// Attach to the root vehicle GameObject alongside a Rigidbody.
/// Each wheel needs a WheelCollider assigned in the inspector.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    // ─── Wheel References ────────────────────────────────────────────────────
    [Header("Wheel Colliders")]
    public WheelCollider wheelFL;
    public WheelCollider wheelFR;
    public WheelCollider wheelRL;
    public WheelCollider wheelRR;

    [Header("Wheel Meshes")]
    public Transform meshFL;
    public Transform meshFR;
    public Transform meshRL;
    public Transform meshRR;

    // ─── Engine ──────────────────────────────────────────────────────────────
    [Header("Engine")]
    [Tooltip("Peak torque in Newton-metres")]
    public float peakTorque = 600f;

    [Tooltip("RPM at which peak torque is produced")]
    public float peakTorqueRPM = 4000f;

    [Tooltip("Redline RPM — engine cuts out above this")]
    public float redlineRPM = 7000f;

    [Tooltip("Idle RPM")]
    public float idleRPM = 800f;

    [Tooltip("Engine braking torque applied when throttle is released")]
    public float engineBrakingTorque = 80f;

    // ─── Gearbox ─────────────────────────────────────────────────────────────
    [Header("Gearbox")]
    public float[] gearRatios = { 3.5f, 2.3f, 1.6f, 1.2f, 0.9f, 0.7f };
    public float finalDriveRatio = 3.7f;
    public float reverseRatio = 3.2f;

    [Tooltip("RPM at which auto shift up occurs")]
    public float shiftUpRPM = 6500f;

    [Tooltip("RPM at which auto shift down occurs")]
    public float shiftDownRPM = 2000f;

    public bool automaticGearbox = false;

    // ─── Drivetrain ──────────────────────────────────────────────────────────
    [Header("Drivetrain")]
    public DriveType driveType = DriveType.RearWheelDrive;

    public enum DriveType { FrontWheelDrive, RearWheelDrive, AllWheelDrive }

    [Range(0f, 1f)]
    [Tooltip("AWD front/rear torque split (0 = full rear, 1 = full front)")]
    public float awdFrontBias = 0.35f;

    // ─── Steering ────────────────────────────────────────────────────────────
    [Header("Steering")]
    public float maxSteerAngle = 35f;

    [Tooltip("Reduces steering angle at high speed")]
    public AnimationCurve steerSpeedCurve = AnimationCurve.Linear(0f, 1f, 200f, 0.3f);

    // ─── Brakes ──────────────────────────────────────────────────────────────
    [Header("Brakes")]
    public float frontBrakeTorque = 3000f;
    public float rearBrakeTorque = 2000f;
    public float handBrakeTorque = 6000f;

    // ─── Suspension ──────────────────────────────────────────────────────────
    [Header("Suspension")]
    public float suspensionSpring = 35000f;
    public float suspensionDamper = 4500f;
    public float suspensionTargetPosition = 0.5f;
    public float suspensionDistance = 0.2f;

    // ─── Tyre ────────────────────────────────────────────────────────────────
    [Header("Tyre")]
    [Tooltip("Grip multiplier. Decreases as tyre heats up during burnout.")]
    [Range(0f, 1f)]
    public float tyreGrip = 1f;

    public float tyreHeatRate = 0.05f;
    public float tyreCoolRate = 0.02f;

    // ─── Runtime State ───────────────────────────────────────────────────────
    [HideInInspector] public float currentRPM;
    [HideInInspector] public int currentGear = 0;   // 0 = 1st
    [HideInInspector] public bool isReversing;
    [HideInInspector] public float speedKPH;
    [HideInInspector] public float throttleInput;
    [HideInInspector] public float brakeInput;
    [HideInInspector] public float steerInput;
    [HideInInspector] public bool handbrakeInput;
    [HideInInspector] public bool lineLockActive;   // set by BurnoutSystem
    [HideInInspector] public float tyreTemperature; // 0–1, fed to BurnoutSystem

    private Rigidbody rb;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.4f, 0.1f);

        ApplySuspensionSettings();
    }

    void Update()
    {
        GatherInput();

        if (automaticGearbox)
            AutoShift();
    }

    void FixedUpdate()
    {
        speedKPH = rb.linearVelocity.magnitude * 3.6f;

        UpdateRPM();
        ApplyDrive();
        ApplySteering();
        ApplyBrakes();
        UpdateWheelMeshes();
        UpdateTyreHeat();
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    void GatherInput()
    {
        throttleInput = Input.GetAxis("Vertical") > 0 ? Input.GetAxis("Vertical") : 0f;
        brakeInput    = Input.GetAxis("Vertical") < 0 ? -Input.GetAxis("Vertical") : 0f;
        steerInput    = Input.GetAxis("Horizontal");
        handbrakeInput = Input.GetButton("Jump");

        // Manual gear shift
        if (!automaticGearbox)
        {
            if (Input.GetKeyDown(KeyCode.E)) ShiftUp();
            if (Input.GetKeyDown(KeyCode.Q)) ShiftDown();
        }

        if (Input.GetKeyDown(KeyCode.R)) isReversing = !isReversing;
    }

    // ─── RPM ─────────────────────────────────────────────────────────────────
    void UpdateRPM()
    {
        float wheelRPM = (wheelRL.rpm + wheelRR.rpm) * 0.5f;
        float ratio = isReversing ? reverseRatio : gearRatios[currentGear];
        float targetRPM = Mathf.Abs(wheelRPM) * ratio * finalDriveRatio;
        targetRPM = Mathf.Clamp(targetRPM, idleRPM, redlineRPM);

        // Blend toward target to simulate flywheel inertia
        currentRPM = Mathf.Lerp(currentRPM, targetRPM + (throttleInput * 1000f), Time.fixedDeltaTime * 5f);
        currentRPM = Mathf.Clamp(currentRPM, idleRPM, redlineRPM);
    }

    // ─── Drive ───────────────────────────────────────────────────────────────
    void ApplyDrive()
    {
        float torque = CalculateTorque() * throttleInput;

        // Reduce grip-based torque by tyre heat (hot tyres spin easier)
        float gripFactor = Mathf.Lerp(1f, 0.4f, tyreTemperature);

        if (!isReversing)
        {
            switch (driveType)
            {
                case DriveType.RearWheelDrive:
                    SetMotorTorque(0f, 0f, torque * gripFactor * 0.5f, torque * gripFactor * 0.5f);
                    break;
                case DriveType.FrontWheelDrive:
                    SetMotorTorque(torque * 0.5f, torque * 0.5f, 0f, 0f);
                    break;
                case DriveType.AllWheelDrive:
                    float front = torque * awdFrontBias;
                    float rear  = torque * (1f - awdFrontBias);
                    SetMotorTorque(front * 0.5f, front * 0.5f, rear * gripFactor * 0.5f, rear * gripFactor * 0.5f);
                    break;
            }
        }
        else
        {
            float reverseTorque = -torque;
            SetMotorTorque(0f, 0f, reverseTorque * 0.5f, reverseTorque * 0.5f);
        }

        // Engine braking
        if (throttleInput < 0.05f)
        {
            ApplyEngineBraking();
        }
    }

    float CalculateTorque()
    {
        // Simple torque curve: ramps up to peak, then falls toward redline
        float rpmRatio = currentRPM / peakTorqueRPM;
        float torqueCurve = rpmRatio <= 1f
            ? Mathf.Lerp(peakTorque * 0.6f, peakTorque, rpmRatio)
            : Mathf.Lerp(peakTorque, peakTorque * 0.5f, (currentRPM - peakTorqueRPM) / (redlineRPM - peakTorqueRPM));

        return torqueCurve;
    }

    void ApplyEngineBraking()
    {
        wheelRL.motorTorque -= engineBrakingTorque * 0.5f;
        wheelRR.motorTorque -= engineBrakingTorque * 0.5f;
    }

    void SetMotorTorque(float fl, float fr, float rl, float rr)
    {
        wheelFL.motorTorque = fl;
        wheelFR.motorTorque = fr;
        wheelRL.motorTorque = rl;
        wheelRR.motorTorque = rr;
    }

    // ─── Steering ────────────────────────────────────────────────────────────
    void ApplySteering()
    {
        float speedFactor = steerSpeedCurve.Evaluate(speedKPH);
        float steer = steerInput * maxSteerAngle * speedFactor;

        // Lock front wheels when line lock is active (burnout mode)
        if (lineLockActive)
            steer = 0f;

        wheelFL.steerAngle = steer;
        wheelFR.steerAngle = steer;
    }

    // ─── Brakes ──────────────────────────────────────────────────────────────
    void ApplyBrakes()
    {
        if (lineLockActive)
        {
            // Line lock: clamp front wheels, rear wheels free to spin
            wheelFL.brakeTorque = frontBrakeTorque + rearBrakeTorque;
            wheelFR.brakeTorque = frontBrakeTorque + rearBrakeTorque;
            wheelRL.brakeTorque = 0f;
            wheelRR.brakeTorque = 0f;
            return;
        }

        if (handbrakeInput)
        {
            wheelFL.brakeTorque = 0f;
            wheelFR.brakeTorque = 0f;
            wheelRL.brakeTorque = handBrakeTorque;
            wheelRR.brakeTorque = handBrakeTorque;
            return;
        }

        float fb = brakeInput * frontBrakeTorque;
        float rb = brakeInput * rearBrakeTorque;
        wheelFL.brakeTorque = fb;
        wheelFR.brakeTorque = fb;
        wheelRL.brakeTorque = rb;
        wheelRR.brakeTorque = rb;
    }

    // ─── Tyre Heat ───────────────────────────────────────────────────────────
    void UpdateTyreHeat()
    {
        bool rearSpinning = Mathf.Abs(wheelRL.rpm) > 200f || Mathf.Abs(wheelRR.rpm) > 200f;

        if (rearSpinning && lineLockActive)
            tyreTemperature = Mathf.Clamp01(tyreTemperature + tyreHeatRate * Time.fixedDeltaTime);
        else
            tyreTemperature = Mathf.Clamp01(tyreTemperature - tyreCoolRate * Time.fixedDeltaTime);
    }

    // ─── Gear Shifting ───────────────────────────────────────────────────────
    public void ShiftUp()
    {
        if (currentGear < gearRatios.Length - 1)
            currentGear++;
    }

    public void ShiftDown()
    {
        if (currentGear > 0)
            currentGear--;
    }

    void AutoShift()
    {
        if (currentRPM >= shiftUpRPM && currentGear < gearRatios.Length - 1)
            ShiftUp();
        else if (currentRPM <= shiftDownRPM && currentGear > 0)
            ShiftDown();
    }

    // ─── Suspension ──────────────────────────────────────────────────────────
    void ApplySuspensionSettings()
    {
        foreach (WheelCollider wc in new[] { wheelFL, wheelFR, wheelRL, wheelRR })
        {
            if (wc == null) continue;
            JointSpring spring = wc.suspensionSpring;
            spring.spring   = suspensionSpring;
            spring.damper   = suspensionDamper;
            spring.targetPosition = suspensionTargetPosition;
            wc.suspensionSpring    = spring;
            wc.suspensionDistance  = suspensionDistance;
        }
    }

    // ─── Wheel Mesh Sync ─────────────────────────────────────────────────────
    void UpdateWheelMeshes()
    {
        SyncMesh(wheelFL, meshFL);
        SyncMesh(wheelFR, meshFR);
        SyncMesh(wheelRL, meshRL);
        SyncMesh(wheelRR, meshRR);
    }

    void SyncMesh(WheelCollider col, Transform mesh)
    {
        if (col == null || mesh == null) return;
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot;
    }

    // ─── Performance Tuning API ──────────────────────────────────────────────
    /// <summary>Called by the customisation system to apply performance parts.</summary>
    public void ApplyPerformanceSpec(PerformanceSpec spec)
    {
        peakTorque      = spec.peakTorque;
        peakTorqueRPM   = spec.peakTorqueRPM;
        redlineRPM      = spec.redlineRPM;
        finalDriveRatio = spec.finalDriveRatio;
        gearRatios      = spec.gearRatios;

        suspensionSpring = spec.suspensionSpring;
        suspensionDamper = spec.suspensionDamper;
        ApplySuspensionSettings();

        frontBrakeTorque = spec.frontBrakeTorque;
        rearBrakeTorque  = spec.rearBrakeTorque;
    }
}

/// <summary>
/// Data container for a full performance specification.
/// Populated by the customisation system and passed to VehicleController.
/// </summary>
[System.Serializable]
public class PerformanceSpec
{
    public float peakTorque      = 600f;
    public float peakTorqueRPM   = 4000f;
    public float redlineRPM      = 7000f;
    public float finalDriveRatio = 3.7f;
    public float[] gearRatios    = { 3.5f, 2.3f, 1.6f, 1.2f, 0.9f, 0.7f };
    public float suspensionSpring = 35000f;
    public float suspensionDamper = 4500f;
    public float frontBrakeTorque = 3000f;
    public float rearBrakeTorque  = 2000f;
}
