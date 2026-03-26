using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.IO;

/// <summary>
/// Photo mode: freeze time, free-fly camera, depth of field,
/// film filters, decal/sticker overlay, and screenshot export.
/// Press F8 (or configured key) to toggle.
/// </summary>
public class PhotoMode : MonoBehaviour
{
    // ─── Activation ──────────────────────────────────────────────────────────
    [Header("Activation")]
    public KeyCode toggleKey    = KeyCode.F8;
    public bool    isActive     = false;

    // ─── Camera ───────────────────────────────────────────────────────────────
    [Header("Camera")]
    public Camera       gameCamera;
    public float        flySpeed         = 20f;
    public float        flySpeedFast     = 60f;   // Hold Shift for fast
    public float        lookSensitivity  = 2f;
    public float        rollSpeed        = 45f;
    public float        fovMin           = 10f;
    public float        fovMax           = 120f;

    // ─── Depth of Field ───────────────────────────────────────────────────────
    [Header("Depth of Field")]
    public UnityEngine.Rendering.Volume postProcessVolume;
    public Slider  dofFocusDistanceSlider;
    public Slider  dofApertureSlider;     // Low = more blur (shallow DOF)
    public Toggle  dofEnabledToggle;

    // ─── Film Filters ─────────────────────────────────────────────────────────
    [Header("Film Filters")]
    public string[] filterNames = { "None", "Cinematic", "Vintage", "B&W", "Summernats", "Night Neon", "Overexposed" };
    public int      activeFilter = 0;
    public TMP_Dropdown filterDropdown;

    // Filter post-process volumes (one per filter, weight switched)
    public UnityEngine.Rendering.Volume[] filterVolumes;

    // ─── HUD ──────────────────────────────────────────────────────────────────
    [Header("Photo Mode HUD")]
    public GameObject photoModeUI;
    public TextMeshProUGUI fovText;
    public TextMeshProUGUI exposureText;
    public TextMeshProUGUI filterNameText;
    public TextMeshProUGUI coordinatesText;
    public Slider          exposureSlider;
    public Button          btnCapture;
    public Button          btnClose;
    public Image           flashOverlay;        // White flash on capture

    // ─── Overlays ─────────────────────────────────────────────────────────────
    [Header("Overlays")]
    public Toggle  toggleRuleOfThirds;
    public Image   ruleOfThirdsGrid;
    public Toggle  toggleTimestamp;
    public TextMeshProUGUI timestampText;
    public Toggle  toggleWatermark;
    public TextMeshProUGUI watermarkText;       // "SEND IT"

    // ─── Screenshot ──────────────────────────────────────────────────────────
    [Header("Screenshot")]
    public int    screenshotWidth  = 1920;
    public int    screenshotHeight = 1080;
    public string screenshotFolder = "SendItPhotos";
    public TextMeshProUGUI lastSavedText;

    // ─── Private ─────────────────────────────────────────────────────────────
    private Vector3    savedCamPosition;
    private Quaternion savedCamRotation;
    private float      savedTimeScale;
    private float      savedFOV;
    private float      camYaw;
    private float      camPitch;
    private float      camRoll;
    private bool       cursorWasLocked;
    private VehicleController playerVehicle;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Start()
    {
        playerVehicle = FindFirstObjectByType<VehicleController>();

        if (btnCapture) btnCapture.onClick.AddListener(TakeScreenshot);
        if (btnClose)   btnClose.onClick.AddListener(ExitPhotoMode);

        if (filterDropdown)
        {
            filterDropdown.ClearOptions();
            filterDropdown.AddOptions(new System.Collections.Generic.List<string>(filterNames));
            filterDropdown.onValueChanged.AddListener(SetFilter);
        }

        if (exposureSlider) exposureSlider.onValueChanged.AddListener(SetExposure);
        if (dofEnabledToggle) dofEnabledToggle.onValueChanged.AddListener(SetDOFEnabled);
        if (dofFocusDistanceSlider) dofFocusDistanceSlider.onValueChanged.AddListener(SetDOFFocus);
        if (dofApertureSlider) dofApertureSlider.onValueChanged.AddListener(SetDOFAperture);

        if (toggleRuleOfThirds) toggleRuleOfThirds.onValueChanged.AddListener(v =>
        { if (ruleOfThirdsGrid) ruleOfThirdsGrid.enabled = v; });

        if (toggleTimestamp) toggleTimestamp.onValueChanged.AddListener(v =>
        { if (timestampText) timestampText.enabled = v; });

        if (toggleWatermark) toggleWatermark.onValueChanged.AddListener(v =>
        { if (watermarkText) watermarkText.enabled = v; });

        if (photoModeUI) photoModeUI.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            TogglePhotoMode();

        if (!isActive) return;

        HandleFlyCamera();
        HandleFOVScroll();
        UpdateHUDText();
    }

