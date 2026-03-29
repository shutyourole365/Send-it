using UnityEngine;
using System;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UGSEconomy = Unity.Services.Economy.EconomyService;

/// <summary>
/// Wraps UGS Economy to keep the in-game BUCKS currency cloud-authoritative.
///
/// Setup in cloud.unity.com → Economy:
/// ─────────────────────────────────────────────────────────────────────────────
///   Currency  ID: BUCKS   Name: Bucks   Initial Balance: 5000   Max: 10000000
///
/// Virtual Item catalogue (one entry per purchasable car):
///   Each item ID matches the carId used by AustralianCarData, e.g.
///   "holden_commodore_vk", "ford_falcon_xy_gt", etc.
/// ─────────────────────────────────────────────────────────────────────────────
///
/// On sign-in  → pulls cloud balance and overwrites local GameManager.currency.
/// Earn/Spend  → update cloud first, then commit the result locally.
///              Falls back to local-only if offline / UGS unavailable.
/// </summary>
public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    public const string CURRENCY_BUCKS = "BUCKS";

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

    async void HandleSignedIn() => await SyncBalanceFromCloudAsync();

    // ─── Balance sync ─────────────────────────────────────────────────────────

    /// <summary>Pull the cloud BUCKS balance and apply it to GameManager.</summary>
    public async Task SyncBalanceFromCloudAsync()
    {
        if (!IsReady()) return;
        try
        {
            var response = await UGSEconomy.Instance.PlayerBalances.GetBalancesAsync();
            var bucks    = response.Results.Find(b => b.CurrencyId == CURRENCY_BUCKS);
            if (bucks != null)
                ApplyBalance((int)bucks.Balance);

            Debug.Log($"[Economy] Cloud balance: {bucks?.Balance ?? 0} BUCKS");
        }
        catch (EconomyException e)
        {
            Debug.LogWarning($"[Economy] Balance sync failed ({e.Reason}): {e.Message}");
        }
    }

    // ─── Earn / Spend ─────────────────────────────────────────────────────────

    /// <summary>
    /// Award BUCKS to the player.  Returns the new balance, or -1 on failure.
    /// </summary>
    public async Task<int> IncrementAsync(int amount)
    {
        if (amount <= 0) return -1;
        if (!IsReady()) return -1;

        try
        {
            var result = await UGSEconomy.Instance.PlayerBalances.IncrementBalanceAsync(CURRENCY_BUCKS, amount);
            int newBalance = (int)result.Balance;
            ApplyBalance(newBalance);
            Debug.Log($"[Economy] +{amount} BUCKS → {newBalance}");
            return newBalance;
        }
        catch (EconomyException e)
        {
            Debug.LogWarning($"[Economy] Increment failed ({e.Reason}): {e.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Deduct BUCKS from the player.
    /// Returns the new balance, or -1 if the player cannot afford it / call failed.
    /// </summary>
    public async Task<int> DecrementAsync(int amount)
    {
        if (amount <= 0) return -1;
        if (!IsReady()) return -1;

        try
        {
            var result = await UGSEconomy.Instance.PlayerBalances.DecrementBalanceAsync(CURRENCY_BUCKS, amount);
            int newBalance = (int)result.Balance;
            ApplyBalance(newBalance);
            Debug.Log($"[Economy] -{amount} BUCKS → {newBalance}");
            return newBalance;
        }
        catch (EconomyValidationException)
        {
            Debug.Log("[Economy] Purchase declined — insufficient BUCKS.");
            return -1;
        }
        catch (EconomyException e)
        {
            Debug.LogWarning($"[Economy] Decrement failed ({e.Reason}): {e.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Set the player's BUCKS balance to an exact value (admin / test use only).
    /// </summary>
    public async Task SetBalanceAsync(int amount)
    {
        if (!IsReady()) return;
        try
        {
            var result = await UGSEconomy.Instance.PlayerBalances.SetBalanceAsync(CURRENCY_BUCKS, amount);
            ApplyBalance((int)result.Balance);
        }
        catch (EconomyException e)
        {
            Debug.LogWarning($"[Economy] SetBalance failed: {e.Message}");
        }
    }

    // ─── Virtual items / car purchases ───────────────────────────────────────

    /// <summary>
    /// Attempt to purchase a car from the UGS virtual item catalogue.
    /// The purchase deducts the configured BUCKS cost defined in the dashboard.
    /// Returns true if successful.
    /// </summary>
    public async Task<bool> PurchaseCarAsync(string carId)
    {
        if (!IsReady()) return false;
        try
        {
            // Virtual purchase — the item and cost are configured in the dashboard
            var result = await UGSEconomy.Instance.Purchases.MakeVirtualPurchaseAsync(carId);

            // Reflect the updated BUCKS balance immediately
            var bucks = result.Costs.Currency.Find(c => c.Id == CURRENCY_BUCKS);
            if (bucks != null)
                ApplyBalance(GameManager.Instance != null
                    ? GameManager.Instance.currency - (int)bucks.Amount
                    : 0);

            Debug.Log($"[Economy] Car purchased: {carId}");
            return true;
        }
        catch (EconomyValidationException)
        {
            Debug.Log($"[Economy] Cannot afford car: {carId}");
            return false;
        }
        catch (EconomyException e)
        {
            Debug.LogWarning($"[Economy] Purchase failed ({e.Reason}): {e.Message}");
            return false;
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    void ApplyBalance(int balance)
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.currency = balance;
        GameManager.Instance.OnCurrencyChanged?.Invoke(balance);
    }

    bool IsReady() => UGSManager.Instance != null && UGSManager.Instance.IsSignedIn;
}
