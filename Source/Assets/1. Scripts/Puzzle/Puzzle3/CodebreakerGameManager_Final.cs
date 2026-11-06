using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CodebreakerGameManager_Final : MonoBehaviour
{
    [System.Serializable] public class PuzzleEvent : UnityEvent<PuzzleDifficulty_Final> { }

    struct DifficultyConfig
    {
        public int referenceLevel;
        public int digitCount;
        public bool allowDuplicate;
        public int maxSameDigitCount;
        public int minDuplicatePairCount;
        public int maxDuplicatePairCount;
    }

    [Header("Final Flow")]
    [SerializeField] private bool startOnAwake;
    [SerializeField] private PuzzleDifficulty_Final defaultDifficulty = PuzzleDifficulty_Final.Normal;
    [SerializeField, Min(0f)] private float totalTimeLimit = 600f;
    public PuzzleEvent OnPuzzleSuccess;
    public PuzzleEvent OnPuzzleFailed;

    [Header("Input Cell Auto Generation")]
    [SerializeField] private RectTransform inputCellParent;
    [SerializeField] private GameObject inputCellPrefab;
    [SerializeField] private bool ensureHorizontalLayoutGroup = true;
    [SerializeField, Min(0f)] private float inputCellSpacing = 8f;
    [SerializeField] private bool matchInputCellHeightToParent = true;
    [SerializeField, Min(0.01f)] private float fallbackInputCellAspectRatio = 1f;
    [SerializeField] private bool clearExistingInputCellChildren = true;

    [Header("Session Timer")]
    [SerializeField] private TMP_Text timerText;

    [Header("DDA")]
    [SerializeField] private Puzzle3DDAController_Final ddaController;

    [Header("Log")]
    [SerializeField] private CodebreakerLogManager logManager;

    [Header("UI Panels")]
    [SerializeField] private GameObject panel_GameUI;
    [SerializeField] private GameObject panel_Success;
    [SerializeField] private GameObject panel_Fail;
    [SerializeField] private TMP_Text ruleInfoText;

    [Header("Options")]
    [SerializeField] private bool showUnderscoreForEmptyCell;
    [SerializeField] private bool inputEnabled = true;

    [Header("UI SFX")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip numberInputClip;
    [SerializeField] private AudioClip deleteClip;
    [SerializeField] private AudioClip submitClip;
    [SerializeField] private AudioClip rejectClip;
    [SerializeField] private AudioClip successClip;

    [Header("Result Colors")]
    [SerializeField] private Color strikeColor = new Color(1f, 0.83f, 0.0f, 1f);
    [SerializeField] private Color ballColor = new Color(0.18f, 0.66f, 1f, 1f);
    [SerializeField] private Color outColor = new Color(1f, 0.23f, 0.19f, 1f);

    readonly List<TMP_Text> inputTexts = new List<TMP_Text>();
    int[] inputValues;
    int inputLength;
    int[] answer;
    int attemptCount;
    int digitCount = 4;
    bool allowDuplicate;
    int maxSameDigitCount = 1;
    int minDuplicatePairCount;
    int maxDuplicatePairCount;
    float remainingTime;
    bool isRunning;
    bool isEnded;
    PuzzleDifficulty_Final activeDifficulty;
    int referenceLevel;

    void Awake()
    {
        EnsureDDAController();
        if (startOnAwake)
            StartPuzzle(defaultDifficulty);
    }

    void Update()
    {
        if (!isRunning || isEnded)
            return;

        if (totalTimeLimit > 0f)
        {
            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            UpdateTimerUI();
            if (remainingTime <= 0f)
            {
                FailPuzzle(true);
                return;
            }
        }

        if (!inputEnabled) return;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            TrySubmit();
            return;
        }

        if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame)
        {
            DeleteDigit();
            return;
        }

        if (DetectDigitKey(out int digit))
            AddDigit(digit);
#endif
    }

    public void StartPuzzle(PuzzleDifficulty_Final difficulty)
    {
        activeDifficulty = difficulty;
        ApplyDifficultyConfig(GetDifficultyConfig(difficulty));

        isRunning = true;
        isEnded = false;
        inputEnabled = true;
        remainingTime = totalTimeLimit;
        attemptCount = 0;

        EnsureDDAController();
        ddaController?.ResetDDAForNewGame();
        BuildInputCells();
        logManager?.Configure(digitCount);
        logManager?.ClearAllLogs();
        answer = GenerateAnswer();
        inputValues = new int[digitCount];
        ClearInput();
        ShowGameUI();
        UpdateRuleInfoText();
        UpdateTimerUI();
        ddaController?.OnPuzzleStarted(referenceLevel, answer);
    }

    DifficultyConfig GetDifficultyConfig(PuzzleDifficulty_Final difficulty)
    {
        switch (difficulty)
        {
            case PuzzleDifficulty_Final.Easy:
                return new DifficultyConfig { referenceLevel = 2, digitCount = 3, allowDuplicate = false, maxSameDigitCount = 1 };
            case PuzzleDifficulty_Final.Hard:
                return new DifficultyConfig { referenceLevel = 4, digitCount = 5, allowDuplicate = false, maxSameDigitCount = 1 };
            default:
                return new DifficultyConfig { referenceLevel = 3, digitCount = 4, allowDuplicate = false, maxSameDigitCount = 1 };
        }
    }

    void ApplyDifficultyConfig(DifficultyConfig config)
    {
        referenceLevel = config.referenceLevel;
        digitCount = Mathf.Max(1, config.digitCount);
        allowDuplicate = config.allowDuplicate;
        maxSameDigitCount = allowDuplicate ? Mathf.Clamp(config.maxSameDigitCount, 2, digitCount) : 1;
        minDuplicatePairCount = allowDuplicate ? Mathf.Max(0, config.minDuplicatePairCount) : 0;
        maxDuplicatePairCount = allowDuplicate ? Mathf.Max(0, config.maxDuplicatePairCount) : 0;

        if (!allowDuplicate && digitCount > 10)
            digitCount = 10;

        int possibleDuplicatePairCount = allowDuplicate ? Mathf.Min(10, digitCount / 2) : 0;
        maxDuplicatePairCount = Mathf.Min(maxDuplicatePairCount, possibleDuplicatePairCount);
        minDuplicatePairCount = Mathf.Min(minDuplicatePairCount, maxDuplicatePairCount);
    }

    void EnsureDDAController()
    {
        if (ddaController != null)
            return;

        ddaController = GetComponent<Puzzle3DDAController_Final>();
        if (ddaController == null)
            ddaController = gameObject.AddComponent<Puzzle3DDAController_Final>();
    }

    public void OnClickExecute() => TrySubmit();
    public void OnClickBackspace() => DeleteDigit();
    public void OnClickDigit0() => AddDigit(0);
    public void OnClickDigit1() => AddDigit(1);
    public void OnClickDigit2() => AddDigit(2);
    public void OnClickDigit3() => AddDigit(3);
    public void OnClickDigit4() => AddDigit(4);
    public void OnClickDigit5() => AddDigit(5);
    public void OnClickDigit6() => AddDigit(6);
    public void OnClickDigit7() => AddDigit(7);
    public void OnClickDigit8() => AddDigit(8);
    public void OnClickDigit9() => AddDigit(9);

