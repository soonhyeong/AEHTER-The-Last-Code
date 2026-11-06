using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHoldInteractor : MonoBehaviour, IInteractionCandidateProvider
{
    [Header("탐색 설정")]
    [SerializeField] private Camera 플레이어카메라;
    [SerializeField] private float 상호작용거리 = 3f;
    [SerializeField] private LayerMask 상호작용레이어 = ~0;
    [SerializeField] private KeyCode 상호작용키 = KeyCode.F;

    [Header("UI 설정")]
    [SerializeField] private GameObject 상호작용UI루트;
    [SerializeField] private TMP_Text 상호작용텍스트;
    [SerializeField] private Image 원형진행이미지;

    private PlayerInteractor owner;
    public int PresentationPriority => 10;
    private void OnEnable()
    {
        owner = GetComponentInParent<PlayerInteractor>();
        if (owner != null) owner.RegisterInteractionProvider(this);
    }
    private void Start() => HideUI();
    private void OnDisable()
    {
        if (owner != null) owner.UnregisterInteractionProvider(this);
        HideUI();
    }
    private void OnDestroy() => OnDisable();
    public bool TryGetCandidate(PlayerInteractor player, out PlayerInteractionArbiter.Candidate candidate)
    {
        candidate = default;
        if (!isActiveAndEnabled || player == null || GetComponentInParent<PlayerInteractor>() != player
            || 상호작용키 != player.interactKey || 플레이어카메라 == null) return false;
        Ray ray = new Ray(플레이어카메라.transform.position, 플레이어카메라.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, 상호작용거리, 상호작용레이어)) return false;
        var hold = hit.collider.GetComponentInParent<IHoldInteractable>();
        if (!(hold is MonoBehaviour target) || target == null || !target.isActiveAndEnabled) return false;
        candidate = new PlayerInteractionArbiter.Candidate { target = target, provider = this, raycast = true, distance = hit.distance };
        return true;
    }
    public void Present(PlayerInteractor player, MonoBehaviour target, float progress, bool visible)
    {
        if (!visible) { HideUI(); return; }
        if (!isActiveAndEnabled || !(target is IHoldInteractable hold)) return;
        if (상호작용UI루트 != null) 상호작용UI루트.SetActive(true);
        if (상호작용텍스트 != null) 상호작용텍스트.text = "[F 길게 누르기] " + hold.GetInteractionName();
        if (원형진행이미지 != null) 원형진행이미지.fillAmount = Mathf.Clamp01(progress);
    }
    private void HideUI()
    {
        if (상호작용UI루트 != null) 상호작용UI루트.SetActive(false);
        if (원형진행이미지 != null) 원형진행이미지.fillAmount = 0f;
    }
}
