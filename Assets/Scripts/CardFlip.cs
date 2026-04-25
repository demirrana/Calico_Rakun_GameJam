using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class CardFlip : MonoBehaviour
{
    [Header("Card Faces")]
    public Sprite frontSprite;    // Kartın ön yüzü (How to Play yazısı)
    public Sprite backSprite;     // Kartın arka yüzü (kurallar image'ı)

    [Header("Flip Settings")]
    public float flipDuration = 0.4f;

    private Image cardImage;
    private TextMeshProUGUI cardText;
    private bool isFlipped = false;
    private bool isFlipping = false;

    void Start()
    {
        cardImage = GetComponent<Image>();
        cardText = GetComponentInChildren<TextMeshProUGUI>();
        frontSprite = cardImage.sprite; // Mevcut sprite'ı ön yüz olarak kaydet
    }

    // Butona bunu bağla
    public void FlipCard()
    {
        if (isFlipping) return;
        StartCoroutine(DoFlip());
    }

    IEnumerator DoFlip()
    {
        isFlipping = true;

        // 1. Aşama: Kartı kapat (scale X: 1 -> 0)
        float elapsed = 0f;
        float halfDuration = flipDuration / 2f;
        Vector3 scale = transform.localScale;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            transform.localScale = new Vector3(Mathf.Lerp(scale.x, 0f, t), scale.y, scale.z);
            yield return null;
        }
        transform.localScale = new Vector3(0f, scale.y, scale.z);

        // Sprite'ı değiştir
        isFlipped = !isFlipped;
        cardImage.sprite = isFlipped ? backSprite : frontSprite;
        if (cardText != null)
            cardText.gameObject.SetActive(!isFlipped);

        // 2. Aşama: Kartı aç (scale X: 0 -> 1)
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            transform.localScale = new Vector3(Mathf.Lerp(0f, scale.x, t), scale.y, scale.z);
            yield return null;
        }
        transform.localScale = scale;

        isFlipping = false;
    }
}