#if ENABLE_INPUT_SYSTEM
    bool DetectDigitKey(out int digit)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame) { digit = 0; return true; }
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) { digit = 1; return true; }
        if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) { digit = 2; return true; }
        if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) { digit = 3; return true; }
        if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) { digit = 4; return true; }
        if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) { digit = 5; return true; }
        if (keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame) { digit = 6; return true; }
        if (keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame) { digit = 7; return true; }
        if (keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame) { digit = 8; return true; }
        if (keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame) { digit = 9; return true; }
        digit = -1;
        return false;
    }
#endif

    void AddDigit(int digit)
    {
        if (isEnded || !inputEnabled) return;
        if (inputTexts.Count == 0)
            BuildInputCells();
        if (inputLength >= digitCount || (inputLength == 0 && digit == 0))
        {
            PlayReject();
            return;
        }

        if (!CanAddDigit(inputValues, inputLength, digit))
        {
            PlayReject();
            return;
        }

        inputValues[inputLength] = digit;
        inputLength++;
        RefreshInputUI();
        PlayOneShot(numberInputClip);
    }

    void DeleteDigit()
    {
        if (isEnded || !inputEnabled) return;
        if (inputLength <= 0)
        {
            PlayReject();
            return;
        }

        inputLength--;
        inputValues[inputLength] = 0;
        RefreshInputUI();
        PlayOneShot(deleteClip);
    }

    void TrySubmit()
    {
        if (isEnded || !inputEnabled) return;
        if (inputLength < digitCount || !IsValidDuplicatePattern(inputValues))
        {
            PlayReject();
            return;
        }

        PlayOneShot(submitClip);
        string submittedInput = FormatInput();
        CountStrikeAndBall(out int strike, out int ball);
        attemptCount++;
        logManager?.AddLog(referenceLevel, attemptCount, submittedInput, FormatResult(strike, ball));
        ddaController?.OnAttemptSubmitted(inputValues, strike, ball);
        ClearInput();

        if (strike == digitCount)
            CompletePuzzle();
    }

    void CompletePuzzle()
    {
        if (isEnded) return;

        isEnded = true;
        isRunning = false;
        inputEnabled = false;
        ddaController?.OnPuzzleCleared();
        ShowSuccessUI();
        PlayOneShot(successClip);
        OnPuzzleSuccess?.Invoke(activeDifficulty);
    }

    void FailPuzzle(bool timeout)
    {
        if (isEnded) return;

        isEnded = true;
        isRunning = false;
        inputEnabled = false;
        if (panel_GameUI != null) panel_GameUI.SetActive(false);
        if (panel_Fail != null) panel_Fail.SetActive(true);

        OnPuzzleFailed?.Invoke(activeDifficulty);
    }

    void BuildInputCells()
    {
        inputTexts.Clear();
        if (inputCellParent == null || inputCellPrefab == null)
            return;

        if (clearExistingInputCellChildren)
            ClearChildren(inputCellParent);

        EnsureLayoutGroup(inputCellParent);

        for (int i = 0; i < digitCount; i++)
        {
            GameObject cellObject = Instantiate(inputCellPrefab, inputCellParent);
            cellObject.name = $"{inputCellPrefab.name}_{i}";
            cellObject.SetActive(true);
            ApplyInputCellSize(cellObject);

            TMP_Text text = cellObject.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                inputTexts.Add(text);
        }
    }

    int[] GenerateAnswer()
    {
        const int MaxAttempts = 1000;
        int[] generated = new int[digitCount];

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            int length = 0;
            for (int i = 0; i < digitCount; i++)
            {
                int guard = 0;
                while (guard++ < 100)
                {
                    int digit = i == 0 ? Random.Range(1, 10) : Random.Range(0, 10);
                    if (!CanAddDigit(generated, length, digit)) continue;
                    generated[length] = digit;
                    length++;
                    break;
                }
            }

            if (length == digitCount && IsValidDuplicatePattern(generated))
                return generated;
        }

        FillFallbackAnswer(generated);
        return generated;
    }

    bool CanAddDigit(int[] values, int length, int digit)
    {
        int sameDigitCount = 0;
        for (int i = 0; i < length; i++)
        {
            if (values[i] == digit)
                sameDigitCount++;
        }

        if (!allowDuplicate)
            return sameDigitCount == 0;

        if (sameDigitCount >= maxSameDigitCount)
            return false;

        values[length] = digit;
        bool isValid = CountDuplicatePairs(values, length + 1) <= maxDuplicatePairCount;
        values[length] = 0;
        return isValid;
    }

    bool IsValidDuplicatePattern(int[] values)
    {
        int[] counts = CountDigits(values, digitCount);
        int duplicatePairs = 0;
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] > maxSameDigitCount)
                return false;
            if (counts[i] >= 2)
                duplicatePairs++;
        }

        if (!allowDuplicate)
            return duplicatePairs == 0;

        return duplicatePairs >= minDuplicatePairCount && duplicatePairs <= maxDuplicatePairCount;
    }

    int CountDuplicatePairs(int[] values, int length)
    {
        int[] counts = CountDigits(values, length);
        int duplicatePairs = 0;
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] >= 2)
                duplicatePairs++;
        }

        return duplicatePairs;
    }

    int[] CountDigits(int[] values, int length)
    {
        int[] counts = new int[10];
        for (int i = 0; i < length; i++)
            counts[values[i]]++;
        return counts;
    }

    void FillFallbackAnswer(int[] generated)
    {
        HashSet<int> used = new HashSet<int>();
        for (int i = 0; i < digitCount; i++)
        {
            int digit = i == 0 ? Random.Range(1, 10) : Random.Range(0, 10);
            while ((!allowDuplicate && used.Contains(digit)) || (i == 0 && digit == 0))
                digit = i == 0 ? Random.Range(1, 10) : Random.Range(0, 10);

            generated[i] = digit;
            used.Add(digit);
        }
    }

    void CountStrikeAndBall(out int strike, out int ball)
    {
        strike = 0;
        ball = 0;
        int[] remainingAnswerCounts = new int[10];
        bool[] strikeUsed = new bool[digitCount];

        for (int i = 0; i < digitCount; i++)
        {
            if (inputValues[i] == answer[i])
            {
                strike++;
                strikeUsed[i] = true;
            }
            else
            {
                remainingAnswerCounts[answer[i]]++;
            }
        }

        for (int i = 0; i < digitCount; i++)
        {
            if (strikeUsed[i]) continue;
            int digit = inputValues[i];
            if (remainingAnswerCounts[digit] <= 0) continue;
            ball++;
            remainingAnswerCounts[digit]--;
        }
    }

    string FormatResult(int strike, int ball)
    {
        string strikeHtml = ColorUtility.ToHtmlStringRGBA(strikeColor);
        string ballHtml = ColorUtility.ToHtmlStringRGBA(ballColor);
        string outHtml = ColorUtility.ToHtmlStringRGBA(outColor);

        if (strike == 0 && ball == 0)
            return $"<color=#{outHtml}>OUT!</color>";
        if (strike > 0 && ball > 0)
            return $"<color=#{strikeHtml}>{strike}S</color> <color=#{ballHtml}>{ball}B</color>";
        if (strike > 0)
            return $"<color=#{strikeHtml}>{strike}S</color>";
        return $"<color=#{ballHtml}>{ball}B</color>";
    }

    void UpdateRuleInfoText()
    {
        if (ruleInfoText == null) return;
        ruleInfoText.text = "";
    }

    string FormatInput()
    {
        if (inputValues == null || digitCount <= 0)
            return "";

        System.Text.StringBuilder builder = new System.Text.StringBuilder(digitCount);
        for (int i = 0; i < digitCount && i < inputValues.Length; i++)
            builder.Append(inputValues[i]);
        return builder.ToString();
    }

    void UpdateTimerUI()
    {
        if (timerText != null)
            timerText.text = Mathf.CeilToInt(remainingTime).ToString();
    }

    void ShowGameUI()
    {
        if (panel_GameUI != null) panel_GameUI.SetActive(true);
        if (panel_Success != null) panel_Success.SetActive(false);
        if (panel_Fail != null) panel_Fail.SetActive(false);
    }

    void ShowSuccessUI()
    {
        if (panel_GameUI != null) panel_GameUI.SetActive(false);
        if (panel_Success != null) panel_Success.SetActive(true);
        if (panel_Fail != null) panel_Fail.SetActive(false);
    }

    void ClearInput()
    {
        inputLength = 0;
        if (inputValues != null)
        {
            for (int i = 0; i < inputValues.Length; i++)
                inputValues[i] = 0;
        }

        RefreshInputUI();
    }

    void RefreshInputUI()
    {
        for (int i = 0; i < inputTexts.Count; i++)
        {
            if (inputTexts[i] == null) continue;
            inputTexts[i].text = i < inputLength ? inputValues[i].ToString() : (showUnderscoreForEmptyCell ? "_" : "");
        }
    }

    void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    void EnsureLayoutGroup(RectTransform parent)
    {
        if (!ensureHorizontalLayoutGroup || parent == null) return;

        HorizontalLayoutGroup layout = parent.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = inputCellSpacing;
    }

    void ApplyInputCellSize(GameObject cellObject)
    {
        if (!matchInputCellHeightToParent || inputCellParent == null || cellObject == null) return;

        RectTransform cellRect = cellObject.GetComponent<RectTransform>();
        if (cellRect == null) return;

        float height = inputCellParent.rect.height;
        if (height <= 0f) height = cellRect.rect.height;
        if (height <= 0f) height = 1f;

        float width = height * GetPrefabAspectRatio();
        cellRect.sizeDelta = new Vector2(width, height);

        LayoutElement layoutElement = cellObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = cellObject.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = height;
        layoutElement.minWidth = width;
        layoutElement.minHeight = height;
    }

    float GetPrefabAspectRatio()
    {
        RectTransform prefabRect = inputCellPrefab != null ? inputCellPrefab.GetComponent<RectTransform>() : null;
        if (prefabRect != null && prefabRect.rect.height > 0f && prefabRect.rect.width > 0f)
            return prefabRect.rect.width / prefabRect.rect.height;
        return Mathf.Max(0.01f, fallbackInputCellAspectRatio);
    }

    void PlayOneShot(AudioClip clip)
    {
        if (uiAudioSource != null && clip != null)
            uiAudioSource.PlayOneShot(clip);
    }

    void PlayReject()
    {
        PlayOneShot(rejectClip);
    }
}
