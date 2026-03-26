using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Lifetime statistics screen and in-game leaderboard.
/// Shows career stats, best times, burnout history, drag records,
/// and achievement/milestones with progress bars.
/// </summary>
public class StatsScreen : MonoBehaviour
{
    // ─── Tabs ─────────────────────────────────────────────────────────────────
    [Header("Tabs")]
    public Button tabCareer;
    public Button tabBurnout;
    public Button tabDrag;
    public Button tabCircuit;
    public Button tabAchievements;
    public Button btnClose;

    [Header("Panels")]
    public GameObject panelCareer;
    public GameObject panelBurnout;
    public GameObject panelDrag;
    public GameObject panelCircuit;
    public GameObject panelAchievements;

    // ─── Career Panel ─────────────────────────────────────────────────────────
    [Header("Career Stats")]
    public TextMeshProUGUI statCurrency;
    public TextMeshProUGUI statCarsOwned;
    public TextMeshProUGUI statTotalBurnouts;
    public TextMeshProUGUI statTotalDistance;
    public TextMeshProUGUI statTotalPlayTime;
    public TextMeshProUGUI statFavouriteCar;
    public TextMeshProUGUI statTopSpeed;
    public Image           careerProgressFill;

    // ─── Burnout Panel ────────────────────────────────────────────────────────
    [Header("Burnout Stats")]
    public TextMeshProUGUI burnoutAllTimeTop;
    public TextMeshProUGUI burnoutTotalRuns;
    public TextMeshProUGUI burnoutBestDuration;
    public TextMeshProUGUI burnoutAvgScore;
    public Transform       burnoutHistoryContainer;
    public GameObject      burnoutRowPrefab;

    // ─── Drag Panel ───────────────────────────────────────────────────────────
    [Header("Drag Stats")]
    public TextMeshProUGUI dragBestET;
    public TextMeshProUGUI dragBestRT;
    public TextMeshProUGUI dragBestTrap;
    public TextMeshProUGUI dragWinLoss;
    public Transform       dragHistoryContainer;
    public GameObject      dragRowPrefab;

    // ─── Circuit Panel ────────────────────────────────────────────────────────
    [Header("Circuit Stats")]
    public Transform       circuitTrackContainer;
    public GameObject      trackRecordRowPrefab;

    // ─── Achievements Panel ───────────────────────────────────────────────────
    [Header("Achievements")]
    public Transform       achievementContainer;
    public GameObject      achievementCardPrefab;

    // ─── Private ─────────────────────────────────────────────────────────────
    private SaveData saveData;
    private GameObject[] allPanels;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void OnEnable()
    {
        // Refresh data every time screen is opened
        saveData = GetSaveData();
        BindTabs();
        ShowTab(panelCareer);
        PopulateCareer();
    }

    void BindTabs()
    {
        allPanels = new[] { panelCareer, panelBurnout, panelDrag, panelCircuit, panelAchievements };

        tabCareer      ?.onClick.RemoveAllListeners();
        tabBurnout     ?.onClick.RemoveAllListeners();
        tabDrag        ?.onClick.RemoveAllListeners();
        tabCircuit     ?.onClick.RemoveAllListeners();
        tabAchievements?.onClick.RemoveAllListeners();
        btnClose       ?.onClick.RemoveAllListeners();

        tabCareer      ?.onClick.AddListener(() => { ShowTab(panelCareer);       PopulateCareer(); });
        tabBurnout     ?.onClick.AddListener(() => { ShowTab(panelBurnout);      PopulateBurnout(); });
        tabDrag        ?.onClick.AddListener(() => { ShowTab(panelDrag);         PopulateDrag(); });
        tabCircuit     ?.onClick.AddListener(() => { ShowTab(panelCircuit);      PopulateCircuit(); });
        tabAchievements?.onClick.AddListener(() => { ShowTab(panelAchievements); PopulateAchievements(); });
        btnClose       ?.onClick.AddListener(OnClose);
    }

    void ShowTab(GameObject panel)
    {
        foreach (var p in allPanels) if (p) p.SetActive(false);
        if (panel) panel.SetActive(true);
    }

    // ─── Career ───────────────────────────────────────────────────────────────
    void PopulateCareer()
    {
        if (saveData == null) return;

        int currency  = saveData.currency;
        int carsOwned = saveData.ownedCarIds?.Count ?? 0;
        int burnouts  = saveData.totalBurnouts;
        float topScore = saveData.allTimeTopScore;

        if (statCurrency)      statCurrency.text      = $"${currency:N0}";
        if (statCarsOwned)     statCarsOwned.text      = $"{carsOwned} / {AustralianCarData.BuildDefaultDatabase().Count}";
        if (statTotalBurnouts) statTotalBurnouts.text  = $"{burnouts:N0}";
        if (statFavouriteCar)  statFavouriteCar.text   = FormatCarName(saveData.currentCarId);

        // Career progress — cars owned %
        if (careerProgressFill)
        {
            int total = AustralianCarData.BuildDefaultDatabase().Count;
            careerProgressFill.fillAmount = total > 0 ? (float)carsOwned / total : 0f;
        }
    }

