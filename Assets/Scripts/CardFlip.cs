using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class CardFlip : MonoBehaviour
{
    [Header("Card Faces")]
    public Sprite frontSprite;
    public Sprite backSprite;

    [Header("Flip Settings")]
    public float flipDuration = 0.4f;

    [Header("Fullscreen Settings")]
    public bool goFullscreen = false;       // Inspector'dan aç/kapa
    public float expandDuration = 0.5f;

    private Image cardImage;
    private TextMeshProUGUI cardText;
    private bool isFlipped = false;
    private bool isFlipping = false;

    private Vector3 originalPos;
    private Vector3 originalScale;
    private Vector2 originalSize;
    private RectTransform rectTransform;

    void Start()
    {
        cardImage = GetComponent<Image>();
        cardText = GetComponentInChildren<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();
        frontSprite = cardImage.sprite;

        originalPos = rectTransform.localPosition;
        originalScale = rectTransform.localScale;
        originalSize = rectTransform.sizeDelta;
    }

    public void FlipCard()
    {
        if (isFlipping) return;
        StartCoroutine(DoFlip());
    }

    IEnumerator DoFlip()
    {
        isFlipping = true;

        if (!isFlipped)
        {
            // Ön -> Arka: flip yap, sonra büyüt
            yield return StartCoroutine(AnimateFlip(frontSprite, backSprite, true));

            if (goFullscreen)
                yield return StartCoroutine(ExpandToFullscreen());
        }
        else
        {
            // Arka -> Ön: önce küçült, sonra flip
            if (goFullscreen)
                yield return StartCoroutine(ShrinkToOriginal());

            yield return StartCoroutine(AnimateFlip(backSprite, frontSprite, false));
        }

        isFlipping = false;
    }

    IEnumerator AnimateFlip(Sprite from, Sprite to, bool flipping)
    {
        float elapsed = 0f;
        float half = flipDuration / 2f;
        Vector3 scale = transform.localScale;

        // Kapat
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / half;
            transform.localScale = new Vector3(Mathf.Lerp(scale.x, 0f, t), scale.y, scale.z);
            yield return null;
        }
        transform.localScale = new Vector3(0f, scale.y, scale.z);

        // Sprite değiştir
        isFlipped = flipping;
        cardImage.sprite = to;
        if (cardText != null)
            cardText.gameObject.SetActive(!isFlipped);

        // Aç
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / half;
            transform.localScale = new Vector3(Mathf.Lerp(0f, scale.x, t), scale.y, scale.z);
            yield return null;
        }
        transform.localScale = scale;
    }

    IEnumerator ExpandToFullscreen()
    {
        float elapsed = 0f;
        Vector3 startPos = rectTransform.localPosition;
        Vector2 startSize = rectTransform.sizeDelta;

        // Canvas boyutunu al
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector2 targetSize = canvasRect.sizeDelta;

        while (elapsed < expandDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / expandDuration);

            rectTransform.localPosition = Vector3.Lerp(startPos, Vector3.zero, t);
            rectTransform.sizeDelta = Vector2.Lerp(startSize, targetSize, t);
            yield return null;
        }

        rectTransform.localPosition = Vector3.zero;
        rectTransform.sizeDelta = targetSize;
    }

    IEnumerator ShrinkToOriginal()
    {
        float elapsed = 0f;
        Vector3 startPos = rectTransform.localPosition;
        Vector2 startSize = rectTransform.sizeDelta;

        while (elapsed < expandDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / expandDuration);

            rectTransform.localPosition = Vector3.Lerp(startPos, originalPos, t);
            rectTransform.sizeDelta = Vector2.Lerp(startSize, originalSize, t);
            yield return null;
        }

        rectTransform.localPosition = originalPos;
        rectTransform.sizeDelta = originalSize;
    }
}