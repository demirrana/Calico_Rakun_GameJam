using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover Settings")]
    public float hoverRise = 150f;        // Kartın yukarı kalkma miktarı
    public float hoverScale = 1.2f;      // Büyüme oranı
    public float animSpeed = 8f;         // Animasyon hızı

    private Vector3 originalPos;
    private Vector3 originalScale;
    private Vector3 targetPos;
    private Vector3 targetScale;
    private bool isHovered = false;

    void Start()
    {
        originalPos = transform.localPosition;
        originalScale = transform.localScale;
        targetPos = originalPos;
        targetScale = originalScale;
    }

    void Update()
    {
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * animSpeed);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        targetPos = originalPos + new Vector3(0, hoverRise, 0);
        targetScale = originalScale * hoverScale;

    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        targetPos = originalPos;
        targetScale = originalScale;
    }
}