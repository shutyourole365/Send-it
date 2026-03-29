using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;

/// <summary>
/// Wraps UGS Cloud Save to keep player data synchronised across devices.
///
/// On sign-in  → pulls cloud data and merges it with the local save (best-of).
/// On each save → pushes the full save blob to the cloud (fire-and-forget).
///
/// Cloud keys
/// ──────────
///   player_data  – JSON of SaveData (progress, owned cars, records)
///   game_settings – JSON of GameSettings
/// </summary>
public class CloudSyncManager : MonoBehaviour
{
    public static CloudSyncManager Instance { get; private set; }

    const string KEY_PLAYER_DATA  = "player_data";
    const string KEY_GAME_SETTINGS = "game_settings";

    // ─── Unity ────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        UGSManager.OnSignedIn += HandleSignedIn;
    }

    void OnDestroy()
    {
        UGSManager.OnSignedIn -= HandleSignedIn;
    }

    void HandleSignedIn() => _ = PullFromCloudAsync();

    // ─── Push ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Serialises the current SaveData to JSON and writes it to UGS Cloud Save.
    /// Call this after every local save (fire-and-forget is fine).
    /// </summary>
    public async Task PushToCloudAsync()
    {
        if (!IsReady()) return;

        SaveData localData = SaveSystem.Instance?.GetSaveData();
        if (localData == null) return;

        try
        {
            var payload = new Dictionary<string, object>
            {
                [KEY_PLAYER_DATA]   = JsonUtility.ToJson(localData),
                [KEY_GAME_SETTINGS] = JsonUtility.ToJson(localData.settings),
            };

            await CloudSaveService.Instance.Data.Player.SaveAsync(payload);
            Debug.Log("[CloudSync] Pushed to cloud.");
        }
        catch (CloudSaveException e)
        {
            Debug.LogWarning($"[CloudSync] Push failed ({e.Reason}): {e.Message}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CloudSync] Push failed: {e.Message}");
        }
    }

    // ─── Pull & merge ─────────────────────────────────────────────────────────
    /// <summary>
    /// Loads the player's cloud save blob and merges it into the local save.
    /// Merge strategy: highest stats win, union of owned cars.
    /// </summary>
    public async Task PullFromCloudAsync()
    {
        if (!IsReady()) return;
        if (SaveSystem.Instance == null) return;

        try
        {
            var keys   = new HashSet<string> { KEY_PLAYER_DATA };
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            if (result.TryGetValue(KEY_PLAYER_DATA, out var item))
            {
                SaveData cloudData = JsonUtility.FromJson<SaveData>(item.Value.GetAs<string>());
                if (cloudData != null)
                {
                    SaveSystem.Instance.MergeCloudData(cloudData);
                    Debug.Log("[CloudSync] Pulled and merged from cloud.");
                }
            }
            else
            {
                // First-time cloud user — push local data up
                Debug.Log("[CloudSync] No cloud data found — pushing local save.");
                await PushToCloudAsync();
            }
        }
        catch (CloudSaveException e)
        {
            Debug.LogWarning($"[CloudSync] Pull failed ({e.Reason}): {e.Message}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CloudSync] Pull failed: {e.Message}");
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    bool IsReady() => UGSManager.Instance != null && UGSManager.Instance.IsSignedIn;
}
