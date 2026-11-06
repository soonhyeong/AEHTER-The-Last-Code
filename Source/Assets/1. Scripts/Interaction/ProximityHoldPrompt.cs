using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProximityHoldPrompt : MonoBehaviour, IInteractionCandidateProvider
{
    [Header("Target")]
    [SerializeField] private MonoBehaviour interactableBehaviour;
    [SerializeField] private Transform player;
    // Kept only for existing serialized data; never used to choose an input owner.
#pragma warning disable CS0414
    [SerializeField] private string playerTag = "Player";
#pragma warning restore CS0414

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private float initialHideDuration = 0.75f;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string promptText = "";
    [SerializeField] private bool useBoundsDistance = true;
    [SerializeField] private float minValidBoundsThickness = 0.05f;
    [SerializeField] private bool requirePlayerFacingTarget = false;
    [SerializeField, Range(1f, 90f)] private float maxFacingAngle = 35f;

    [Header("UI")]
    [SerializeField] private InteractionHoldUI interactionUI;
    [SerializeField] private Canvas promptCanvas;
    [SerializeField] private CanvasGroup promptGroup;
    [SerializeField] private Image radialFill;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Color circleColor = new Color(0.70f, 0.94f, 1f, 0.92f);
    [SerializeField] private Color fillColor = new Color(1f, 1f, 1f, 0.96f);

    private IHoldInteractable interactable;
    private Collider[] targetColliders;
    private Renderer[] targetRenderers;
    private PlayerInteractor viewOwner;
    private readonly System.Collections.Generic.List<PlayerInteractor> registeredPlayers = new System.Collections.Generic.List<PlayerInteractor>();
    public int PresentationPriority => 0;
    private float promptAllowedTime;


    private void Awake()
    {
        interactable = GetComponent<IHoldInteractable>();

        if (interactable == null && interactableBehaviour != null)
            interactable = interactableBehaviour as IHoldInteractable;

        if (interactionUI == null)
            interactionUI = FindFirstObjectByType<InteractionHoldUI>(FindObjectsInactive.Include);

        if (interactionUI == null)
            EnsurePromptUI();

        CacheDistanceSources();
        promptAllowedTime = Time.time + initialHideDuration;
        HidePrompt();
    }

    private void OnEnable()
    {
        promptAllowedTime = Time.time + initialHideDuration;
        HidePrompt();
        foreach (var candidatePlayer in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None))
        {
            candidatePlayer.RegisterInteractionProvider(this);
            if (!registeredPlayers.Contains(candidatePlayer)) registeredPlayers.Add(candidatePlayer);
        }
    }
    private void OnDisable()
    {
        foreach (var candidatePlayer in registeredPlayers)
            if (candidatePlayer != null) candidatePlayer.UnregisterInteractionProvider(this);
        registeredPlayers.Clear();
        viewOwner = null;
        HidePrompt();
    }
    private void OnDestroy() => OnDisable();
    public bool TryGetCandidate(PlayerInteractor candidatePlayer, out PlayerInteractionArbiter.Candidate candidate)
    {
        candidate = default;
        if (!isActiveAndEnabled || candidatePlayer == null || interactKey != candidatePlayer.interactKey
            || Time.time < promptAllowedTime) return false;
        if (player != null && player.GetComponentInParent<PlayerInteractor>() != candidatePlayer
            && candidatePlayer.transform != player && !candidatePlayer.transform.IsChildOf(player)) return false;
        if (interactable == null)
            interactable = GetComponent<IHoldInteractable>() ?? interactableBehaviour as IHoldInteractable;
        if (!(interactable is MonoBehaviour target) || target == null || !target.isActiveAndEnabled) return false;
        float distance = GetDistanceToTarget(candidatePlayer.transform.position);
        if (distance > interactDistance || (requirePlayerFacingTarget && !IsPlayerFacingTarget(candidatePlayer))) return false;
        candidate = new PlayerInteractionArbiter.Candidate { target = target, provider = this, distance = distance };
        if (!registeredPlayers.Contains(candidatePlayer)) registeredPlayers.Add(candidatePlayer);
        return true;
    }
    public void Present(PlayerInteractor candidatePlayer, MonoBehaviour target, float progress, bool visible)
    {
        if (!visible)
        {
            if (viewOwner != null && viewOwner != candidatePlayer) return;
            viewOwner = null;
            HidePrompt();
            return;
        }
        if (!isActiveAndEnabled || candidatePlayer == null || (viewOwner != null && viewOwner != candidatePlayer)) return;
        bool changed = viewOwner != candidatePlayer;
        viewOwner = candidatePlayer;
        if (changed) ShowPrompt();
        SetProgress(progress);
    }

    private void CacheDistanceSources()
    {
        targetColliders = GetComponentsInChildren<Collider>(true);
        targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private float GetDistanceToTarget(Vector3 worldPosition)
    {
        float bestDistance = Vector3.Distance(transform.position, worldPosition);

        if (!useBoundsDistance)
            return bestDistance;

        if (targetColliders != null)
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider targetCollider = targetColliders[i];
                if (targetCollider == null || !targetCollider.enabled)
                    continue;

                if (!HasUsableBounds(targetCollider.bounds))
                    continue;

                Vector3 closestPoint = targetCollider.ClosestPoint(worldPosition);
                bestDistance = Mathf.Min(bestDistance, Vector3.Distance(closestPoint, worldPosition));
            }
        }

        if (targetRenderers != null)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null || !targetRenderer.enabled)
                    continue;

                if (!HasUsableBounds(targetRenderer.bounds))
                    continue;

                Vector3 closestPoint = targetRenderer.bounds.ClosestPoint(worldPosition);
                bestDistance = Mathf.Min(bestDistance, Vector3.Distance(closestPoint, worldPosition));
            }
        }

        return bestDistance;
    }

    private bool HasUsableBounds(Bounds bounds)
    {
        Vector3 size = bounds.size;
        return size.x >= minValidBoundsThickness
            && size.y >= minValidBoundsThickness
            && size.z >= minValidBoundsThickness;
    }

    private bool IsPlayerFacingTarget(PlayerInteractor candidatePlayer)
    {
        Camera playerCamera = candidatePlayer.playerCamera;
        if (playerCamera == null) playerCamera = candidatePlayer.GetComponentInChildren<Camera>();

        if (playerCamera == null)
            return false;

        Vector3 aimPoint = GetAimPoint();
        Vector3 direction = aimPoint - playerCamera.transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return true;

        float angle = Vector3.Angle(playerCamera.transform.forward, direction.normalized);
        return angle <= maxFacingAngle;
    }

    private Vector3 GetAimPoint()
    {
        if (targetColliders != null)
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider targetCollider = targetColliders[i];
                if (targetCollider != null && targetCollider.enabled && HasUsableBounds(targetCollider.bounds))
                    return targetCollider.bounds.center;
            }
        }

        if (targetRenderers != null)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer != null && targetRenderer.enabled && HasUsableBounds(targetRenderer.bounds))
                    return targetRenderer.bounds.center;
            }
        }

        return transform.position;
    }

    private void ShowPrompt()
    {
        string title = GetPromptTitle();
        string actionText = GetPromptActionText(title);

        if (interactionUI != null)
        {
            HideLocalPrompt();
            interactionUI.ShowFor(this, title, actionText);
            return;
        }

        if (promptGroup == null)
            return;

        promptGroup.alpha = 1f;
        promptGroup.interactable = false;
        promptGroup.blocksRaycasts = false;

        if (labelText != null)
            labelText.text = title + "\n" + actionText;
    }

    private string GetPromptTitle()
    {
        if (interactable == null)
            return "상호작용";

        string name = interactable.GetInteractionName();
        return string.IsNullOrWhiteSpace(name) ? "상호작용" : name.Trim();
    }

    private string GetPromptActionText(string title)
    {
        if (!string.IsNullOrWhiteSpace(promptText))
            return promptText.Trim();

        return "[F키를 길게 누르기] " + title;
    }

    private void HidePrompt()
    {
        if (interactionUI != null)
        {
            HideLocalPrompt();
            interactionUI.HideFor(this);
            return;
        }

        if (promptGroup == null)
            return;

        promptGroup.alpha = 0f;
        promptGroup.interactable = false;
        promptGroup.blocksRaycasts = false;
    }

    private void SetProgress(float progress)
    {
        if (interactionUI != null)
        {
            HideLocalPrompt();
            interactionUI.UpdateProgressFor(this, progress);
            return;
        }

        if (radialFill != null)
            radialFill.fillAmount = Mathf.Clamp01(progress);
    }

    private void HideLocalPrompt()
    {
        if (promptGroup != null)
        {
            promptGroup.alpha = 0f;
            promptGroup.interactable = false;
            promptGroup.blocksRaycasts = false;
        }

        if (promptCanvas != null)
            promptCanvas.enabled = false;
    }

    private void EnsurePromptUI()
    {
        if (promptCanvas != null && promptGroup != null && radialFill != null)
            return;

        GameObject canvasObject = new GameObject("Proximity Interaction Prompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        promptCanvas = canvasObject.GetComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        promptCanvas.sortingOrder = 350;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        promptGroup = canvasObject.GetComponent<CanvasGroup>();

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        GameObject circleObject = new GameObject("Hold Circle", typeof(RectTransform), typeof(Image));
        circleObject.transform.SetParent(root, false);
        Image circle = circleObject.GetComponent<Image>();
        circle.color = new Color(0f, 0.03f, 0.05f, 0.58f);
        circle.raycastTarget = false;
        RectTransform circleRect = circleObject.GetComponent<RectTransform>();
        circleRect.anchorMin = new Vector2(0.5f, 0.5f);
        circleRect.anchorMax = new Vector2(0.5f, 0.5f);
        circleRect.pivot = new Vector2(0.5f, 0.5f);
        circleRect.sizeDelta = new Vector2(96f, 96f);
        circleRect.anchoredPosition = new Vector2(0f, -150f);

        GameObject fillObject = new GameObject("Hold Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(circleRect, false);
        radialFill = fillObject.GetComponent<Image>();
        radialFill.color = fillColor;
        radialFill.type = Image.Type.Filled;
        radialFill.fillMethod = Image.FillMethod.Radial360;
        radialFill.fillOrigin = 2;
        radialFill.fillClockwise = true;
        radialFill.fillAmount = 0f;
        radialFill.raycastTarget = false;
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(8f, 8f);
        fillRect.offsetMax = new Vector2(-8f, -8f);

        keyText = CreateText("Key Text", circleRect, "F", 42f, circleColor, FontStyles.Bold);
        Stretch(keyText.rectTransform);

        labelText = CreateText("Prompt Label", root, string.Empty, 24f, Color.white, FontStyles.Bold);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(420f, 42f);
        labelRect.anchoredPosition = new Vector2(0f, -222f);
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text tmp = textObject.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
