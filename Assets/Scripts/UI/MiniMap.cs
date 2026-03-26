using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Real-time minimap with radar blips.
/// Uses a top-down RenderTexture camera OR a 2D icon overlay approach.
/// Tracks player, AI traffic, checkpoints, and points of interest.
/// Attach to the HUD canvas.
/// </summary>
public class MiniMap : MonoBehaviour
{
    // ─── Mode ─────────────────────────────────────────────────────────────────
    public enum MiniMapMode { RenderTexture, IconOverlay }

    [Header("Mode")]
    public MiniMapMode mode = MiniMapMode.IconOverlay;

    // ─── RenderTexture Mode ───────────────────────────────────────────────────
    [Header("Render Texture Mode")]
    public Camera   minimapCamera;
    public RawImage minimapDisplay;
    public float    cameraHeight    = 150f;
    public float    cameraOrthoSize = 200f;
    public bool     rotateWithPlayer = true;

    // ─── Icon Overlay Mode ────────────────────────────────────────────────────
    [Header("Icon Overlay Mode")]
    public RectTransform mapContainer;    // Masked circular container
    public float         mapWorldRadius  = 200f;   // World units shown on map
    public float         mapDisplayRadius = 80f;   // UI pixel radius

    // ─── Icons ────────────────────────────────────────────────────────────────
    [Header("Icons")]
    public Sprite iconPlayer;
    public Sprite iconTraffic;
    public Sprite iconAIRacer;
    public Sprite iconCheckpoint;
    public Sprite iconPOI;            // Points of interest (fuel, workshop)

    public Color colourPlayer     = Color.yellow;
    public Color colourTraffic    = Color.white;
    public Color colourAIRacer    = Color.red;
    public Color colourCheckpoint = Color.green;
    public Color colourPOI        = Color.cyan;

    // ─── Player Reference ─────────────────────────────────────────────────────
    [Header("References")]
    public Transform playerTransform;

    // ─── Points of Interest ───────────────────────────────────────────────────
    [Header("Points of Interest")]
    public List<PointOfInterest> pointsOfInterest = new List<PointOfInterest>();

    // ─── Compass ─────────────────────────────────────────────────────────────
    [Header("Compass")]
    public RectTransform compassNeedle;   // N indicator needle
    public TextMeshProUGUI headingText;   // "NE" "SW" etc.

    // ─── Runtime ──────────────────────────────────────────────────────────────
    private Dictionary<Transform, RectTransform> blipMap = new Dictionary<Transform, RectTransform>();
    private RectTransform playerBlip;
    private AITrafficManager trafficManager;
    private CircuitRace circuitRace;

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Start()
    {
        trafficManager = FindFirstObjectByType<AITrafficManager>();
        circuitRace    = FindFirstObjectByType<CircuitRace>();

        if (mode == MiniMapMode.RenderTexture)
            SetupRenderTextureMode();
        else
            SetupIconOverlayMode();

        SpawnCheckpointBlips();
        SpawnPOIBlips();
    }

    void LateUpdate()
    {
        if (!isActiveAndEnabled) return;

        if (mode == MiniMapMode.RenderTexture)
            UpdateRenderTextureCamera();
        else
            UpdateIconOverlay();

        UpdateCompass();
    }

    // ─── Render Texture Setup ─────────────────────────────────────────────────
    void SetupRenderTextureMode()
    {
        if (minimapCamera == null) return;
        minimapCamera.orthographic     = true;
        minimapCamera.orthographicSize = cameraOrthoSize;
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Create render texture if not assigned
        if (minimapCamera.targetTexture == null)
        {
            var rt = new RenderTexture(256, 256, 16);
            minimapCamera.targetTexture = rt;
            if (minimapDisplay) minimapDisplay.texture = rt;
        }
    }

    void UpdateRenderTextureCamera()
    {
        if (minimapCamera == null || playerTransform == null) return;

        Vector3 camPos = playerTransform.position;
        camPos.y += cameraHeight;
        minimapCamera.transform.position = camPos;

        if (rotateWithPlayer)
            minimapCamera.transform.rotation = Quaternion.Euler(90f, playerTransform.eulerAngles.y, 0f);
    }

    // ─── Icon Overlay Setup ───────────────────────────────────────────────────
    void SetupIconOverlayMode()
    {
        if (playerTransform != null)
            playerBlip = SpawnBlip(playerTransform, iconPlayer, colourPlayer, 12f, isPlayer: true);
    }

    void SpawnCheckpointBlips()
    {
        if (circuitRace?.checkpoints == null) return;
        foreach (var cp in circuitRace.checkpoints)
        {
            if (cp == null) continue;
            SpawnBlip(cp.transform, iconCheckpoint, colourCheckpoint, 6f);
        }
    }

