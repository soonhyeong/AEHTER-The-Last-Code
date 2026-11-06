using UnityEngine;
using TMPro;

public class PuzzleManager : MonoBehaviour
{
    [Header("제한 시간")]
    public float timeLimit = 60f;
    private float remainingTime;
    private bool isPlaying = false;

    [Header("이동(회전) 횟수")]
    public int moveCount = 0;

    [Header("참조")]
    public TileSpawner tileSpawner;
    public TileManager tileManager;

    [Header("UI (TMP)")]
    public TMP_Text timeText;
    public TMP_Text moveText;
    public TMP_Text clearText;   // "CLEAR" 표시용
    public GameObject clearPanel; // Existing result artwork, explicitly wired by the scene.

    [Header("퍼즐 클리어 시 열릴 문")]
    public Door Door;

    [Header("퍼즐 열고 닫는 오프너 (화면 닫기용)")]
    public PuzzleOpener puzzleOpener;

    private bool isCleared = false;

    // ====== 스케일 애니메이션 관련 ======
    [Header("연출: 퍼즐 패널 스케일 업")]
    public RectTransform puzzleRoot;              // 퍼즐 전체 패널
    public Vector3 openScale = Vector3.one;       // Screen Space이므로 (1,1,1)
    public float openDuration = 0.25f;
    public AnimationCurve openCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private bool isAnimating = false;
    private bool initialized;
    public bool IsPlaying => isPlaying && !isCleared && !isAnimating;
    // ===================================

    void Awake()
    {
        if (tileSpawner == null)
            tileSpawner = FindObjectOfType<TileSpawner>();
        if (tileManager == null)
            tileManager = FindObjectOfType<TileManager>();
    }

    void Start()
    {
        InitializePuzzle();
    }

    private void InitializePuzzle()
    {
        if (initialized) return;
        initialized = true;
        // 자동 시작 X, 외부에서 StartGame 호출
        remainingTime = timeLimit;
        moveCount = 0;
        isCleared = false;
        isPlaying = false;

        SetClearUIVisible(false);

        if (puzzleRoot != null)
            puzzleRoot.localScale = Vector3.zero;   // 처음엔 안 보이게
    }

    void Update()
    {
        if (puzzleRoot != null && !puzzleRoot.gameObject.activeInHierarchy)
        {
            StopGame();
            return;
        }
        if (!isPlaying || isCleared)
            return;

        // TimeScale 0일 수도 있으니 unscaledDeltaTime 사용해도 됨
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            isPlaying = false;
            OnTimeOver();
        }

        UpdateUI();
    }

    // 전력박스 상호작용에서 호출
    public void StartGame()
    {
        InitializePuzzle();
        StopGame();

        remainingTime = timeLimit;
        moveCount = 0;
        isCleared = false;
        isPlaying = false;   // 애니메이션 끝난 뒤 true로
        UpdateUI();

        SetClearUIVisible(false);

        if (tileSpawner == null || !tileSpawner.TryRegenerateTiles())
        {
            if (puzzleOpener != null) puzzleOpener.ClosePuzzle();
            return;
        }

        if (puzzleRoot != null)
        {
            StartCoroutine(OpenScaleRoutine());
        }
        else
        {
            isPlaying = true;
        }
    }

    public void OnTileRotated()
    {
        if (!isPlaying || isCleared)
            return;

        moveCount++;
        UpdateUI();
    }

    public void OnPuzzleCleared()
    {
        if (!isPlaying || isCleared)
            return;

        isCleared = true;
        isPlaying = false;

        if (clearText != null)
        {
            clearText.text = "CLEAR";
        }
        SetClearUIVisible(true);

        
        Debug.Log("퍼즐 클리어");
        // 여기에서 PuzzleOpener.ClosePuzzle() 호출하도록 연결해도 됨

        // 1) 문 열기
        if (Door != null)
            Door.OpenDoors();

        // 2) 3초 후 퍼즐 닫기
        if (puzzleOpener != null)
            StartCoroutine(ClosePuzzleAfterDelay());
    }

    private System.Collections.IEnumerator ClosePuzzleAfterDelay()
    {
        // Time.timeScale 1이면 WaitForSeconds, 0으로 멈출 생각이면 WaitForSecondsRealtime 사용
        yield return new WaitForSeconds(3f);

        puzzleOpener.ClosePuzzle();
    }

    private void OnTimeOver()
    {
        Debug.Log("시간 종료");
        // Timeout terminates this attempt and releases the existing modal lease.
        StopGame();
        if (puzzleOpener != null) puzzleOpener.ClosePuzzle();
    }

    public void StopGame()
    {
        StopAllCoroutines();
        isPlaying = false;
        isAnimating = false;
        SetClearUIVisible(false);
    }

    private void SetClearUIVisible(bool visible)
    {
        if (clearPanel != null)
            clearPanel.SetActive(visible);
        if (clearText != null)
            clearText.gameObject.SetActive(visible);
    }

    private void OnDisable()
    {
        StopGame();
    }

    private void UpdateUI()
    {
        if (timeText != null)
            timeText.text = Mathf.CeilToInt(remainingTime).ToString();

        if (moveText != null)
            moveText.text = moveCount.ToString();
    }

    public void OnClickRestart()
    {
        StartGame();
    }

    // ====== 스케일 0 → 1 애니메이션 ======
    private System.Collections.IEnumerator OpenScaleRoutine()
    {
        isAnimating = true;

        if (puzzleRoot != null)
            puzzleRoot.localScale = Vector3.zero;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / openDuration;
            float eval = openCurve.Evaluate(Mathf.Clamp01(t));
            if (puzzleRoot != null)
            {
                puzzleRoot.localScale = Vector3.LerpUnclamped(
                    Vector3.zero,
                    openScale,
                    eval
                );
            }
            yield return null;
        }

        if (puzzleRoot != null)
            puzzleRoot.localScale = openScale;

        isAnimating = false;
        isPlaying = true;
    }
}
