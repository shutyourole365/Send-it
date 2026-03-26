using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Full visual and performance customisation system.
/// Attach to the vehicle root alongside VehicleController.
/// Assign renderer references and part lists in the inspector.
/// </summary>
public class CarCustomisation : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────────────────────
    [Header("References")]
    public VehicleController vehicle;
    public Renderer bodyRenderer;           // Main body paint renderer
    public Renderer[] rimRenderers;         // One per wheel

    [Header("Body Kit Slots")]
    public GameObject[] frontBumperSlots;   // Index = part variant
    public GameObject[] rearBumperSlots;
    public GameObject[] sideSkirtsSlots;
    public GameObject[] bonnetsSlots;
    public GameObject[] spoilerSlots;
    public GameObject[] exhaustTipSlots;

    [Header("Interior")]
    public GameObject[] rollCageSlots;
    public GameObject[] seatSlots;
    public GameObject   steeringWheelMesh;

    // ─── Current Loadout ─────────────────────────────────────────────────────
    [Header("Current Loadout (Runtime)")]
    public CarLoadout loadout = new CarLoadout();

    // ─── Part Catalogues ─────────────────────────────────────────────────────
    [Header("Part Catalogues")]
    public List<EnginePart>    engineCatalogue    = new List<EnginePart>();
    public List<ForcedInduction> turboCatalogue   = new List<ForcedInduction>();
    public List<GearboxPart>   gearboxCatalogue   = new List<GearboxPart>();
    public List<DiffPart>      diffCatalogue      = new List<DiffPart>();
    public List<SuspensionPart> suspensionCatalogue = new List<SuspensionPart>();
    public List<BrakePart>     brakeCatalogue     = new List<BrakePart>();
    public List<TyrePart>      tyreCatalogue      = new List<TyrePart>();
    public List<WheelVisual>   wheelCatalogue     = new List<WheelVisual>();

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Start()
    {
        ApplyFullLoadout();
    }

    // ─── Apply Everything ────────────────────────────────────────────────────
    public void ApplyFullLoadout()
    {
        ApplyPaint();
        ApplyBodyKit();
        ApplyWheels();
        ApplyPerformance();
    }

    // ─── Paint ───────────────────────────────────────────────────────────────
    public void SetPaint(Color colour, PaintFinish finish)
    {
        loadout.paintColour = colour;
        loadout.paintFinish = finish;
        ApplyPaint();
    }

    void ApplyPaint()
    {
        if (bodyRenderer == null) return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        bodyRenderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", loadout.paintColour);

        switch (loadout.paintFinish)
        {
            case PaintFinish.Gloss:
                block.SetFloat("_Smoothness", 0.95f);
                block.SetFloat("_Metallic",   0.0f);
                break;
            case PaintFinish.Matte:
                block.SetFloat("_Smoothness", 0.05f);
                block.SetFloat("_Metallic",   0.0f);
                break;
            case PaintFinish.Metallic:
                block.SetFloat("_Smoothness", 0.85f);
                block.SetFloat("_Metallic",   0.8f);
                break;
            case PaintFinish.Candy:
                block.SetFloat("_Smoothness", 1.0f);
                block.SetFloat("_Metallic",   0.3f);
                break;
            case PaintFinish.Chrome:
                block.SetFloat("_Smoothness", 1.0f);
                block.SetFloat("_Metallic",   1.0f);
                break;
            case PaintFinish.Satin:
                block.SetFloat("_Smoothness", 0.4f);
                block.SetFloat("_Metallic",   0.0f);
                break;
        }

        bodyRenderer.SetPropertyBlock(block);
    }

    // ─── Body Kit ────────────────────────────────────────────────────────────
    public void SetBodyKit(BodyKitSlot slot, int variantIndex)
    {
        switch (slot)
        {
            case BodyKitSlot.FrontBumper: loadout.frontBumperIndex = variantIndex; ActivateSlot(frontBumperSlots, variantIndex); break;
            case BodyKitSlot.RearBumper:  loadout.rearBumperIndex  = variantIndex; ActivateSlot(rearBumperSlots,  variantIndex); break;
            case BodyKitSlot.SideSkirts:  loadout.sideSkirtsIndex  = variantIndex; ActivateSlot(sideSkirtsSlots,  variantIndex); break;
            case BodyKitSlot.Bonnet:      loadout.bonnetsIndex     = variantIndex; ActivateSlot(bonnetsSlots,     variantIndex); break;
            case BodyKitSlot.Spoiler:     loadout.spoilerIndex     = variantIndex; ActivateSlot(spoilerSlots,     variantIndex); break;
            case BodyKitSlot.ExhaustTip:  loadout.exhaustTipIndex  = variantIndex; ActivateSlot(exhaustTipSlots,  variantIndex); break;
            case BodyKitSlot.RollCage:    loadout.rollCageIndex    = variantIndex; ActivateSlot(rollCageSlots,    variantIndex); break;
            case BodyKitSlot.Seats:       loadout.seatsIndex       = variantIndex; ActivateSlot(seatSlots,        variantIndex); break;
        }
    }

    void ApplyBodyKit()
    {
        ActivateSlot(frontBumperSlots, loadout.frontBumperIndex);
        ActivateSlot(rearBumperSlots,  loadout.rearBumperIndex);
        ActivateSlot(sideSkirtsSlots,  loadout.sideSkirtsIndex);
        ActivateSlot(bonnetsSlots,     loadout.bonnetsIndex);
        ActivateSlot(spoilerSlots,     loadout.spoilerIndex);
        ActivateSlot(exhaustTipSlots,  loadout.exhaustTipIndex);
        ActivateSlot(rollCageSlots,    loadout.rollCageIndex);
        ActivateSlot(seatSlots,        loadout.seatsIndex);
    }

    void ActivateSlot(GameObject[] slots, int index)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].SetActive(i == index);
        }
    }

    // ─── Wheels ──────────────────────────────────────────────────────────────
    public void SetWheel(int catalogueIndex)
    {
        loadout.wheelIndex = catalogueIndex;
        ApplyWheels();
    }

    public void SetRimColour(Color colour)
    {
        loadout.rimColour = colour;
        ApplyWheels();
    }

    void ApplyWheels()
    {
        if (rimRenderers == null) return;
        foreach (Renderer r in rimRenderers)
        {
            if (r == null) continue;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", loadout.rimColour);
            r.SetPropertyBlock(block);
        }

        if (loadout.wheelIndex >= 0 && loadout.wheelIndex < wheelCatalogue.Count)
        {
            WheelVisual w = wheelCatalogue[loadout.wheelIndex];
            // Mesh swap would happen here via MeshFilter — hook into your wheel prefab system
            Debug.Log($"[Customisation] Wheel set: {w.partName}  Size: {w.wheelDiameterInch}\"");
        }
    }

    // ─── Performance ─────────────────────────────────────────────────────────
    public void SetEnginePart(int catalogueIndex)
    {
        loadout.engineIndex = catalogueIndex;
        ApplyPerformance();
    }

    public void SetTurboPart(int catalogueIndex)
    {
        loadout.turboIndex = catalogueIndex;
        ApplyPerformance();
    }

    public void SetGearboxPart(int catalogueIndex)
    {
        loadout.gearboxIndex = catalogueIndex;
        ApplyPerformance();
    }

    public void SetDiffPart(int catalogueIndex)
    {
        loadout.diffIndex = catalogueIndex;
        ApplyPerformance();
    }

    public void SetSuspensionPart(int catalogueIndex)
    {
        loadout.suspensionIndex = catalogueIndex;
        ApplyPerformance();
    }

    public void SetBrakePart(int catalogueIndex)
    {
        loadout.brakeIndex = catalogueIndex;
        ApplyPerformance();
    }

    public void SetTyrePart(int catalogueIndex)
    {
        loadout.tyreIndex = catalogueIndex;
        ApplyPerformance();
    }

    void ApplyPerformance()
    {
        if (vehicle == null) return;

        PerformanceSpec spec = new PerformanceSpec();

        // --- Engine base
        if (loadout.engineIndex >= 0 && loadout.engineIndex < engineCatalogue.Count)
        {
            EnginePart e = engineCatalogue[loadout.engineIndex];
            spec.peakTorque    = e.torqueNm;
            spec.peakTorqueRPM = e.peakTorqueRPM;
            spec.redlineRPM    = e.redlineRPM;
        }

        // --- Forced induction boost
        if (loadout.turboIndex >= 0 && loadout.turboIndex < turboCatalogue.Count)
        {
            ForcedInduction fi = turboCatalogue[loadout.turboIndex];
            spec.peakTorque    *= fi.torqueMultiplier;
            spec.redlineRPM    += fi.redlineBoost;
        }

        // --- Gearbox
        if (loadout.gearboxIndex >= 0 && loadout.gearboxIndex < gearboxCatalogue.Count)
        {
            GearboxPart g = gearboxCatalogue[loadout.gearboxIndex];
            spec.gearRatios = g.gearRatios;
        }

        // --- Diff
        if (loadout.diffIndex >= 0 && loadout.diffIndex < diffCatalogue.Count)
        {
            DiffPart d = diffCatalogue[loadout.diffIndex];
            spec.finalDriveRatio = d.finalDriveRatio;
        }

        // --- Suspension
        if (loadout.suspensionIndex >= 0 && loadout.suspensionIndex < suspensionCatalogue.Count)
        {
            SuspensionPart s = suspensionCatalogue[loadout.suspensionIndex];
            spec.suspensionSpring = s.springRate;
            spec.suspensionDamper = s.damperRate;
        }

        // --- Brakes
        if (loadout.brakeIndex >= 0 && loadout.brakeIndex < brakeCatalogue.Count)
        {
            BrakePart b = brakeCatalogue[loadout.brakeIndex];
            spec.frontBrakeTorque = b.frontBrakeTorque;
            spec.rearBrakeTorque  = b.rearBrakeTorque;
        }

        vehicle.ApplyPerformanceSpec(spec);

        // --- Tyre grip
        if (loadout.tyreIndex >= 0 && loadout.tyreIndex < tyreCatalogue.Count)
        {
            vehicle.tyreGrip = tyreCatalogue[loadout.tyreIndex].gripFactor;
        }

        Debug.Log($"[Customisation] Performance applied — Torque: {spec.peakTorque:F0}Nm  Redline: {spec.redlineRPM:F0}rpm");
    }
}

