using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Australian car definitions — base specs, default loadouts, and factory data.
/// Create a CarDatabase ScriptableObject and populate it in the inspector,
/// or call AustralianCarData.BuildDefaultDatabase() at runtime.
/// </summary>

// ─── Car Definition ───────────────────────────────────────────────────────────

[System.Serializable]
public class AussieCarDefinition
{
    public string  id;              // Unique identifier e.g. "vk_commodore"
    public string  displayName;     // e.g. "VK Commodore"
    public string  manufacturer;    // "Holden" / "Ford" / "Chrysler" etc.
    public int     year;
    public string  bodyStyle;       // "Sedan" / "Ute" / "Coupe" / "Wagon"

    [TextArea(2, 4)]
    public string  lore;            // Flavour text for the garage screen

    public Sprite  thumbnail;       // Preview image
    public GameObject prefab;       // Vehicle prefab

    // Factory performance
    public PerformanceSpec factorySpec;

    // Default loadout applied when car is first unlocked
    public CarLoadout defaultLoadout;

    // Parts that are compatible with this car
    public List<string> compatibleEngineParts    = new List<string>();
    public List<string> compatibleBodyKitSlugs   = new List<string>();

    public int unlockCost;          // In-game currency to unlock
    public bool unlockedByDefault;
}

// ─── Database ─────────────────────────────────────────────────────────────────

[CreateAssetMenu(fileName = "CarDatabase", menuName = "Send-it/Car Database")]
public class CarDatabase : ScriptableObject
{
    public List<AussieCarDefinition> cars = new List<AussieCarDefinition>();

    public AussieCarDefinition GetById(string id)
    {
        return cars.Find(c => c.id == id);
    }
}

// ─── Static Factory — Pre-built Australian Cars ───────────────────────────────

public static class AustralianCarData
{
    /// <summary>Returns the full catalogue of built-in Australian cars.</summary>
    public static List<AussieCarDefinition> BuildDefaultDatabase()
    {
        return new List<AussieCarDefinition>
        {
            // ── Holden ──────────────────────────────────────────────────────

            HoldenTorana_LJ(),
            HoldenCommodore_VK(),
            HoldenCommodore_VN(),
            HoldenCommodore_VS(),
            HoldenCommodore_VT(),
            HoldenCommodore_VY(),
            HoldenCommodore_VE(),
            HoldenHSV_GTSR(),
            HoldenMonaro_CV8(),
            HoldenUte_VU(),

            // ── Ford ─────────────────────────────────────────────────────────

            FordFalcon_XY_GT(),
            FordFalcon_XA(),
            FordFalcon_XB_Coupe(),
            FordFalcon_EA(),
            FordFalcon_EF(),
            FordFalcon_AU(),
            FordFalcon_BA_XR8(),
            FordFalcon_FG_XR6T(),
            FordFPV_GT_F(),
            FordUte_FG(),

            // ── Chrysler / Valiant ────────────────────────────────────────────

            ChryslerValiant_Charger_E49(),
            ChryslerValiant_Pacer(),
        };
    }

    // ─── Holden ───────────────────────────────────────────────────────────────

    static AussieCarDefinition HoldenTorana_LJ() => new AussieCarDefinition
    {
        id           = "holden_torana_lj",
        displayName  = "LJ Torana GTR XU-1",
        manufacturer = "Holden",
        year         = 1972,
        bodyStyle    = "Sedan",
        lore         = "The little Torana that terrorised Bathurst. Triple carb 2850 means it screams above all else.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 285f,
            peakTorqueRPM  = 4200f,
            redlineRPM     = 6800f,
            finalDriveRatio = 3.9f,
            gearRatios     = new float[] { 3.2f, 2.1f, 1.5f, 1.1f },
            frontBrakeTorque = 2200f,
            rearBrakeTorque  = 1600f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(1f, 0.4f, 0f), paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 18000,
    };

