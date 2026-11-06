using UnityEngine;

/// <summary>
/// 플레이어 이동/시야 조작을 잠그고, 원래 값으로 되돌리는 헬퍼.
///
/// Yunseo_Move / Hankyul_Move는 공통 부모 클래스나 인터페이스가 없으므로
/// ShopManager.CachePlayerLookState()와 동일한 방식으로 두 컴포넌트를 각각 찾아 처리한다.
/// (기존 이동 스크립트는 수정하지 않는다.)
///
/// MonoBehaviour가 아닌 일반 클래스이며, 잠금이 필요한 쪽이 필드로 하나씩 보유한다.
/// 이미 잠근 상태에서 Lock()을 다시 호출하면 원래 값을 덮어쓰지 않도록 무시한다.
/// </summary>
public class PlayerControlLock
{
    private Yunseo_Move yunseoMove;
    private Hankyul_Move hankyulMove;

    private bool yunseoAllowMove;
    private bool yunseoAllowLook;
    private bool hankyulAllowMove;
    private bool hankyulAllowLook;

    public bool IsLocked { get; private set; }

    /// <summary>
    /// playerRoot의 이동/시야를 잠근다.
    /// playerRoot가 null이면 "Player" 태그로 찾는다.
    /// </summary>
    public void Lock(GameObject playerRoot)
    {
        if (IsLocked) return;

        if (playerRoot == null)
            playerRoot = FindPlayerRoot();

        if (playerRoot == null)
            return;

        yunseoMove = FindMove<Yunseo_Move>(playerRoot);
        hankyulMove = FindMove<Hankyul_Move>(playerRoot);

        if (yunseoMove == null && hankyulMove == null)
            return;

        if (yunseoMove != null)
        {
            yunseoAllowMove = yunseoMove.allowMove;
            yunseoAllowLook = yunseoMove.allowLook;
            yunseoMove.allowMove = false;
            yunseoMove.allowLook = false;
        }

        if (hankyulMove != null)
        {
            hankyulAllowMove = hankyulMove.allowMove;
            hankyulAllowLook = hankyulMove.allowLook;
            hankyulMove.allowMove = false;
            hankyulMove.allowLook = false;
        }

        IsLocked = true;
    }

    /// <summary>
    /// Lock() 시점에 캐시해 둔 값으로 되돌린다.
    /// </summary>
    public void Unlock()
    {
        if (!IsLocked) return;

        if (yunseoMove != null)
        {
            yunseoMove.allowMove = yunseoAllowMove;
            yunseoMove.allowLook = yunseoAllowLook;
        }

        if (hankyulMove != null)
        {
            hankyulMove.allowMove = hankyulAllowMove;
            hankyulMove.allowLook = hankyulAllowLook;
        }

        yunseoMove = null;
        hankyulMove = null;
        IsLocked = false;
    }

    private static T FindMove<T>(GameObject root) where T : Component
    {
        T found = root.GetComponent<T>();
        if (found == null) found = root.GetComponentInParent<T>();
        if (found == null) found = root.GetComponentInChildren<T>();
        return found;
    }

    public static GameObject FindPlayerRoot()
    {
        // 프로젝트 전반에서 "Player" 태그를 사용한다. (ChangeScene, ShopManager 등)
        return GameObject.FindGameObjectWithTag("Player");
    }
}