    void SpawnPOIBlips()
    {
        foreach (var poi in pointsOfInterest)
        {
            if (poi.transform == null) continue;
            SpawnBlip(poi.transform, iconPOI, colourPOI, 8f);
        }
    }

    // ─── Icon Overlay Update ──────────────────────────────────────────────────
    void UpdateIconOverlay()
    {
        if (playerTransform == null || mapContainer == null) return;

        // Player blip always centred, rotated to face forward
        if (playerBlip != null)
        {
            playerBlip.anchoredPosition = Vector2.zero;
            playerBlip.localEulerAngles = new Vector3(0f, 0f, -playerTransform.eulerAngles.y);
        }

        // Update tracked blips
        var toRemove = new List<Transform>();
        foreach (var kvp in blipMap)
        {
            if (kvp.Key == null) { toRemove.Add(kvp.Key); continue; }
            if (kvp.Key == playerTransform) continue;

            Vector2 screenPos = WorldToMinimap(kvp.Key.position);
            float   dist      = screenPos.magnitude;

            if (dist > mapDisplayRadius)
            {
                // Clamp to edge
                screenPos = screenPos.normalized * mapDisplayRadius;
            }

            kvp.Value.anchoredPosition = screenPos;
        }

        foreach (var k in toRemove)
        {
            if (blipMap[k] != null) Destroy(blipMap[k].gameObject);
            blipMap.Remove(k);
        }

        // Add new traffic blips dynamically
        UpdateTrafficBlips();
    }

    void UpdateTrafficBlips()
    {
        if (trafficManager == null) return;

        var vehicles = FindObjectsByType<AITrafficVehicle>(FindObjectsSortMode.None);
        foreach (var v in vehicles)
        {
            if (v == null) continue;
            if (!blipMap.ContainsKey(v.transform))
                SpawnBlip(v.transform, iconTraffic, colourTraffic, 5f);
        }
    }

    Vector2 WorldToMinimap(Vector3 worldPos)
    {
        Vector3 delta = worldPos - playerTransform.position;

        // Rotate delta by negative player yaw so map is player-relative
        float yaw    = -playerTransform.eulerAngles.y * Mathf.Deg2Rad;
        float cosYaw = Mathf.Cos(yaw);
        float sinYaw = Mathf.Sin(yaw);

        float x = delta.x * cosYaw - delta.z * sinYaw;
        float z = delta.x * sinYaw + delta.z * cosYaw;

        float scale = mapDisplayRadius / mapWorldRadius;
        return new Vector2(x * scale, z * scale);
    }

    RectTransform SpawnBlip(Transform target, Sprite icon, Color colour, float size, bool isPlayer = false)
    {
        if (mapContainer == null) return null;

        var go  = new GameObject($"Blip_{target.name}");
        go.transform.SetParent(mapContainer, false);

        var img     = go.AddComponent<Image>();
        img.sprite  = icon;
        img.color   = colour;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;

        if (!isPlayer)
            blipMap[target] = rt;

        return rt;
    }

    // ─── Public API ───────────────────────────────────────────────────────────
    public void AddPOI(Transform poi, Sprite icon = null, Color? colour = null)
    {
        SpawnBlip(poi, icon ?? iconPOI, colour ?? colourPOI, 8f);
    }

    public void RemovePOI(Transform poi)
    {
        if (blipMap.TryGetValue(poi, out var blip))
        {
            Destroy(blip.gameObject);
            blipMap.Remove(poi);
        }
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (minimapCamera != null) minimapCamera.enabled = visible;
    }

    // ─── Compass ─────────────────────────────────────────────────────────────
    void UpdateCompass()
    {
        if (playerTransform == null) return;

        float yaw = playerTransform.eulerAngles.y;

        if (compassNeedle)
            compassNeedle.localEulerAngles = new Vector3(0f, 0f, yaw);

        if (headingText)
            headingText.text = YawToCardinal(yaw);
    }

    static string YawToCardinal(float yaw)
    {
        yaw = (yaw % 360f + 360f) % 360f;
        if (yaw <  22.5f || yaw >= 337.5f) return "N";
        if (yaw <  67.5f) return "NE";
        if (yaw < 112.5f) return "E";
        if (yaw < 157.5f) return "SE";
        if (yaw < 202.5f) return "S";
        if (yaw < 247.5f) return "SW";
        if (yaw < 292.5f) return "W";
        return "NW";
    }
}

// ─── Supporting Types ─────────────────────────────────────────────────────────

[System.Serializable]
public class PointOfInterest
{
    public Transform transform;
    public string    label;
    public POIType   type;
}

public enum POIType { Fuel, Workshop, BurnoutPad, DragStrip, Circuit, FreeRoamStart }

// Add TMPro reference needed by compass
namespace TMPro { }