    static AussieCarDefinition HoldenCommodore_VK() => new AussieCarDefinition
    {
        id           = "holden_commodore_vk",
        displayName  = "VK Commodore Group A SS",
        manufacturer = "Holden",
        year         = 1985,
        bodyStyle    = "Sedan",
        lore         = "The Brock replica that started the Holden cult. Fuel injected 5.0L — proper muscle.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 370f,
            peakTorqueRPM  = 3800f,
            redlineRPM     = 5800f,
            finalDriveRatio = 3.36f,
            gearRatios     = new float[] { 3.4f, 2.1f, 1.4f, 1.0f },
            frontBrakeTorque = 2600f,
            rearBrakeTorque  = 1800f,
        },
        defaultLoadout   = new CarLoadout { paintColour = Color.white, paintFinish = PaintFinish.Gloss },
        unlockedByDefault = true,
        unlockCost       = 0,
    };

    static AussieCarDefinition HoldenCommodore_VN() => new AussieCarDefinition
    {
        id           = "holden_commodore_vn",
        displayName  = "VN Commodore SS",
        manufacturer = "Holden",
        year         = 1989,
        bodyStyle    = "Sedan",
        lore         = "5.0L V8 in a big Aussie box. Classic burnout pad weapon.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 380f,
            peakTorqueRPM  = 3600f,
            redlineRPM     = 5500f,
            finalDriveRatio = 3.45f,
            gearRatios     = new float[] { 3.4f, 2.1f, 1.4f, 1.0f },
            frontBrakeTorque = 2700f,
            rearBrakeTorque  = 1900f,
        },
        defaultLoadout   = new CarLoadout { paintColour = Color.black, paintFinish = PaintFinish.Gloss },
        unlockedByDefault = true,
        unlockCost       = 0,
    };

    static AussieCarDefinition HoldenCommodore_VS() => new AussieCarDefinition
    {
        id           = "holden_commodore_vs",
        displayName  = "VS Commodore SS",
        manufacturer = "Holden",
        year         = 1995,
        bodyStyle    = "Sedan",
        lore         = "The last of the old-school V8 Commodores before the LS era. A Summernats staple.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 400f,
            peakTorqueRPM  = 3800f,
            redlineRPM     = 5800f,
            finalDriveRatio = 3.45f,
            gearRatios     = new float[] { 3.4f, 2.1f, 1.4f, 1.0f },
            frontBrakeTorque = 2800f,
            rearBrakeTorque  = 2000f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.4f, 0f, 0.8f), paintFinish = PaintFinish.Metallic },
        unlockedByDefault = false,
        unlockCost       = 12000,
    };

    static AussieCarDefinition HoldenCommodore_VT() => new AussieCarDefinition
    {
        id           = "holden_commodore_vt",
        displayName  = "VT Commodore SS",
        manufacturer = "Holden",
        year         = 1997,
        bodyStyle    = "Sedan",
        lore         = "LS1 arrives in Australia. The Commodore grew up — and got a lot faster.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 430f,
            peakTorqueRPM  = 4400f,
            redlineRPM     = 6200f,
            finalDriveRatio = 3.46f,
            gearRatios     = new float[] { 3.3f, 2.0f, 1.35f, 1.0f, 0.75f },
            frontBrakeTorque = 3000f,
            rearBrakeTorque  = 2100f,
        },
        defaultLoadout   = new CarLoadout { paintColour = Color.white, paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 14000,
    };

    static AussieCarDefinition HoldenCommodore_VY() => new AussieCarDefinition
    {
        id           = "holden_commodore_vy",
        displayName  = "VY Commodore SS",
        manufacturer = "Holden",
        year         = 2002,
        bodyStyle    = "Sedan",
        lore         = "Refined LS1, improved chassis. The workhorse of the Aussie street scene.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 455f,
            peakTorqueRPM  = 4400f,
            redlineRPM     = 6200f,
            finalDriveRatio = 3.46f,
            gearRatios     = new float[] { 3.3f, 2.0f, 1.35f, 1.0f, 0.75f },
            frontBrakeTorque = 3100f,
            rearBrakeTorque  = 2200f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.1f, 0.1f, 0.1f), paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 15000,
    };

    static AussieCarDefinition HoldenCommodore_VE() => new AussieCarDefinition
    {
        id           = "holden_commodore_ve",
        displayName  = "VE Commodore SS V8",
        manufacturer = "Holden",
        year         = 2006,
        bodyStyle    = "Sedan",
        lore         = "LS2 6.0L, 270kW from the factory. The best-driving Commodore ever built.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 530f,
            peakTorqueRPM  = 4400f,
            redlineRPM     = 6500f,
            finalDriveRatio = 3.46f,
            gearRatios     = new float[] { 4.06f, 2.37f, 1.55f, 1.16f, 0.85f, 0.67f },
            frontBrakeTorque = 3500f,
            rearBrakeTorque  = 2500f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.05f, 0.05f, 0.5f), paintFinish = PaintFinish.Metallic },
        unlockedByDefault = false,
        unlockCost       = 20000,
    };

    static AussieCarDefinition HoldenHSV_GTSR() => new AussieCarDefinition
    {
        id           = "holden_hsv_gtsr",
        displayName  = "HSV GTSR W1",
        manufacturer = "Holden Special Vehicles",
        year         = 2017,
        bodyStyle    = "Sedan",
        lore         = "The last and greatest HSV. 474kW LS9 supercharged V8. Built in Fishermans Bend.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 815f,
            peakTorqueRPM  = 6000f,
            redlineRPM     = 7000f,
            finalDriveRatio = 3.73f,
            gearRatios     = new float[] { 4.06f, 2.37f, 1.55f, 1.16f, 0.85f, 0.67f },
            frontBrakeTorque = 4500f,
            rearBrakeTorque  = 3200f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.6f, 0f, 0f), paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 80000,
    };

    static AussieCarDefinition HoldenMonaro_CV8() => new AussieCarDefinition
    {
        id           = "holden_monaro_cv8",
        displayName  = "Monaro CV8",
        manufacturer = "Holden",
        year         = 2001,
        bodyStyle    = "Coupe",
        lore         = "Two-door legend reborn. 5.7L LS1, rear wheel drive, wide guards. Made for smoke.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 475f,
            peakTorqueRPM  = 4400f,
            redlineRPM     = 6200f,
            finalDriveRatio = 3.46f,
            gearRatios     = new float[] { 3.3f, 2.0f, 1.35f, 1.0f, 0.75f },
            frontBrakeTorque = 3200f,
            rearBrakeTorque  = 2300f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.8f, 0.5f, 0f), paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 25000,
    };

    static AussieCarDefinition HoldenUte_VU() => new AussieCarDefinition
    {
        id           = "holden_ute_vu",
        displayName  = "VU Commodore Ute SS",
        manufacturer = "Holden",
        year         = 2000,
        bodyStyle    = "Ute",
        lore         = "Australia's favourite: a car up front and a ute out back. LS1 power, tray life.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 450f,
            peakTorqueRPM  = 4400f,
            redlineRPM     = 6200f,
            finalDriveRatio = 3.46f,
            gearRatios     = new float[] { 3.3f, 2.0f, 1.35f, 1.0f, 0.75f },
            frontBrakeTorque = 3000f,
            rearBrakeTorque  = 2100f,
        },
        defaultLoadout   = new CarLoadout { paintColour = Color.red, paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 16000,
    };

    // ─── Ford ─────────────────────────────────────────────────────────────────

    static AussieCarDefinition FordFalcon_XY_GT() => new AussieCarDefinition
    {
        id           = "ford_falcon_xy_gt",
        displayName  = "XY Falcon GT-HO Phase III",
        manufacturer = "Ford",
        year         = 1971,
        bodyStyle    = "Sedan",
        lore         = "The fastest production four-door sedan in the world — 1971. 351 Cleveland. Legend.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 480f,
            peakTorqueRPM  = 3800f,
            redlineRPM     = 6000f,
            finalDriveRatio = 3.5f,
            gearRatios     = new float[] { 2.78f, 1.93f, 1.36f, 1.0f },
            frontBrakeTorque = 2400f,
            rearBrakeTorque  = 1700f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.95f, 0.6f, 0f), paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 40000,
    };

    static AussieCarDefinition FordFalcon_XA() => new AussieCarDefinition
    {
        id           = "ford_falcon_xa",
        displayName  = "XA Falcon GT",
        manufacturer = "Ford",
        year         = 1973,
        bodyStyle    = "Sedan",
        lore         = "351 Cleveland in the new razor-edge body. Pure muscle with Aussie attitude.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 460f,
            peakTorqueRPM  = 3800f,
            redlineRPM     = 5800f,
            finalDriveRatio = 3.5f,
            gearRatios     = new float[] { 2.78f, 1.93f, 1.36f, 1.0f },
            frontBrakeTorque = 2400f,
            rearBrakeTorque  = 1700f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0f, 0.3f, 0.7f), paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 30000,
    };

    static AussieCarDefinition FordFalcon_XB_Coupe() => new AussieCarDefinition
    {
        id           = "ford_falcon_xb_coupe",
        displayName  = "XB Falcon GT Hardtop",
        manufacturer = "Ford",
        year         = 1974,
        bodyStyle    = "Coupe",
        lore         = "The last of the big Falcons. Hardtop style, 351 grunt. Mad Max approved.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 465f,
            peakTorqueRPM  = 3800f,
            redlineRPM     = 5800f,
            finalDriveRatio = 3.5f,
            gearRatios     = new float[] { 2.78f, 1.93f, 1.36f, 1.0f },
            frontBrakeTorque = 2400f,
            rearBrakeTorque  = 1700f,
        },
        defaultLoadout   = new CarLoadout { paintColour = Color.black, paintFinish = PaintFinish.Matte },
        unlockedByDefault = false,
        unlockCost       = 35000,
    };

    static AussieCarDefinition FordFalcon_EA() => new AussieCarDefinition
    {
        id           = "ford_falcon_ea",
        displayName  = "EA Falcon",
        manufacturer = "Ford",
        year         = 1988,
        bodyStyle    = "Sedan",
        lore         = "Controversial when new, beloved now. The start of the modern Falcon era.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 290f,
            peakTorqueRPM  = 3800f,
            redlineRPM     = 5800f,
            finalDriveRatio = 3.45f,
            gearRatios     = new float[] { 3.4f, 2.1f, 1.4f, 1.0f },
            frontBrakeTorque = 2500f,
            rearBrakeTorque  = 1700f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.6f, 0.6f, 0.6f), paintFinish = PaintFinish.Gloss },
        unlockedByDefault = true,
        unlockCost       = 0,
    };

    static AussieCarDefinition FordFalcon_EF() => new AussieCarDefinition
    {
        id           = "ford_falcon_ef",
        displayName  = "EF Falcon XR8",
        manufacturer = "Ford",
        year         = 1994,
        bodyStyle    = "Sedan",
        lore         = "302 Windsor V8, factory bodykit, and attitude. Cheap fun on any street.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 380f,
            peakTorqueRPM  = 3800f,
            redlineRPM     = 5800f,
            finalDriveRatio = 3.45f,
            gearRatios     = new float[] { 3.4f, 2.1f, 1.4f, 1.0f },
            frontBrakeTorque = 2700f,
            rearBrakeTorque  = 1900f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.1f, 0.1f, 0.7f), paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 10000,
    };

    static AussieCarDefinition FordFalcon_AU() => new AussieCarDefinition
    {
        id           = "ford_falcon_au",
        displayName  = "AU Falcon XR8",
        manufacturer = "Ford",
        year         = 1998,
        bodyStyle    = "Sedan",
        lore         = "Controversial styling, brilliant mechanicals. Intech V8, stiff chassis.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 420f,
            peakTorqueRPM  = 4000f,
            redlineRPM     = 6200f,
            finalDriveRatio = 3.45f,
            gearRatios     = new float[] { 3.4f, 2.1f, 1.4f, 1.0f, 0.75f },
            frontBrakeTorque = 2900f,
            rearBrakeTorque  = 2000f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.9f, 0.9f, 0.9f), paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 12000,
    };

    static AussieCarDefinition FordFalcon_BA_XR8() => new AussieCarDefinition
    {
        id           = "ford_falcon_ba_xr8",
        displayName  = "BA Falcon XR8",
        manufacturer = "Ford",
        year         = 2002,
        bodyStyle    = "Sedan",
        lore         = "260kW Barra V8. Proper balance, proper grunt. The sweet spot Falcon.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 470f,
            peakTorqueRPM  = 4250f,
            redlineRPM     = 6500f,
            finalDriveRatio = 3.45f,
            gearRatios     = new float[] { 4.06f, 2.37f, 1.55f, 1.16f, 0.85f, 0.67f },
            frontBrakeTorque = 3200f,
            rearBrakeTorque  = 2200f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.5f, 0f, 0f), paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 18000,
    };

    static AussieCarDefinition FordFalcon_FG_XR6T() => new AussieCarDefinition
    {
        id           = "ford_falcon_fg_xr6t",
        displayName  = "FG Falcon XR6 Turbo",
        manufacturer = "Ford",
        year         = 2008,
        bodyStyle    = "Sedan",
        lore         = "270kW factory. 500kW with a tune and supporting mods. The Barra is limitless.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 533f,
            peakTorqueRPM  = 2200f,
            redlineRPM     = 6000f,
            finalDriveRatio = 3.46f,
            gearRatios     = new float[] { 4.06f, 2.37f, 1.55f, 1.16f, 0.85f, 0.67f },
            frontBrakeTorque = 3400f,
            rearBrakeTorque  = 2400f,
        },
        defaultLoadout   = new CarLoadout { paintColour = Color.white, paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 22000,
    };

    static AussieCarDefinition FordFPV_GT_F() => new AussieCarDefinition
    {
        id           = "ford_fpv_gt_f",
        displayName  = "FPV GT F 335",
        manufacturer = "Ford Performance Vehicles",
        year         = 2014,
        bodyStyle    = "Sedan",
        lore         = "335kW supercharged 5.0L Coyote V8. The last FPV. They saved the best for last.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 570f,
            peakTorqueRPM  = 4500f,
            redlineRPM     = 7000f,
            finalDriveRatio = 3.73f,
            gearRatios     = new float[] { 4.06f, 2.37f, 1.55f, 1.16f, 0.85f, 0.67f },
            frontBrakeTorque = 4200f,
            rearBrakeTorque  = 3000f,
        },
        defaultLoadout   = new CarLoadout { paintColour = Color.black, paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 60000,
    };

    static AussieCarDefinition FordUte_FG() => new AussieCarDefinition
    {
        id           = "ford_ute_fg",
        displayName  = "FG Falcon Ute XR6 Turbo",
        manufacturer = "Ford",
        year         = 2009,
        bodyStyle    = "Ute",
        lore         = "Barra power in a ute. Tray full of burnout trophies.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 533f,
            peakTorqueRPM  = 2200f,
            redlineRPM     = 6000f,
            finalDriveRatio = 3.46f,
            gearRatios     = new float[] { 4.06f, 2.37f, 1.55f, 1.16f, 0.85f, 0.67f },
            frontBrakeTorque = 3300f,
            rearBrakeTorque  = 2300f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.2f, 0.2f, 0.2f), paintFinish = PaintFinish.Matte },
        unlockedByDefault = false,
        unlockCost       = 20000,
    };

    // ─── Chrysler / Valiant ───────────────────────────────────────────────────

    static AussieCarDefinition ChryslerValiant_Charger_E49() => new AussieCarDefinition
    {
        id           = "chrysler_valiant_charger_e49",
        displayName  = "Valiant Charger E49",
        manufacturer = "Chrysler Australia",
        year         = 1972,
        bodyStyle    = "Coupe",
        lore         = "265 Hemi Six with triple Webers. 'When you're hot you're hot' — and this is always hot.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 370f,
            peakTorqueRPM  = 4200f,
            redlineRPM     = 6400f,
            finalDriveRatio = 3.55f,
            gearRatios     = new float[] { 2.65f, 1.93f, 1.36f, 1.0f },
            frontBrakeTorque = 2200f,
            rearBrakeTorque  = 1600f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.9f, 0.7f, 0.1f), paintFinish = PaintFinish.Gloss },
        unlockedByDefault = false,
        unlockCost       = 35000,
    };

    static AussieCarDefinition ChryslerValiant_Pacer() => new AussieCarDefinition
    {
        id           = "chrysler_valiant_pacer",
        displayName  = "Valiant Pacer",
        manufacturer = "Chrysler Australia",
        year         = 1963,
        bodyStyle    = "Sedan",
        lore         = "The original Aussie Valiant. Slant six, tank-like, surprisingly driftable.",
        factorySpec  = new PerformanceSpec
        {
            peakTorque     = 240f,
            peakTorqueRPM  = 3600f,
            redlineRPM     = 5500f,
            finalDriveRatio = 3.23f,
            gearRatios     = new float[] { 2.65f, 1.93f, 1.36f, 1.0f },
            frontBrakeTorque = 1800f,
            rearBrakeTorque  = 1200f,
        },
        defaultLoadout   = new CarLoadout { paintColour = new Color(0.7f, 0.85f, 0.7f), paintFinish = PaintFinish.Gloss },
        unlockedByDefault = true,
        unlockCost       = 0,
    };
}