    // ─── Toggle ───────────────────────────────────────────────────────────────
    public void TogglePhotoMode()
    {
        if (isActive) ExitPhotoMode();
        else          EnterPhotoMode();
    }

    void EnterPhotoMode()
    {
        isActive         = true;
        savedTimeScale   = Time.timeScale;
        savedCamPosition = gameCamera.transform.position;
        savedCamRotation = gameCamera.transform.rotation;
        savedFOV         = gameCamera.fieldOfView;
        cursorWasLocked  = Cursor.lockState == CursorLockMode.Locked;

        Time.timeScale = 0f;

        // Detach camera from vehicle rig
        gameCamera.transform.SetParent(null);

        // Init euler from current rotation
        Vector3 e = gameCamera.transform.eulerAngles;
        camYaw   = e.y;
        camPitch = e.x;
        camRoll  = e.z;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        if (photoModeUI) photoModeUI.SetActive(true);

        // Disable player input
        if (playerVehicle) playerVehicle.enabled = false;

        Debug.Log("[PhotoMode] Entered.");
    }

    public void ExitPhotoMode()
    {
        isActive = false;
        Time.timeScale = savedTimeScale;

        // Restore camera
        gameCamera.transform.position    = savedCamPosition;
        gameCamera.transform.rotation    = savedCamRotation;
        gameCamera.fieldOfView           = savedFOV;

        // Reattach to follow rig — handled by CameraController
        var camCtrl = FindFirstObjectByType<CameraController>();
        if (camCtrl != null) gameCamera.transform.SetParent(camCtrl.transform);

        Cursor.lockState = cursorWasLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !cursorWasLocked;

        if (photoModeUI) photoModeUI.SetActive(false);
        if (playerVehicle) playerVehicle.enabled = true;

        // Reset all filter volumes
        SetFilter(0);

        Debug.Log("[PhotoMode] Exited.");
    }

    // ─── Fly Camera ───────────────────────────────────────────────────────────
    void HandleFlyCamera()
    {
        // Mouse look (unscaled — Time.timeScale = 0)
        float mouseX = Input.GetAxisRaw("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * lookSensitivity;

        camYaw   += mouseX;
        camPitch -= mouseY;
        camPitch  = Mathf.Clamp(camPitch, -89f, 89f);

        // Q/E for roll
        if (Input.GetKey(KeyCode.Q)) camRoll -= rollSpeed * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.E)) camRoll += rollSpeed * Time.unscaledDeltaTime;
        if (Input.GetKeyDown(KeyCode.R)) camRoll = 0f;  // Reset roll

        gameCamera.transform.rotation = Quaternion.Euler(camPitch, camYaw, camRoll);

