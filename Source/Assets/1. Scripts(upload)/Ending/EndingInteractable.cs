using UnityEngine;

/// <summary>
/// NPC / 오브젝트에 붙이는 엔딩 상호작용 스크립트.
///
/// 기존 IInteractable / PlayerInteractor 흐름을 그대로 사용한다.
/// (PlayerInteractor가 카메라 중앙 Raycast로 찾고, F키로 Interact를 호출한다)
///
/// 전체 흐름 조립 담당:
///   F키 → DialogueController.Play(sequence)
///        → (대사 진행)
///        → OnSequenceFinished
///        → sequence.showChoiceAtEnd면 EndingChoiceUI.Show()
///             → EndingDecisionManager.SubmitLocalChoice()
///                  → EndingCinematicManager.PlayEnding()
///
/// IInteractSfxProvider도 구현하므로, 상호작용 시 아이템 pickup 소리가 나지 않는다.
/// (interactSfxKey를 비워두면 무음)
/// </summary>
[RequireComponent(typeof(Collider))]
public class EndingInteractable : MonoBehaviour, IInteractable, IInteractSfxProvider
{
    [Header("UI 표시 정보")]
    [SerializeField] private string displayName = "대화하기";
    [SerializeField] private Sprite icon;

    [Header("대화")]
    [SerializeField] private DialogueSequence sequence;
    [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private DialogueController dialogueController;

    [Header("선택 UI (sequence.showChoiceAtEnd가 true일 때 사용)")]
    [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private EndingChoiceUI choiceUI;

    [Header("상호작용 설정")]
    [Tooltip("한 번만 상호작용할 수 있게 한다.")]
    [SerializeField] private bool oneShot = true;

    [Tooltip("PlayerInteractor가 재생할 SFX key. 비우면 무음 (pickup 소리가 나지 않는다).")]
    [SerializeField] private string interactSfxKey = "";

    [Header("플레이어별 대사 라인")]
    [SerializeField] private bool usePlayerSpecificLineRanges = true;
    [SerializeField] private Vector2Int shiriLineRange = new Vector2Int(0, 2);
    [SerializeField] private Vector2Int mekaLineRange = new Vector2Int(3, 5);
    [SerializeField] private Vector2Int commonLineRange = new Vector2Int(6, 8);

    private bool hasInteracted;
    private bool isWaitingForDialogue;
    private GameObject cachedPlayerRoot;
    private PlayerInteractor cachedInteractor;

    public string GetDisplayName()
    {
        return !string.IsNullOrEmpty(displayName) ? displayName : gameObject.name;
    }

    public Sprite GetIcon()
    {
        return icon;
    }

    public string GetInteractSfxKey()
    {
        return interactSfxKey;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (oneShot && hasInteracted)
            return;

        if (isWaitingForDialogue)
            return;

        if (sequence == null)
        {
            Debug.LogError("[EndingInteractable] DialogueSequence가 연결되지 않았습니다.", this);
            return;
        }

        if (dialogueController == null)
            dialogueController = FindAnyObjectByType<DialogueController>();

        if (dialogueController == null)
        {
            Debug.LogError("[EndingInteractable] 씬에서 DialogueController를 찾을 수 없습니다.", this);
            return;
        }

        if (dialogueController.IsPlaying)
            return;

        cachedPlayerRoot = interactor != null ? interactor.gameObject : null;
        cachedInteractor = interactor;
        SetInteractionUISuppressed(true);
        hasInteracted = true;
        isWaitingForDialogue = true;

        dialogueController.OnSequenceFinished -= HandleSequenceFinished;
        dialogueController.OnSequenceFinished += HandleSequenceFinished;

        dialogueController.Play(sequence, cachedPlayerRoot, BuildDialogueLineIndices(interactor));
    }

    private int[] BuildDialogueLineIndices(PlayerInteractor interactor)
    {
        if (!usePlayerSpecificLineRanges)
            return null;

        Vector2Int characterRange = IsMeka(interactor) ? mekaLineRange : shiriLineRange;
        int characterCount = CountRange(characterRange);
        int commonCount = CountRange(commonLineRange);
        int[] indices = new int[characterCount + commonCount];
        int cursor = 0;

        AddRange(indices, ref cursor, characterRange);
        AddRange(indices, ref cursor, commonLineRange);

        return indices;
    }

    private static bool IsMeka(PlayerInteractor interactor)
    {
        if (interactor == null)
            return false;

        return interactor.GetComponentInParent<Yunseo_Move>() != null ||
               interactor.GetComponentInChildren<Yunseo_Move>() != null;
    }

    private static int CountRange(Vector2Int range)
    {
        return Mathf.Max(0, range.y - range.x + 1);
    }

    private static void AddRange(int[] indices, ref int cursor, Vector2Int range)
    {
        int count = CountRange(range);

        for (int i = 0; i < count; i++)
        {
            indices[cursor] = range.x + i;
            cursor++;
        }
    }

    private void HandleSequenceFinished(DialogueSequence finished)
    {
        if (finished != sequence)
            return;

        isWaitingForDialogue = false;

        if (dialogueController != null)
            dialogueController.OnSequenceFinished -= HandleSequenceFinished;

        if (!sequence.showChoiceAtEnd)
        {
            SetInteractionUISuppressed(false);
            return;
        }

        if (choiceUI == null)
            choiceUI = FindAnyObjectByType<EndingChoiceUI>();

        if (choiceUI == null)
        {
            Debug.LogError("[EndingInteractable] 씬에서 EndingChoiceUI를 찾을 수 없습니다.", this);
            SetInteractionUISuppressed(false);
            return;
        }

        // 선택 UI 표시까지만 한다. 엔딩 결정/재생은 EndingDecisionManager가 담당한다.
        choiceUI.OnChoiceSubmitted -= HandleChoiceSubmitted;
        choiceUI.OnChoiceSubmitted += HandleChoiceSubmitted;
        choiceUI.Show(cachedPlayerRoot);
    }

    private void HandleChoiceSubmitted(EndingChoice choice)
    {
        if (choiceUI != null)
            choiceUI.OnChoiceSubmitted -= HandleChoiceSubmitted;

        // 엔딩 시네마틱이 바로 이어지므로 상호작용 안내 UI는 숨긴 상태로 유지한다.
    }

    private void SetInteractionUISuppressed(bool suppressed)
    {
        if (cachedInteractor != null)
            cachedInteractor.SetInteractionUISuppressed(suppressed);
    }

    private void OnDisable()
    {
        if (dialogueController != null)
            dialogueController.OnSequenceFinished -= HandleSequenceFinished;

        if (choiceUI != null)
            choiceUI.OnChoiceSubmitted -= HandleChoiceSubmitted;

        SetInteractionUISuppressed(false);
        isWaitingForDialogue = false;
    }
}
