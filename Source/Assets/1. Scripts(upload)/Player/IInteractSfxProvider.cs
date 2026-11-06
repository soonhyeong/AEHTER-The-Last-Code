/// <summary>
/// IInteractable이 "상호작용 시 재생할 SFX key"를 직접 지정하고 싶을 때
/// 함께 구현하는 선택적(optional) 인터페이스.
///
/// PlayerInteractor는 이 인터페이스를 구현하지 않은 대상에 대해서는
/// 기존과 동일하게 pickupSfxKey를 재생한다. (기존 호출부 동작 유지)
///
/// 빈 문자열(또는 null)을 반환하면 SFX를 재생하지 않는다.
/// 예) EndingInteractable은 대화 시작이므로 pickup 소리가 나지 않도록 비워둔다.
/// </summary>
public interface IInteractSfxProvider
{
    string GetInteractSfxKey();
}
