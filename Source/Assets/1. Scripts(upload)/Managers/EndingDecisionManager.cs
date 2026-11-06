using System;
using UnityEngine;

/// <summary>
/// O/X 선택을 모아 EndingType을 결정하고, 시네마틱 재생을 지시한다.
///
/// 역할 분리:
/// - EndingChoiceUI   : 선택 UI 표시 / 입력만 담당 (시네마틱을 직접 재생하지 않는다)
/// - EndingDecisionManager (이 파일) : 선택 조합 → EndingType 결정
/// - EndingCinematicManager : 결정된 EndingType에 맞는 시네마틱 재생
///
/// 솔로플레이(soloPlay = true):
///   로컬 선택을 파트너 선택으로 그대로 복사한다. → O는 BothO, X는 BothX
///
/// 멀티플레이 확장:
///   soloPlay를 끄고 transportBehaviour에 IEndingChoiceTransport 구현체를 연결하면
///   localChoice + remoteChoice 조합으로 4가지 엔딩이 결정된다.
///   (지금은 패키지를 설치하지 않으므로 구현체 없음)
/// </summary>
public class EndingDecisionManager : MonoBehaviour
{
    public static EndingDecisionManager Instance { get; private set; }

    [Header("시네마틱 재생 (필수)")]
    public EndingCinematicManager cinematicManager;

    [Header("플레이 모드")]
    [Tooltip("솔로플레이: 로컬 선택을 파트너 선택으로 복사한다. (O → BothO, X → BothX)")]
    public bool soloPlay = true;

    [Header("멀티플레이 확장 (현재는 비워둔다)")]
    [Tooltip("IEndingChoiceTransport를 구현한 컴포넌트. 인터페이스는 인스펙터 직렬화가 안 되므로 MonoBehaviour로 받는다.")]
    public MonoBehaviour transportBehaviour;

    public EndingChoice LocalChoice { get; private set; } = EndingChoice.None;
    public EndingChoice RemoteChoice { get; private set; } = EndingChoice.None;
    public bool IsResolved { get; private set; }
    public EndingType ResolvedEndingType { get; private set; }

    public event Action<EndingChoice> OnLocalChoiceSubmitted;
    public event Action<EndingChoice> OnRemoteChoiceSubmitted;
    public event Action<EndingType> OnEndingResolved;

    private IEndingChoiceTransport transport;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[EndingDecisionManager] 씬에 두 개 이상 존재합니다. 마지막 것이 Instance가 됩니다.", this);

        Instance = this;

        BindTransport();
    }

    private void OnDestroy()
    {
        if (transport != null)
            transport.OnRemoteChoiceReceived -= SubmitRemoteChoice;

        if (Instance == this)
            Instance = null;
    }

    private void BindTransport()
    {
        if (transportBehaviour == null)
            return;

        transport = transportBehaviour as IEndingChoiceTransport;

        if (transport == null)
        {
            Debug.LogError(
                $"[EndingDecisionManager] {transportBehaviour.GetType().Name}은 IEndingChoiceTransport를 구현하지 않았습니다.",
                this);
            return;
        }

        transport.OnRemoteChoiceReceived += SubmitRemoteChoice;
    }

    /// <summary>
    /// 로컬 플레이어의 선택을 등록한다. EndingChoiceUI가 호출한다.
    /// </summary>
    public void SubmitLocalChoice(EndingChoice choice)
    {
        if (choice == EndingChoice.None)
        {
            Debug.LogWarning("[EndingDecisionManager] None은 유효한 선택이 아닙니다.", this);
            return;
        }

        if (IsResolved)
            return;

        LocalChoice = choice;

        if (OnLocalChoiceSubmitted != null)
            OnLocalChoiceSubmitted(choice);

        // 멀티플레이: 내 선택을 상대에게 전송
        if (transport != null)
            transport.SendLocalChoice(choice);

        // 솔로플레이: 파트너도 같은 선택을 한 것으로 처리
        if (soloPlay)
            RemoteChoice = choice;

        TryResolve();
    }

    /// <summary>
    /// 파트너(원격 플레이어)의 선택을 등록한다.
    /// 지금은 호출되지 않으며, 멀티플레이 수신부에서 호출할 확장 지점이다.
    /// </summary>
    public void SubmitRemoteChoice(EndingChoice choice)
    {
        if (choice == EndingChoice.None)
            return;

        if (IsResolved)
            return;

        RemoteChoice = choice;

        if (OnRemoteChoiceSubmitted != null)
            OnRemoteChoiceSubmitted(choice);

        TryResolve();
    }

    /// <summary>
    /// 선택을 초기화한다. (재시도/디버깅용)
    /// </summary>
    public void ResetChoices()
    {
        LocalChoice = EndingChoice.None;
        RemoteChoice = EndingChoice.None;
        IsResolved = false;
    }

    /// <summary>
    /// 두 선택의 조합으로 EndingType을 결정하는 순수 함수.
    /// 멀티플레이에서도 그대로 재사용된다.
    /// </summary>
    public static EndingType Resolve(EndingChoice local, EndingChoice remote)
    {
        if (local == EndingChoice.O)
            return remote == EndingChoice.O ? EndingType.BothO : EndingType.PlayerO_PartnerX;

        return remote == EndingChoice.O ? EndingType.PlayerX_PartnerO : EndingType.BothX;
    }

    private void TryResolve()
    {
        if (IsResolved)
            return;

        // 양쪽 선택이 모두 들어와야 엔딩을 결정한다.
        if (LocalChoice == EndingChoice.None || RemoteChoice == EndingChoice.None)
            return;

        IsResolved = true;
        ResolvedEndingType = Resolve(LocalChoice, RemoteChoice);

        Debug.Log($"[EndingDecisionManager] Local={LocalChoice}, Remote={RemoteChoice} → {ResolvedEndingType}");

        if (OnEndingResolved != null)
            OnEndingResolved(ResolvedEndingType);

        if (cinematicManager != null)
        {
            cinematicManager.PlayEnding(ResolvedEndingType);
        }
        else
        {
            Debug.LogError("[EndingDecisionManager] cinematicManager가 연결되지 않아 시네마틱을 재생할 수 없습니다.", this);
        }
    }
}
