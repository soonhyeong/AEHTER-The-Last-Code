using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;


public class StartButtonImageSwap : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Image Sprites")]
    public Sprite normalSprite;
    public Sprite hoverSprite;

    [Header("Audio Clips")]
    public AudioClip hoverSound;
    public AudioClip clickSound;

    [Header("Scene Name")]
    public string nextSceneName = "MainScene";

    [Header("Hover Scale Animation")]
    public float scaleUpSize = 1.1f;     // 확대 비율
    public float scaleSpeed = 1.2f;      // 확대/축소 속도

    private Image buttonImage;
    private Coroutine scaleCoroutine;
    private bool isHovering = false;
    private Vector3 originalScale;

    private void Start()
    {
        buttonImage = GetComponent<Image>();
        buttonImage.sprite = normalSprite;

        originalScale = transform.localScale;
    }

    // 마우스 올림 (Hover)
    public void OnPointerEnter(PointerEventData eventData)
    {
        buttonImage.sprite = hoverSprite;

        AudioManager.Instance.PlaySFX(hoverSound);

        isHovering = true;

        // 이미 코루틴이 돌고 있다면 중지
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(ScaleLoop());
    }

    // 마우스 나감
    public void OnPointerExit(PointerEventData eventData)
    {
        buttonImage.sprite = normalSprite;
        isHovering = false;

        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        // 크기 되돌리기
        transform.localScale = originalScale;
    }

    // 클릭
    public void OnPointerClick(PointerEventData eventData)
    {
        AudioManager.Instance.PlaySFX(clickSound);
        Invoke("LoadNextScene", 0.3f);
    }

    private void LoadNextScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }

    // 확대 → 축소 반복 애니메이션
    private IEnumerator ScaleLoop()
    {
        while (isHovering)
        {
            // ↑ 1) 원래 크기 → 확대
            yield return StartCoroutine(ScaleTo(originalScale * scaleUpSize));

            // ↑ 2) 확대 → 원래 크기
            yield return StartCoroutine(ScaleTo(originalScale));
        }
    }

    // 스케일 애니메이션 실행
    private IEnumerator ScaleTo(Vector3 targetScale)
    {
        while (Vector3.Distance(transform.localScale, targetScale) > 0.01f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
            yield return null;
        }

        transform.localScale = targetScale;
    }
}
