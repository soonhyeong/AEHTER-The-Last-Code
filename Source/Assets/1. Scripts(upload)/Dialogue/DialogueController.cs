using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 대사 UI 전담 컨트롤러.
///
/// 역할:
/// - DialogueSequence를 받아 한 줄씩 표시
/// - 마우스 클릭 / Space / Enter 로 다음 대사 진행 (타이핑 중이면 즉시 완성)
/// - 대화 중 플레이어 이동/시야 잠금, 커서 표시
///
/// 이 스크립트는 엔딩/선택 UI를 전혀 모른다.
/// 마지막 대사 후 처리는 OnSequenceFinished 이벤트를 받는 쪽(EndingInteractable)이 담당한다.
/// → 일반 NPC 대화에도 그대로 재사용할 수 있다.
/// </summary>
public class DialogueController : MonoBehaviour
{
    [Header("UI 참조")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI lineText;
    [Tooltip("\"▼\" 같은 계속 표시 아이콘 (선택)")]
    public GameObject continueIndicator;

    [Header("타이핑 효과")]
    public bool useTypewriter = true;
    [Range(0f, 0.3f)]
    public float charInterval = 0.03f;

    [Header("진행 입력")]
    public bool advanceOnMouseClick = true;
    public bool advanceOnSpaceKey = true;
    public bool advanceOnEnterKey = true;

    [Header("플레이어 조작 / 커서")]
    public bool lockPlayerControl = true;
    [Tooltip("대화가 끝나면 이동/시야를 원래대로 되돌린다. (선택 UI가 이어질 때도 같은 프레임에 다시 잠기므로 안전)")]
    public bool unlockPlayerControlOnFinish = true;
    public bool controlCursor = true;
    [Tooltip("대화가 끝나면 커서를 다시 잠근다. 선택 UI가 이어지면 그 쪽에서 다시 풀어준다.")]
    public bool restoreCursorOnFinish = true;

    public bool IsPlaying { get; private set; }
    public DialogueSequence CurrentSequence { get; private set; }
    public int CurrentLineIndex { get; private set; } = -1;

    public event Action<DialogueSequence> OnSequenceStarted;
    public event Action<DialogueSequence> OnSequenceFinished;

    private readonly PlayerControlLock controlLock = new PlayerControlLock();
    private Coroutine typingRoutine;
    private bool isTyping;
    private string currentFullText = string.Empty;
    private int[] activeLineIndices;
    private int activeLineCursor = -1;

    // 대화를 연 F키 입력이 같은 프레임에 첫 대사를 넘겨버리지 않도록 하는 가드.
    // (ShopManager.resultMessageShownFrame과 동일한 방식)
    private int inputGuardFrame = -1;

    private void Awake()
    {
        ClearText();

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        SetContinueIndicator(false);
    }

    private void Update()
    {
        if (!IsPlaying) return;
        if (Time.frameCount <= inputGuardFrame) return;
        if (!HasAdvanceInput()) return;

        Advance();
    }

    /// <summary>
    /// 대화를 시작한다.
    /// </summary>
    /// <param name="sequence">재생할 대화 데이터</param>
    /// <param name="playerRoot">조작을 잠글 플레이어 오브젝트 (null이면 "Player" 태그로 탐색)</param>
    public void Play(DialogueSequence sequence, GameObject playerRoot = null)
    {
        Play(sequence, playerRoot, null);
    }

    public void Play(DialogueSequence sequence, GameObject playerRoot, int[] lineIndices)
    {
        if (sequence == null)
        {
            Debug.LogWarning("[DialogueController] DialogueSequence가 비어 있습니다.", this);
            return;
        }

        if (sequence.LineCount == 0)
        {
            Debug.LogWarning($"[DialogueController] '{sequence.name}'에 대사가 없습니다.", this);
            return;
        }

        if (IsPlaying)
            return;

        CurrentSequence = sequence;
        CurrentLineIndex = -1;
        activeLineIndices = lineIndices;
        activeLineCursor = -1;
        ClearText();
        IsPlaying = true;
        inputGuardFrame = Time.frameCount;

        if (lockPlayerControl)
            controlLock.Lock(playerRoot);

        if (controlCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        if (!string.IsNullOrEmpty(sequence.bgmKey))
            PlayBgmKey(sequence.bgmKey);

        if (OnSequenceStarted != null)
            OnSequenceStarted(sequence);

        ShowNextLine();
    }

    /// <summary>
    /// 다음 대사로 진행한다. (타이핑 중이면 먼저 즉시 완성)
    /// UI 버튼 onClick에 직접 연결해도 된다.
    /// </summary>
    public void Advance()
    {
        if (!IsPlaying) return;

        if (CurrentSequence != null && !string.IsNullOrEmpty(CurrentSequence.advanceSfxKey))
            PlaySfxKey(CurrentSequence.advanceSfxKey);

        if (isTyping)
        {
            CompleteTyping();
            return;
        }

        ShowNextLine();
    }

    /// <summary>
    /// 대화를 강제로 닫는다. (OnSequenceFinished는 발생하지 않는다)
    /// </summary>
    public void Close()
    {
        StopTyping();

        IsPlaying = false;
        CurrentSequence = null;
        CurrentLineIndex = -1;
        activeLineIndices = null;
        activeLineCursor = -1;
        currentFullText = string.Empty;
        ClearText();

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        SetContinueIndicator(false);

        if (unlockPlayerControlOnFinish)
            controlLock.Unlock();

        if (controlCursor && restoreCursorOnFinish)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void ShowNextLine()
    {
        if (activeLineIndices != null && activeLineIndices.Length > 0)
        {
            ShowNextFilteredLine();
            return;
        }

        int next = CurrentLineIndex + 1;

        while (CurrentSequence != null && next < CurrentSequence.LineCount)
        {
            DialogueLine line;
            if (CurrentSequence.TryGetLine(next, out line))
            {
                CurrentLineIndex = next;
                ApplyLine(line);
                return;
            }

            next++; // 비어 있는 항목은 건너뛴다
        }

        Finish();
    }

    private void ShowNextFilteredLine()
    {
        int nextCursor = activeLineCursor + 1;

        while (CurrentSequence != null && nextCursor < activeLineIndices.Length)
        {
            int lineIndex = activeLineIndices[nextCursor];
            DialogueLine line;

            if (CurrentSequence.TryGetLine(lineIndex, out line))
            {
                activeLineCursor = nextCursor;
                CurrentLineIndex = lineIndex;
                ApplyLine(line);
                return;
            }

            nextCursor++;
        }

        Finish();
    }

    private void ApplyLine(DialogueLine line)
    {
        if (speakerText != null)
        {
            bool hasSpeaker = !string.IsNullOrEmpty(line.speakerName);
            speakerText.gameObject.SetActive(hasSpeaker);

            if (hasSpeaker)
                speakerText.text = line.speakerName;
        }

        currentFullText = line.text != null ? line.text : string.Empty;

        StopTyping();

        if (useTypewriter && charInterval > 0f && currentFullText.Length > 0)
        {
            isTyping = true;
            SetContinueIndicator(false);
            typingRoutine = StartCoroutine(TypeRoutine());
        }
        else
        {
            if (lineText != null)
                lineText.text = currentFullText;

            isTyping = false;
            SetContinueIndicator(true);
        }
    }

    private IEnumerator TypeRoutine()
    {
        if (lineText != null)
            lineText.text = string.Empty;

        for (int i = 0; i < currentFullText.Length; i++)
        {
            if (lineText != null)
                lineText.text = currentFullText.Substring(0, i + 1);

            yield return new WaitForSeconds(charInterval);
        }

        isTyping = false;
        typingRoutine = null;
        SetContinueIndicator(true);
    }

    private void CompleteTyping()
    {
        StopTyping();

        if (lineText != null)
            lineText.text = currentFullText;

        SetContinueIndicator(true);
    }

    private void StopTyping()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        isTyping = false;
    }

    private void Finish()
    {
        DialogueSequence finished = CurrentSequence;

        Close();

        if (OnSequenceFinished != null)
            OnSequenceFinished(finished);
    }

    private void SetContinueIndicator(bool visible)
    {
        if (continueIndicator != null)
            continueIndicator.SetActive(visible);
    }

    private void ClearText()
    {
        if (speakerText != null)
        {
            speakerText.text = string.Empty;
            speakerText.gameObject.SetActive(false);
        }

        if (lineText != null)
            lineText.text = string.Empty;
    }

    // 새 Input System 우선, 없으면 구 Input Manager로 폴백.
    // (ShopManager.HasDismissMessageInput()과 동일한 패턴)
    private bool HasAdvanceInput()
    {
        if (advanceOnMouseClick)
        {
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                    return true;
            }
            else if (Input.GetMouseButtonDown(0))
            {
                return true;
            }
        }

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (advanceOnSpaceKey && kb.spaceKey.wasPressedThisFrame)
                return true;

            if (advanceOnEnterKey && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                return true;

            return false;
        }

        if (advanceOnSpaceKey && Input.GetKeyDown(KeyCode.Space))
            return true;

        if (advanceOnEnterKey && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            return true;

        return false;
    }

    // GlobalManager.Audio는 Instance가 null이면 NRE가 나므로 Instance까지 확인한다.
    private static void PlaySfxKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (GlobalManager.Instance == null || GlobalManager.Audio == null) return;

        GlobalManager.Audio.PlaySFX(key);
    }

    private static void PlayBgmKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (GlobalManager.Instance == null || GlobalManager.Audio == null) return;

        GlobalManager.Audio.PlayBGM(key);
    }
}
