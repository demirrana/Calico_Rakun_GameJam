using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    public enum CardType
    {
        Card1,
        Card2,
        Card3,
        Card4,
        Card5,
        Card6,
        Card7,
        Card8,
        Card9,
        Card10,
        Card11,
        Card12,
        Card13
    }

    public event EventHandler<CardEventArgs.ChooseCardEventArgs> OnCardChosen;

    [SerializeField] private GameObject turnManagerObj;
    [SerializeField] private GameObject ringRotationObj;

    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform cardDeckTransform;

    [SerializeField] private int maxCardCount = 6;
    [SerializeField] private int minCardCount = 3;

    [SerializeField] private List<SO_Card> allCards = new();

    [SerializeField] private List<int> mandatoryCardIndexes = new();

    [Header("Card Placements")]
    [SerializeField] private Vector2 cardDeckLocation;
    [SerializeField] private Transform player1CardHolder;
    [SerializeField] private Transform player2CardHolder;
    //private float screenWidth = Camera.main.orthographicSize * 2.0f * Screen.width / Screen.height;
    //cardSpacing, screenWidth'in 7'de biri kadar olabilir. başlangıç noktası da buna göre olmalı. (screenWidth / 7 / 2)

    [SerializeField] private float cardAnimationPeriod = 4f;
    [SerializeField] private float reachCenterPeriod = 1.3f;

    private List<Card> player1Cards = new();
    private List<Card> player2Cards = new();

    private List<SO_Card> player1CardsData = new();
    private List<SO_Card> player2CardsData = new();

    [Header("Slot References")]
    [SerializeField] private float cardSpacing = 2.56f; // Kartlar arası mesafe
    [SerializeField] private float startX = -6.5f;     // En soldaki kartın X pozisyonu
    [SerializeField] private float p1YHeight = -4.0f;  // Oyuncu 1'in Y yüksekliği
    [SerializeField] private float p2YHeight = 4.0f;   // Oyuncu 2'in Y yüksekliği

    private int p1Index = 0;
    private int p2Index = 0;

    private Card lastDrawnCard;

    public bool isPlayer1sTurn = true;

    private bool isStartOfGame = true;

    private readonly string Trigger_SendToCenter = "SendToCenter";

    private Button button1;
    private Button button2;

    public GameObject hintObject;
    public TextMeshProUGUI hintText;
    public Image hoveredImage;
    public void RaiseCardChosen(object sender, Card card)
    {
        OnCardChosen?.Invoke(sender, new CardEventArgs.ChooseCardEventArgs(isPlayer1sTurn, card));
    }

    private void Awake()
    {
        SetSingleton();
    }

    private void SetSingleton()
    {
        if (Instance != this && Instance != null)
        {
            Destroy(gameObject);
        }
        Instance = this;
    }

    private void Start()
    {
        button1 = CameraManager.Instance.button1;
        button2 = CameraManager.Instance.button2;
        button1.interactable = false;
        button2.interactable = false;
        StartCoroutine(SetupGameRoutine());
    }

    private IEnumerator SetupGameRoutine()
    {
        yield return StartCoroutine(DealInitialCards(player1Cards, player1CardsData, mandatoryCardIndexes.Count));
        ToggleCardHoverOfPlayer(player1Cards);
        isPlayer1sTurn = false;
        yield return StartCoroutine(DealInitialCards(player2Cards, player2CardsData, mandatoryCardIndexes.Count));
        ToggleCardHoverOfPlayer(player2Cards);
        SetPlayer2HoverRise();
        DeleteMandatoryCards(); //some cards are given to player only at start, so delete them for random card deal
        isPlayer1sTurn = true;
        isStartOfGame = false;
        turnManagerObj.SetActive(true);
        ringRotationObj.SetActive(true);
        button1.interactable = true;
        button2.interactable = true;
    }

    private void SetPlayer2HoverRise()
    {
        Debug.Log("SetPlayer2HoverRise");
        foreach (Card card in player2Cards)
        {
            CardHover cardHover = card.GetComponent<CardHover>();
            cardHover.ChangeHoverRise();
        }
    }

    private void LogList<T>(List<T> list) //to be deleted later
    {
        foreach (T item in list)
        {
            Debug.Log(item.ToString());
        }
    }

    private void DetectDealingCards() //consider deleting other player's cards
    {
        if (isPlayer1sTurn) //player 1's turn
        {
            if (player1CardsData.Count <= minCardCount)
            {
                DealCards(player1Cards, player1CardsData, maxCardCount - player1Cards.Count);

                //increase or decrease cards
            }
        }
        else //player 2's turn
        {
            if (player2CardsData.Count <= minCardCount)
            {
                DealCards(player2Cards, player2CardsData, maxCardCount - player2Cards.Count);

                //increase or decrease cards
            }
        }
    }

    public void TriggerChangeActivePlayer(PlayerController activePlayer)
    {
        isPlayer1sTurn = activePlayer == TurnManager.Instance.player1 ? true : false;
        DetectDealingCards(); //bunu da turnmanagerda endphase kismina atmam gerekebilir
    }

    public void SwapHoverRises()
    {
        Debug.Log("SwapHoverRise");
        foreach (Card card in player1Cards)
        {
            CardHover cardHover = card.GetComponent<CardHover>();
            cardHover.ChangeHoverRise();
        }
        foreach (Card card in player2Cards)
        {
            CardHover cardHover = card.GetComponent<CardHover>();
            cardHover.ChangeHoverRise();
        }
    }

    private IEnumerator DealInitialCards(List<Card> playerCards, List<SO_Card> playerCardsData, int mandatoryCardCount)
    {
        foreach (int mandatoryCardIndex in mandatoryCardIndexes)
        {
            playerCardsData.Add(allCards[mandatoryCardIndex]);
            lastDrawnCard = InstantiateCardOnDeck(allCards[mandatoryCardIndex], isPlayer1sTurn);
            playerCards.Add(lastDrawnCard);
            //lastDrawnCard = allCards[mandatoryCardIndex]; //!!!!!!!!bunu da instantiate içine at!!!!!!!
            yield return StartCoroutine(AnimateCardDealing(isPlayer1sTurn, lastDrawnCard));
        }
        yield return StartCoroutine(DealCards(playerCards, playerCardsData, maxCardCount - mandatoryCardCount));
    }

    private IEnumerator DealCards(List<Card> playerCards, List<SO_Card> playerCardsData, int cardCount) //gives random distinct cards to players
    {
        List<int> chosenIndexes = new();
        for (int i = 0; i < cardCount; i++)
        {
            System.Random random = new();
            int randomIndex = random.Next(allCards.Count);
            bool playerAlreadyHasThatCard = playerCardsData.Contains(allCards[randomIndex]);
            while (chosenIndexes.Contains(randomIndex) || playerAlreadyHasThatCard)
            {
                randomIndex = random.Next(allCards.Count);
                playerAlreadyHasThatCard = playerCardsData.Contains(allCards[randomIndex]);
            }

            chosenIndexes.Add(randomIndex);
            //create card object on the deck with its so data
            lastDrawnCard = InstantiateCardOnDeck(allCards[randomIndex], isPlayer1sTurn);
            playerCardsData.Add(allCards[randomIndex]);
            playerCards.Add(lastDrawnCard);

            yield return StartCoroutine(AnimateCardDealing(isPlayer1sTurn, lastDrawnCard));
        }
        ActivateHoverEffect(isPlayer1sTurn);
    }

    private Card InstantiateCardOnDeck(SO_Card cardData, bool isPlayer1sTurn) //create card object on deck transform
    {
        GameObject cardObj = Instantiate(cardPrefab, cardDeckTransform);
        if (cardObj.TryGetComponent<Card>(out Card newCard))
        {
            newCard.SetCard(cardData, isPlayer1sTurn);
            return newCard;
        }
        else //error (Card has no component as Card. This cannot happen normally) !!!!!JUST FOR DEBUG
        {
            Debug.Log("Card object has no Card component.");
            Destroy(cardObj);
            return null;
        }
    }

    private void DeleteMandatoryCards() //WARNING: mandatory card list is getting changed here
    {
        List<int> toBeDeletedIndexes = mandatoryCardIndexes.Distinct().OrderByDescending(x => x).ToList();

        foreach (int cardIndex in toBeDeletedIndexes)
        {
            SO_Card card = allCards[cardIndex];

            allCards.Remove(card);
        }
    }

    private IEnumerator AnimateCardDealing(bool isPlayer1sTurn, Card card) //add parameter of card (type Card)
    {
        //assign drawn card's back side as a random back face (from lastDrawnCard's cardFace)
        Animator cardAnimator = card.GetAnimator();
        cardAnimator.SetTrigger(Trigger_SendToCenter);
        yield return new WaitForSeconds(reachCenterPeriod);
        yield return StartCoroutine(FlipCard(card, card.GetCardData().frontFace));
        yield return StartCoroutine(SendCardToItsPlace(card));
    }

    private IEnumerator FlipCard(Card card, Sprite frontSprite)
    {
        float duration = 0.10f; //half of flip time
        Vector3 originalScale = card.transform.localScale;

        float timer = 0; //narrow the card down
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;
            card.transform.localScale = new Vector3(Mathf.Lerp(originalScale.x, 0, progress), originalScale.y, originalScale.z);
            yield return null;
        }

        card.SetSpriteOfShownFace(frontSprite); //change sprite with front sprite

        timer = 0;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;
            card.transform.localScale = new Vector3(Mathf.Lerp(0, originalScale.x, progress), originalScale.y, originalScale.z);
            yield return null;
        }
    }

    private IEnumerator SendCardToItsPlace(Card card)
    {
        Animator anim = card.GetAnimator();
        if (anim != null) anim.enabled = false;

        card.transform.SetParent(null, true);

        Transform currentHolder = isPlayer1sTurn ? player1CardHolder : player2CardHolder;
        
        int currentIndex = isPlayer1sTurn ? p1Index : p2Index;
        
        float xPos = startX + (currentIndex * cardSpacing); 
        float yPos = currentHolder.position.y; 
        Vector3 targetWorldPos = new Vector3(xPos, yPos, 0);

        if (isPlayer1sTurn) p1Index++; else p2Index++;

        Debug.Log($"{card.name} için hedef: {targetWorldPos} (Sıra: {currentIndex})");

        float timer = 0f;
        float duration = 0.5f; 
        Vector3 startWorldPos = card.transform.position;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.SmoothStep(0, 1, timer / duration);
            card.transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, progress);
            yield return null;
        }

        card.transform.position = targetWorldPos;
        card.transform.SetParent(currentHolder, true);

        card.transform.position = targetWorldPos;
    }

    private void ActivateHoverEffect(bool isPlayer1sTurn)
    {
        List<Card> playerCards = isPlayer1sTurn ? player1Cards : player2Cards;

        foreach (Card card in playerCards)
        {
            if(card.TryGetComponent<CardHover>(out var hover))
            {
                hover.enabled = true; // Scripti aç
                hover.Activate(card.transform.localPosition); // Doğru yerel pozisyonu kaydet
            }
        }
    }
    public void RemoveCard(Card card, bool isPlayer1)
    {
        if (isPlayer1)
        {
            player1Cards.Remove(card);
            player1CardsData.Remove(card.GetCardData());
        }
        else
        {
            player2Cards.Remove(card);
            player2CardsData.Remove(card.GetCardData());
        }
        Debug.Log($"[CardManager] Kart silindi: {card.GetCardData().cardType}. Kalan: {(isPlayer1 ? player1Cards.Count : player2Cards.Count)}");
    }

    public void ToggleCardHoversOfPlayers()
    {
        ToggleCardHoverOfPlayer(player1Cards);
        ToggleCardHoverOfPlayer(player2Cards);
    }

    private void ToggleCardHoverOfPlayer(List<Card> playerCards)
    {
        foreach (Card card in playerCards)
        {
            CardHover cardHover = card.GetComponent<CardHover>();
            cardHover.enabled = !cardHover.enabled;
        }
    }
}