        // WASD movement
        float speed  = Input.GetKey(KeyCode.LeftShift) ? flySpeedFast : flySpeed;
        float fwd    = Input.GetAxisRaw("Vertical")   * speed * Time.unscaledDeltaTime;
        float right  = Input.GetAxisRaw("Horizontal") * speed * Time.unscaledDeltaTime;
        float up     = 0f;
        if (Input.GetKey(KeyCode.Space))        up =  speed * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.LeftControl))  up = -speed * Time.unscaledDeltaTime;

        gameCamera.transform.Translate(new Vector3(right, up, fwd), Space.Self);
    }

    void HandleFOVScroll()
    {
        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            gameCamera.fieldOfView = Mathf.Clamp(
                gameCamera.fieldOfView - scroll * 10f, fovMin, fovMax);
        }
    }

    // ─── Filters ─────────────────────────────────────────────────────────────
    public void SetFilter(int index)
    {
        activeFilter = index;
        if (filterVolumes == null) return;

        for (int i = 0; i < filterVolumes.Length; i++)
            if (filterVolumes[i] != null)
                filterVolumes[i].weight = i == index - 1 ? 1f : 0f;  // index 0 = None

        if (filterNameText) filterNameText.text = index < filterNames.Length ? filterNames[index] : "";
    }

    // ─── DOF ─────────────────────────────────────────────────────────────────
    void SetDOFEnabled(bool enabled)
    {
        // Hook into URP DOF override — architecture-specific
        // postProcessVolume.profile.TryGet<DepthOfField>(out var dof);
        // if (dof != null) dof.active = enabled;
        Debug.Log($"[PhotoMode] DOF {(enabled ? "ON" : "OFF")}");
    }

    void SetDOFFocus(float distance)    => Debug.Log($"[PhotoMode] DOF focus: {distance:F1}m");
    void SetDOFAperture(float aperture) => Debug.Log($"[PhotoMode] DOF aperture: {aperture:F2}");

    // ─── Exposure ─────────────────────────────────────────────────────────────
    void SetExposure(float value)
    {
        // Hook into URP Color Adjustments exposure
        if (exposureText) exposureText.text = $"EV  {value:+0.0;-0.0}";
    }

    // ─── HUD Text ─────────────────────────────────────────────────────────────
    void UpdateHUDText()
    {
        if (fovText) fovText.text = $"FOV  {gameCamera.fieldOfView:F0}°";

        if (coordinatesText)
        {
            Vector3 p = gameCamera.transform.position;
            coordinatesText.text = $"{p.x:F0}  {p.y:F0}  {p.z:F0}";
        }

        if (timestampText && timestampText.enabled)
            timestampText.text = System.DateTime.Now.ToString("dd/MM/yyyy  HH:mm");
    }

    // ─── Screenshot ──────────────────────────────────────────────────────────
    public void TakeScreenshot()
    {
        StartCoroutine(CaptureRoutine());
    }

    IEnumerator CaptureRoutine()
    {
        // Flash effect
        if (flashOverlay)
        {
            flashOverlay.color = Color.white;
            flashOverlay.enabled = true;
        }

        // Hide UI for clean shot
        if (photoModeUI) photoModeUI.SetActive(false);
        yield return new WaitForEndOfFrame();

        string folder = Path.Combine(Application.persistentDataPath, screenshotFolder);
        Directory.CreateDirectory(folder);

        string filename = $"SendIt_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        string fullPath = Path.Combine(folder, filename);

        // Capture via RenderTexture for custom resolution
        var rt  = new RenderTexture(screenshotWidth, screenshotHeight, 24);
        var prev = gameCamera.targetTexture;
        gameCamera.targetTexture = rt;
        gameCamera.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(screenshotWidth, screenshotHeight, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, screenshotWidth, screenshotHeight), 0, 0);
        tex.Apply();

        gameCamera.targetTexture = prev;
        RenderTexture.active     = null;
        Destroy(rt);

        byte[] bytes = tex.EncodeToPNG();
        Destroy(tex);
        File.WriteAllBytes(fullPath, bytes);

        Debug.Log($"[PhotoMode] Screenshot saved: {fullPath}");

        // Restore UI
        if (photoModeUI) photoModeUI.SetActive(true);
        if (lastSavedText) lastSavedText.text = $"Saved  {filename}";

        // Fade out flash
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            if (flashOverlay) flashOverlay.color = new Color(1f, 1f, 1f, 1f - (t / 0.4f));
            yield return null;
        }
        if (flashOverlay) flashOverlay.enabled = false;

        SoundManager.Instance?.PlaySFX(SFXEvent.Unlock);
    }
}
