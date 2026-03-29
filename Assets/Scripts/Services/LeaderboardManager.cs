using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;

/// <summary>
/// Submits and retrieves scores from UGS Leaderboards.
///
/// Leaderboard IDs (create these in cloud.unity.com → Leaderboards):
/// ──────────────────────────────────────────────────────────────────
///   burnout-all-time   Burnout competition — score ascending (higher = better)
///   drag-race-et       Drag race ET — stored as (1000 – ET) so shorter time = higher score
///   circuit-winton     Winton circuit lap — stored as (10000 – lapTime), same inversion
///
/// All boards use descending sort (UGS default) which naturally gives rank #1 to
/// the fastest / highest-scoring entry after the score inversion above.
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    // ─── Board IDs (must match what you create in the UGS dashboard) ──────────
    public const string BOARD_BURNOUT        = "burnout-all-time";
    public const string BOARD_DRAG_ET        = "drag-race-et";
    public const string BOARD_CIRCUIT_WINTON = "circuit-winton";

    public const int TOP_SCORES_LIMIT = 10;

    // ─── Unity ────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── Submit ───────────────────────────────────────────────────────────────

    /// <summary>Submit a burnout competition score (e.g. 24750).</summary>
    public async Task SubmitBurnoutScoreAsync(float score)
    {
        if (!IsReady()) return;
        try
        {
            var entry = await LeaderboardsService.Instance.AddPlayerScoreAsync(BOARD_BURNOUT, score);
            Debug.Log($"[Leaderboard] Burnout {score:F0} pts → global rank #{entry.Rank + 1}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Leaderboard] Burnout submit failed: {e.Message}");
        }
    }

    /// <summary>
    /// Submit a drag race elapsed time.
    /// The score is inverted (1000 – ET) so shorter times rank higher.
    /// </summary>
    public async Task SubmitDragETAsync(float elapsedTimeSec)
    {
        if (!IsReady()) return;
        double score = 1000.0 - elapsedTimeSec;   // e.g. ET 10.248 → score 989.752
        try
        {
            var entry = await LeaderboardsService.Instance.AddPlayerScoreAsync(BOARD_DRAG_ET, score);
            Debug.Log($"[Leaderboard] Drag ET {elapsedTimeSec:F3}s → global rank #{entry.Rank + 1}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Leaderboard] Drag ET submit failed: {e.Message}");
        }
    }

    /// <summary>
    /// Submit a circuit lap time.
    /// Score is inverted (10000 – lapTime) so faster laps rank higher.
    /// </summary>
    public async Task SubmitCircuitLapAsync(string trackId, float lapTimeSec)
    {
        if (!IsReady()) return;
        string boardId = TrackIdToBoard(trackId);
        double score   = 10000.0 - lapTimeSec;
        try
        {
            var entry = await LeaderboardsService.Instance.AddPlayerScoreAsync(boardId, score);
            Debug.Log($"[Leaderboard] Lap {lapTimeSec:F3}s on {trackId} → global rank #{entry.Rank + 1}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Leaderboard] Lap submit failed: {e.Message}");
        }
    }

    // ─── Fetch top scores ─────────────────────────────────────────────────────

    public async Task<List<LeaderboardEntry>> GetTopBurnoutScoresAsync()
        => await FetchTopAsync(BOARD_BURNOUT);

    public async Task<List<LeaderboardEntry>> GetTopDragETsAsync()
        => await FetchTopAsync(BOARD_DRAG_ET);

    public async Task<List<LeaderboardEntry>> GetTopCircuitLapsAsync(string trackId)
        => await FetchTopAsync(TrackIdToBoard(trackId));

    /// <summary>Fetch the calling player's own entry on a board (may be null if unranked).</summary>
    public async Task<LeaderboardEntry> GetPlayerEntryAsync(string boardId)
    {
        if (!IsReady()) return null;
        try
        {
            return await LeaderboardsService.Instance.GetPlayerScoreAsync(boardId);
        }
        catch (LeaderboardsException e) when (e.Reason == LeaderboardsExceptionReason.EntryNotFound)
        {
            return null;   // Player hasn't submitted a score yet
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Leaderboard] Player entry fetch failed: {e.Message}");
            return null;
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    async Task<List<LeaderboardEntry>> FetchTopAsync(string boardId)
    {
        if (!IsReady()) return null;
        try
        {
            var options = new GetScoresOptions { Limit = TOP_SCORES_LIMIT };
            var page    = await LeaderboardsService.Instance.GetScoresAsync(boardId, options);
            return page.Results;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Leaderboard] Fetch '{boardId}' failed: {e.Message}");
            return null;
        }
    }

    string TrackIdToBoard(string trackId)
    {
        // Map scene/track IDs to dashboard board IDs
        switch (trackId)
        {
            case "Circuit_Winton":
            case "circuit_winton":
                return BOARD_CIRCUIT_WINTON;
            default:
                // Fallback: sanitise the trackId as a board suffix
                return "circuit-" + trackId.ToLower().Replace("_", "-");
        }
    }

    bool IsReady() => UGSManager.Instance != null && UGSManager.Instance.IsSignedIn;
}
