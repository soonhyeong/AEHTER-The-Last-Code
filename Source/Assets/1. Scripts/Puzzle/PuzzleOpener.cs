using UnityEngine;

public class PuzzleOpener : MonoBehaviour, IInteractable
{
    [Header("UI에 표시될 이름/아이콘")]
    [SerializeField] private string displayName = "전력 박스를 조작한다";
    [SerializeField] private Sprite icon;

    [Header("퍼즐 관련 참조")]
    [SerializeField] private GameObject puzzleCanvas;
    [SerializeField] private PuzzleManager puzzleManager;

    [Header("플레이어 이동/시야 스크립트")]
    public Yunseo_Move playerMoveScript;

    private bool isPuzzleOpen = false;

    private PlayerModalControlContext.Lease modalLease;

    public bool IsPuzzleOpen => isPuzzleOpen;

    public string GetDisplayName() => displayName;
    public Sprite GetIcon() => icon;

    public void Interact(PlayerInteractor interactor)
    {
        if (isPuzzleOpen || interactor == null) return;
        OpenPuzzle(interactor.gameObject);
    }

    private void OpenPuzzle(GameObject player)
    {
        if (isPuzzleOpen || !isActiveAndEnabled) return;

        if (puzzleCanvas == null || puzzleManager == null)
        {
            Debug.LogError("[PuzzleOpener] 퍼즐 레퍼런스가 비어있습니다.", this);
            return;
        }

        if (!CacheAndLockPlayer(player)) return;
        isPuzzleOpen = true;

        try
        {
            // Screen Space Canvas 켜기
            puzzleCanvas.SetActive(true);

            // Activation callbacks can close/disable the opener or its UI.
            if (!isPuzzleOpen || !isActiveAndEnabled || puzzleCanvas == null || !puzzleCanvas.activeInHierarchy)
            {
                ClosePuzzle();
                return;
            }

            // 스케일 0→1 애니메이션 포함
            puzzleManager.StartGame();

            if (!isPuzzleOpen) return;
            if (puzzleCanvas == null || !puzzleCanvas.activeInHierarchy)
            {
                ClosePuzzle();
                return;
            }


        }
        catch
        {
            ClosePuzzle();
            throw;
        }
    }


    public void OpenFromProximity(Move_Hankyul playerMove)
    {
        OpenPuzzle(playerMove != null ? playerMove.gameObject : null);
    }

    public void ClosePuzzle()
    {
        if (!isPuzzleOpen) return;

        // Clear first so UI OnDisable callbacks cannot restore the snapshot twice.
        isPuzzleOpen = false;
        try
        {
            if (puzzleCanvas != null)
                puzzleCanvas.SetActive(false);
        }
        finally
        {
            RestorePlayerControl();
        }
    }

    private bool CacheAndLockPlayer(GameObject player)
    {
        var context = PlayerModalControlContext.For(player, true);
        if (context == null && playerMoveScript != null)
            context = PlayerModalControlContext.For(playerMoveScript.gameObject, true);
        if (context == null) return false;
        modalLease = context.Acquire(this);
        return modalLease != null && !modalLease.IsReleased;
    }

    private void RestorePlayerControl()
    {
        modalLease?.Release();
        modalLease = null;
    }

    private void LateUpdate()
    {
        if (isPuzzleOpen && (puzzleCanvas == null || !puzzleCanvas.activeInHierarchy || modalLease == null || modalLease.IsReleased))
            ClosePuzzle();
    }

    private void OnDestroy() { ClosePuzzle(); }

    private void OnDisable()
    {
        ClosePuzzle();
    }
}