    // ─── Burnout ──────────────────────────────────────────────────────────────
    void PopulateBurnout()
    {
        if (saveData == null) return;

        var history = saveData.burnoutHistory ?? new List<BurnoutRunRecord>();

        float best     = 0f;
        float bestDur  = 0f;
        float totalScr = 0f;

        foreach (var r in history)
        {
            if (r.score    > best)    best    = r.score;
            if (r.duration > bestDur) bestDur = r.duration;
            totalScr += r.score;
        }

        float avg = history.Count > 0 ? totalScr / history.Count : 0f;

        if (burnoutAllTimeTop)  burnoutAllTimeTop.text  = $"{saveData.allTimeTopScore:F0}";
        if (burnoutTotalRuns)   burnoutTotalRuns.text   = $"{history.Count}";
        if (burnoutBestDuration)burnoutBestDuration.text = $"{bestDur:F1}s";
        if (burnoutAvgScore)    burnoutAvgScore.text    = $"{avg:F0}";

        PopulateHistoryList(burnoutHistoryContainer, burnoutRowPrefab, history,
            r => $"{r.score:F0}",
            r => $"{r.duration:F1}s",
            r => FormatTimestamp(r.timestamp));
    }

    // ─── Drag ─────────────────────────────────────────────────────────────────
    void PopulateDrag()
    {
        if (saveData == null) return;

        var history = saveData.dragHistory ?? new List<DragRecord>();

        int wins = 0, losses = 0;
        float bestET = float.MaxValue, bestRT = float.MaxValue, bestTrap = 0f;

        foreach (var r in history)
        {
            if (r.won) wins++; else losses++;
            if (r.et       < bestET)   bestET   = r.et;
            if (r.reactionTime < bestRT) bestRT = r.reactionTime;
            if (r.trapKPH  > bestTrap) bestTrap = r.trapKPH;
        }

        if (dragBestET)   dragBestET.text   = bestET   < float.MaxValue ? $"{bestET:F3}s"   : "—";
        if (dragBestRT)   dragBestRT.text   = bestRT   < float.MaxValue ? $"{bestRT:F4}s"   : "—";
        if (dragBestTrap) dragBestTrap.text = bestTrap > 0f             ? $"{bestTrap:F1} km/h" : "—";
        if (dragWinLoss)  dragWinLoss.text  = $"{wins}W  /  {losses}L";

        PopulateHistoryList(dragHistoryContainer, dragRowPrefab, history,
            r => $"{r.et:F3}s",
            r => r.won ? "WIN" : "LOSS",
            r => FormatTimestamp(r.timestamp));
    }

    // ─── Circuit ──────────────────────────────────────────────────────────────
    void PopulateCircuit()
    {
        if (saveData?.lapRecords == null || trackRecordRowPrefab == null || circuitTrackContainer == null) return;

        foreach (Transform child in circuitTrackContainer) Destroy(child.gameObject);

        string[] trackNames = {
            "Summernats Burnout Pad",
            "Calder Park Thunderdome",
            "Winton Motor Raceway",
            "Phillip Island GP",
            "Wakefield Park",
        };

        string[] trackIds = {
            "burnout_pad_summernats",
            "drag_calder",
            "circuit_winton",
            "circuit_pi",
            "circuit_wakefield",
        };

        for (int i = 0; i < trackIds.Length; i++)
        {
            var row  = Instantiate(trackRecordRowPrefab, circuitTrackContainer);
            var txts = row.GetComponentsInChildren<TextMeshProUGUI>();

            float best = SaveSystem.Instance?.GetBestLap(trackIds[i]) ?? float.MaxValue;

            if (txts.Length > 0) txts[0].text = trackNames[i];
            if (txts.Length > 1) txts[1].text = best < float.MaxValue
                ? CircuitRace.FormatTime(best)
                : "—";
        }
    }

    // ─── Achievements ─────────────────────────────────────────────────────────
    void PopulateAchievements()
    {
        if (achievementContainer == null || achievementCardPrefab == null) return;
        foreach (Transform child in achievementContainer) Destroy(child.gameObject);

        var achievements = BuildAchievements();
        foreach (var ach in achievements)
        {
            var card  = Instantiate(achievementCardPrefab, achievementContainer);
            var txts  = card.GetComponentsInChildren<TextMeshProUGUI>();
            var fill  = card.transform.Find("ProgressFill")?.GetComponent<Image>();
            var icon  = card.transform.Find("Icon")?.GetComponent<Image>();

            if (txts.Length > 0) txts[0].text = ach.title;
            if (txts.Length > 1) txts[1].text = ach.description;
            if (txts.Length > 2) txts[2].text = ach.unlocked ? "UNLOCKED" : $"{ach.progressCurrent}/{ach.progressTarget}";

            if (fill) fill.fillAmount = Mathf.Clamp01(ach.progressCurrent / (float)Mathf.Max(1, ach.progressTarget));

            // Greyed out if locked
            var canvasGroup = card.GetComponent<CanvasGroup>() ?? card.AddComponent<CanvasGroup>();
            canvasGroup.alpha = ach.unlocked ? 1f : 0.5f;
        }
    }

