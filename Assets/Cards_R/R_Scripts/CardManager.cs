using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

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

    private bool isPlayer1sTurn = true; //gamemanager'a atılabilir

    private readonly string Trigger_SendToCenter = "SendToCenter";

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
        StartCoroutine(SetupGameRoutine());
    }

    private IEnumerator SetupGameRoutine()
    {
        yield return StartCoroutine(DealInitialCards(player1Cards, player1CardsData, mandatoryCardIndexes.Count));
        isPlayer1sTurn = false;
        yield return StartCoroutine(DealInitialCards(player2Cards, player2CardsData, mandatoryCardIndexes.Count));
        DeleteMandatoryCards(); //some cards are given to player only at start, so delete them for random card deal
        isPlayer1sTurn = true;
        //Debug.Log("player 1 cards data:\n");
        //LogList(player1CardsData);
        //Debug.Log("player 1 cards: \n");
        //LogList(player1Cards);
    }

    private void LogList<T>(List<T> list) //to be deleted later
    {
        foreach (T item in list)
        {
            Debug.Log(item.ToString());
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            isPlayer1sTurn = !isPlayer1sTurn;
            DetectDealingCards();
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            isPlayer1sTurn = !isPlayer1sTurn;
            DetectDealingCards();
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

    private IEnumerator DealInitialCards(List<Card> playerCards, List<SO_Card> playerCardsData, int mandatoryCardCount)
    {
        //Debug.Log("METHOD: DealInitialCards is called.");
        foreach (int mandatoryCardIndex in mandatoryCardIndexes)
        {
            playerCardsData.Add(allCards[mandatoryCardIndex]);
            //Debug.Log("Mandatory card added: " + allCards[mandatoryCardIndex]);
            lastDrawnCard = InstantiateCardOnDeck(allCards[mandatoryCardIndex], isPlayer1sTurn);
            playerCards.Add(lastDrawnCard);
            //lastDrawnCard = allCards[mandatoryCardIndex]; //!!!!!!!!bunu da instantiate içine at!!!!!!!
            yield return StartCoroutine(AnimateCardDealing(isPlayer1sTurn, lastDrawnCard));
        }
        yield return StartCoroutine(DealCards(playerCards, playerCardsData, maxCardCount - mandatoryCardCount));
    }

    private IEnumerator DealCards(List<Card> playerCards, List<SO_Card> playerCardsData, int cardCount) //gives random distinct cards to players
    {
        //Debug.Log("METHOD: Deal Cards called and " + cardCount + " cards are drawn.");
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

            //Debug.Log("randomly chosen index:" + randomIndex);
            chosenIndexes.Add(randomIndex);
            //create card object on the deck with its so data
            lastDrawnCard = InstantiateCardOnDeck(allCards[randomIndex], isPlayer1sTurn);
            playerCardsData.Add(allCards[randomIndex]);
            playerCards.Add(lastDrawnCard);
            //Debug.Log("so data: " + allCards[randomIndex].ToString());
            //Debug.Log("last drawn card: " + lastDrawnCard.name);

            yield return StartCoroutine(AnimateCardDealing(isPlayer1sTurn, lastDrawnCard));
        }
        Debug.Log("list after dealt cards: ");
        LogList(playerCards);
    }

    private Card InstantiateCardOnDeck(SO_Card cardData, bool isPlayer1sTurn) //create card object on deck transform
    {
        //Card newCard = new(cardData, isPlayer1sTurn);
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
        //Debug.Log("Trigger for " + card.name + " has been triggered.");
        yield return StartCoroutine(SendCardToItsPlace(card));
    }

    private IEnumerator FlipCard(Card card, Sprite frontSprite)
    {
        float duration = 0.25f; //half of flip time
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
        // 1. ANIMATOR'I KAPAT (Işınlanmayı durduran kritik hamle)
        // Merkeze gidiş animasyonu bittiği için artık kontrol koda geçmeli.
        Animator anim = card.GetAnimator();
        if (anim != null) anim.enabled = false;

        // 2. Parent'ı serbest bırak (Dünya koordinatlarına geçiş)
        card.transform.SetParent(null, true);

        // 3. Hedef Tayini
        Transform currentHolder = isPlayer1sTurn ? player1CardHolder : player2CardHolder;
        
        // Hangi indexi kullanacağımızı belirle
        int currentIndex = isPlayer1sTurn ? p1Index : p2Index;
        
        // X ve Y hesapla
        float xPos = startX + (currentIndex * cardSpacing); 
        float yPos = currentHolder.position.y; 
        Vector3 targetWorldPos = new Vector3(xPos, yPos, 0);

        // BİR SONRAKİ KART İÇİN SAYACI ARTIR
        if (isPlayer1sTurn) p1Index++; else p2Index++;

        Debug.Log($"{card.name} için hedef: {targetWorldPos} (Sıra: {currentIndex})");

        // 4. HAREKET DÖNGÜSÜ
        float timer = 0f;
        float duration = 1.0f; 
        Vector3 startWorldPos = card.transform.position;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.SmoothStep(0, 1, timer / duration);
            card.transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, progress);
            yield return null;
        }

        // 5. SABİTLEME
        card.transform.position = targetWorldPos;
        card.transform.SetParent(currentHolder, true);
    }
}