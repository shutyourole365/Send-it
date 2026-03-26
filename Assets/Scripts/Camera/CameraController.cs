using UnityEngine;

/// <summary>
/// Dual-mode camera controller: chase cam and first-person.
/// Attach to a dedicated Camera GameObject.
/// Assign the vehicle's root transform and the driver's head bone/transform.
/// </summary>
public class CameraController : MonoBehaviour
{
    public enum CameraMode { Chase, FirstPerson }

    // ─── References ──────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Root transform of the vehicle")]
    public Transform vehicleTransform;

    [Tooltip("Driver's head/eye position for first-person view")]
    public Transform driverHeadTransform;

    private Camera cam;

    // ─── Chase Cam Settings ──────────────────────────────────────────────────
    [Header("Chase Camera")]
    public Vector3 chaseOffset = new Vector3(0f, 2.5f, -7f);
    public float chaseFOV = 70f;

    [Tooltip("How quickly the camera follows the vehicle")]
    public float chaseFollowSpeed = 8f;

    [Tooltip("How quickly the camera rotates to match vehicle direction")]
    public float chaseRotateSpeed = 6f;

    [Tooltip("Extra FOV added at high speed for speed feel")]
    public float maxSpeedFOVBoost = 15f;

    [Tooltip("Speed (km/h) at which max FOV boost is reached")]
    public float fovBoostMaxSpeed = 200f;

    [Tooltip("Camera shake intensity during burnout")]
    public float burnoutShakeIntensity = 0.08f;

    // ─── First Person Settings ───────────────────────────────────────────────
    [Header("First Person Camera")]
    public float firstPersonFOV = 80f;

    [Tooltip("Head bob intensity at high engine RPM")]
    public float headBobIntensity = 0.015f;

    [Tooltip("Tilt multiplier when the car rotates (body roll feel)")]
    public float bodyRollMultiplier = 0.5f;

    // ─── State ───────────────────────────────────────────────────────────────
    [Header("Runtime")]
    public CameraMode currentMode = CameraMode.Chase;

    private Vector3 currentVelocity;
    private float currentShake;
    private VehicleController vehicle;
    private BurnoutSystem burnout;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Awake()
    {
        cam = GetComponent<Camera>();

        if (vehicleTransform != null)
        {
            vehicle = vehicleTransform.GetComponent<VehicleController>();
            burnout = vehicleTransform.GetComponent<BurnoutSystem>();
        }
    }

    void Update()
    {
        HandleModeSwitch();
    }

    void LateUpdate()
    {
        switch (currentMode)
        {
            case CameraMode.Chase:       UpdateChaseCamera();      break;
            case CameraMode.FirstPerson: UpdateFirstPersonCamera(); break;
        }
    }

    // ─── Mode Switching ──────────────────────────────────────────────────────
    void HandleModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.C))
            ToggleMode();
    }

    public void ToggleMode()
    {
        currentMode = currentMode == CameraMode.Chase
            ? CameraMode.FirstPerson
            : CameraMode.Chase;
    }

    // ─── Chase Camera ────────────────────────────────────────────────────────
    void UpdateChaseCamera()
    {
        if (vehicleTransform == null) return;

        // Target position: offset relative to vehicle rotation
        Vector3 targetPos = vehicleTransform.TransformPoint(chaseOffset);

        // Smooth follow
        transform.position = Vector3.SmoothDamp(
            transform.position, targetPos, ref currentVelocity, 1f / chaseFollowSpeed);

        // Smooth look at vehicle centre
        Vector3 lookTarget = vehicleTransform.position + Vector3.up * 0.8f;
        Quaternion targetRot = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
            Time.deltaTime * chaseRotateSpeed);

        // Speed-based FOV
        float speedRatio = vehicle != null ? vehicle.speedKPH / fovBoostMaxSpeed : 0f;
        float targetFOV  = chaseFOV + Mathf.Lerp(0f, maxSpeedFOVBoost, speedRatio);

        // Burnout shake
        float burnoutIntensity = burnout != null ? burnout.CurrentIntensity : 0f;
        currentShake = Mathf.Lerp(currentShake, burnoutIntensity * burnoutShakeIntensity, Time.deltaTime * 10f);
        transform.position += Random.insideUnitSphere * currentShake;

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 5f);
    }

    // ─── First Person Camera ─────────────────────────────────────────────────
    void UpdateFirstPersonCamera()
    {
        if (driverHeadTransform == null) return;

        // Snap to head position
        transform.position = driverHeadTransform.position;

        // Base rotation matches vehicle
        Quaternion baseRot = vehicleTransform != null
            ? vehicleTransform.rotation
            : Quaternion.identity;

        // Body roll feel: tilt camera by vehicle's local angular velocity on Z
        float roll = 0f;
        if (vehicle != null)
        {
            Vector3 localAngVel = vehicleTransform.InverseTransformDirection(
                vehicle.GetComponent<Rigidbody>().angularVelocity);
            roll = -localAngVel.y * bodyRollMultiplier;
        }

        transform.rotation = baseRot * Quaternion.Euler(0f, 0f, roll);

        // Head bob at high RPM during burnout
        float burnoutIntensity = burnout != null ? burnout.CurrentIntensity : 0f;
        float bob = Mathf.Sin(Time.time * 30f) * headBobIntensity * burnoutIntensity;
        transform.position += transform.up * bob;

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, firstPersonFOV, Time.deltaTime * 8f);
    }

    // ─── Public API ──────────────────────────────────────────────────────────
    public void SetMode(CameraMode mode) => currentMode = mode;
}
