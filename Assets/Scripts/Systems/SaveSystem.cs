using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Full JSON save/load system.
/// Persists player progress, owned cars, all car loadouts, settings,
/// and statistics to a JSON file on disk.
/// Attach to a persistent GameObject alongside GameManager.
/// </summary>
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    [Header("Settings")]
    public string saveFileName = "sendit_save.json";
    public bool   autoSaveEnabled = true;
    public float  autoSaveInterval = 60f;   // Seconds

    private string savePath;
    private SaveData data;
    private float autoSaveTimer;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        savePath = Path.Combine(Application.persistentDataPath, saveFileName);
        Load();
    }

    void Update()
    {
        if (!autoSaveEnabled) return;
        autoSaveTimer += Time.deltaTime;
        if (autoSaveTimer >= autoSaveInterval)
        {
            autoSaveTimer = 0f;
            Save();
        }
    }

    void OnApplicationQuit() => Save();
    void OnApplicationPause(bool paused) { if (paused) Save(); }

    // ─── Save ─────────────────────────────────────────────────────────────────
    public void Save()
    {
        if (data == null) data = new SaveData();

        // Sync from GameManager
        if (GameManager.Instance != null)
        {
            data.currency        = GameManager.Instance.currency;
            data.allTimeTopScore = GameManager.Instance.allTimeTopScore;
            data.totalBurnouts   = GameManager.Instance.totalBurnouts;
            data.currentCarId    = GameManager.Instance.currentCarId;
        }

        data.lastSaved = DateTime.UtcNow.ToString("o");

        try
        {
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(savePath, json);
            Debug.Log($"[SaveSystem] Saved to {savePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Save failed: {e.Message}");
        }
    }

    // ─── Load ─────────────────────────────────────────────────────────────────
    public void Load()
    {
        if (!File.Exists(savePath))
        {
            data = NewGame();
            Debug.Log("[SaveSystem] No save found — starting new game.");
            return;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            data = JsonUtility.FromJson<SaveData>(json);

            if (data == null)
            {
                data = NewGame();
                return;
            }

            // Sync to GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.currency        = data.currency;
                GameManager.Instance.allTimeTopScore = data.allTimeTopScore;
                GameManager.Instance.totalBurnouts   = data.totalBurnouts;
                GameManager.Instance.currentCarId    = data.currentCarId;
            }

            Debug.Log($"[SaveSystem] Loaded save — Currency: ${data.currency}  Car: {data.currentCarId}  Last: {data.lastSaved}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Load failed: {e.Message} — Starting fresh.");
            data = NewGame();
        }
    }

    // ─── New Game ─────────────────────────────────────────────────────────────
    SaveData NewGame()
    {
        var d = new SaveData
        {
            currency        = 5000,
            allTimeTopScore = 0f,
            totalBurnouts   = 0,
            currentCarId    = "holden_commodore_vk",
            lastSaved       = DateTime.UtcNow.ToString("o"),
        };

        // Default unlocked cars
        d.ownedCarIds.Add("holden_commodore_vk");
        d.ownedCarIds.Add("ford_falcon_ea");
        d.ownedCarIds.Add("chrysler_valiant_pacer");

        return d;
    }

    // ─── Car Ownership ───────────────────────────────────────────────────────
    public bool IsCarOwned(string carId)
    {
        if (data == null) return false;
        return data.ownedCarIds.Contains(carId);
    }

    public void UnlockCar(string carId)
    {
        if (data == null) data = NewGame();
        if (!data.ownedCarIds.Contains(carId))
            data.ownedCarIds.Add(carId);
        Save();
        SoundManager.Instance?.PlaySFX(SFXEvent.Unlock);
    }

    // ─── Car Loadouts ─────────────────────────────────────────────────────────
    public void SaveLoadout(string carId, CarLoadout loadout)
    {
        if (data == null) data = NewGame();

        var existing = data.carLoadouts.Find(l => l.carId == carId);
        if (existing != null)
        {
            existing.loadout = loadout;
        }
        else
        {
            data.carLoadouts.Add(new SavedCarLoadout { carId = carId, loadout = loadout });
        }
        Save();
    }

    public CarLoadout LoadLoadout(string carId)
    {
        if (data == null) return null;
        var saved = data.carLoadouts.Find(l => l.carId == carId);
        return saved?.loadout;
    }

    // ─── Statistics ───────────────────────────────────────────────────────────
    public void RecordBurnoutRun(BurnoutRun run)
    {
        if (data == null) data = NewGame();
        data.burnoutHistory.Add(new BurnoutRunRecord
        {
            score     = run.score,
            duration  = run.duration,
            maxHeat   = run.maxHeat,
            timestamp = DateTime.UtcNow.ToString("o"),
        });

        // Keep last 50
        while (data.burnoutHistory.Count > 50)
            data.burnoutHistory.RemoveAt(0);
    }

    public void RecordLapTime(string trackId, float lapTime)
    {
        if (data == null) data = NewGame();

        var record = data.lapRecords.Find(r => r.trackId == trackId);
        if (record == null)
        {
            data.lapRecords.Add(new TrackRecord { trackId = trackId, bestLap = lapTime });
        }
        else if (lapTime < record.bestLap)
        {
            record.bestLap = lapTime;
        }
        Save();
    }

    public float GetBestLap(string trackId)
    {
        var record = data?.lapRecords.Find(r => r.trackId == trackId);
        return record?.bestLap ?? float.MaxValue;
    }

    public void RecordDragResult(DragResult result)
    {
        if (data == null) data = NewGame();
        data.dragHistory.Add(new DragRecord
        {
            et         = result.playerET,
            reactionTime = result.playerRT,
            trapKPH    = result.playerTrapKPH,
            won        = result.winner == "Player",
            timestamp  = DateTime.UtcNow.ToString("o"),
        });

        while (data.dragHistory.Count > 50)
            data.dragHistory.RemoveAt(0);

        // Update best ET
        if (result.playerET < data.bestDragET && !result.isFoul)
        {
            data.bestDragET = result.playerET;
            data.bestTrapSpeed = result.playerTrapKPH;
        }

        Save();
    }

    // ─── Settings ─────────────────────────────────────────────────────────────
    public void SaveSettings(GameSettings settings)
    {
        if (data == null) data = NewGame();
        data.settings = settings;
        Save();
    }

    public GameSettings LoadSettings()
    {
        return data?.settings ?? new GameSettings();
    }

    // ─── Delete Save ──────────────────────────────────────────────────────────
    public void DeleteSave()
    {
        if (File.Exists(savePath))
            File.Delete(savePath);
        data = NewGame();
        if (GameManager.Instance != null) GameManager.Instance.ResetProgress();
        Debug.Log("[SaveSystem] Save deleted.");
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
    public void PrintSaveInfo()
    {
        if (data == null) { Debug.Log("[SaveSystem] No data."); return; }
        Debug.Log($"[SaveSystem] Currency: ${data.currency}  Cars owned: {data.ownedCarIds.Count}  Burnouts: {data.totalBurnouts}  Best ET: {data.bestDragET:F3}s");
    }
}

