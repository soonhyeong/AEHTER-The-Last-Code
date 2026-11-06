using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;

public class HologramNumber : MonoBehaviour
{
    [Header("Target TMP Text")]
    public TMP_Text targetText;              // 숫자를 표시할 TMP_Text

    [Header("Number Settings")]
    public int digitPerLine = 6;             // 한 줄에 표시할 자리 수
    public int lineCount = 10;               // 총 줄 수
    public float changeInterval = 0.05f;     // 숫자 변경 주기 (초)

    [Header("Scroll Settings")]
    public float scrollSpeed = 20f;          // 아래로 내려가는 속도
    public float extraLoopMargin = 50f;      // 여유 마진 (조금 더 흘렀다가 리셋)

    private bool isRunning = true;
    private Coroutine randomCoroutine;
    private Vector3 startLocalPos;
    private float loopHeight;                // 자동 계산된 루프 높이

    private void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        if (targetText != null)
        {
            RectTransform textRT = targetText.rectTransform;
            startLocalPos = textRT.localPosition;

            // 부모 패널 기준 높이 계산
            RectTransform parentRT = textRT.parent as RectTransform;
            if (parentRT != null)
            {
                // 패널 높이 + 텍스트 높이 + 여유 margin
                loopHeight = parentRT.rect.height + textRT.rect.height + extraLoopMargin;
            }
            else
            {
                // 부모가 없으면 기본값
                loopHeight = 300f;
            }
        }
    }

    private void OnEnable()
    {
        StartEffect();
    }

    private void OnDisable()
    {
        StopEffect();
    }

    private void Update()
    {
        if (!isRunning || targetText == null)
            return;

        RectTransform rt = targetText.rectTransform;
        Vector3 pos = rt.localPosition;

        // 아래로 이동 (y 감소)
        pos.y -= scrollSpeed * Time.deltaTime;
        rt.localPosition = pos;

        // 시작 위치에서 loopHeight만큼 내려가면 다시 처음 위치로
        if (startLocalPos.y - pos.y >= loopHeight)
        {
            rt.localPosition = startLocalPos;
        }
    }

    public void StartEffect()
    {
        if (targetText == null) return;

        isRunning = true;

        if (randomCoroutine != null)
            StopCoroutine(randomCoroutine);

        randomCoroutine = StartCoroutine(RandomNumberLoop());
    }

    public void StopEffect()
    {
        isRunning = false;

        if (randomCoroutine != null)
        {
            StopCoroutine(randomCoroutine);
            randomCoroutine = null;
        }

        if (targetText != null)
            targetText.rectTransform.localPosition = startLocalPos;
    }

    private IEnumerator RandomNumberLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(changeInterval);
        StringBuilder sb = new StringBuilder();

        while (isRunning)
        {
            sb.Clear();

            for (int line = 0; line < lineCount; line++)
            {
                for (int i = 0; i < digitPerLine; i++)
                {
                    int randomDigit = Random.Range(0, 10); // 0~9
                    sb.Append(randomDigit);
                }

                if (line < lineCount - 1)
                    sb.Append('\n');
            }

            targetText.text = sb.ToString();

            yield return wait;
        }
    }
}
