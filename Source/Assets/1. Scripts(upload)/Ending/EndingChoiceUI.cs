using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 화면 중앙 O/X 선택 UI.
///
/// 이 스크립트는 "선택을 받아서 EndingDecisionManager에 넘기는 것"까지만 한다.
/// 시네마틱을 직접 재생하지 않는다. (요구사항)
///
/// 화면 중앙 배치는 Canvas의 RectTransform 앵커로 처리한다. (스크립트는 표시/숨김만)
/// 커서 처리 방식은 기존 ChoiceUIController와 동일하게 맞췄다.
/// </summary>
public class EndingChoiceUI : MonoBehaviour
{
    [Header("UI 참조")]
    [Tooltip("화면 중앙에 배치된 선택 패널/캔버스")]
    public GameObject choicePanel;
    public Button oButton;
    public Button xButton;

    [Header("선택 결과 처리 (필수)")]
    [Tooltip("비워두면 EndingDecisionManager.Instance를 사용한다.")]
    public EndingDecisionManager decisionManager;

    [Header("버튼 자동 연결")]
    [Tooltip("런타임에 oButton/xButton의 onClick에 SelectO/SelectX를 자동 등록한다. 인스펙터에서 직접 연결해도 중복 호출은 무시된다.")]
    public bool autoBindButtons = true;

    [Header("키보드 입력")]
    public bool allowKeyboardInput = true;

    [Header("사운드 (AudioManager 문자열 key)")]
    public string showSfxKey = "";
    public string selectSfxKey = "btn_Click";

    [Header("플레이어 조작 / 커서")]
    public bool lockPlayerControl = true;
    public bool controlCursor = true;
    [Tooltip("표시 중에는 매 프레임 커서를 보이도록 강제한다. (기존 ChoiceUIController와 동일)")]
    public bool keepCursorVisibleWhileShowing = true;

    public bool IsShowing { get; private set; }
    public EndingChoice SubmittedChoice { get; private set; } = EndingChoice.None;

    public event Action<EndingChoice> OnChoiceSubmitted;

    private readonly PlayerControlLock controlLock = new PlayerControlLock();
    private int shownFrame = -1;

    private void Awake()
    {
        if (choicePanel != null)
            choicePanel.SetActive(false);

        if (autoBindButtons)
        {
            if (oButton != null)
            {
                oButton.onClick.RemoveListener(SelectO);
                oButton.onClick.AddListener(SelectO);
            }

            if (xButton != null)
            {
                xButton.onClick.RemoveListener(SelectX);
                xButton.onClick.AddListener(SelectX);
            }
        }
    }

    private void Update()
    {
        if (!IsShowing)
            return;

        if (controlCursor && keepCursorVisibleWhileShowing)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (!allowKeyboardInput)
            return;

        // 대사를 넘긴 입력이 같은 프레임에 선택까지 확정하지 않도록 가드
        if (Time.frameCount <= shownFrame)
            return;

        if (HasKeyDown(Key.O, KeyCode.O))
            SelectO();
        else if (HasKeyDown(Key.X, KeyCode.X))
            SelectX();
    }

    /// <summary>
    /// 선택 UI를 표시한다.
    /// </summary>
    /// <param name="playerRoot">조작을 잠글 플레이어 오브젝트 (null이면 "Player" 태그로 탐색)</param>
    public void Show(GameObject playerRoot = null)
    {
        if (IsShowing)
            return;

        IsShowing = true;
        SubmittedChoice = EndingChoice.None;
        shownFrame = Time.frameCount;

        if (choicePanel != null)
            choicePanel.SetActive(true);

        if (lockPlayerControl)
            controlLock.Lock(playerRoot);

        if (controlCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        SetButtonsInteractable(true);
        PlaySfxKey(showSfxKey);
    }

    /// <summary>
    /// 선택 UI를 숨긴다.
    /// 플레이어 조작 잠금은 유지한다 — 바로 뒤에 엔딩 시네마틱이 이어지기 때문이다.
    /// 조작을 되돌려야 하면 ReleasePlayerControl()을 호출한다.
    /// </summary>
    public void Hide()
    {
        IsShowing = false;

        if (choicePanel != null)
            choicePanel.SetActive(false);
    }

    /// <summary>
    /// 잠근 플레이어 조작을 원래대로 되돌린다. (엔딩이 아닌 흐름에서 필요할 때 사용)
    /// </summary>
    public void ReleasePlayerControl()
    {
        controlLock.Unlock();
    }

    /// <summary>O 버튼 onClick에 연결. 키보드 O키로도 호출된다.</summary>
    public void SelectO()
    {
        Submit(EndingChoice.O);
    }

    /// <summary>X 버튼 onClick에 연결. 키보드 X키로도 호출된다.</summary>
    public void SelectX()
    {
        Submit(EndingChoice.X);
    }

    private void Submit(EndingChoice choice)
    {
        if (SubmittedChoice != EndingChoice.None)
            return; // 중복 선택 방지 (버튼 중복 연결에도 안전)

        SubmittedChoice = choice;

        PlaySfxKey(selectSfxKey);
        SetButtonsInteractable(false);
        Hide();

        if (OnChoiceSubmitted != null)
            OnChoiceSubmitted(choice);

        EndingDecisionManager manager = decisionManager != null
            ? decisionManager
            : EndingDecisionManager.Instance;

        if (manager == null)
        {
            Debug.LogError("[EndingChoiceUI] EndingDecisionManager를 찾을 수 없어 선택을 전달할 수 없습니다.", this);
            return;
        }

        // 시네마틱 재생은 EndingDecisionManager → EndingCinematicManager가 담당한다.
        manager.SubmitLocalChoice(choice);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (oButton != null) oButton.interactable = interactable;
        if (xButton != null) xButton.interactable = interactable;
    }

    // 새 Input System 우선, 없으면 구 Input Manager로 폴백
    private static bool HasKeyDown(Key newKey, KeyCode legacyKey)
    {
        var kb = Keyboard.current;
        if (kb != null)
            return kb[newKey].wasPressedThisFrame;

        return Input.GetKeyDown(legacyKey);
    }

    // GlobalManager.Audio는 Instance가 null이면 NRE가 나므로 Instance까지 확인한다.
    private static void PlaySfxKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (GlobalManager.Instance == null || GlobalManager.Audio == null) return;

        GlobalManager.Audio.PlaySFX(key);
    }
}