    List<Achievement> BuildAchievements()
    {
        int burnouts = saveData?.totalBurnouts ?? 0;
        int cars     = saveData?.ownedCarIds?.Count ?? 0;
        float bestET = saveData?.bestDragET ?? float.MaxValue;
        float topScore = saveData?.allTimeTopScore ?? 0f;

        return new List<Achievement>
        {
            new Achievement("Send It",          "Complete your first burnout",                  burnouts >= 1,   burnouts, 1),
            new Achievement("Smoke Machine",     "Complete 50 burnouts",                         burnouts >= 50,  burnouts, 50),
            new Achievement("Summernats Legend", "Complete 500 burnouts",                        burnouts >= 500, burnouts, 500),
            new Achievement("Floor It",          "Score 5,000 in a single burnout run",          topScore >= 5000,  (int)topScore, 5000),
            new Achievement("Absolute Unit",     "Score 20,000 in a single burnout run",         topScore >= 20000, (int)topScore, 20000),
            new Achievement("Collector",         "Own 5 cars",                                   cars >= 5,  cars, 5),
            new Achievement("Garage Full",       "Own all 22 cars",                              cars >= 22, cars, 22),
            new Achievement("Sub 12",            "Run a 1/4 mile in under 12 seconds",           bestET < 12f,  bestET < float.MaxValue ? (int)((12f-bestET)*10) : 0, 10),
            new Achievement("Sub 10",            "Run a 1/4 mile in under 10 seconds",           bestET < 10f,  bestET < float.MaxValue ? (int)((10f-bestET)*10) : 0, 10),
            new Achievement("XY GT",             "Unlock the XY Falcon GT-HO Phase III",         saveData?.ownedCarIds?.Contains("ford_falcon_xy_gt") ?? false, 0, 1),
            new Achievement("HSV GTSR",          "Unlock the HSV GTSR W1",                       saveData?.ownedCarIds?.Contains("holden_hsv_gtsr") ?? false,    0, 1),
            new Achievement("Photographer",      "Take 10 photos in Photo Mode",                 false, 0, 10),
            new Achievement("Bald Eagle",        "Blow a tyre during a burnout competition run", false, 0, 1),
            new Achievement("Clean Sheet",       "Win a drag race with a perfect reaction time (< 0.1s)", false, 0, 1),
        };
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    void PopulateHistoryList<T>(Transform container, GameObject prefab, List<T> history,
        System.Func<T, string> col1, System.Func<T, string> col2, System.Func<T, string> col3)
    {
        if (container == null || prefab == null) return;
        foreach (Transform child in container) Destroy(child.gameObject);

        // Show last 20, most recent first
        int start = Mathf.Max(0, history.Count - 20);
        for (int i = history.Count - 1; i >= start; i--)
        {
            var row  = Instantiate(prefab, container);
            var txts = row.GetComponentsInChildren<TextMeshProUGUI>();
            if (txts.Length > 0) txts[0].text = col1(history[i]);
            if (txts.Length > 1) txts[1].text = col2(history[i]);
            if (txts.Length > 2) txts[2].text = col3(history[i]);
        }
    }

    void OnClose()
    {
        gameObject.SetActive(false);
    }

    SaveData GetSaveData()
    {
        // Access save data directly — SaveSystem exposes it internally
        // In practice, expose a public getter on SaveSystem
        if (SaveSystem.Instance == null) return new SaveData();
        // Reload to get latest
        SaveSystem.Instance.Load();
        return new SaveData
        {
            currency        = GameManager.Instance?.currency        ?? 0,
            allTimeTopScore = GameManager.Instance?.allTimeTopScore ?? 0f,
            totalBurnouts   = GameManager.Instance?.totalBurnouts   ?? 0,
            currentCarId    = GameManager.Instance?.currentCarId    ?? "",
            bestDragET      = SaveSystem.Instance.GetBestLap("drag_best_et_key"),
            ownedCarIds     = new List<string>(),  // Populated from SaveSystem in full impl
            burnoutHistory  = new List<BurnoutRunRecord>(),
            dragHistory     = new List<DragRecord>(),
            lapRecords      = new List<TrackRecord>(),
        };
    }

    string FormatCarName(string id)
    {
        var car = AustralianCarData.BuildDefaultDatabase().Find(c => c.id == id);
        return car?.displayName ?? id;
    }

    string FormatTimestamp(string iso)
    {
        if (System.DateTime.TryParse(iso, out var dt))
            return dt.ToString("dd/MM/yy HH:mm");
        return "—";
    }
}

// ─── Achievement ─────────────────────────────────────────────────────────────

[System.Serializable]
public class Achievement
{
    public string title;
    public string description;
    public bool   unlocked;
    public int    progressCurrent;
    public int    progressTarget;

    public Achievement(string title, string description, bool unlocked, int current, int target)
    {
        this.title           = title;
        this.description     = description;
        this.unlocked        = unlocked;
        this.progressCurrent = current;
        this.progressTarget  = target;
    }
}
