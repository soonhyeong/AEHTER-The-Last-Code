using UnityEngine;

public class ConsolePuzzleInteractor : MonoBehaviour
{
    [Header("상호작용 가능 플레이어 태그")]
    [SerializeField] private string allowedPlayerTag = "Player";

    [Header("F 홀드 설정")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private float holdDuration = 2f;

    [Header("UI")]
    [SerializeField] private HoldInteractionUI holdInteractionUI;
    [SerializeField] private string interactionMessage = "F 키를 누르고 해킹 시작";

    [Header("퍼즐 컨트롤러")]
    [SerializeField] private PuzzleConsoleController puzzleConsoleController;

    [Header("1회 해킹 후 문구")]
    [SerializeField] private string solvedMessage = "이미 해킹 완료된 콘솔입니다.";

    private bool isPlayerInside = false;
    private float currentHoldTime = 0f;
    private GameObject currentPlayer;

    private void Update()
    {
        if (!isPlayerInside)
            return;

        if (puzzleConsoleController != null && puzzleConsoleController.IsPuzzleOpen())
            return;

        if (puzzleConsoleController != null && puzzleConsoleController.IsSolved())
        {
            if (holdInteractionUI != null)
            {
                holdInteractionUI.ShowUI(solvedMessage);
                holdInteractionUI.SetProgress(1f);
            }
            return;
        }

        if (Input.GetKey(interactKey))
        {
            currentHoldTime += Time.deltaTime;

            float progress = currentHoldTime / holdDuration;

            if (holdInteractionUI != null)
            {
                holdInteractionUI.ShowUI(interactionMessage);
                holdInteractionUI.SetProgress(progress);
            }

            if (currentHoldTime >= holdDuration)
            {
                currentHoldTime = 0f;

                if (holdInteractionUI != null)
                    holdInteractionUI.HideUI();

                if (puzzleConsoleController != null)
                    puzzleConsoleController.OpenPuzzle();
            }
        }
        else
        {
            currentHoldTime = 0f;

            if (holdInteractionUI != null)
            {
                holdInteractionUI.ShowUI(interactionMessage);
                holdInteractionUI.SetProgress(0f);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(allowedPlayerTag))
            return;

        isPlayerInside = true;
        currentPlayer = other.gameObject;
        currentHoldTime = 0f;

        if (holdInteractionUI != null)
        {
            if (puzzleConsoleController != null && puzzleConsoleController.IsSolved())
            {
                holdInteractionUI.ShowUI(solvedMessage);
                holdInteractionUI.SetProgress(1f);
            }
            else
            {
                holdInteractionUI.ShowUI(interactionMessage);
                holdInteractionUI.SetProgress(0f);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (currentPlayer == null || other.gameObject != currentPlayer)
            return;

        isPlayerInside = false;
        currentPlayer = null;
        currentHoldTime = 0f;

        if (holdInteractionUI != null)
            holdInteractionUI.HideUI();
    }
}