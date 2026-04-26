using UnityEngine;

public enum GameState
{
    Setup_Player1,
    Setup_Player2,
    Player1_MovePhase,
    Player1_ActionPhase,
    Player2_MovePhase,
    Player2_ActionPhase,
    GameOver
}

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    [Header("Oyuncular")]
    public PlayerController player1;
    public PlayerController player2;

    [Header("Oyun Durumu")]
    public GameState currentState;

    [Header("D�� Sistem Haberle�me (Card & Ring)")]
    public bool cardOrRingTurnPlayable = false;
    public bool cardOrRingTurnPlayed = false;

    private int cursorRing = 0;
    private int cursorSlice = 0;

    [HideInInspector] public PlayerController activePlayer;
    private TileData currentHighlightedTile;

    private bool isProcessingMovementOrTraps = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        ChangeState(GameState.Setup_Player1);
    }

    void Update()
    {
        if (isProcessingMovementOrTraps) return;

        switch (currentState)
        {
            case GameState.Setup_Player1:
            case GameState.Setup_Player2:
                HandleFreeCursorInput();
                if (Input.GetKeyDown(KeyCode.Space)) ConfirmSetupSelection();
                break;

            case GameState.Player1_MovePhase:
            case GameState.Player2_MovePhase:
                HandleAdjacentCursorInput();
                if (Input.GetKeyDown(KeyCode.Space)) ConfirmMoveSelection();
                break;

            case GameState.Player1_ActionPhase:
            case GameState.Player2_ActionPhase:
                if (cardOrRingTurnPlayed)
                {
                    EndActionPhase();
                }
                break;

            case GameState.GameOver:
                break;
        }
    }

    void ChangeState(GameState newState)
    {
        currentState = newState;
        cardOrRingTurnPlayable = false;
        cardOrRingTurnPlayed = false;

        switch (currentState)
        {
            case GameState.Setup_Player1:
                ChangeActivePlayer(player1);
                ResetCursorToCenter();
                break;
            case GameState.Setup_Player2:
                ChangeActivePlayer(player2);
                ResetCursorToCenter();
                break;
            case GameState.Player1_MovePhase:
                ChangeActivePlayer(player1);
                SnapCursorToPlayer(activePlayer);
                break;
            case GameState.Player2_MovePhase:
                ChangeActivePlayer(player2);
                SnapCursorToPlayer(activePlayer);
                break;
            case GameState.Player1_ActionPhase:
            case GameState.Player2_ActionPhase:
                cardOrRingTurnPlayable = true;
                HideCursor();
                break;
        }
    }
    void ChangeActivePlayer(PlayerController activePlayer)
    {
        this.activePlayer = activePlayer;
        CardManager.Instance.TriggerChangeActivePlayer(activePlayer);
    }

    void ConfirmSetupSelection()
    {
        TileData selectedTile = GridManager.Instance.GetTile(cursorRing, cursorSlice);
        activePlayer.SetPositionImmediate(cursorRing, cursorSlice, selectedTile.tileTransform);

        if (currentState == GameState.Setup_Player1)
        {
            ChangeState(GameState.Setup_Player2);
        }
        else
        {
            if (GridFiller.Instance != null)
            {
                GridFiller.Instance.FillGrid();
            }
            else
            {
                Debug.LogError("Sahnede GridFiller objesi bulunamad�!");
            }

            ChangeState(GameState.Player1_MovePhase);
        }
    }

    void ConfirmMoveSelection()
    {
        // Oyuncu zaten oldu�u yeri tekrar se�erse hi�bir �ey yapma
        if (cursorRing == activePlayer.currentRing && cursorSlice == activePlayer.currentSlice) return;

        TileData targetTile = GridManager.Instance.GetTile(cursorRing, cursorSlice);

        // Sistemi kitle ve hareketi ba�lat
        isProcessingMovementOrTraps = true;
        HideCursor();

        activePlayer.MoveTo(cursorRing, cursorSlice, targetTile.tileTransform, EvaluateCurrentPlayerTile);
    }

    public void EvaluateCurrentPlayerTile()
    {
        TileData currentTile = GridManager.Instance.GetTile(activePlayer.currentRing, activePlayer.currentSlice);

        // 1. HAZ�NE KONTROL�
        if (currentTile.hasTreasure)
        {
            Debug.Log($"<color=green>OYUN B�TT�! {activePlayer.playerName} HAZ�NEY� BULDU!</color>");
            isProcessingMovementOrTraps = false;
            ChangeState(GameState.GameOver);
            return;
        }

        // 2. TUZAK KONTROL�
        if (currentTile.trapType != TrapType.None)
        {
            Debug.Log($"{activePlayer.playerName} tuza�a bast�! Tuzak Tipi: {currentTile.trapType}");

            // Tuzak eventini �al��t�r (isProcessingMovementOrTraps hala TRUE, yani oyun kilitli bekliyor)
            currentTile.onTrapTriggered?.Invoke();

            // Zincirleme i�in burada kesiyoruz, tuza��n coroutine'i i�i bitince bu fonksiyonu tekrar �a��racak.
            return;
        }

        // 3. G�VENL� ALAN (None)
        isProcessingMovementOrTraps = false;

        if (currentState == GameState.Player1_MovePhase)
            ChangeState(GameState.Player1_ActionPhase);
        else if (currentState == GameState.Player2_MovePhase)
            ChangeState(GameState.Player2_ActionPhase);
    }

    void EndActionPhase()
    {
        if (currentState == GameState.Player1_ActionPhase)
            ChangeState(GameState.Player2_MovePhase);
        else if (currentState == GameState.Player2_ActionPhase)
            ChangeState(GameState.Player1_MovePhase);
    }

    // --- CURSOR (�MLE�) S�STEM� ---

    void HandleFreeCursorInput()
    {
        bool moved = false;

        if (Input.GetKeyDown(KeyCode.UpArrow) && cursorRing < GridManager.Instance.totalRings - 1) { cursorRing++; moved = true; }
        if (Input.GetKeyDown(KeyCode.DownArrow) && cursorRing > 0) { cursorRing--; moved = true; }

        if (Input.GetKeyDown(KeyCode.RightArrow)) { cursorSlice = (cursorSlice + 1) % GridManager.Instance.totalSlices; moved = true; }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) { cursorSlice = (cursorSlice - 1 + GridManager.Instance.totalSlices) % GridManager.Instance.totalSlices; moved = true; }

        if (moved) UpdateCursorVisual();
    }

    void HandleAdjacentCursorInput()
    {
        bool moved = false;
        int r = activePlayer.currentRing;
        int s = activePlayer.currentSlice;

        if (Input.GetKeyDown(KeyCode.UpArrow) && r < GridManager.Instance.totalRings - 1) { cursorRing = r + 1; cursorSlice = s; moved = true; }
        if (Input.GetKeyDown(KeyCode.DownArrow) && r > 0) { cursorRing = r - 1; cursorSlice = s; moved = true; }

        if (Input.GetKeyDown(KeyCode.RightArrow)) { cursorRing = r; cursorSlice = (s + 1) % GridManager.Instance.totalSlices; moved = true; }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) { cursorRing = r; cursorSlice = (s - 1 + GridManager.Instance.totalSlices) % GridManager.Instance.totalSlices; moved = true; }

        if (moved) UpdateCursorVisual();
    }

    void UpdateCursorVisual()
    {
        HideCursor();

        currentHighlightedTile = GridManager.Instance.GetTile(cursorRing, cursorSlice);

        // Tile'�n 1. child'�n� bul ve aktif et
        if (currentHighlightedTile != null && currentHighlightedTile.tileTransform.childCount > 0)
        {
            currentHighlightedTile.tileTransform.GetChild(0).gameObject.SetActive(true);
        }
    }

    void HideCursor()
    {
        // �nceki aktif imleci kapat
        if (currentHighlightedTile != null && currentHighlightedTile.tileTransform.childCount > 0)
        {
            currentHighlightedTile.tileTransform.GetChild(0).gameObject.SetActive(false);
        }
    }

    void ResetCursorToCenter()
    {
        cursorRing = 0;
        cursorSlice = 0;
        UpdateCursorVisual();
    }

    void SnapCursorToPlayer(PlayerController player)
    {
        cursorRing = player.currentRing;
        cursorSlice = player.currentSlice;
        UpdateCursorVisual();
    }
}