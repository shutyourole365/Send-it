using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Garage screen controller.
/// Handles car selection, visual customisation, performance part fitting,
/// currency display, and stat comparison.
/// Attach to the Garage scene's root UI canvas.
/// </summary>
public class GarageUI : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────────────────────
    [Header("Systems")]
    public CarCustomisation customisation;
    public CarDatabase       carDatabase;
    public SaveSystem        saveSystem;

    // ─── Top Bar ─────────────────────────────────────────────────────────────
    [Header("Top Bar")]
    public TextMeshProUGUI currencyText;
    public TextMeshProUGUI carNameText;
    public Button          enterEventButton;

    // ─── Tab Buttons ─────────────────────────────────────────────────────────
    [Header("Tabs")]
    public Button tabCars;
    public Button tabPaint;
    public Button tabBodyKit;
    public Button tabWheels;
    public Button tabEngine;
    public Button tabDrivetrain;
    public Button tabSuspension;
    public Button tabBrakes;
    public Button tabTyres;

    [Header("Tab Panels")]
    public GameObject panelCars;
    public GameObject panelPaint;
    public GameObject panelBodyKit;
    public GameObject panelWheels;
    public GameObject panelEngine;
    public GameObject panelDrivetrain;
    public GameObject panelSuspension;
    public GameObject panelBrakes;
    public GameObject panelTyres;

    // ─── Car Select Panel ─────────────────────────────────────────────────────
    [Header("Car Select")]
    public Transform       carCardContainer;
    public GameObject      carCardPrefab;
    public TextMeshProUGUI carLoreText;
    public TextMeshProUGUI carYearManufacturerText;
    public Button          buyCarButton;
    public TextMeshProUGUI buyCarButtonText;

    // ─── Paint Panel ─────────────────────────────────────────────────────────
    [Header("Paint")]
    public Slider          hueSlider;
    public Slider          saturationSlider;
    public Slider          valueSlider;
    public Image           paintPreviewSwatch;
    public Button[]        finishButtons;         // One per PaintFinish enum value
    public TextMeshProUGUI selectedFinishText;

    // ─── Body Kit Panel ───────────────────────────────────────────────────────
    [Header("Body Kit")]
    public Transform       bodyKitContainer;
    public GameObject      bodyKitOptionPrefab;

    // ─── Wheels Panel ─────────────────────────────────────────────────────────
    [Header("Wheels")]
    public Transform       wheelContainer;
    public GameObject      wheelOptionPrefab;
    public Slider          rimHueSlider;
    public Slider          rimSatSlider;
    public Slider          rimValSlider;
    public Image           rimColourSwatch;

    // ─── Performance Panels ───────────────────────────────────────────────────
    [Header("Performance Parts")]
    public Transform       engineContainer;
    public Transform       turboContainer;
    public Transform       gearboxContainer;
    public Transform       diffContainer;
    public Transform       suspensionContainer;
    public Transform       brakeContainer;
    public Transform       tyreContainer;
    public GameObject      partCardPrefab;

    // ─── Stat Bar ─────────────────────────────────────────────────────────────
    [Header("Stat Bars")]
    public Slider statPower;
    public Slider statTorque;
    public Slider statGrip;
    public Slider statBraking;
    public Slider statHandling;
    public TextMeshProUGUI statPowerText;
    public TextMeshProUGUI statTorqueText;

    // ─── Private ─────────────────────────────────────────────────────────────
    private AussieCarDefinition selectedCar;
    private PaintFinish         selectedFinish = PaintFinish.Gloss;
    private Color               currentPaintColour;
    private GameObject[]        allPanels;
    private List<GameObject>    spawnedCards = new List<GameObject>();

    // ─── Unity ───────────────────────────────────────────────────────────────
    void Start()
    {
        allPanels = new[] {
            panelCars, panelPaint, panelBodyKit, panelWheels,
            panelEngine, panelDrivetrain, panelSuspension, panelBrakes, panelTyres
        };

        BindTabButtons();
        BindPaintSliders();
        BindRimSliders();

        ShowTab(panelCars);
        PopulateCarList();
        RefreshCurrency();
        RefreshStats();
    }

    // ─── Tabs ─────────────────────────────────────────────────────────────────
    void BindTabButtons()
    {
        tabCars       .onClick.AddListener(() => { ShowTab(panelCars);        PopulateCarList(); });
        tabPaint      .onClick.AddListener(() => { ShowTab(panelPaint);       RefreshPaintPanel(); });
        tabBodyKit    .onClick.AddListener(() => { ShowTab(panelBodyKit);     PopulateBodyKit(); });
        tabWheels     .onClick.AddListener(() => { ShowTab(panelWheels);      PopulateWheels(); });
        tabEngine     .onClick.AddListener(() => { ShowTab(panelEngine);      PopulateParts(engineContainer,    customisation.engineCatalogue,    OnEnginePurchase); });
        tabDrivetrain .onClick.AddListener(() => { ShowTab(panelDrivetrain);  PopulateParts(turboContainer,    customisation.turboCatalogue,     OnTurboPurchase);
                                                                              PopulateParts(gearboxContainer,  customisation.gearboxCatalogue,   OnGearboxPurchase);
                                                                              PopulateParts(diffContainer,     customisation.diffCatalogue,      OnDiffPurchase); });
        tabSuspension .onClick.AddListener(() => { ShowTab(panelSuspension);  PopulateParts(suspensionContainer, customisation.suspensionCatalogue, OnSuspensionPurchase); });
        tabBrakes     .onClick.AddListener(() => { ShowTab(panelBrakes);      PopulateParts(brakeContainer,    customisation.brakeCatalogue,     OnBrakePurchase); });
        tabTyres      .onClick.AddListener(() => { ShowTab(panelTyres);       PopulateParts(tyreContainer,     customisation.tyreCatalogue,      OnTyrePurchase); });
    }

    void ShowTab(GameObject panel)
    {
        foreach (var p in allPanels)
            if (p != null) p.SetActive(false);
        if (panel != null) panel.SetActive(true);
    }

    // ─── Car List ─────────────────────────────────────────────────────────────
    void PopulateCarList()
    {
        foreach (var c in spawnedCards) Destroy(c);
        spawnedCards.Clear();

        if (carDatabase == null) return;

        foreach (var car in carDatabase.cars)
        {
            var card = Instantiate(carCardPrefab, carCardContainer);
            spawnedCards.Add(card);

            var nameLabel = card.GetComponentInChildren<TextMeshProUGUI>();
            if (nameLabel) nameLabel.text = car.displayName;

            var thumb = card.transform.Find("Thumbnail")?.GetComponent<Image>();
            if (thumb && car.thumbnail) thumb.sprite = car.thumbnail;

            var btn = card.GetComponent<Button>();
            var capturedCar = car;
            if (btn) btn.onClick.AddListener(() => SelectCar(capturedCar));

            // Lock icon for unowned
            var lockIcon = card.transform.Find("LockIcon")?.gameObject;
            if (lockIcon) lockIcon.SetActive(!car.unlockedByDefault);
        }
    }

    void SelectCar(AussieCarDefinition car)
    {
        selectedCar = car;

        if (carNameText)              carNameText.text             = car.displayName;
        if (carLoreText)              carLoreText.text             = car.lore;
        if (carYearManufacturerText)  carYearManufacturerText.text = $"{car.year}  ·  {car.manufacturer}";

        bool owned = car.unlockedByDefault || saveSystem.IsCarOwned(car.id);
        if (buyCarButton)     buyCarButton.gameObject.SetActive(!owned);
        if (buyCarButtonText) buyCarButtonText.text = $"BUY  ${car.unlockCost:N0}";

        if (buyCarButton) buyCarButton.onClick.RemoveAllListeners();
        if (buyCarButton) buyCarButton.onClick.AddListener(() => TryBuyCar(car));

        if (owned && GameManager.Instance != null)
        {
            GameManager.Instance.currentCarId = car.id;
            saveSystem.Save();
        }

        RefreshStats();
    }

    void TryBuyCar(AussieCarDefinition car)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.SpendCurrency(car.unlockCost))
        {
            saveSystem.UnlockCar(car.id);
            SelectCar(car);
            RefreshCurrency();
            Debug.Log($"[Garage] Purchased {car.displayName}");
        }
        else
        {
            Debug.Log("[Garage] Not enough currency.");
            // Trigger insufficient funds UI flash here
        }
    }

    // ─── Paint ────────────────────────────────────────────────────────────────
    void RefreshPaintPanel()
    {
        if (customisation == null) return;
        Color c = customisation.loadout.paintColour;
        Color.RGBToHSV(c, out float h, out float s, out float v);
        if (hueSlider) hueSlider.value = h;
        if (saturationSlider) saturationSlider.value = s;
        if (valueSlider) valueSlider.value = v;
        UpdatePaintSwatch();
    }

    void BindPaintSliders()
    {
        if (hueSlider)        hueSlider.onValueChanged.AddListener(_        => OnPaintSliderChanged());
        if (saturationSlider) saturationSlider.onValueChanged.AddListener(_ => OnPaintSliderChanged());
        if (valueSlider)      valueSlider.onValueChanged.AddListener(_      => OnPaintSliderChanged());

        for (int i = 0; i < finishButtons.Length; i++)
        {
            int captured = i;
            finishButtons[i]?.onClick.AddListener(() => SelectFinish((PaintFinish)captured));
        }
    }

    void OnPaintSliderChanged()
    {
        float h = hueSlider        ? hueSlider.value        : 0f;
        float s = saturationSlider ? saturationSlider.value : 1f;
        float v = valueSlider      ? valueSlider.value      : 1f;
        currentPaintColour = Color.HSVToRGB(h, s, v);
        UpdatePaintSwatch();
        customisation?.SetPaint(currentPaintColour, selectedFinish);
        RefreshStats();
    }

    void SelectFinish(PaintFinish finish)
    {
        selectedFinish = finish;
        if (selectedFinishText) selectedFinishText.text = finish.ToString().ToUpper();
        customisation?.SetPaint(currentPaintColour, finish);
    }

    void UpdatePaintSwatch()
    {
        if (paintPreviewSwatch) paintPreviewSwatch.color = currentPaintColour;
    }

    // ─── Body Kit ─────────────────────────────────────────────────────────────
    void PopulateBodyKit()
    {
        if (bodyKitContainer == null || bodyKitOptionPrefab == null) return;

        foreach (Transform child in bodyKitContainer) Destroy(child.gameObject);

        string[] slotNames = System.Enum.GetNames(typeof(BodyKitSlot));
        for (int i = 0; i < slotNames.Length; i++)
        {
            var option   = Instantiate(bodyKitOptionPrefab, bodyKitContainer);
            var label    = option.GetComponentInChildren<TextMeshProUGUI>();
            if (label) label.text = slotNames[i];

            var prevBtn = option.transform.Find("PrevButton")?.GetComponent<Button>();
            var nextBtn = option.transform.Find("NextButton")?.GetComponent<Button>();
            int slotIndex = i;

            prevBtn?.onClick.AddListener(() => CycleBodyKitSlot((BodyKitSlot)slotIndex, -1));
            nextBtn?.onClick.AddListener(() => CycleBodyKitSlot((BodyKitSlot)slotIndex, +1));
        }
    }

    void CycleBodyKitSlot(BodyKitSlot slot, int dir)
    {
        if (customisation == null) return;
        int current = GetBodyKitIndex(slot);
        int next    = current + dir;
        customisation.SetBodyKit(slot, next);
    }

    int GetBodyKitIndex(BodyKitSlot slot)
    {
        if (customisation == null) return 0;
        var l = customisation.loadout;
        return slot switch {
            BodyKitSlot.FrontBumper => l.frontBumperIndex,
            BodyKitSlot.RearBumper  => l.rearBumperIndex,
            BodyKitSlot.SideSkirts  => l.sideSkirtsIndex,
            BodyKitSlot.Bonnet      => l.bonnetsIndex,
            BodyKitSlot.Spoiler     => l.spoilerIndex,
            BodyKitSlot.ExhaustTip  => l.exhaustTipIndex,
            BodyKitSlot.RollCage    => l.rollCageIndex,
            BodyKitSlot.Seats       => l.seatsIndex,
            _ => 0
        };
    }

    // ─── Wheels ───────────────────────────────────────────────────────────────
    void PopulateWheels()
    {
        if (wheelContainer == null || customisation == null) return;
        foreach (Transform child in wheelContainer) Destroy(child.gameObject);

        for (int i = 0; i < customisation.wheelCatalogue.Count; i++)
        {
            var wheel = customisation.wheelCatalogue[i];
            var card  = Instantiate(wheelOptionPrefab, wheelContainer);
            var label = card.GetComponentInChildren<TextMeshProUGUI>();
            if (label) label.text = $"{wheel.partName}\n{wheel.wheelDiameterInch}\" × {wheel.wheelWidthInch}\"";

            var btn = card.GetComponent<Button>();
            int idx = i;
            btn?.onClick.AddListener(() => { customisation.SetWheel(idx); RefreshStats(); });
        }
    }

    void BindRimSliders()
    {
        if (rimHueSlider) rimHueSlider.onValueChanged.AddListener(_ => OnRimColourChanged());
        if (rimSatSlider) rimSatSlider.onValueChanged.AddListener(_ => OnRimColourChanged());
        if (rimValSlider) rimValSlider.onValueChanged.AddListener(_ => OnRimColourChanged());
    }

    void OnRimColourChanged()
    {
        float h = rimHueSlider ? rimHueSlider.value : 0f;
        float s = rimSatSlider ? rimSatSlider.value : 0f;
        float v = rimValSlider ? rimValSlider.value : 1f;
        Color c = Color.HSVToRGB(h, s, v);
        if (rimColourSwatch) rimColourSwatch.color = c;
        customisation?.SetRimColour(c);
    }

    // ─── Performance Parts ────────────────────────────────────────────────────
    void PopulateParts<T>(Transform container, List<T> catalogue, System.Action<int> onBuy) where T : class
    {
        if (container == null || partCardPrefab == null) return;
        foreach (Transform child in container) Destroy(child.gameObject);

        for (int i = 0; i < catalogue.Count; i++)
        {
            var card  = Instantiate(partCardPrefab, container);
            var texts = card.GetComponentsInChildren<TextMeshProUGUI>();
            var btn   = card.GetComponent<Button>();

            // Use reflection to pull partName, description, cost from any part type
            var type = catalogue[i].GetType();
            string name = type.GetField("partName")?.GetValue(catalogue[i])?.ToString()  ?? "";
            string desc = type.GetField("description")?.GetValue(catalogue[i])?.ToString() ?? "";
            int cost    = (int)(type.GetField("cost")?.GetValue(catalogue[i]) ?? 0);

            if (texts.Length > 0) texts[0].text = name;
            if (texts.Length > 1) texts[1].text = desc;
            if (texts.Length > 2) texts[2].text = $"${cost:N0}";

            int idx = i;
            btn?.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null && GameManager.Instance.SpendCurrency(cost))
                {
                    onBuy(idx);
                    RefreshCurrency();
                    RefreshStats();
                    saveSystem?.Save();
                }
            });
        }
    }

    void OnEnginePurchase(int idx)    => customisation.SetEnginePart(idx);
    void OnTurboPurchase(int idx)     => customisation.SetTurboPart(idx);
    void OnGearboxPurchase(int idx)   => customisation.SetGearboxPart(idx);
    void OnDiffPurchase(int idx)      => customisation.SetDiffPart(idx);
    void OnSuspensionPurchase(int idx)=> customisation.SetSuspensionPart(idx);
    void OnBrakePurchase(int idx)     => customisation.SetBrakePart(idx);
    void OnTyrePurchase(int idx)      => customisation.SetTyrePart(idx);

    // ─── Stats ────────────────────────────────────────────────────────────────
    void RefreshStats()
    {
        if (customisation == null || customisation.vehicle == null) return;
        var v = customisation.vehicle;

        float maxTorque = 900f;
        float maxPower  = 600f;

        if (statTorque)     statTorque.value    = v.peakTorque / maxTorque;
        if (statPower)      statPower.value      = (v.peakTorque * v.redlineRPM / 9549f) / maxPower;
        if (statTorqueText) statTorqueText.text  = $"{v.peakTorque:F0} Nm";
        if (statPowerText)  statPowerText.text   = $"{v.peakTorque * v.redlineRPM / 9549f:F0} kW";

        // Grip and braking — normalise against known max
        if (statGrip)    statGrip.value    = customisation.loadout.tyreIndex >= 0
            && customisation.loadout.tyreIndex < customisation.tyreCatalogue.Count
            ? customisation.tyreCatalogue[customisation.loadout.tyreIndex].gripFactor : 0.8f;

        if (statBraking) statBraking.value = Mathf.Clamp01(v.frontBrakeTorque / 5000f);
        if (statHandling) statHandling.value = Mathf.Clamp01(1f - (v.suspensionSpring - 20000f) / 60000f);
    }

    // ─── Currency ─────────────────────────────────────────────────────────────
    void RefreshCurrency()
    {
        if (currencyText && GameManager.Instance != null)
            currencyText.text = $"${GameManager.Instance.currency:N0}";
    }

    // ─── Enter Event ──────────────────────────────────────────────────────────
    public void OnEnterEventPressed()
    {
        saveSystem?.Save();
        GameManager.Instance?.GoToFreeRoam();
    }
}