// ─── Enums ───────────────────────────────────────────────────────────────────

public enum PaintFinish { Gloss, Matte, Metallic, Candy, Chrome, Satin }
public enum BodyKitSlot { FrontBumper, RearBumper, SideSkirts, Bonnet, Spoiler, ExhaustTip, RollCage, Seats }

// ─── Loadout ─────────────────────────────────────────────────────────────────

[System.Serializable]
public class CarLoadout
{
    // Visual
    public Color      paintColour   = Color.red;
    public PaintFinish paintFinish  = PaintFinish.Gloss;
    public Color      rimColour     = Color.white;
    public int        wheelIndex    = 0;

    // Body kit
    public int frontBumperIndex = 0;
    public int rearBumperIndex  = 0;
    public int sideSkirtsIndex  = 0;
    public int bonnetsIndex     = 0;
    public int spoilerIndex     = 0;
    public int exhaustTipIndex  = 0;
    public int rollCageIndex    = 0;
    public int seatsIndex       = 0;

    // Performance
    public int engineIndex     = 0;
    public int turboIndex      = -1;   // -1 = no forced induction
    public int gearboxIndex    = 0;
    public int diffIndex       = 0;
    public int suspensionIndex = 0;
    public int brakeIndex      = 0;
    public int tyreIndex       = 0;
}