// ─── Save Data Structure ─────────────────────────────────────────────────────

[Serializable]
public class SaveData
{
    // Player progress
    public int    currency        = 5000;
    public float  allTimeTopScore = 0f;
    public int    totalBurnouts   = 0;
    public string currentCarId   = "holden_commodore_vk";
    public float  bestDragET     = float.MaxValue;
    public float  bestTrapSpeed  = 0f;
    public string lastSaved      = "";

    // Owned cars
    public List<string> ownedCarIds = new List<string>();

    // Per-car loadouts
    public List<SavedCarLoadout> carLoadouts = new List<SavedCarLoadout>();

    // History
    public List<BurnoutRunRecord> burnoutHistory = new List<BurnoutRunRecord>();
    public List<DragRecord>       dragHistory    = new List<DragRecord>();
    public List<TrackRecord>      lapRecords     = new List<TrackRecord>();

    // Settings
    public GameSettings settings = new GameSettings();
}

[Serializable]
public class SavedCarLoadout
{
    public string    carId;
    public CarLoadout loadout;
}

[Serializable]
public class BurnoutRunRecord
{
    public float  score;
    public float  duration;
    public float  maxHeat;
    public string timestamp;
}

[Serializable]
public class DragRecord
{
    public float  et;
    public float  reactionTime;
    public float  trapKPH;
    public bool   won;
    public string timestamp;
}

[Serializable]
public class TrackRecord
{
    public string trackId;
    public float  bestLap;
}

[Serializable]
public class GameSettings
{
    [Range(0f, 1f)] public float masterVolume  = 1f;
    [Range(0f, 1f)] public float musicVolume   = 0.7f;
    [Range(0f, 1f)] public float sfxVolume     = 1f;
    [Range(0f, 1f)] public float ambientVolume = 0.5f;

    public int    graphicsQuality  = 2;    // 0=Low 1=Med 2=High 3=Ultra
    public bool   motionBlur       = true;
    public bool   screenShake      = true;
    public bool   showMiniMap      = true;
    public bool   imperialUnits    = false;   // Metric by default (Australia)
    public bool   automaticGearbox = false;
    public string steeringDevice   = "keyboard";   // "keyboard" "controller" "wheel"
}
