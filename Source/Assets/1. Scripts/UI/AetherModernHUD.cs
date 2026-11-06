using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the gameplay HUD to match the reference layout with a single canvas.
/// It reads existing gameplay values only; player, camera, combat, and movement logic stay untouched.
/// </summary>
[ExecuteAlways]
public class AetherModernHUD : MonoBehaviour
{
    private const float CornerHudScale = 0.85f;
    private const float CompassHudScale = 0.88f;

    public const string RootName = "PF_GameplayHUD_ReferenceExact";
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    private static AetherModernHUD instance;

    private static readonly string[] LegacyHudNameTokens =
    {
        "hpbar",
        "hpslider",
        "shieldbar",
        "shieldslider",
        "currentammo",
        "totalammo",
        "firemode",
        "energyslot",
        "weaponui",
        "oldhud",
        "modernhud",
        "compasshud",
        "objectivehud",
        "aetherlevelhud",
        "aetherlevel",
        "alterlevel",
        "alertlevel",
        "playerstatushud",
        "ammohud"
    };

    [Header("Runtime Values")]
    [SerializeField] private string objectiveText = "\uC5F0\uAD6C\uC18C \uB0B4\uBD80\uB85C \uC774\uB3D9\uD558\uC5EC\n\uC5D0\uD14C\uB974 \uC81C\uC5B4\uC2E4\uB85C \uC9C4\uC785\uD558\uB77C.";
    [SerializeField] private int aetherLevel = 2;
    [SerializeField] private float currentAetherExp = 450f;
    [SerializeField] private float maxAetherExp = 1000f;

    [Header("Options")]
    [SerializeField] private bool autoHideLegacyHud = true;
    [SerializeField] private bool showOnlyWhenGameplayPlayerExists = false;

    private Canvas canvas;
    private TMP_FontAsset uiFont;
    private PlayerStatus playerStatus;
    private Hankyul_Fire hankyulFire;
    private Camera mainCamera;
    private readonly LocalPlayerUIBinding localBinding = new LocalPlayerUIBinding();
    private RectTransform ammoPanel;

    private TextMeshProUGUI objectiveBodyText;
    private RectTransform objectivePanel;
    private TextMeshProUGUI aetherLevelText;
    private TextMeshProUGUI aetherExpText;
    private Image aetherExpFill;
    private Image hpFill;
    private Image thirstFill;
    private TextMeshProUGUI hpValueText;
    private TextMeshProUGUI thirstValueText;
    private TextMeshProUGUI currentAmmoText;
    private TextMeshProUGUI reserveAmmoText;
    private TextMeshProUGUI ammoTypeText;
    private TextMeshProUGUI fireModeText;
    private TextMeshProUGUI reloadStateText;
    private Image magazineFill;
    private TextMeshProUGUI compassDegreeText;
    private readonly TextMeshProUGUI[] compassLabels = new TextMeshProUGUI[9];

    private int lastHp = int.MinValue;
    private int lastMaxHp = int.MinValue;
    private int lastThirst = int.MinValue;
    private int lastMaxThirst = int.MinValue;
    private int lastCurrentAmmo = int.MinValue;
    private int lastReserveAmmo = int.MinValue;
    private Hankyul_Fire.FireMode lastFireMode = (Hankyul_Fire.FireMode)(-1);
    private bool lastReloading;
    private int lastAetherLevel = int.MinValue;
    private int lastAetherExp = int.MinValue;
    private int lastMaxAetherExp = int.MinValue;
    private int lastCompassYaw = int.MinValue;
    private float objectiveHideAt;
    private Coroutine objectiveHideRoutine;
    private const float ObjectiveVisibleSeconds = 120f;
    private const float ObjectiveBackgroundAlpha = 0.72f;

