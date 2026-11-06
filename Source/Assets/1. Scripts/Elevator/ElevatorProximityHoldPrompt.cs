using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ElevatorProximityHoldPrompt : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private ElevatorInteractionController elevator;
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("?곹샇?묒슜")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private float initialHideDuration = 0.75f;
    [SerializeField] private bool suppressIfPlayerStartsInRange = true;
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("UI")]
    [SerializeField] private InteractionHoldUI interactionUI;
    [SerializeField] private string promptText = "F키를 길게 눌러 엘레베이터에 탑승";
    [SerializeField] private Canvas promptCanvas;
    [SerializeField] private CanvasGroup promptGroup;
    [SerializeField] private Image radialFill;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Color circleColor = new Color(0.70f, 0.94f, 1f, 0.92f);
    [SerializeField] private Color fillColor = new Color(1f, 1f, 1f, 0.96f);

    private float holdTimer;
    private float promptAllowedTime;
    private bool holding;
    private bool waitingForPlayerToLeaveRange;
    private Collider[] elevatorColliders;
    private Renderer[] elevatorRenderers;

    private void Awake()
    {
        if (elevator == null)
            elevator = GetComponent<ElevatorInteractionController>();

        if (interactionUI == null)
            interactionUI = FindFirstObjectByType<InteractionHoldUI>(FindObjectsInactive.Include);

        if (interactionUI == null)
            EnsurePromptUI();

        CacheDistanceSources();
        promptAllowedTime = Time.time + initialHideDuration;
        waitingForPlayerToLeaveRange = suppressIfPlayerStartsInRange;
        HidePrompt();
    }

    private void OnEnable()
    {
        CancelHold();
        promptAllowedTime = Time.time + initialHideDuration;
        waitingForPlayerToLeaveRange = suppressIfPlayerStartsInRange;
        HidePrompt();
    }

    private void Update()
    {
        if (Time.time < promptAllowedTime)
        {
            CancelHold();
            HidePrompt();
            return;
        }

        if (elevator == null || !elevator.CanInteract)
        {
            CancelHold();
            HidePrompt();
            return;
        }

        if (player == null)
            FindPlayer();

        if (player == null)
        {
            HidePrompt();
            return;
        }

        bool inRange = GetDistanceToElevator(player.position) <= interactDistance;
        if (waitingForPlayerToLeaveRange)
        {
            if (!inRange)
                waitingForPlayerToLeaveRange = false;

            CancelHold();
            HidePrompt();
            return;
        }

        if (!inRange)
        {
            CancelHold();
            HidePrompt();
            return;
        }

        ShowPrompt();
        HandleInput();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(interactKey))
        {
            holding = true;
            holdTimer = 0f;
            elevator.BeginHold(player.gameObject);
        }

        if (holding && Input.GetKey(interactKey))
        {
            holdTimer += Time.deltaTime;
            float duration = Mathf.Max(0.01f, elevator.GetHoldDuration());
            float progress = Mathf.Clamp01(holdTimer / duration);

            elevator.UpdateHold(player.gameObject, progress);
            SetProgress(progress);

            if (progress >= 1f)
            {
                elevator.CompleteHold(player.gameObject);
                holding = false;
                holdTimer = 0f;
                SetProgress(0f);
                HidePrompt();
            }
        }

        if (holding && Input.GetKeyUp(interactKey))
            CancelHold();
    }

    private void CancelHold()
    {
        if (holding && elevator != null && player != null)
            elevator.CancelHold(player.gameObject);

        holding = false;
        holdTimer = 0f;
        SetProgress(0f);
    }

    private void FindPlayer()
    {
        GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);
        if (foundPlayer != null)
            player = foundPlayer.transform;
    }

    private void CacheDistanceSources()
    {
        elevatorColliders = GetComponentsInChildren<Collider>(true);
        elevatorRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private float GetDistanceToElevator(Vector3 worldPosition)
    {
        float bestDistance = Vector3.Distance(transform.position, worldPosition);

        if (elevatorColliders != null)
        {
            for (int i = 0; i < elevatorColliders.Length; i++)
            {
                Collider targetCollider = elevatorColliders[i];
                if (targetCollider == null || !targetCollider.enabled)
                    continue;

                Vector3 closestPoint = targetCollider.ClosestPoint(worldPosition);
                bestDistance = Mathf.Min(bestDistance, Vector3.Distance(closestPoint, worldPosition));
            }
        }

        if (elevatorRenderers != null)
        {
            for (int i = 0; i < elevatorRenderers.Length; i++)
            {
                Renderer targetRenderer = elevatorRenderers[i];
                if (targetRenderer == null || !targetRenderer.enabled)
                    continue;

                Vector3 closestPoint = targetRenderer.bounds.ClosestPoint(worldPosition);
                bestDistance = Mathf.Min(bestDistance, Vector3.Distance(closestPoint, worldPosition));
            }
        }

        return bestDistance;
    }

    private void ShowPrompt()
    {
        string title = elevator != null && !string.IsNullOrWhiteSpace(elevator.GetInteractionName())
            ? elevator.GetInteractionName().Trim()
            : "엘레베이터";
        string actionText = string.IsNullOrWhiteSpace(promptText)
            ? "[F키를 길게 누르기] " + title
            : promptText.Trim();

        if (interactionUI != null)
        {
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

    private void HidePrompt()
    {
        if (interactionUI != null)
        {
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
            interactionUI.UpdateProgressFor(this, progress);
            return;
        }

        if (radialFill != null)
            radialFill.fillAmount = Mathf.Clamp01(progress);
    }

    private void EnsurePromptUI()
    {
        if (promptCanvas != null && promptGroup != null && radialFill != null)
            return;

        GameObject canvasObject = new GameObject("Elevator Interaction Prompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
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

        labelText = CreateText("Prompt Label", root, "", 24f, Color.white, FontStyles.Bold);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(360f, 42f);
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
