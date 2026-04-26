using UnityEngine;
using UnityEngine.EventSystems;

public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Hover Settings")]
    public float hoverRise = 1f;
    public float hoverScale = 1.15f;    
    public float animSpeed = 10f;       

    private Vector3 originalPos;
    private Vector3 originalScale;
    private Vector3 targetPos;
    private Vector3 targetScale;
    private bool isReady = false;

    // CardManager kartı yerine koyduğunda bu çalışacak
    public void Activate(Vector3 correctLocalPos)
    {
        originalPos = correctLocalPos;
        originalScale = transform.localScale;
        targetPos = originalPos;
        targetScale = originalScale;
        isReady = true;
    }

    void Update()
    {
        if (!isReady) return;

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * animSpeed);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animSpeed);
    }

    public void OnPointerClick(PointerEventData eventData) //Trigger event OnCardChosen in CardManager script
    {
        if (!isReady) return;

        // Sol tık kontrolü (istersen sağ tıkı da ayırabilirsin)
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Card card = GetComponent<Card>();
            CardManager.Instance.RaiseCardChosen(this, card);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isReady) return;
        targetPos = originalPos + new Vector3(0, hoverRise, 0);
        targetScale = originalScale * hoverScale;
        transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isReady) return;
        targetPos = originalPos;
        targetScale = originalScale;
    }
}