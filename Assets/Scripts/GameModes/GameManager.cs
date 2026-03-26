using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Central game manager. Controls game state, mode transitions, currency,
/// and player progression. Singleton — one instance persists across scenes.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ─── Singleton ───────────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ─── Game State ──────────────────────────────────────────────────────────
    public enum GameState
    {
        MainMenu,
        Garage,
        FreeRoam,
        BurnoutComp,
        DragRace,
        CircuitRace,
        Paused,
        Results,
    }

    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    // ─── Player Data ─────────────────────────────────────────────────────────
    [Header("Player Progress")]
    public int   currency         = 5000;
    public float allTimeTopScore  = 0f;
    public int   totalBurnouts    = 0;
    public float totalSmokeMetres = 0f;    // Cumulative tyre metres burned

    public string currentCarId = "holden_commodore_vk";

    // ─── Scene Names ─────────────────────────────────────────────────────────
    [Header("Scene Names")]
    public string mainMenuScene    = "MainMenu";
    public string garageScene      = "Garage";
    public string freeRoamScene    = "FreeRoam_Canberra";
    public string burnoutPadScene  = "BurnoutPad_Summernats";
    public string dragStripScene   = "DragStrip_Calder";
    public string circuitScene     = "Circuit_Winton";

    // ─── Events ──────────────────────────────────────────────────────────────
    public System.Action<GameState>  OnStateChanged;
    public System.Action<int>        OnCurrencyChanged;
    public System.Action<float>      OnNewHighScore;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadProgress();
    }

    // ─── State Transitions ───────────────────────────────────────────────────
    public void GoToMainMenu()    => LoadScene(mainMenuScene,   GameState.MainMenu);
    public void GoToGarage()      => LoadScene(garageScene,     GameState.Garage);
    public void GoToFreeRoam()    => LoadScene(freeRoamScene,   GameState.FreeRoam);
    public void GoToBurnoutComp() => LoadScene(burnoutPadScene, GameState.BurnoutComp);
    public void GoToDragRace()    => LoadScene(dragStripScene,  GameState.DragRace);
    public void GoToCircuit()     => LoadScene(circuitScene,    GameState.CircuitRace);

    void LoadScene(string sceneName, GameState newState)
    {
        StartCoroutine(LoadSceneRoutine(sceneName, newState));
    }

    IEnumerator LoadSceneRoutine(string sceneName, GameState newState)
    {
        SetState(GameState.Paused);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
            yield return null;

        SetState(newState);
    }

    void SetState(GameState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);

        Time.timeScale = newState == GameState.Paused ? 0f : 1f;
    }

    // ─── Pause ───────────────────────────────────────────────────────────────
    public void TogglePause()
    {
        if (CurrentState == GameState.Paused)
            SetState(GameState.FreeRoam);   // resume to whatever was active
        else
            SetState(GameState.Paused);
    }

    // ─── Currency ────────────────────────────────────────────────────────────
    public bool SpendCurrency(int amount)
    {
        if (currency < amount) return false;
        currency -= amount;
        OnCurrencyChanged?.Invoke(currency);
        SaveProgress();
        return true;
    }

    public void EarnCurrency(int amount)
    {
        currency += amount;
        OnCurrencyChanged?.Invoke(currency);
        SaveProgress();
    }

    // ─── Score Submission ────────────────────────────────────────────────────
    /// <summary>Called by BurnoutCompetition or DragRace when a run ends.</summary>
    public void SubmitScore(float score, GameMode mode)
    {
        int reward = CalculateCurrencyReward(score, mode);
        EarnCurrency(reward);

        if (mode == GameMode.BurnoutComp)
        {
            totalBurnouts++;
            if (score > allTimeTopScore)
            {
                allTimeTopScore = score;
                OnNewHighScore?.Invoke(score);
            }
        }

        Debug.Log($"[GameManager] {mode} score: {score:F0}  Reward: ${reward}  Balance: ${currency}");
        SaveProgress();
    }

    int CalculateCurrencyReward(float score, GameMode mode)
    {
        switch (mode)
        {
            case GameMode.BurnoutComp: return Mathf.RoundToInt(score * 0.5f);
            case GameMode.DragRace:    return Mathf.RoundToInt(score * 2f);
            case GameMode.Circuit:     return Mathf.RoundToInt(score * 3f);
            default:                   return 0;
        }
    }

    // ─── Save / Load ─────────────────────────────────────────────────────────
    const string KEY_CURRENCY       = "sendit_currency";
    const string KEY_TOP_SCORE      = "sendit_topscore";
    const string KEY_TOTAL_BURNOUTS = "sendit_burnouts";
    const string KEY_CURRENT_CAR    = "sendit_currentcar";

    public void SaveProgress()
    {
        PlayerPrefs.SetInt(KEY_CURRENCY,       currency);
        PlayerPrefs.SetFloat(KEY_TOP_SCORE,    allTimeTopScore);
        PlayerPrefs.SetInt(KEY_TOTAL_BURNOUTS, totalBurnouts);
        PlayerPrefs.SetString(KEY_CURRENT_CAR, currentCarId);
        PlayerPrefs.Save();
    }

    void LoadProgress()
    {
        currency        = PlayerPrefs.GetInt(KEY_CURRENCY,       5000);
        allTimeTopScore = PlayerPrefs.GetFloat(KEY_TOP_SCORE,    0f);
        totalBurnouts   = PlayerPrefs.GetInt(KEY_TOTAL_BURNOUTS, 0);
        currentCarId    = PlayerPrefs.GetString(KEY_CURRENT_CAR, "holden_commodore_vk");
    }

    public void ResetProgress()
    {
        PlayerPrefs.DeleteAll();
        currency        = 5000;
        allTimeTopScore = 0f;
        totalBurnouts   = 0;
        currentCarId    = "holden_commodore_vk";
        SaveProgress();
    }
}

public enum GameMode { FreeRoam, BurnoutComp, DragRace, Circuit }
