using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Central sound manager.
/// Handles music tracks, ambient sound beds, crowd reactions,
/// one-shot SFX pooling, and volume mixing.
/// Singleton — persists across scenes.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    // ─── Music ────────────────────────────────────────────────────────────────
    [Header("Music")]
    public AudioClip[] garageTrack;       // Chill garage browsing music
    public AudioClip[] freeRoamTracks;    // Cruising tracks — Aussie rock/electronic
    public AudioClip[] raceIntenseTracks; // Race/event music
    public AudioClip[] burnoutTracks;     // High energy for burnout comp

    public float musicFadeTime = 2f;

    // ─── Ambient ──────────────────────────────────────────────────────────────
    [Header("Ambient Beds")]
    public AudioClip ambientCity;         // Traffic hum, distant cars
    public AudioClip ambientCrowd;        // Summernats crowd chatter
    public AudioClip ambientRacetrack;    // Track ambience, distant engines
    public AudioClip ambientNight;        // Crickets, night ambience

    // ─── Crowd ────────────────────────────────────────────────────────────────
    [Header("Crowd Reactions")]
    public AudioClip crowdCheer;
    public AudioClip crowdBigCheer;
    public AudioClip crowdGasp;
    public AudioClip crowdOoh;
    public AudioClip crowdCountdown;   // 3-2-1 chant

    // ─── Event SFX ────────────────────────────────────────────────────────────
    [Header("Event SFX")]
    public AudioClip sfxNewBestLap;
    public AudioClip sfxPenalty;
    public AudioClip sfxRaceFinish;
    public AudioClip sfxCashRegister;   // Currency earned
    public AudioClip sfxUnlock;         // Car/part unlocked
    public AudioClip sfxCountdown;
    public AudioClip sfxGreenLight;

    // ─── Volume ───────────────────────────────────────────────────────────────
    [Header("Volume")]
    [Range(0f, 1f)] public float masterVolume  = 1f;
    [Range(0f, 1f)] public float musicVolume   = 0.7f;
    [Range(0f, 1f)] public float ambientVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume     = 1f;
    [Range(0f, 1f)] public float crowdVolume   = 0.8f;

    // ─── Audio Sources ────────────────────────────────────────────────────────
    private AudioSource musicSource;
    private AudioSource musicSourceB;    // For crossfade
    private AudioSource ambientSource;
    private AudioSource crowdSource;
    private AudioSource sfxSource;

    private bool        usingSourceA = true;
    private Coroutine   fadeCoroutine;

    // SFX pool
    private Queue<AudioSource> sfxPool = new Queue<AudioSource>();
    private const int POOL_SIZE = 16;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource   = CreateSource("Music_A",   loop: true,  volume: musicVolume);
        musicSourceB  = CreateSource("Music_B",   loop: true,  volume: 0f);
        ambientSource = CreateSource("Ambient",   loop: true,  volume: ambientVolume);
        crowdSource   = CreateSource("Crowd",     loop: false, volume: crowdVolume);
        sfxSource     = CreateSource("SFX",       loop: false, volume: sfxVolume);

        for (int i = 0; i < POOL_SIZE; i++)
            sfxPool.Enqueue(CreateSource($"SFX_Pool_{i}", loop: false, volume: sfxVolume));
    }

    void Start()
    {
        // Subscribe to game state changes
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += OnGameStateChanged;
    }

    // ─── State Reaction ───────────────────────────────────────────────────────
    void OnGameStateChanged(GameManager.GameState state)
    {
        switch (state)
        {
            case GameManager.GameState.Garage:
                PlayAmbient(ambientCrowd);
                CrossfadeMusic(garageTrack);
                break;
            case GameManager.GameState.FreeRoam:
                PlayAmbient(ambientCity);
                CrossfadeMusic(freeRoamTracks);
                break;
            case GameManager.GameState.BurnoutComp:
                PlayAmbient(ambientCrowd);
                CrossfadeMusic(burnoutTracks);
                break;
            case GameManager.GameState.DragRace:
            case GameManager.GameState.CircuitRace:
                PlayAmbient(ambientRacetrack);
                CrossfadeMusic(raceIntenseTracks);
                break;
            case GameManager.GameState.MainMenu:
                StopAmbient();
                CrossfadeMusic(garageTrack);
                break;
        }
    }

    // ─── Music ────────────────────────────────────────────────────────────────
    public void CrossfadeMusic(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip pick = clips[Random.Range(0, clips.Length)];
        CrossfadeMusic(pick);
    }

    public void CrossfadeMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(CrossfadeRoutine(clip));
    }

    IEnumerator CrossfadeRoutine(AudioClip newClip)
    {
        AudioSource fadeIn  = usingSourceA ? musicSourceB : musicSource;
        AudioSource fadeOut = usingSourceA ? musicSource  : musicSourceB;

        fadeIn.clip   = newClip;
        fadeIn.volume = 0f;
        fadeIn.Play();

        float elapsed = 0f;
        float targetVol = musicVolume * masterVolume;

        while (elapsed < musicFadeTime)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / musicFadeTime;
            fadeIn.volume  = Mathf.Lerp(0f,       targetVol, t);
            fadeOut.volume = Mathf.Lerp(targetVol, 0f,       t);
            yield return null;
        }

        fadeOut.Stop();
        fadeOut.clip = null;
        usingSourceA = !usingSourceA;
    }

    public void StopMusic()
    {
        StartCoroutine(FadeOut(musicSource,  musicFadeTime));
        StartCoroutine(FadeOut(musicSourceB, musicFadeTime));
    }

    // ─── Ambient ──────────────────────────────────────────────────────────────
    public void PlayAmbient(AudioClip clip)
    {
        if (clip == null || ambientSource.clip == clip) return;
        ambientSource.clip   = clip;
        ambientSource.volume = ambientVolume * masterVolume;
        ambientSource.Play();
    }

    public void StopAmbient() => StartCoroutine(FadeOut(ambientSource, 1f));

    // ─── Crowd ────────────────────────────────────────────────────────────────
    /// <summary>Play a crowd reaction based on burnout score 0–100.</summary>
    public void PlayCrowdReaction(float score)
    {
        if (score > 80f)        PlayOneShot(crowdBigCheer, crowdVolume);
        else if (score > 50f)   PlayOneShot(crowdCheer,    crowdVolume);
        else if (score > 30f)   PlayOneShot(crowdOoh,      crowdVolume);
        else                    PlayOneShot(crowdGasp,     crowdVolume * 0.5f);
    }

    public void PlayCrowdCountdown() => PlayOneShot(crowdCountdown, crowdVolume);

    // ─── SFX ──────────────────────────────────────────────────────────────────
    public void PlaySFX(SFXEvent sfx)
    {
        AudioClip clip = sfx switch
        {
            SFXEvent.NewBestLap   => sfxNewBestLap,
            SFXEvent.Penalty      => sfxPenalty,
            SFXEvent.RaceFinish   => sfxRaceFinish,
            SFXEvent.CashRegister => sfxCashRegister,
            SFXEvent.Unlock       => sfxUnlock,
            SFXEvent.Countdown    => sfxCountdown,
            SFXEvent.GreenLight   => sfxGreenLight,
            _ => null
        };

        if (clip != null) PlayOneShot(clip, sfxVolume);
    }

    public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;

        AudioSource src = GetPooledSource();
        src.clip   = clip;
        src.volume = volumeScale * masterVolume;
        src.Play();
        StartCoroutine(ReturnToPool(src, clip.length + 0.1f));
    }

    public void PlayOneShotAt(AudioClip clip, Vector3 worldPosition, float volumeScale = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, worldPosition, volumeScale * masterVolume * sfxVolume);
    }

    // ─── Pool ─────────────────────────────────────────────────────────────────
    AudioSource GetPooledSource()
    {
        if (sfxPool.Count > 0) return sfxPool.Dequeue();
        // All pooled — create a temporary one
        return CreateSource("SFX_Temp", false, sfxVolume);
    }

    IEnumerator ReturnToPool(AudioSource src, float delay)
    {
        yield return new WaitForSeconds(delay);
        src.Stop();
        sfxPool.Enqueue(src);
    }

    // ─── Volume Control ───────────────────────────────────────────────────────
    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        ApplyAllVolumes();
    }

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        musicSource.volume  = musicVolume * masterVolume;
        musicSourceB.volume = musicVolume * masterVolume;
    }

    public void SetAmbientVolume(float v)
    {
        ambientVolume = Mathf.Clamp01(v);
        ambientSource.volume = ambientVolume * masterVolume;
    }

    public void SetSFXVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
    }

    void ApplyAllVolumes()
    {
        SetMusicVolume(musicVolume);
        SetAmbientVolume(ambientVolume);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    AudioSource CreateSource(string sourceName, bool loop, float volume)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform);
        var src     = go.AddComponent<AudioSource>();
        src.loop    = loop;
        src.volume  = volume;
        src.playOnAwake = false;
        return src;
    }

    IEnumerator FadeOut(AudioSource src, float duration)
    {
        float startVol = src.volume;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed    += Time.deltaTime;
            src.volume  = Mathf.Lerp(startVol, 0f, elapsed / duration);
            yield return null;
        }
        src.Stop();
    }
}

public enum SFXEvent
{
    NewBestLap,
    Penalty,
    RaceFinish,
    CashRegister,
    Unlock,
    Countdown,
    GreenLight,
}
