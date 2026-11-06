using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CraftingController : MonoBehaviour
{
    [Header("Material Slots")]
    public InventorySlot[] materialSlots;
    [Tooltip("Optional 5x5 grid coordinates for each material slot. If empty, slots follow the active recipe expectedPositions order.")]
    public Vector2Int[] materialSlotGridPositions;
    public int fallbackGridColumnCount = 5;

    [Header("Result Slots")]
    public InventorySlot[] resultSlots;
    [Tooltip("Clears any serialized/test result items when the crafting UI is first initialized. Crafted parts are added only by CreateParts().")]
    public bool clearResultSlotsOnStart = true;

    [Header("Preview")]
    public RawImage previewImage;
    public Button createButton;

    [Header("Feedback")]
    public TextMeshProUGUI feedbackText;
    public string wrongPlacementMessage = "nope";
    public Color wrongPlacementColor = new Color(1f, 0.18f, 0.12f, 1f);

    [Header("Module Upgrade Details")]
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI defenseText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI detailText;
    public TextMeshProUGUI moduleUpgradeSummaryText;
    public TextMeshProUGUI moduleUpgradeHeaderText;
    public TextMeshProUGUI attackValueText;
    public TextMeshProUGUI defenseValueText;
    public TextMeshProUGUI speedValueText;
    public TextMeshProUGUI detailHeaderText;
    private Transform moduleUpgradePanel;

    [Header("Legacy Recipes")]
    public CraftRecipeSO[] recipes;

    [Header("AETHER Inventory Recipes")]
    public PlayerInventory playerInventory;
    public CraftingRecipeData[] partRecipes;
    public bool autoLoadPartRecipes = true;
    public bool consumePlayerInventoryOnCreate = true;
    public bool addResultToPlayerInventory = true;

    private CraftRecipeSO currentRecipe;
    private CraftingRecipeData currentPartRecipe;
    private CraftingRecipeData activePartRecipeOverride;
    private bool isCrafting;
    private readonly Dictionary<ItemData, CraftItemDataSO> runtimeItems = new Dictionary<ItemData, CraftItemDataSO>();
    private readonly List<EventTrigger> resultSlotClickHandlers = new List<EventTrigger>();

    private void Start()
    {
        ResolveReferences();
        if (clearResultSlotsOnStart)
            ClearResultSlots();
        ClearPreview();
        ClearModuleUpgradeDetails();
        currentRecipe = null;
        currentPartRecipe = null;
    }

    private void OnEnable()
    {
        ResolveReferences();
        OnMaterialsChanged();
    }

    public void OnMaterialsChanged()
    {
        ResolveReferences();

        List<CraftItemDataSO> items = new List<CraftItemDataSO>();
        if (materialSlots != null)
        {
            foreach (InventorySlot slot in materialSlots)
            {
                if (slot != null && !slot.IsEmpty())
                    items.Add(slot.itemData);
            }
        }

        currentRecipe = null;
        currentPartRecipe = FindMatchingPartRecipe(items);

        if (currentPartRecipe != null)
        {
            bool hasRequiredModules = playerInventory == null || HasRequiredModules(currentPartRecipe);
            bool gridMatches = MaterialGridMatchesRecipe(currentPartRecipe);

            if (hasRequiredModules && gridMatches)
            {
                ShowPreview(currentPartRecipe.targetPart);
                HideFeedback();
            }
            else
            {
                ClearPreview();
                if (ShouldShowWrongPlacementFeedback(currentPartRecipe, items, hasRequiredModules, gridMatches))
                    ShowFeedback(wrongPlacementMessage);
                else
                    HideFeedback();
            }

            UpdateCreateButtonState();
            return;
        }

        if (activePartRecipeOverride != null)
        {
            ClearPreview();
            HideFeedback();
            UpdateCreateButtonState();
            return;
        }

        if (recipes != null)
        {
            foreach (CraftRecipeSO recipe in recipes)
            {
                if (recipe == null) continue;

                if (IsRecipeMatch(recipe.requiredItems, items))
                {
                    currentRecipe = recipe;
                    ShowPreview(recipe.resultItem);
                    UpdateCreateButtonState();
                    return;
                }
            }
        }

        ClearPreview();
        HideFeedback();
        UpdateCreateButtonState();
    }

    public CraftItemDataSO GetOrCreateRuntimeItem(ItemData item)
    {
        if (item == null)
            return null;

        if (runtimeItems.TryGetValue(item, out CraftItemDataSO existing) && existing != null)
            return existing;

        CraftItemDataSO data = ScriptableObject.CreateInstance<CraftItemDataSO>();
        data.name = item.name + "_CraftRuntime";
        data.itemId = !string.IsNullOrWhiteSpace(item.id) ? item.id : item.name;
        data.itemName = !string.IsNullOrWhiteSpace(item.displayName) ? item.displayName : item.name;
        data.description = item.description;
        data.itemType = item is PartData ? CraftItemType.Module : CraftItemType.Material;
        data.linkedInventoryItem = item;
        data.linkedPartData = item as PartData;
        if (item.itemSprite != null)
        {
            data.iconTexture = item.itemSprite.texture;
            data.previewTexture = item.itemSprite.texture;
        }

        runtimeItems[item] = data;
        return data;
    }

    public void SetActivePartRecipe(CraftingRecipeData recipe)
    {
        activePartRecipeOverride = recipe;
        OnMaterialsChanged();
    }

    private bool IsRecipeMatch(List<CraftItemDataSO> recipe, List<CraftItemDataSO> current)
    {
        if (recipe == null || recipe.Count != current.Count)
            return false;

        foreach (CraftItemDataSO item in recipe)
        {
            if (!current.Contains(item))
                return false;
        }

        return true;
    }

    private void ShowPreview(CraftItemDataSO result)
    {
        if (result == null)
            return;

        if (previewImage != null)
        {
            previewImage.texture = result.GetPreviewTexture();
            previewImage.enabled = true;
        }

        ShowModuleUpgradeDetails(result);
        HideFeedback();
    }

    private void ShowPreview(PartData result)
    {
        if (result == null)
            return;

        if (previewImage != null)
        {
            previewImage.texture = result.itemSprite != null ? result.itemSprite.texture : null;
            previewImage.enabled = true;
        }

        ShowModuleUpgradeDetails(result);
        HideFeedback();
    }

    private void ClearPreview()
    {
        if (previewImage != null)
        {
            previewImage.texture = null;
            previewImage.enabled = false;
        }

        ClearModuleUpgradeDetails();
    }

    public void CreateParts()
    {
        if (isCrafting)
            return;

        isCrafting = true;

        if (currentPartRecipe != null)
        {
            CreatePartFromInventoryRecipe();
            isCrafting = false;
            return;
        }

        if (currentRecipe == null)
        {
            isCrafting = false;
            return;
        }

        InventorySlot target = FindEmptyResultSlot();
        if (target == null)
        {
            isCrafting = false;
            return;
        }

        target.SetItem(currentRecipe.resultItem);

        if (materialSlots != null)
        {
            foreach (InventorySlot slot in materialSlots)
            {
                if (slot != null)
                    slot.Clear();
            }
        }

        ClearPreview();
        currentRecipe = null;
        isCrafting = false;
    }

    private void CreatePartFromInventoryRecipe()
    {
        if (currentPartRecipe == null || currentPartRecipe.targetPart == null)
            return;

        if (playerInventory == null)
            playerInventory = PartSystemRuntimeState.GetOrBindBestInventory();

        if (playerInventory == null)
        {
            Debug.LogWarning("[CraftingController] PlayerInventory is missing.");
            return;
        }

        if (!HasRequiredModules(currentPartRecipe))
        {
            Debug.LogWarning("[CraftingController] Not enough module items for recipe: " + currentPartRecipe.recipeId);
            ShowFeedback(wrongPlacementMessage);
            OnMaterialsChanged();
            return;
        }

        if (!MaterialGridMatchesRecipe(currentPartRecipe))
        {
            Debug.LogWarning("[CraftingController] Module placement does not match recipe: " + currentPartRecipe.recipeId);
            ShowFeedback(wrongPlacementMessage);
            OnMaterialsChanged();
            return;
        }

        if (consumePlayerInventoryOnCreate)
        {
            foreach (string id in currentPartRecipe.requiredModuleIds)
                ConsumeInventoryById(id, 1);
        }

        if (addResultToPlayerInventory && !playerInventory.AddItem(currentPartRecipe.targetPart, 1))
        {
            Debug.LogWarning("[CraftingController] Crafted part could not be added to inventory: " + currentPartRecipe.targetPart.displayName);
            return;
        }

        PartData craftedPart = currentPartRecipe.targetPart;
        PartSystemRuntimeState.RegisterCraftedEquipmentPart(craftedPart);
        RefreshEquipmentUIsForCraftedPart(craftedPart);
        InventorySlot target = FindEmptyResultSlot();
        if (target != null)
        {
            target.SetItem(GetOrCreateRuntimeItem(craftedPart));
        }

        if (materialSlots != null)
        {
            foreach (InventorySlot slot in materialSlots)
            {
                if (slot != null)
                    slot.Clear();
            }
        }

        ClearPreview();
        HideFeedback();
        if (target != null)
            ShowModuleUpgradeDetails(craftedPart);

        currentPartRecipe = null;
        currentRecipe = null;
        UpdateCreateButtonState();

        PartsCraftingInventoryBridge bridge = GetComponent<PartsCraftingInventoryBridge>();
        if (bridge == null)
            bridge = GetComponentInChildren<PartsCraftingInventoryBridge>(true);
        if (bridge != null)
            bridge.RefreshFromInventory();
    }

    private void RefreshEquipmentUIsForCraftedPart(PartData craftedPart)
    {
        EquipmentUIController[] controllers = Resources.FindObjectsOfTypeAll<EquipmentUIController>();
        if (controllers == null)
            return;

        for (int i = 0; i < controllers.Length; i++)
        {
            EquipmentUIController controller = controllers[i];
            if (controller == null || string.IsNullOrEmpty(controller.gameObject.scene.name))
                continue;

            controller.SetSourceInventory(playerInventory);
            controller.AddCraftedPartToSlots(craftedPart);
        }
    }

    private CraftingRecipeData FindMatchingPartRecipe(List<CraftItemDataSO> items)
    {
        if (items == null || items.Count == 0 || partRecipes == null)
            return null;

        List<string> currentIds = items
            .Where(item => item != null)
            .Select(item => ItemNameRuleUtility.NormalizeName(item.GetInventoryId()))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        IEnumerable<CraftingRecipeData> candidates = activePartRecipeOverride != null
            ? new[] { activePartRecipeOverride }
            : partRecipes;

        foreach (CraftingRecipeData recipe in candidates)
        {
            if (recipe == null || recipe.requiredModuleIds == null)
                continue;

            List<string> required = recipe.requiredModuleIds
                .Select(ItemNameRuleUtility.NormalizeName)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            if (required.Count != currentIds.Count)
                continue;

            bool matched = true;
            foreach (string requiredId in required)
            {
                if (!currentIds.Contains(requiredId))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
                return recipe;
        }

        return null;
    }

    private bool ShouldShowWrongPlacementFeedback(CraftingRecipeData recipe, List<CraftItemDataSO> items, bool hasRequiredModules, bool gridMatches)
    {
        if (recipe == null || recipe.expectedPositions == null || items == null)
            return false;

        return hasRequiredModules && !gridMatches && items.Count >= recipe.expectedPositions.Length;
    }

    private bool MaterialGridMatchesRecipe(CraftingRecipeData recipe)
    {
        return PartCraftingGridManager.MatchesRecipe(recipe, BuildMaterialGridPlacement(recipe));
    }

    private Dictionary<Vector2Int, string> BuildMaterialGridPlacement(CraftingRecipeData recipe)
    {
        Dictionary<Vector2Int, string> placed = new Dictionary<Vector2Int, string>();
        if (materialSlots == null)
            return placed;

        for (int i = 0; i < materialSlots.Length; i++)
        {
            InventorySlot slot = materialSlots[i];
            if (slot == null || slot.IsEmpty() || slot.itemData == null)
                continue;

            string itemId = slot.itemData.GetInventoryId();
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            placed[GetMaterialSlotGridPosition(recipe, i)] = itemId;
        }

        return placed;
    }

    private Vector2Int GetMaterialSlotGridPosition(CraftingRecipeData recipe, int slotIndex)
    {
        if (materialSlotGridPositions != null
            && slotIndex >= 0
            && slotIndex < materialSlotGridPositions.Length)
            return materialSlotGridPositions[slotIndex];

        if (recipe != null
            && recipe.expectedPositions != null
            && slotIndex >= 0
            && slotIndex < recipe.expectedPositions.Length)
            return recipe.expectedPositions[slotIndex].position;

        int columns = Mathf.Max(1, fallbackGridColumnCount);
        return new Vector2Int(slotIndex % columns, slotIndex / columns);
    }

    private bool HasRequiredModules(CraftingRecipeData recipe)
    {
        if (playerInventory == null || recipe == null || recipe.requiredModuleIds == null)
            return false;

        foreach (string id in recipe.requiredModuleIds)
        {
            if (CountInventoryById(id) <= 0)
                return false;
        }

        return true;
    }

    private int CountInventoryById(string itemId)
    {
        string normalized = ItemNameRuleUtility.NormalizeName(itemId);
        int count = 0;
        for (int i = 0; i < playerInventory.slots.Count; i++)
        {
            PlayerInventory.Slot slot = playerInventory.slots[i];
            if (slot != null && slot.item != null && slot.count > 0 && ItemNameRuleUtility.GetBestItemId(slot.item) == normalized)
                count += slot.count;
        }

        return count;
    }

    private bool ConsumeInventoryById(string itemId, int amount)
    {
        string normalized = ItemNameRuleUtility.NormalizeName(itemId);
        int remaining = amount;

        for (int i = 0; i < playerInventory.slots.Count && remaining > 0; i++)
        {
            PlayerInventory.Slot slot = playerInventory.slots[i];
            if (slot == null || slot.item == null || slot.count <= 0 || ItemNameRuleUtility.GetBestItemId(slot.item) != normalized)
                continue;

            int used = Mathf.Min(slot.count, remaining);
            slot.count -= used;
            remaining -= used;

            if (slot.count <= 0)
            {
                slot.item = null;
                slot.count = 0;
            }
        }

        if (remaining == 0)
        {
            playerInventory.NotifyInventoryChanged();
            return true;
        }

        return false;
    }

    private InventorySlot FindEmptyResultSlot()
    {
        if (resultSlots == null)
            return null;

        foreach (InventorySlot slot in resultSlots)
        {
            if (slot != null && slot.IsEmpty())
                return slot;
        }

        return null;
    }

    private void ClearResultSlots()
    {
        if (resultSlots == null)
            return;

        foreach (InventorySlot slot in resultSlots)
        {
            if (slot != null)
                slot.Clear();
        }
    }

    private void ResolveReferences()
    {
        if (playerInventory == null)
            playerInventory = PartSystemRuntimeState.GetOrBindBestInventory();

        if ((partRecipes == null || partRecipes.Length == 0) && autoLoadPartRecipes)
            partRecipes = Resources.LoadAll<CraftingRecipeData>("AETHERParts");

        if (createButton == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button != null && button.name.ToLowerInvariant().Contains("create"))
                {
                    createButton = button;
                    break;
                }
            }
        }

        EnsureMaterialSlotGridPositions();
        ResolveModuleUpgradeTexts();
        EnsureModuleUpgradeSummaryText();
        EnsureResultSlotClickHandlers();
        EnsureFeedbackText();
    }

    private void ResolveModuleUpgradeTexts()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text == null)
                continue;

            string normalizedName = text.name.ToLowerInvariant();
            if (normalizedName.Contains("moduleupgrade") || normalizedName.Contains("value"))
                continue;

            if (attackText == null && normalizedName.Contains("attack"))
                attackText = text as TextMeshProUGUI;
            else if (defenseText == null && normalizedName.Contains("defense"))
                defenseText = text as TextMeshProUGUI;
            else if (speedText == null && (normalizedName.Contains("speed") || normalizedName.Contains("spped")))
                speedText = text as TextMeshProUGUI;
            else if (detailText == null && normalizedName.Contains("detail"))
                detailText = text as TextMeshProUGUI;
        }

        Transform panel = GetModuleUpgradePanel();
        if (panel == null)
            return;

        if (attackValueText == null)
            attackValueText = FindModuleUpgradeText(panel, "AttackValueText", "ModuleUpgradeAttackValueText");
        if (defenseValueText == null)
            defenseValueText = FindModuleUpgradeText(panel, "DefenseValueText", "DefenceValueText", "ModuleUpgradeDefenseValueText");
        if (speedValueText == null)
            speedValueText = FindModuleUpgradeText(panel, "SpeedValueText", "ModuleUpgradeSpeedValueText");
        if (moduleUpgradeHeaderText == null)
            moduleUpgradeHeaderText = FindModuleUpgradeText(panel, "ModuleUpgradeHeaderText", "Module Upgrades", "MODULE UPGRADES");
        if (detailHeaderText == null)
            detailHeaderText = FindModuleUpgradeText(panel, "ModuleUpgradeDetailHeaderText", "DetailHeaderText");
    }

    private void EnsureModuleUpgradeSummaryText()
    {
        Transform panel = GetModuleUpgradePanel();
        if (panel == null)
            return;

        moduleUpgradeHeaderText = moduleUpgradeHeaderText != null ? moduleUpgradeHeaderText : FindModuleUpgradeText(panel, "ModuleUpgradeHeaderText", "Module Upgrades", "MODULE UPGRADES");
        attackValueText = attackValueText != null ? attackValueText : GetOrCreateModuleUpgradeText(panel, "ModuleUpgradeAttackValueText");
        defenseValueText = defenseValueText != null ? defenseValueText : GetOrCreateModuleUpgradeText(panel, "ModuleUpgradeDefenseValueText");
        speedValueText = speedValueText != null ? speedValueText : GetOrCreateModuleUpgradeText(panel, "ModuleUpgradeSpeedValueText");
        detailHeaderText = detailHeaderText != null ? detailHeaderText : FindModuleUpgradeText(panel, "ModuleUpgradeDetailHeaderText", "DetailHeaderText");

        if (moduleUpgradeSummaryText != null)
        {
            moduleUpgradeSummaryText.text = string.Empty;
            moduleUpgradeSummaryText.gameObject.SetActive(false);
        }
    }

    private Transform GetModuleUpgradePanel()
    {
        if (moduleUpgradePanel != null)
            return moduleUpgradePanel;

        Transform panel = null;
        if (attackText != null && !IsGeneratedModuleUpgradeText(attackText))
            panel = attackText.transform.parent;
        else if (defenseText != null && !IsGeneratedModuleUpgradeText(defenseText))
            panel = defenseText.transform.parent;
        else if (speedText != null && !IsGeneratedModuleUpgradeText(speedText))
            panel = speedText.transform.parent;
        else if (detailText != null && !IsGeneratedModuleUpgradeText(detailText))
            panel = detailText.transform.parent;

        if (panel == null)
            panel = FindChildByName(transform, "Deail panel");
        if (panel == null)
            panel = FindChildByName(transform, "Detail panel");

        moduleUpgradePanel = panel;
        return panel;
    }

    private static bool IsGeneratedModuleUpgradeText(TextMeshProUGUI text)
    {
        return text != null && text.name.StartsWith("ModuleUpgrade", System.StringComparison.Ordinal);
    }

    private TextMeshProUGUI GetOrCreateModuleUpgradeText(Transform panel, string objectName)
    {
        Transform existing = FindChildByName(panel, objectName);
        if (existing != null)
        {
            TextMeshProUGUI existingText = existing.GetComponent<TextMeshProUGUI>();
            if (existingText != null)
                return existingText;
        }

        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel, false);
        return textObject.GetComponent<TextMeshProUGUI>();
    }

    private TextMeshProUGUI FindModuleUpgradeText(Transform panel, params string[] names)
    {
        if (panel == null || names == null)
            return null;

        foreach (string textName in names)
        {
            if (string.IsNullOrWhiteSpace(textName))
                continue;

            Transform existing = FindChildByName(panel, textName);
            if (existing == null)
                continue;

            TextMeshProUGUI existingText = existing.GetComponent<TextMeshProUGUI>();
            if (existingText != null)
                return existingText;
        }

        return null;
    }

    private static void ConfigureModuleText(TextMeshProUGUI text, string defaultText, Vector2 anchoredPosition, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment, FontStyles style, bool anchorRight = false)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        if (anchorRight)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
        }

        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        if (!string.IsNullOrEmpty(defaultText))
            text.text = defaultText;
        text.alignment = alignment;
        text.color = color;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.gameObject.SetActive(true);
        text.transform.SetAsLastSibling();
    }

    private void EnsureResultSlotClickHandlers()
    {
        if (resultSlots == null)
            return;

        for (int i = 0; i < resultSlots.Length; i++)
        {
            InventorySlot slot = resultSlots[i];
            if (slot == null)
                continue;

            EventTrigger trigger = slot.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = slot.gameObject.AddComponent<EventTrigger>();

            if (resultSlotClickHandlers.Contains(trigger))
                continue;

            int capturedIndex = i;
            EventTrigger.Entry clickEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerClick
            };
            clickEntry.callback.AddListener(_ => ShowResultSlotDetails(capturedIndex));
            trigger.triggers.Add(clickEntry);
            resultSlotClickHandlers.Add(trigger);
        }
    }

    private void ShowResultSlotDetails(int index)
    {
        if (resultSlots == null || index < 0 || index >= resultSlots.Length)
            return;

        InventorySlot slot = resultSlots[index];
        if (slot == null || slot.IsEmpty() || slot.itemData == null)
            return;

        ShowModuleUpgradeDetails(slot.itemData);
    }

    private void ShowModuleUpgradeDetails(CraftItemDataSO item)
    {
        if (item == null)
        {
            ClearModuleUpgradeDetails();
            return;
        }

        if (item.linkedPartData != null)
        {
            ShowModuleUpgradeDetails(item.linkedPartData);
            return;
        }

        PartData linkedPart = item.linkedInventoryItem as PartData;
        if (linkedPart != null)
        {
            ShowModuleUpgradeDetails(linkedPart);
            return;
        }

        SetModuleUpgradeTexts(item.attack, item.defense, item.speed, item.description);
    }

    private void ShowModuleUpgradeDetails(PartData part)
    {
        if (part == null)
        {
            ClearModuleUpgradeDetails();
            return;
        }

        string detail = !string.IsNullOrWhiteSpace(part.description)
            ? part.description
            : part.displayName;
        SetModuleUpgradeTexts(part.attackBonus, part.defenseBonus, part.speedBonus, detail);
    }

    private void SetModuleUpgradeTexts(int attack, int defense, int speed, string detail)
    {
        ResolveModuleUpgradeTexts();
        EnsureModuleUpgradeSummaryText();
        SetModuleUpgradePanelVisible(true);

        if (detailHeaderText != null)
            detailHeaderText.text = "DETAIL";
        if (attackValueText != null)
            attackValueText.text = FormatSignedStat(attack);
        if (defenseValueText != null)
            defenseValueText.text = FormatSignedStat(defense);
        if (speedValueText != null)
            speedValueText.text = FormatSignedStat(speed);
        if (detailText != null && !IsGeneratedModuleUpgradeText(detailText))
            detailText.gameObject.SetActive(true);
    }

    private void ClearModuleUpgradeDetails()
    {
        ResolveModuleUpgradeTexts();
        EnsureModuleUpgradeSummaryText();

        if (moduleUpgradeSummaryText != null)
            moduleUpgradeSummaryText.text = string.Empty;
        if (detailHeaderText != null)
            detailHeaderText.text = "DETAIL";
        if (attackValueText != null)
            attackValueText.text = string.Empty;
        if (defenseValueText != null)
            defenseValueText.text = string.Empty;
        if (speedValueText != null)
            speedValueText.text = string.Empty;
        if (detailText != null && !IsGeneratedModuleUpgradeText(detailText))
            detailText.gameObject.SetActive(false);
        SetModuleUpgradePanelVisible(false);
    }

    private void SetModuleUpgradePanelVisible(bool visible)
    {
        Transform panel = GetModuleUpgradePanel();
        if (panel == null)
            return;

        panel.gameObject.SetActive(visible);
    }

    private static string FormatSignedStat(int value)
    {
        return value >= 0 ? "+" + value : value.ToString();
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void EnsureMaterialSlotGridPositions()
    {
        if (materialSlotGridPositions != null && materialSlotGridPositions.Length > 0)
            return;

        if (materialSlots == null || materialSlots.Length != 5)
            return;

        materialSlotGridPositions = new[]
        {
            new Vector2Int(0, 0),
            new Vector2Int(1, 1),
            new Vector2Int(2, 2),
            new Vector2Int(3, 1),
            new Vector2Int(4, 0)
        };
    }

    private void UpdateCreateButtonState()
    {
        if (createButton != null)
            createButton.interactable =
                (currentPartRecipe != null && (playerInventory == null || HasRequiredModules(currentPartRecipe)) && MaterialGridMatchesRecipe(currentPartRecipe))
                || currentRecipe != null;
    }

    private void EnsureFeedbackText()
    {
        if (feedbackText != null || previewImage == null)
            return;

        Transform parent = previewImage.transform;
        GameObject feedbackObject = new GameObject("CraftingFeedbackText", typeof(RectTransform), typeof(TextMeshProUGUI));
        feedbackObject.transform.SetParent(parent, false);

        RectTransform rect = feedbackObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        feedbackText = feedbackObject.GetComponent<TextMeshProUGUI>();
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.fontSize = 42f;
        feedbackText.fontStyle = FontStyles.Bold;
        feedbackText.color = wrongPlacementColor;
        feedbackText.raycastTarget = false;
        feedbackText.gameObject.SetActive(false);
    }

    private void ShowFeedback(string message)
    {
        EnsureFeedbackText();
        if (feedbackText == null)
            return;

        feedbackText.text = string.IsNullOrWhiteSpace(message) ? "nope" : message;
        feedbackText.color = wrongPlacementColor;
        feedbackText.gameObject.SetActive(true);
    }

    private void HideFeedback()
    {
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);
    }
}
