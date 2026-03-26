using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Main menu controller.
/// Handles title screen, new game / continue, event select,
/// settings panel, credits, and animated background car.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    // ─── Panels ──────────────────────────────────────────────────────────────
    [Header("Panels")]
    public GameObject panelTitle;
    public GameObject panelEventSelect;
    public GameObject panelSettings;
    public GameObject panelCredits;
    public GameObject panelConfirmNewGame;

    // ─── Title Panel ─────────────────────────────────────────────────────────
    [Header("Title")]
    public TextMeshProUGUI titleText;          // "SEND IT"
    public TextMeshProUGUI subtitleText;       // "Australian Burnout Simulator"
    public Button          btnContinue;
    public Button          btnNewGame;
    public Button          btnEventSelect;
    public Button          btnSettings;
    public Button          btnCredits;
    public Button          btnQuit;
    public TextMeshProUGUI versionText;
    public TextMeshProUGUI saveInfoText;       // "Last save: ..."
    public CanvasGroup     titleFadeGroup;

    // ─── Event Select ─────────────────────────────────────────────────────────
    [Header("Event Select")]
    public Button btnGarage;
    public Button btnFreeRoam;
    public Button btnBurnoutComp;
    public Button btnDragRace;
    public Button btnCircuit;
    public Button btnEventBack;

    public TextMeshProUGUI eventDescriptionText;
    public Image           eventPreviewImage;

    [System.Serializable]
    public class EventInfo
    {
        public string description;
        public Sprite preview;
    }
    public EventInfo infoGarage;
    public EventInfo infoFreeRoam;
    public EventInfo infoBurnout;
    public EventInfo infoDrag;
    public EventInfo infoCircuit;

    // ─── Settings ─────────────────────────────────────────────────────────────
    [Header("Settings")]
    public Slider  sliderMaster;
    public Slider  sliderMusic;
    public Slider  sliderSFX;
    public Slider  sliderAmbient;
    public Toggle  toggleMotionBlur;
    public Toggle  toggleScreenShake;
    public Toggle  toggleMiniMap;
    public Toggle  toggleAutoGearbox;
    public Toggle  toggleImperialUnits;
    public TMP_Dropdown dropdownGraphics;
    public TMP_Dropdown dropdownSteering;
    public Button  btnSettingsBack;
    public Button  btnSettingsSave;

    // ─── Credits ──────────────────────────────────────────────────────────────
    [Header("Credits")]
    public Button btnCreditsBack;
    public TextMeshProUGUI creditsText;

    // ─── Background ───────────────────────────────────────────────────────────
    [Header("Background")]
    public Animator backgroundCarAnimator;    // Looping idle/burnout animation
    public ParticleSystem backgroundSmoke;
    public AudioSource    backgroundEngineAudio;

    // ─── Private ──────────────────────────────────────────────────────────────
    private GameObject[] allPanels;
    private GameSettings currentSettings;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Start()
    {
        allPanels = new[] { panelTitle, panelEventSelect, panelSettings, panelCredits, panelConfirmNewGame };

        BindButtons();
        LoadSettings();
        RefreshSaveInfo();
        SetVersionText();

        ShowPanel(panelTitle);
        StartCoroutine(FadeInTitle());
        PlayBackgroundEngine();
    }

    // ─── Button Wiring ────────────────────────────────────────────────────────
    void BindButtons()
    {
        btnContinue   ?.onClick.AddListener(OnContinue);
        btnNewGame    ?.onClick.AddListener(OnNewGame);
        btnEventSelect?.onClick.AddListener(() => ShowPanel(panelEventSelect));
        btnSettings   ?.onClick.AddListener(() => { ShowPanel(panelSettings); PopulateSettings(); });
        btnCredits    ?.onClick.AddListener(() => ShowPanel(panelCredits));
        btnQuit       ?.onClick.AddListener(OnQuit);

        // Event select
        btnGarage    ?.onClick.AddListener(() => { HoverEvent(infoGarage);   GameManager.Instance?.GoToGarage(); });
        btnFreeRoam  ?.onClick.AddListener(() => { HoverEvent(infoFreeRoam); GameManager.Instance?.GoToFreeRoam(); });
        btnBurnoutComp?.onClick.AddListener(() => { HoverEvent(infoBurnout); GameManager.Instance?.GoToBurnoutComp(); });
        btnDragRace  ?.onClick.AddListener(() => { HoverEvent(infoDrag);     GameManager.Instance?.GoToDragRace(); });
        btnCircuit   ?.onClick.AddListener(() => { HoverEvent(infoCircuit);  GameManager.Instance?.GoToCircuit(); });
        btnEventBack ?.onClick.AddListener(() => ShowPanel(panelTitle));

        // Event hover previews
        BindHover(btnGarage,     infoGarage);
        BindHover(btnFreeRoam,   infoFreeRoam);
        BindHover(btnBurnoutComp,infoBurnout);
        BindHover(btnDragRace,   infoDrag);
        BindHover(btnCircuit,    infoCircuit);

        // Settings
        btnSettingsBack?.onClick.AddListener(() => ShowPanel(panelTitle));
        btnSettingsSave?.onClick.AddListener(SaveSettings);

        // Credits
        btnCreditsBack?.onClick.AddListener(() => ShowPanel(panelTitle));

        // Confirm new game
        var btnConfirmYes = panelConfirmNewGame?.transform.Find("BtnYes")?.GetComponent<Button>();
        var btnConfirmNo  = panelConfirmNewGame?.transform.Find("BtnNo") ?.GetComponent<Button>();
        btnConfirmYes?.onClick.AddListener(ConfirmNewGame);
        btnConfirmNo ?.onClick.AddListener(() => ShowPanel(panelTitle));
    }

    void BindHover(Button btn, EventInfo info)
    {
        if (btn == null || info == null) return;
        var trigger = btn.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        var entry = new UnityEngine.EventSystems.EventTrigger.Entry
        {
            eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
        };
        entry.callback.AddListener(_ => HoverEvent(info));
        trigger.triggers.Add(entry);
    }

    void HoverEvent(EventInfo info)
    {
        if (info == null) return;
        if (eventDescriptionText) eventDescriptionText.text = info.description;
        if (eventPreviewImage && info.preview) eventPreviewImage.sprite = info.preview;
    }

    // ─── Actions ─────────────────────────────────────────────────────────────
    void OnContinue()
    {
        bool hasSave = SaveSystem.Instance != null && SaveSystem.Instance.LoadSettings() != null;

        if (hasSave)
            GameManager.Instance?.GoToGarage();
        else
        {
            // No save — treat as new game
            ConfirmNewGame();
        }
    }

    void OnNewGame()
    {
        bool hasSave = SaveSystem.Instance != null;
        if (hasSave)
            ShowPanel(panelConfirmNewGame);   // Warn before overwriting
        else
            ConfirmNewGame();
    }

    void ConfirmNewGame()
    {
        SaveSystem.Instance?.DeleteSave();
        GameManager.Instance?.GoToGarage();
    }

    void OnQuit()
    {
        SaveSystem.Instance?.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─── Settings ─────────────────────────────────────────────────────────────
    void PopulateSettings()
    {
        if (currentSettings == null) currentSettings = new GameSettings();

        if (sliderMaster)       sliderMaster.value       = currentSettings.masterVolume;
        if (sliderMusic)        sliderMusic.value         = currentSettings.musicVolume;
        if (sliderSFX)          sliderSFX.value           = currentSettings.sfxVolume;
        if (sliderAmbient)      sliderAmbient.value       = currentSettings.ambientVolume;
        if (toggleMotionBlur)   toggleMotionBlur.isOn     = currentSettings.motionBlur;
        if (toggleScreenShake)  toggleScreenShake.isOn    = currentSettings.screenShake;
        if (toggleMiniMap)      toggleMiniMap.isOn        = currentSettings.showMiniMap;
        if (toggleAutoGearbox)  toggleAutoGearbox.isOn    = currentSettings.automaticGearbox;
        if (toggleImperialUnits)toggleImperialUnits.isOn  = currentSettings.imperialUnits;
        if (dropdownGraphics)   dropdownGraphics.value    = currentSettings.graphicsQuality;

        // Bind live preview for volume sliders
        sliderMaster ?.onValueChanged.AddListener(v => SoundManager.Instance?.SetMasterVolume(v));
        sliderMusic  ?.onValueChanged.AddListener(v => SoundManager.Instance?.SetMusicVolume(v));
        sliderSFX    ?.onValueChanged.AddListener(v => SoundManager.Instance?.SetSFXVolume(v));
        sliderAmbient?.onValueChanged.AddListener(v => SoundManager.Instance?.SetAmbientVolume(v));
    }

    void LoadSettings()
    {
        currentSettings = SaveSystem.Instance?.LoadSettings() ?? new GameSettings();
        ApplySettings(currentSettings);
    }

    void SaveSettings()
    {
        if (currentSettings == null) currentSettings = new GameSettings();

        currentSettings.masterVolume    = sliderMaster       ? sliderMaster.value       : 1f;
        currentSettings.musicVolume     = sliderMusic        ? sliderMusic.value        : 0.7f;
        currentSettings.sfxVolume       = sliderSFX          ? sliderSFX.value          : 1f;
        currentSettings.ambientVolume   = sliderAmbient      ? sliderAmbient.value      : 0.5f;
        currentSettings.motionBlur      = toggleMotionBlur   ? toggleMotionBlur.isOn    : true;
        currentSettings.screenShake     = toggleScreenShake  ? toggleScreenShake.isOn   : true;
        currentSettings.showMiniMap     = toggleMiniMap      ? toggleMiniMap.isOn       : true;
        currentSettings.automaticGearbox= toggleAutoGearbox  ? toggleAutoGearbox.isOn   : false;
        currentSettings.imperialUnits   = toggleImperialUnits? toggleImperialUnits.isOn : false;
        currentSettings.graphicsQuality = dropdownGraphics   ? dropdownGraphics.value   : 2;

        ApplySettings(currentSettings);
        SaveSystem.Instance?.SaveSettings(currentSettings);
        ShowPanel(panelTitle);
    }

    void ApplySettings(GameSettings s)
    {
        QualitySettings.SetQualityLevel(s.graphicsQuality, true);
        SoundManager.Instance?.SetMasterVolume(s.masterVolume);
        SoundManager.Instance?.SetMusicVolume(s.musicVolume);
        SoundManager.Instance?.SetSFXVolume(s.sfxVolume);
        SoundManager.Instance?.SetAmbientVolume(s.ambientVolume);
    }

    // ─── Panel Helpers ────────────────────────────────────────────────────────
    void ShowPanel(GameObject panel)
    {
        foreach (var p in allPanels)
            if (p != null) p.SetActive(false);
        if (panel != null) panel.SetActive(true);
    }

    // ─── UI Refresh ───────────────────────────────────────────────────────────
    void RefreshSaveInfo()
    {
        if (saveInfoText == null) return;
        var settings = SaveSystem.Instance?.LoadSettings();
        int currency = GameManager.Instance?.currency ?? 0;
        int burnouts = GameManager.Instance?.totalBurnouts ?? 0;
        saveInfoText.text = $"${currency:N0}  ·  {burnouts} burnouts";
    }

    void SetVersionText()
    {
        if (versionText) versionText.text = $"v{Application.version}";

        if (creditsText) creditsText.text =
            "SEND IT — Australian Burnout Simulator\n\n" +
            "A love letter to Summernats, Calder Park, and every\n" +
            "backyard mechanic who ever dropped a V8 into something ridiculous.\n\n" +
            "Cars: Holden · Ford · Chrysler Australia\n" +
            "Physics: Custom WheelCollider simulation\n" +
            "Audio: Procedural engine synthesis\n\n" +
            "Special thanks to every bloke who ever said\n\"Nah she'll be right, chuck another tyre on it.\"";
    }

    // ─── Title Fade In ────────────────────────────────────────────────────────
    IEnumerator FadeInTitle()
    {
        if (titleFadeGroup == null) yield break;
        titleFadeGroup.alpha = 0f;
        yield return new WaitForSeconds(0.3f);

        float t = 0f;
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            titleFadeGroup.alpha = Mathf.Clamp01(t / 1.5f);
            yield return null;
        }
        titleFadeGroup.alpha = 1f;
    }

    // ─── Background ───────────────────────────────────────────────────────────
    void PlayBackgroundEngine()
    {
        if (backgroundEngineAudio != null && !backgroundEngineAudio.isPlaying)
            backgroundEngineAudio.Play();

        if (backgroundSmoke != null) backgroundSmoke.Play();
    }
}
