using UnityEngine;
using UnityEngine.UI;

public class ModuleWorldMarker : MonoBehaviour
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private float maxVisibleDistance = 18f;
    [SerializeField] private float fullVisibleDistance = 7f;
    [SerializeField] private float closeFadeDistance = 1.6f;
    [SerializeField] private float bobAmplitude = 0.08f;
    [SerializeField] private float bobSpeed = 2.4f;
    [SerializeField] private Vector2 markerSize = new Vector2(0.1f, 0.1f);
    [SerializeField] private Color glowColor = new Color(0.15f, 0.95f, 1f, 0.42f);
    [SerializeField] private Color iconColor = Color.white;

    private Transform markerRoot;
    private CanvasGroup canvasGroup;
    private Image iconImage;
    private Camera cachedCamera;
    private float seed;

    public void Configure(ItemData data)
    {
        if (data != null)
            itemData = data;

        EnsureMarker();
        RefreshIcon();
    }

    private void Awake()
    {
        seed = Random.value * 10f;
        EnsureMarker();
        RefreshIcon();
    }

    private void LateUpdate()
    {
        EnsureMarker();

        if (cachedCamera == null)
            cachedCamera = Camera.main;

        if (cachedCamera == null || markerRoot == null || canvasGroup == null)
            return;

        Vector3 bob = Vector3.up * (Mathf.Sin(Time.time * bobSpeed + seed) * bobAmplitude);
        markerRoot.position = transform.position + worldOffset + bob;
        markerRoot.forward = cachedCamera.transform.forward;

        float distance = Vector3.Distance(cachedCamera.transform.position, transform.position);
        float farAlpha = Mathf.InverseLerp(maxVisibleDistance, fullVisibleDistance, distance);
        float closeAlpha = Mathf.InverseLerp(0.4f, closeFadeDistance, distance);
        canvasGroup.alpha = Mathf.Clamp01(farAlpha * closeAlpha);
        canvasGroup.gameObject.SetActive(canvasGroup.alpha > 0.02f);
    }

    private void EnsureMarker()
    {
        if (markerRoot != null)
            return;

        Transform existing = transform.Find("Module World Marker");
        if (existing != null)
        {
            markerRoot = existing;
            canvasGroup = markerRoot.GetComponent<CanvasGroup>();
            iconImage = markerRoot.GetComponentInChildren<Image>();
            if (canvasGroup == null)
                canvasGroup = markerRoot.gameObject.AddComponent<CanvasGroup>();
            return;
        }

        GameObject root = new GameObject("Module World Marker", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        root.transform.localPosition = worldOffset;
        root.layer = gameObject.layer;

        markerRoot = root.transform;
        canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = markerSize;
        rootRect.localScale = Vector3.one;

        GameObject glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        glow.transform.SetParent(root.transform, false);
        RectTransform glowRect = glow.GetComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = Vector2.zero;
        glowRect.offsetMax = Vector2.zero;
        glowRect.localEulerAngles = new Vector3(0f, 0f, 45f);
        Image glowImage = glow.GetComponent<Image>();
        glowImage.color = glowColor;
        glowImage.raycastTarget = false;

        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(root.transform, false);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.18f, 0.18f);
        iconRect.anchorMax = new Vector2(0.82f, 0.82f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        iconImage = icon.GetComponent<Image>();
        iconImage.color = iconColor;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
    }

    private void RefreshIcon()
    {
        if (iconImage == null)
            return;

        if (itemData == null)
            itemData = PartResourceResolver.FindItemData(gameObject.name);

        Sprite sprite = itemData != null ? itemData.itemSprite : null;
        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
    }
}
