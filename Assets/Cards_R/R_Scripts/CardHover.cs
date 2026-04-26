using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Hover Settings")]
    public float hoverRise = 3f;
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

    public void OnPointerClick(PointerEventData eventData) 
    {
        if (!isReady) return;

        Card card = this.gameObject.GetComponent<Card>();
        bool isPlayer1sTurn = CardManager.Instance.isPlayer1sTurn;
        bool doesHolderBelongToPlayer1 = card.transform.parent == CardManager.Instance.GetPlayer1CardHolder();

        if ((isPlayer1sTurn && !doesHolderBelongToPlayer1) || (!isPlayer1sTurn && doesHolderBelongToPlayer1))
        {
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            CardManager.Instance.RaiseCardChosen(this, card);
            if (TurnManager.Instance.cardOrRingTurnPlayable)
            {
                CardManager.Instance.RemoveCardAndReorganize(card);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CardManager.Instance.hintObject.SetActive(true);
        CardManager.Instance.hintObject.GetComponent<Animator>().SetTrigger("menuOpen");
        CardManager.Instance.hintText.text = GetComponent<Card>().GetCardData().hintText;
        CardManager.Instance.hoveredImage.sprite = GetComponent<Card>().GetShownFace();
        if (!isReady) return;
        targetPos = originalPos + new Vector3(0, hoverRise, 0);
        targetScale = originalScale * hoverScale;
        transform.SetAsLastSibling();
    }

    public void ChangeHoverRise()
    {
        hoverRise = -hoverRise;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CardManager.Instance.hintObject.GetComponent<Animator>().SetTrigger("menuClose");
        if (!isReady) return;
        targetPos = originalPos;
        targetScale = originalScale;
    }
}