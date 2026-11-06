using System;

/// <summary>
/// 플레이어 한 명의 O/X 선택.
/// None = 아직 선택하지 않음.
/// </summary>
public enum EndingChoice
{
    None,
    O,
    X
}

/// <summary>
/// 두 플레이어의 선택 조합으로 결정되는 엔딩 종류.
///
/// Player(local) / Partner(remote) 기준:
/// - O, O → BothO
/// - O, X → PlayerO_PartnerX
/// - X, O → PlayerX_PartnerO
/// - X, X → BothX
///
/// 솔로플레이에서는 remote = local로 취급하므로 BothO / BothX만 나온다.
/// </summary>
public enum EndingType
{
    BothO,
    PlayerO_PartnerX,
    PlayerX_PartnerO,
    BothX
}

/// <summary>
/// 멀티플레이 선택 동기화 확장 지점.
///
/// 지금은 구현체가 없다(솔로플레이). 나중에 Netcode/Photon 등을 도입할 때
/// 이 인터페이스만 구현한 MonoBehaviour를 만들어
/// EndingDecisionManager.transportBehaviour에 연결하면 된다.
///
/// - SendLocalChoice: 내 선택을 상대에게 전송
/// - OnRemoteChoiceReceived: 상대 선택을 수신했을 때 발생
///   (EndingDecisionManager가 구독해서 SubmitRemoteChoice로 넘긴다)
/// </summary>
public interface IEndingChoiceTransport
{
    void SendLocalChoice(EndingChoice choice);

    event Action<EndingChoice> OnRemoteChoiceReceived;
}
