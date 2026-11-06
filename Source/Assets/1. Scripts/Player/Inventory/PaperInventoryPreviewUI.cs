using UnityEngine;
using UnityEngine.UI;

public class PaperInventoryPreviewUI : MonoBehaviour
{
    private const string PreviewSpritePath = "AETHERParts/PaperInventoryPreview";
    private const string FallbackSpritePath = "AETHERParts/PaperRecipePreview";

    private static PaperInventoryPreviewUI instance;

    private CanvasGroup canvasGroup;
    private Image previewImage;
    private ItemData currentItem;
    private PlayerModalControlContext playerContext;
    private PlayerModalControlContext.Lease modalLease;

    public static void Toggle(ItemData item)
    {
        // Preserve the signature, but never guess a Player for an unbound preview.
        Toggle(item, instance != null ? instance.playerContext : null);
    }

    public static void Toggle(ItemData item, PlayerModalControlContext context)
    {
        if (item == null || context == null || !context.isActiveAndEnabled)
            return;

        PaperInventoryPreviewUI ui = GetOrCreate();
        if (ui.gameObject.activeSelf && ui.playerContext != null && ui.playerContext != context)
            return; // A second Player cannot replace the open preview's ownership.
        if (ui.gameObject.activeSelf && ui.currentItem == item)
        {
            ui.Hide();
            return;
        }

        ui.Show(item, context);
    }

    public static void HideIfOpen()
    {
        if (instance != null)
            instance.Hide();
    }

    private static PaperInventoryPreviewUI GetOrCreate()
    {
        if (instance != null)
            return instance;

        GameObject canvasObject = new GameObject("Paper Inventory Preview UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        if (Application.isPlaying)
            Object.DontDestroyOnLoad(canvasObject);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        instance = canvasObject.AddComponent<PaperInventoryPreviewUI>();
        instance.BuildUI(canvasObject.GetComponent<CanvasGroup>());
        instance.Hide();
        return instance;
    }

    private void BuildUI(CanvasGroup group)
    {
        canvasGroup = group;

        GameObject dimObject = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dimObject.transform.SetParent(transform, false);

        RectTransform dimRect = dimObject.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        Image dim = dimObject.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);

        Button dimButton = dimObject.GetComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Hide);

        GameObject imageObject = new GameObject("Paper Preview Image", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(transform, false);

        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = Vector2.zero;

        previewImage = imageObject.GetComponent<Image>();
        previewImage.preserveAspect = true;
        previewImage.raycastTarget = false;
    }

    private void Update()
    {
        if (gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            Hide();

    }

    private void LateUpdate()
    {
        if (gameObject.activeSelf && (playerContext == null || modalLease == null || modalLease.IsReleased))
            Hide();
    }

    private void Show(ItemData item, PlayerModalControlContext context)
    {
        if (context == null || !context.isActiveAndEnabled) return;
        playerContext = context;
        try
        {
            gameObject.SetActive(true);
            modalLease = context.Acquire(this);
            if (modalLease == null || modalLease.IsReleased) { Hide(); return; }
            currentItem = item;

            Sprite sprite = Resources.Load<Sprite>(PreviewSpritePath);
            if (sprite == null)
                sprite = Resources.Load<Sprite>(FallbackSpritePath);

            if (previewImage != null)
            {
                previewImage.sprite = sprite;
                previewImage.enabled = sprite != null;

                RectTransform rect = previewImage.rectTransform;
                if (sprite != null)
                {
                    Vector2 size = sprite.rect.size * 2f;
                    size.x = Mathf.Min(size.x, 1400f);
                    size.y = Mathf.Min(size.y, 820f);
                    rect.sizeDelta = size;
                }
                else
                {
                    rect.sizeDelta = new Vector2(1100f, 620f);
                }
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

        }
        catch { Hide(); throw; }
    }

    private void Hide()
    {
        try
        {
            currentItem = null;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }
        finally { ReleaseOwnership(); }
    }

    private void ReleaseOwnership()
    {
        modalLease?.Release();
        modalLease = null;
        playerContext = null;
    }

    private void OnDisable() { Hide(); }
    private void OnDestroy()
    {
        ReleaseOwnership();
        if (instance == this) instance = null;
    }
}
