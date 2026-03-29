using UnityEngine;
using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;

/// <summary>
/// Initialises Unity Gaming Services and handles anonymous sign-in.
/// All other UGS service managers wait for OnSignedIn before making API calls.
///
/// Setup:
///   1. Open Edit → Project Settings → Services and link your UGS project.
///   2. Add this component (and the other UGS managers) to a persistent
///      GameObject in your bootstrap / main-menu scene alongside GameManager.
/// </summary>
public class UGSManager : MonoBehaviour
{
    public static UGSManager Instance { get; private set; }

    public bool IsSignedIn => AuthenticationService.Instance != null &&
                              AuthenticationService.Instance.IsSignedIn;

    public string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : null;

    // Fires once sign-in succeeds; other managers subscribe to this.
    public static event Action    OnSignedIn;
    public static event Action<string> OnSignInFailed;

    // ─── Unity ────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    async void Start()
    {
        await InitAsync();
    }

    // ─── Init ─────────────────────────────────────────────────────────────────
    async Task InitAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();
            Debug.Log("[UGS] Services initialised.");

            // Re-attach session events every run (they don't persist across domain reloads)
            AuthenticationService.Instance.SignedIn      += OnAuthSignedIn;
            AuthenticationService.Instance.SignInFailed  += OnAuthSignInFailed;
            AuthenticationService.Instance.SignedOut     += OnAuthSignedOut;
            AuthenticationService.Instance.Expired       += OnSessionExpired;

            await SignInAnonymouslyAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[UGS] Init failed: {e.Message}");
            OnSignInFailed?.Invoke(e.Message);
        }
    }

    // ─── Anonymous sign-in ────────────────────────────────────────────────────
    async Task SignInAnonymouslyAsync()
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log($"[UGS] Already signed in: {PlayerId}");
            OnSignedIn?.Invoke();
            return;
        }

        try
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch (AuthenticationException e)
        {
            Debug.LogError($"[UGS] Auth exception ({e.ErrorCode}): {e.Message}");
            OnSignInFailed?.Invoke(e.Message);
        }
        catch (RequestFailedException e)
        {
            Debug.LogError($"[UGS] Request failed ({e.ErrorCode}): {e.Message}");
            OnSignInFailed?.Invoke(e.Message);
        }
    }

    // ─── Account linking ──────────────────────────────────────────────────────
    /// <summary>Link the anonymous account to a Google account (optional upgrade flow).</summary>
    public async Task LinkWithGoogleAsync(string googleIdToken)
    {
        if (!IsSignedIn)
        {
            Debug.LogWarning("[UGS] Cannot link: not signed in.");
            return;
        }

        try
        {
            await AuthenticationService.Instance.LinkWithGoogleAsync(googleIdToken);
            Debug.Log("[UGS] Google account linked successfully.");
        }
        catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
        {
            Debug.LogWarning("[UGS] Account already linked to a Google identity.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UGS] Google link failed: {e.Message}");
        }
    }

    // ─── Sign-out ─────────────────────────────────────────────────────────────
    public void SignOut()
    {
        AuthenticationService.Instance.SignOut(clearCredentials: false);
    }

    // ─── Auth event callbacks ─────────────────────────────────────────────────
    void OnAuthSignedIn()
    {
        Debug.Log($"[UGS] Signed in as {PlayerId}");
        OnSignedIn?.Invoke();
    }

    void OnAuthSignInFailed(RequestFailedException e)
    {
        Debug.LogError($"[UGS] Sign-in failed: {e.Message}");
        OnSignInFailed?.Invoke(e.Message);
    }

    void OnAuthSignedOut()    => Debug.Log("[UGS] Signed out.");
    void OnSessionExpired()   => Debug.Log("[UGS] Session expired — reconnecting…");
}