// ─── Part Data Classes ────────────────────────────────────────────────────────

[System.Serializable]
public class EnginePart
{
    public string partName;
    public string description;
    public float  torqueNm       = 400f;
    public float  peakTorqueRPM  = 4000f;
    public float  redlineRPM     = 6500f;
    public int    cost;
}

[System.Serializable]
public class ForcedInduction
{
    public string partName;
    public string description;
    [Tooltip("Multiplies engine torque")]
    public float  torqueMultiplier = 1.3f;
    public float  redlineBoost     = 500f;
    public int    cost;
}

[System.Serializable]
public class GearboxPart
{
    public string  partName;
    public string  description;
    public float[] gearRatios = { 3.5f, 2.3f, 1.6f, 1.2f, 0.9f, 0.7f };
    public int     numGears   = 6;
    public int     cost;
}

[System.Serializable]
public class DiffPart
{
    public string partName;
    public string description;
    public float  finalDriveRatio = 3.7f;
    public DiffType diffType      = DiffType.LimitedSlip;
    public int    cost;
}

public enum DiffType { Open, LimitedSlip, Spool, Torsen }

[System.Serializable]
public class SuspensionPart
{
    public string partName;
    public string description;
    public float  springRate  = 35000f;
    public float  damperRate  = 4500f;
    public float  rideHeight  = 0f;     // offset from default in metres
    public int    cost;
}

[System.Serializable]
public class BrakePart
{
    public string partName;
    public string description;
    public float  frontBrakeTorque = 3000f;
    public float  rearBrakeTorque  = 2000f;
    public int    cost;
}

[System.Serializable]
public class TyrePart
{
    public string partName;
    public string description;
    [Range(0f, 1f)]
    public float  gripFactor    = 0.85f;
    public float  wearRate      = 0.004f;
    [Tooltip("Wider tyres produce more smoke")]
    public float  smokeMultiplier = 1f;
    public int    cost;
}

[System.Serializable]
public class WheelVisual
{
    public string  partName;
    public float   wheelDiameterInch = 18f;
    public float   wheelWidthInch    = 8.5f;
    public GameObject prefab;
    public int     cost;
}