    private void Awake()
    {
        // Edit-mode previews are rebuilt only by the explicit Reset action, not by a domain reload.
        if (!Application.isPlaying)
            return;
        if (Application.isPlaying)
            instance = this;

        gameObject.name = RootName;
        ResolveUIFont();
        BuildHud();
        if (Application.isPlaying)
        {
            RebindSceneReferences();
            ShowObjectiveForSceneStart();
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        localBinding.Clear();
        playerStatus = null;
        hankyulFire = null;
        mainCamera = null;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        bool ownerChanged = localBinding.Poll(this);
        if (ownerChanged || (localBinding.Queried && localBinding.Root != null
            && (playerStatus == null || mainCamera == null
                || (localBinding.Root.GetComponent<Move_Hankyul>() != null && hankyulFire == null))))
            RebindSceneReferences();

        UpdateCanvasVisibility();
        if (canvas == null || !canvas.enabled)
            return;

        UpdateStatus();
        UpdateWeapon();
        UpdateAetherLevel();
        UpdateCompass();
        UpdateObjectiveVisibility();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindSceneReferences();
        ShowObjectiveForSceneStart();
    }

    private void Reset()
    {
        gameObject.name = RootName;
        ResolveUIFont();
        BuildHud();
    }


    private void BuildHud()
    {
        RemoveGeneratedChildren();

        canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        if (canvas == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            gameObject.AddComponent<GraphicRaycaster>();

        RectTransform root = GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        RectTransform safeArea = CreateEmpty(root, "SafeArea", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        safeArea.offsetMin = Vector2.zero;
        safeArea.offsetMax = Vector2.zero;
        safeArea.localScale = Vector3.one;

        BuildObjective(safeArea);
        BuildCompass(safeArea);
        BuildAetherLevel(safeArea);
        BuildStatus(safeArea);
        BuildAmmo(safeArea);
        BuildCrosshair(safeArea);
    }

    private void RemoveGeneratedChildren()
    {
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null)
            return;

        for (int i = rect.childCount - 1; i >= 0; i--)
            DestroyImmediate(rect.GetChild(i).gameObject);
    }

    private void BuildObjective(RectTransform parent)
    {
        RectTransform panel = CreateTexturePanel(parent, "ObjectiveHUD", "UI/HUDExact/ObjectivePanel", AnchorTopLeft(), new Vector2(0f, 1f), new Vector2(28f, -56f), new Vector2(405f, 120f));
        panel.localScale = Vector3.one * CornerHudScale;
        RawImage background = panel.GetComponentInChildren<RawImage>(true);
        if (background != null)
            background.color = new Color(1f, 1f, 1f, ObjectiveBackgroundAlpha);
        objectivePanel = panel;
        objectiveBodyText = CreateTMP(panel, "DescriptionText", AnchorTopLeft(), AnchorTopLeft(), new Vector2(0f, 1f), new Vector2(79f, -57f), new Vector2(298f, 55f), objectiveText, 18, TextAlignmentOptions.TopLeft, Color.white);
        objectiveBodyText.enableWordWrapping = true;
        objectiveBodyText.overflowMode = TextOverflowModes.Overflow;
        objectiveBodyText.lineSpacing = -3f;
        objectiveBodyText.fontStyle = FontStyles.Normal;
    }

    private void BuildCompass(RectTransform parent)
    {
        RectTransform panel = CreateEmpty(parent, "CompassHUD", AnchorTopCenter(), AnchorTopCenter(), new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(900f, 78f));
        panel.localScale = Vector3.one * CompassHudScale;
        RectTransform viewport = CreateEmpty(panel, "CompassViewport", new Vector2(0f, 0f), Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateEmpty(viewport, "CompassContent", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(900f, 52f));
        CreateTextureImage(content, "CompassArtwork", "UI/HUDExact/CompassPanel", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -1f), Vector2.zero);

        int[] offsets = { -90, -60, -30, -15, 0, 15, 30, 60, 90 };
        for (int i = 0; i < offsets.Length; i++)
        {
            float x = offsets[i] * 4.1f;
            compassLabels[i] = CreateTMP(panel, "Compass Label " + i, AnchorTopCenter(), AnchorTopCenter(), new Vector2(x, -40f), new Vector2(72f, 25f), string.Empty, i == 4 ? 22 : 16, TextAlignmentOptions.Center, Color.white);
        }
        RectTransform marker = CreateEmpty(panel, "CenterMarker", AnchorTopCenter(), AnchorTopCenter(), new Vector2(0.5f, 1f), new Vector2(0f, -15f), new Vector2(26f, 20f));
        compassDegreeText = CreateTMP(panel, "CurrentAngleText", AnchorTopCenter(), AnchorTopCenter(), new Vector2(0f, -64f), new Vector2(90f, 30f), "000", 24, TextAlignmentOptions.Center, Color.white);
        compassDegreeText.fontStyle = FontStyles.Bold;
    }

    private void BuildAetherLevel(RectTransform parent)
    {
        RectTransform panel = CreateTexturePanel(parent, "AetherLevelHUD", "UI/HUDExact/AetherLevelPanel", AnchorTopRight(), new Vector2(1f, 1f), new Vector2(-30f, -40f), new Vector2(315f, 96f));
        panel.localScale = Vector3.one * CornerHudScale;
        aetherLevelText = CreateTMP(panel, "LevelText", AnchorBottomLeft(), AnchorBottomLeft(), new Vector2(0.5f, 0.5f), new Vector2(48f, 39f), new Vector2(58f, 48f), "2", 46, TextAlignmentOptions.Center, Color.white);
        aetherExpText = CreateTMP(panel, "ExperienceText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(1f, 0.5f), new Vector2(-18f, 38f), new Vector2(96f, 20f), "450 / 1000", 12, TextAlignmentOptions.MidlineRight, new Color(0.92f, 0.97f, 1f, 0.95f));
        ConfigureNumericText(aetherLevelText);
        ConfigureNumericText(aetherExpText);
        aetherExpFill = CreateSolidImage(panel, "ProgressBarFill", AnchorBottomLeft(), AnchorBottomLeft(), new Vector2(0f, 0.5f), new Vector2(88f, 22f), new Vector2(166f, 8f), Cyan(0.95f));
        aetherExpFill.type = Image.Type.Filled;
        aetherExpFill.fillMethod = Image.FillMethod.Horizontal;
    }

    private void BuildStatus(RectTransform parent)
    {
        RectTransform panel = CreateTexturePanel(parent, "PlayerStatusHUD", "UI/HUDExact/PlayerStatusPanel", AnchorBottomLeft(), new Vector2(0f, 0f), new Vector2(28f, 30f), new Vector2(470f, 115f));
        panel.localScale = Vector3.one * CornerHudScale;
        hpFill = CreateSolidImage(panel, "HPBarFill", AnchorBottomLeft(), AnchorBottomLeft(), new Vector2(0f, 0.5f), new Vector2(116f, 79f), new Vector2(220f, 12f), new Color(0.92f, 0.95f, 0.98f, 1f));
        thirstFill = CreateSolidImage(panel, "ThirstBarFill", AnchorBottomLeft(), AnchorBottomLeft(), new Vector2(0f, 0.5f), new Vector2(116f, 32f), new Vector2(220f, 12f), Cyan(1f));
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        thirstFill.type = Image.Type.Filled;
        thirstFill.fillMethod = Image.FillMethod.Horizontal;
        CreateSolidImage(panel, "ValueMask", AnchorBottomRight(), AnchorBottomRight(), new Vector2(1f, 0.5f), new Vector2(-2f, 56f), new Vector2(112f, 101f), new Color(0.025f, 0.075f, 0.125f, 0.96f));
        hpValueText = CreateTMP(panel, "HPValueText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(1f, 0.5f), new Vector2(-17f, 79f), new Vector2(88f, 25f), "100 / 100", 14, TextAlignmentOptions.MidlineRight, Color.white);
        thirstValueText = CreateTMP(panel, "ThirstValueText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(1f, 0.5f), new Vector2(-17f, 32f), new Vector2(88f, 25f), "100 / 100", 14, TextAlignmentOptions.MidlineRight, Color.white);
        ConfigureNumericText(hpValueText);
        ConfigureNumericText(thirstValueText);
    }

    private void BuildAmmo(RectTransform parent)
    {
        RectTransform panel = CreateTexturePanel(parent, "AmmoHUD", "UI/HUDExact/AmmoPanel", AnchorBottomRight(), new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(350f, 108f));
        ammoPanel = panel;
        panel.localScale = Vector3.one * CornerHudScale;
        currentAmmoText = CreateTMP(panel, "CurrentAmmoText", AnchorMiddleRight(), AnchorMiddleRight(), new Vector2(-103f, 13f), new Vector2(78f, 56f), "30", 48, TextAlignmentOptions.MidlineRight, Color.white);
        CreateLine(panel, "AmmoDivider", AnchorMiddleRight(), AnchorMiddleRight(), new Vector2(0.5f, 0.5f), new Vector2(-91f, 13f), new Vector2(2f, 45f), new Color(1f, 1f, 1f, 0.28f));
        reserveAmmoText = CreateTMP(panel, "ReserveAmmoText", AnchorMiddleRight(), AnchorMiddleRight(), new Vector2(-43f, 14f), new Vector2(52f, 28f), "120", 19, TextAlignmentOptions.Center, Color.white);
        ammoTypeText = CreateTMP(panel, "CaliberText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(-131f, 19f), new Vector2(92f, 22f), "5.56mm", 15, TextAlignmentOptions.Center, Color.white);
        fireModeText = CreateTMP(panel, "FireModeText", AnchorBottomRight(), AnchorBottomRight(), new Vector2(-40f, 19f), new Vector2(66f, 22f), "AUTO", 15, TextAlignmentOptions.Center, Color.white);
        magazineFill = CreateSolidImage(panel, "MagazineProgress", AnchorBottomRight(), AnchorBottomRight(), new Vector2(0f, 0.5f), new Vector2(-173f, 7f), new Vector2(132f, 3f), Cyan(0.9f));
        magazineFill.type = Image.Type.Filled;
        magazineFill.fillMethod = Image.FillMethod.Horizontal;
        reloadStateText = CreateTMP(panel, "ReloadStateText", AnchorTopRight(), AnchorTopRight(), new Vector2(-81f, -14f), new Vector2(128f, 22f), string.Empty, 14, TextAlignmentOptions.Center, Cyan(1f));
        reloadStateText.fontStyle = FontStyles.Bold;
        ConfigureNumericText(currentAmmoText);
        ConfigureNumericText(reserveAmmoText);
    }

    private void BuildCrosshair(RectTransform parent)
    {
        RectTransform group = CreateEmpty(parent, "CrosshairHUD", AnchorCenter(), AnchorCenter(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32f, 32f));
        CreateLine(group, "TopLine", AnchorCenter(), AnchorCenter(), new Vector2(0.5f, 0.5f), new Vector2(0f, 13f), new Vector2(2f, 9f), new Color(1f, 1f, 1f, 0.62f));
        CreateLine(group, "BottomLine", AnchorCenter(), AnchorCenter(), new Vector2(0.5f, 0.5f), new Vector2(0f, -13f), new Vector2(2f, 9f), new Color(1f, 1f, 1f, 0.62f));
        CreateLine(group, "LeftLine", AnchorCenter(), AnchorCenter(), new Vector2(0.5f, 0.5f), new Vector2(-13f, 0f), new Vector2(9f, 2f), new Color(1f, 1f, 1f, 0.62f));
        CreateLine(group, "RightLine", AnchorCenter(), AnchorCenter(), new Vector2(0.5f, 0.5f), new Vector2(13f, 0f), new Vector2(9f, 2f), new Color(1f, 1f, 1f, 0.62f));
    }

    private void RebindSceneReferences()
    {
        localBinding.Poll(this, null, true);
        Transform root = localBinding.Root;
        playerStatus = LocalPlayerBindingResolver.Owned<PlayerStatus>(root);
        hankyulFire = root != null && root.GetComponent<Move_Hankyul>() != null
            ? LocalPlayerBindingResolver.Owned<Hankyul_Fire>(root) : null;
        Transform cameraTransform = LocalPlayerUIBinding.CameraTransform(root);
        mainCamera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
        lastHp = lastMaxHp = lastThirst = lastMaxThirst = int.MinValue;
        lastCurrentAmmo = lastReserveAmmo = lastCompassYaw = int.MinValue;
        lastFireMode = (Hankyul_Fire.FireMode)(-1);
        if (ammoPanel != null) ammoPanel.gameObject.SetActive(hankyulFire != null);

        if (autoHideLegacyHud && root != null)
            HideLegacyHud();

        UpdateCanvasVisibility();
    }

    private void ShowObjectiveForSceneStart()
    {
        objectiveHideAt = Time.unscaledTime + ObjectiveVisibleSeconds;
        if (objectivePanel != null)
            objectivePanel.gameObject.SetActive(true);

        if (objectiveHideRoutine != null)
            StopCoroutine(objectiveHideRoutine);
        objectiveHideRoutine = StartCoroutine(HideObjectiveAfterDelay());
    }

    private IEnumerator HideObjectiveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(ObjectiveVisibleSeconds);

        if (objectivePanel != null)
            objectivePanel.gameObject.SetActive(false);
        objectiveHideRoutine = null;
    }

    private void UpdateObjectiveVisibility()
    {
        if (objectivePanel != null && objectivePanel.gameObject.activeSelf && Time.unscaledTime >= objectiveHideAt)
            objectivePanel.gameObject.SetActive(false);
    }

    private void HideLegacyHud()
    {
        HideComponentObjects<CompassScroller>();
        HideComponentObjects<CompassTickBuilder>();
        HideComponentObjects<Hankyul_WeaponUI>();

        if (playerStatus != null)
        {
            if (playerStatus.hpSlider != null)
                HideLegacySliderRoot(playerStatus.hpSlider);
            if (playerStatus.shieldSlider != null)
                HideLegacySliderRoot(playerStatus.shieldSlider);
            HideLegacyHudChildren(localBinding.Root);
        }

        if (hankyulFire != null)
            HideLegacyHudChildren(localBinding.Root);

        PlayerUIManager[] playerUis = localBinding.Root.GetComponentsInChildren<PlayerUIManager>(true);
        for (int i = 0; i < playerUis.Length; i++)
        {
            PlayerUIManager ui = playerUis[i];
            if (ui != null && ui.crosshairImage != null)
                ui.crosshairImage.gameObject.SetActive(false);
        }
    }

    private void HideComponentObjects<T>() where T : Component
    {
        T[] components = FindObjectsOfType<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
                continue;
            Transform owner = LocalPlayerBindingResolver.PlayerRoot(components[i]);
            if (owner != null && owner != localBinding.Root) continue;
            Canvas parentCanvas = components[i].GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.renderMode == RenderMode.WorldSpace) continue;

            AetherModernHUD ownHud = components[i].GetComponentInParent<AetherModernHUD>(true);
            if (ownHud == null)
                components[i].gameObject.SetActive(false);
        }
    }

    private void UpdateCanvasVisibility()
    {
        if (canvas == null)
            return;

        canvas.enabled = localBinding.Root != null && (!showOnlyWhenGameplayPlayerExists || playerStatus != null);
    }

    private static void HideLegacySliderRoot(Slider slider)
    {
        if (slider == null)
            return;

        Transform root = slider.transform;
        if (root.parent != null)
        {
            string parentName = root.parent.name.ToLowerInvariant();
            if (parentName.Contains("hp") || parentName.Contains("shield") || parentName.Contains("bar"))
                root = root.parent;
        }

        root.gameObject.SetActive(false);
    }

    private static void HideLegacyHudChildren(Transform root)
    {
        if (root == null)
            return;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == root)
                continue;
            if (LocalPlayerBindingResolver.PlayerRoot(child) != root) continue;
            Canvas ownerCanvas = child.GetComponentInParent<Canvas>();
            if (ownerCanvas != null && ownerCanvas.renderMode == RenderMode.WorldSpace) continue;

            if (!LooksLikeLegacyUi(child))
                continue;

            string normalizedName = NormalizeName(child.name);
            for (int tokenIndex = 0; tokenIndex < LegacyHudNameTokens.Length; tokenIndex++)
            {
                if (!normalizedName.Contains(LegacyHudNameTokens[tokenIndex]))
                    continue;

                if (child.GetComponentInParent<AetherModernHUD>(true) == null)
                    child.gameObject.SetActive(false);
                break;
            }
        }
    }

    private static bool LooksLikeLegacyUi(Transform target)
    {
        if (target == null)
            return false;

        return target.GetComponent<RectTransform>() != null
            || target.GetComponent<Graphic>() != null
            || target.GetComponent<Slider>() != null
            || target.GetComponent<Canvas>() != null
            || target.GetComponentInChildren<Graphic>(true) != null
            || target.GetComponentInChildren<TMP_Text>(true) != null;
    }

    private static string NormalizeName(string rawName)
    {
        return string.IsNullOrEmpty(rawName)
            ? string.Empty
            : rawName.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    private void UpdateStatus()
    {
        float hp = playerStatus != null ? playerStatus.currentHP : 100f;
        float maxHp = playerStatus != null ? playerStatus.maxHP : 100f;
        float hpRatio = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
        float thirst = playerStatus != null ? playerStatus.currentThirst : 100f;
        float maxThirst = playerStatus != null ? playerStatus.maxThirst : 100f;
        float thirstRatio = maxThirst > 0f ? Mathf.Clamp01(thirst / maxThirst) : 0f;

        if (hpFill != null) hpFill.fillAmount = hpRatio;
        if (thirstFill != null) thirstFill.fillAmount = thirstRatio;

        int roundedHp = Mathf.RoundToInt(hp);
        int roundedMaxHp = Mathf.RoundToInt(maxHp);
        int roundedThirst = Mathf.RoundToInt(thirst);
        int roundedMaxThirst = Mathf.RoundToInt(maxThirst);

        if (hpValueText != null && (roundedHp != lastHp || roundedMaxHp != lastMaxHp))
        {
            lastHp = roundedHp;
            lastMaxHp = roundedMaxHp;
            hpValueText.text = roundedHp + " / " + roundedMaxHp;
        }

        if (thirstValueText != null && (roundedThirst != lastThirst || roundedMaxThirst != lastMaxThirst))
        {
            lastThirst = roundedThirst;
            lastMaxThirst = roundedMaxThirst;
            thirstValueText.text = roundedThirst + " / " + roundedMaxThirst;
        }
    }

    private void UpdateWeapon()
    {
        if (hankyulFire == null)
            return;

        int currentAmmo = hankyulFire.CurrentMagazineAmmo;
        int reserveAmmo = hankyulFire.CurrentReserveAmmo;
        Hankyul_Fire.FireMode fireMode = hankyulFire.CurrentFireMode;
        bool isReloading = hankyulFire.IsReloading;

        if (currentAmmoText != null && currentAmmo != lastCurrentAmmo)
        {
            lastCurrentAmmo = currentAmmo;
            currentAmmoText.text = currentAmmo.ToString();
        }

        if (reserveAmmoText != null && reserveAmmo != lastReserveAmmo)
        {
            lastReserveAmmo = reserveAmmo;
            reserveAmmoText.text = reserveAmmo.ToString();
        }

        if (fireModeText != null && fireMode != lastFireMode)
        {
            lastFireMode = fireMode;
            fireModeText.text = fireMode == Hankyul_Fire.FireMode.Auto ? "AUTO" : "SINGLE";
        }

        if (magazineFill != null)
            magazineFill.fillAmount = hankyulFire.MagazineSize > 0 ? Mathf.Clamp01((float)currentAmmo / hankyulFire.MagazineSize) : 0f;

        if (reloadStateText != null && isReloading != lastReloading)
        {
            lastReloading = isReloading;
            reloadStateText.text = isReloading ? "RELOADING" : string.Empty;
            currentAmmoText.color = isReloading ? Cyan(1f) : Color.white;
        }
    }

    private void UpdateAetherLevel()
    {
        float ratio = maxAetherExp > 0f ? Mathf.Clamp01(currentAetherExp / maxAetherExp) : 0f;
        if (aetherExpFill != null)
            aetherExpFill.fillAmount = ratio;

        int roundedAetherExp = Mathf.RoundToInt(currentAetherExp);
        int roundedMaxAetherExp = Mathf.RoundToInt(maxAetherExp);

        if (aetherLevelText != null && aetherLevel != lastAetherLevel)
        {
            lastAetherLevel = aetherLevel;
            aetherLevelText.text = aetherLevel.ToString();
        }

        if (aetherExpText != null && (roundedAetherExp != lastAetherExp || roundedMaxAetherExp != lastMaxAetherExp))
        {
            lastAetherExp = roundedAetherExp;
            lastMaxAetherExp = roundedMaxAetherExp;
            aetherExpText.text = roundedAetherExp + " / " + roundedMaxAetherExp;
        }
    }

    private void UpdateCompass()
    {
        if (mainCamera == null)
            return;

        int yaw = Mathf.RoundToInt(mainCamera.transform.eulerAngles.y) % 360;
        if (yaw == lastCompassYaw)
            return;

        lastCompassYaw = yaw;
        if (compassDegreeText != null)
            compassDegreeText.text = yaw.ToString("000");

        int[] offsets = { -90, -60, -30, -15, 0, 15, 30, 60, 90 };
        for (int i = 0; i < compassLabels.Length; i++)
        {
            if (compassLabels[i] == null)
                continue;

            int deg = NormalizeDegree(yaw + offsets[i]);
            compassLabels[i].text = i == 4 ? GetNearestDirectionLabel(deg) : GetCompassLabel(deg);
            compassLabels[i].fontStyle = i == 4 ? FontStyles.Bold : FontStyles.Normal;
            compassLabels[i].color = i == 4 ? Cyan(1f) : new Color(1f, 1f, 1f, 0.76f);
        }
    }

    private static int NormalizeDegree(int degree)
    {
        degree %= 360;
        if (degree < 0) degree += 360;
        return degree;
    }

    private static string GetCompassLabel(int degree)
    {
        int rounded = Mathf.RoundToInt(degree / 15f) * 15;
        rounded = NormalizeDegree(rounded);

        switch (rounded)
        {
            case 0: return "N";
            case 45: return "NE";
            case 90: return "E";
            case 135: return "SE";
            case 180: return "S";
            case 225: return "SW";
            case 270: return "W";
            case 315: return "NW";
            default: return rounded.ToString();
        }
    }

    private static string GetNearestDirectionLabel(int degree)
    {
        int rounded = Mathf.RoundToInt(degree / 45f) * 45;
        rounded = NormalizeDegree(rounded);

        switch (rounded)
        {
            case 0: return "N";
            case 45: return "NE";
            case 90: return "E";
            case 135: return "SE";
            case 180: return "S";
            case 225: return "SW";
            case 270: return "W";
            case 315: return "NW";
            default: return "N";
        }
    }

    private RectTransform CreatePanel(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color backgroundColor)
    {
        RectTransform panel = CreateEmpty(parent, name, anchor, anchor, pivot, anchoredPosition, size);
        RectTransform background = CreateEmpty(panel, "Background", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;
        Image image = background.gameObject.AddComponent<Image>();
        image.sprite = SolidSprite;
        image.color = backgroundColor;
        image.raycastTarget = false;
        return panel;
    }

    private RectTransform CreateTexturePanel(RectTransform parent, string name, string resourcePath, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform panel = CreateEmpty(parent, name, anchor, anchor, pivot, anchoredPosition, size);
        CreateTextureImage(panel, "Background", resourcePath, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return panel;
    }

    private RawImage CreateTextureImage(RectTransform parent, string name, string resourcePath, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform rect = CreateEmpty(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size);
        if (anchorMin != anchorMax)
        {
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = Resources.Load<Texture2D>(resourcePath);
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private Image CreateSolidImage(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform rect = CreateEmpty(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = SolidSprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private RectTransform CreateLine(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        return CreateSolidImage(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size, color).rectTransform;
    }

    private TextMeshProUGUI CreateTMP(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, string value, int fontSize, TextAlignmentOptions alignment, Color color)
    {
        return CreateTMP(parent, name, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), anchoredPosition, size, value, fontSize, alignment, color);
    }

    private TextMeshProUGUI CreateTMP(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, string value, int fontSize, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateEmpty(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = uiFont;
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.text = value;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = Vector4.zero;
        return text;
    }

    private void ResolveUIFont()
    {
        uiFont = Resources.Load<TMP_FontAsset>("UI/Fonts/NanumGothicCoding-Bold SDF");
        if (uiFont == null)
            uiFont = TMP_Settings.defaultFontAsset;
    }

    private static void ConfigureNumericText(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = 11f;
        text.fontSizeMax = text.fontSize;
        text.overflowMode = TextOverflowModes.Overflow;
        text.characterSpacing = 0f;
    }

    private static RectTransform CreateEmpty(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static Vector2 AnchorTopLeft() { return new Vector2(0f, 1f); }
    private static Vector2 AnchorTopCenter() { return new Vector2(0.5f, 1f); }
    private static Vector2 AnchorTopRight() { return new Vector2(1f, 1f); }
    private static Vector2 AnchorBottomLeft() { return new Vector2(0f, 0f); }
    private static Vector2 AnchorBottomRight() { return new Vector2(1f, 0f); }
    private static Vector2 AnchorMiddleLeft() { return new Vector2(0f, 0.5f); }
    private static Vector2 AnchorMiddleRight() { return new Vector2(1f, 0.5f); }
    private static Vector2 AnchorCenter() { return new Vector2(0.5f, 0.5f); }

    private static Color Cyan(float alpha)
    {
        return new Color(0.30f, 0.84f, 1f, alpha);
    }

    private static Sprite solidSprite;
    private static Sprite SolidSprite
    {
        get
        {
            if (solidSprite != null)
                return solidSprite;

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
            return solidSprite;
        }
    }
}
