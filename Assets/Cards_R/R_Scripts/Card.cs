using UnityEngine;

public class Card : MonoBehaviour
{
    [SerializeField] private SpriteRenderer shownSpriteRenderer; //should be called using ".sprite"

    private Animator cardAnimator;

    private SO_Card cardData;
    private bool belongsToPlayer1;

    private void Awake()
    {
        cardAnimator = GetComponent<Animator>();
    }

    void LateUpdate()
    {
        transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward, 
                     Camera.main.transform.rotation * Vector3.up);
    }

    public Card(SO_Card cardData, bool belongsToPlayer1)
    {
        this.cardData = cardData;
        this.belongsToPlayer1 = belongsToPlayer1;
    }

    public void SetCard(SO_Card cardData, bool belongsToPlayer1)
    {
        this.cardData = cardData;
        this.belongsToPlayer1 = belongsToPlayer1;
    }

    public SO_Card GetCardData()
    {
        return cardData;
    }

    public Sprite GetShownFace()
    {
        return shownSpriteRenderer.sprite;
    }

    public void SetSpriteOfShownFace(Sprite newFace)
    {
        shownSpriteRenderer.sprite = newFace;
    }

    public bool DoesBelongToPlayer1()
    {
        return belongsToPlayer1;
    }

    public Animator GetAnimator()
    {
        return cardAnimator;
    }

    /*
    public void SwapFacesSortingOrders()
    {
        int frontFaceSortingOrder = cardData.frontFace.sortingOrder;
        cardData.frontFace.sortingOrder = backFace.sortingOrder;
        backFace.sortingOrder = frontFaceSortingOrder;
    }
    */
}
