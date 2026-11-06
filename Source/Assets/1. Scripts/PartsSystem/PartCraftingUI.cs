using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartCraftingUI : MonoBehaviour
{
    [Header("Systems")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PaperData activePaper;
    [SerializeField] private CraftingRecipeData activeRecipe;
    [SerializeField] private CraftingRecipeData[] fallbackRecipes;

    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Transform moduleListParent;
    [SerializeField] private PartInventoryEntryUI moduleEntryPrefab;
    [SerializeField] private PartCraftingGridManager gridManager;
    [SerializeField] private TMP_Text previewNameText;
    [SerializeField] private TMP_Text previewDescriptionText;
    [SerializeField] private Image previewIcon;
    [SerializeField] private Button createButton;
    [SerializeField] private Button clearButton;

    public ItemData SelectedModule { get; private set; }

    private readonly List<GameObject> spawnedEntries = new List<GameObject>();
    private bool craftingInProgress;

    private void Awake()
    {
        ResolveReferences();
        EnsureMinimalUI();
        EnsureFallbackRecipe();

        if (gridManager != null)
        {
            gridManager.Setup(this, GetRecipeGridSize());
            gridManager.OnGridChanged += RefreshPreview;
        }

        if (createButton != null)
            createButton.onClick.AddListener(CreatePart);

        if (clearButton != null)
            clearButton.onClick.AddListener(() => gridManager?.ClearGrid());
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (inventory != null)
            inventory.OnInventoryChanged += RefreshInventoryList;

        RefreshInventoryList();
        RefreshPreview();
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= RefreshInventoryList;
    }

    private void OnDestroy()
    {
        if (gridManager != null)
            gridManager.OnGridChanged -= RefreshPreview;
    }

    public void SetActivePaper(PaperData paper)
    {
        activePaper = paper;
        if (activePaper != null && activePaper.relatedRecipe != null)
            activeRecipe = activePaper.relatedRecipe;

        if (gridManager != null)
            gridManager.Setup(this, GetRecipeGridSize());

        RefreshPreview();
    }

    public void SelectModule(ItemData item)
    {
        SelectedModule = item;
    }

    public void RefreshInventoryList()
    {
        ResolveReferences();

        for (int i = 0; i < spawnedEntries.Count; i++)
        {
            if (spawnedEntries[i] != null)
                Destroy(spawnedEntries[i]);
        }
        spawnedEntries.Clear();

        if (inventory == null || moduleListParent == null)
            return;

        for (int i = 0; i < inventory.slots.Count; i++)
        {
            PlayerInventory.Slot slot = inventory.slots[i];
            if (slot == null || slot.item == null || slot.count <= 0)
                continue;

            if (!ShouldShowAsModuleMaterial(slot.item))
                continue;

            PartInventoryEntryUI entry = CreateEntry();
            entry.Setup(this, slot.item, slot.count);
            spawnedEntries.Add(entry.gameObject);
        }
    }

    private void CreatePart()
    {
        if (craftingInProgress)
            return;

        PartData resultPart = activeRecipe != null ? activeRecipe.resultPart : null;
        if (activeRecipe == null || resultPart == null || inventory == null)
        {
            RefreshPreview();
            return;
        }

        if (gridManager == null || !gridManager.MatchesRecipe(activeRecipe))
            return;

        if (!TryBuildRequiredModules(activeRecipe.requiredModuleIds, out List<PlayerInventory.ItemQuantity> materials))
            return;

        craftingInProgress = true;
        try
        {
            if (!inventory.TryExchangeItems(materials, new[] { new PlayerInventory.ItemQuantity(resultPart, 1) }))
                return;

            try
            {
                if (GlobalManager.Audio != null)
                    GlobalManager.Audio.PlaySFX("craft_success");
            }
            catch
            {
            }

            gridManager.ClearGrid();
            RefreshInventoryList();
            RefreshPreview();
        }
        finally
        {
            craftingInProgress = false;
        }
    }

    private void RefreshPreview()
    {
        bool matches = activeRecipe != null
            && gridManager != null
            && gridManager.MatchesRecipe(activeRecipe)
            && HasRequiredModules(activeRecipe.requiredModuleIds);

        if (createButton != null)
            createButton.interactable = matches;

        PartData target = activeRecipe != null ? activeRecipe.resultPart : null;

        if (previewNameText != null)
            previewNameText.text = matches && target != null ? target.displayName : "Design mismatch";

        if (previewDescriptionText != null)
            previewDescriptionText.text = matches && target != null
                ? target.description
                : GetMismatchPreviewText();

        if (previewIcon != null)
        {
            previewIcon.sprite = matches && target != null ? target.itemSprite : null;
            previewIcon.enabled = previewIcon.sprite != null;
        }
    }

    private bool HasRequiredModules(string[] requiredIds)
    {
        return TryBuildRequiredModules(requiredIds, out _);
    }

    private bool TryBuildRequiredModules(string[] requiredIds, out List<PlayerInventory.ItemQuantity> materials)
    {
        materials = new List<PlayerInventory.ItemQuantity>();
        if (inventory == null || inventory.slots == null || requiredIds == null || requiredIds.Length == 0)
            return false;

        Dictionary<string, int> needed = BuildNeededMap(requiredIds);
        if (needed.Count == 0)
            return false;

        foreach (KeyValuePair<string, int> need in needed)
        {
            int remaining = need.Value;
            for (int i = 0; i < inventory.slots.Count && remaining > 0; i++)
            {
                PlayerInventory.Slot slot = inventory.slots[i];
                if (slot == null || slot.item == null || slot.count <= 0)
                    continue;

                if (ItemNameRuleUtility.GetBestItemId(slot.item) != need.Key)
                    continue;

                int used = Mathf.Min(slot.count, remaining);
                materials.Add(new PlayerInventory.ItemQuantity(slot.item, used));
                remaining -= used;
            }

            if (remaining > 0)
                return false;
        }

        return true;
    }

    private static Dictionary<string, int> BuildNeededMap(string[] requiredIds)
    {
        Dictionary<string, int> needed = new Dictionary<string, int>();
        if (requiredIds == null)
            return needed;

        for (int i = 0; i < requiredIds.Length; i++)
        {
            string id = ItemNameRuleUtility.NormalizeName(requiredIds[i]);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!needed.ContainsKey(id))
                needed[id] = 0;
            needed[id]++;
        }

        return needed;
    }

    private Vector2Int GetRecipeGridSize()
    {
        if (activeRecipe != null && activeRecipe.gridSize.x > 0 && activeRecipe.gridSize.y > 0)
            return activeRecipe.gridSize;

        return new Vector2Int(5, 5);
    }

    private string GetMismatchPreviewText()
    {
        if (activeRecipe != null && !string.IsNullOrWhiteSpace(activeRecipe.recipeHintText))
            return activeRecipe.recipeHintText;

        return "Place the module items according to the manual.";
    }

    private void EnsureFallbackRecipe()
    {
        if (activeRecipe != null)
            return;

        if (activePaper != null && activePaper.relatedRecipe != null)
        {
            activeRecipe = activePaper.relatedRecipe;
            return;
        }

        if (PaperReadUI.LastReadPaper != null && PaperReadUI.LastReadPaper.relatedRecipe != null)
        {
            activeRecipe = PaperReadUI.LastReadPaper.relatedRecipe;
            activePaper = PaperReadUI.LastReadPaper;
            return;
        }

        if (fallbackRecipes != null && fallbackRecipes.Length > 0)
        {
            activeRecipe = fallbackRecipes[0];
            return;
        }

        activeRecipe = PartResourceResolver.FindRecipe(string.Empty);
    }

    private void ResolveReferences()
    {
        if (inventory == null)
            inventory = PartSystemRuntimeState.GetOrBindBestInventory();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (gridManager == null)
            gridManager = GetComponentInChildren<PartCraftingGridManager>(true);

        if (moduleListParent == null)
            moduleListParent = transform.Find("ModuleList");
    }

    private static bool ShouldShowAsModuleMaterial(ItemData item)
    {
        if (item == null)
            return false;

        string itemId = ItemNameRuleUtility.GetBestItemId(item);
        string displayName = item.displayName;
        string bestName = ItemNameRuleUtility.GetBestItemName(null, item);

        bool isModule = ItemNameRuleUtility.IsModuleItem(itemId)
            || ItemNameRuleUtility.IsModuleItem(displayName)
            || ItemNameRuleUtility.IsModuleItem(bestName);

        bool isPaper = ItemNameRuleUtility.IsPaperObject(itemId)
            || ItemNameRuleUtility.IsPaperObject(displayName)
            || ItemNameRuleUtility.IsPaperObject(bestName);

        return isModule && !isPaper;
    }

    private void EnsureMinimalUI()
    {
        if (moduleListParent == null)
        {
            GameObject listObject = new GameObject("ModuleList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listObject.transform.SetParent(transform, false);
            RectTransform rect = listObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.15f);
            rect.anchorMax = new Vector2(0.27f, 0.85f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            moduleListParent = listObject.transform;
        }

        if (gridManager == null)
        {
            GameObject gridObject = new GameObject("PartCraftingGrid", typeof(RectTransform), typeof(PartCraftingGridManager));
            gridObject.transform.SetParent(transform, false);
            RectTransform rect = gridObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.36f, 0.22f);
            rect.anchorMax = new Vector2(0.64f, 0.78f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            gridManager = gridObject.GetComponent<PartCraftingGridManager>();
        }

        if (previewNameText == null)
            previewNameText = CreateText("PreviewName", new Vector2(0.70f, 0.62f), new Vector2(0.93f, 0.70f), "Design mismatch", 24f);

        if (previewDescriptionText == null)
            previewDescriptionText = CreateText("PreviewDescription", new Vector2(0.70f, 0.42f), new Vector2(0.93f, 0.60f), string.Empty, 18f);

        if (createButton == null)
            createButton = CreateButton("CreateButton", new Vector2(0.72f, 0.20f), new Vector2(0.92f, 0.30f), "CREATE PART");

        if (clearButton == null)
            clearButton = CreateButton("ClearButton", new Vector2(0.72f, 0.32f), new Vector2(0.92f, 0.40f), "CLEAR");
    }

    private TMP_Text CreateText(string name, Vector2 anchorMin, Vector2 anchorMax, string text, float fontSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text tmp = textObject.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button CreateButton(string name, Vector2 anchorMin, Vector2 anchorMax, string text)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.85f, 0.62f, 0.22f, 0.95f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = 20f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        label.raycastTarget = false;

        return buttonObject.GetComponent<Button>();
    }

    private PartInventoryEntryUI CreateEntry()
    {
        if (moduleEntryPrefab != null)
            return Instantiate(moduleEntryPrefab, moduleListParent);

        GameObject entryObject = new GameObject("Module Entry", typeof(RectTransform), typeof(Image), typeof(Button), typeof(PartInventoryEntryUI));
        entryObject.transform.SetParent(moduleListParent, false);

        Image image = entryObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.16f);

        RectTransform rect = entryObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(220f, 42f);

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(entryObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(24f, 0f);
        iconRect.sizeDelta = new Vector2(30f, 30f);

        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(entryObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(46f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.fontSize = 18f;
        label.color = Color.white;
        label.raycastTarget = false;

        return entryObject.GetComponent<PartInventoryEntryUI>();
    }
}
