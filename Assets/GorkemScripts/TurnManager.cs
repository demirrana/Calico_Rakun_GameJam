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

    [Header("Dış Sistem Haberleşme (Card & Ring)")]
    public bool cardOrRingTurnPlayable = false;
    public bool cardOrRingTurnPlayed = false;

    // YENİ: Kamera ve Kontrol Kilitleri
    [HideInInspector] public bool isCameraMoving = false;
    [HideInInspector] public bool invertControls = false;

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
        // YENİ: Kamera dönerken veya tuzaklar çalışırken girdi almayı engelle
        if (isProcessingMovementOrTraps || isCameraMoving) return;

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
        if (CardManager.Instance != null)
            CardManager.Instance.TriggerChangeActivePlayer(activePlayer);
    }

    void ConfirmSetupSelection()
    {
        TileData selectedTile = GridManager.Instance.GetTile(cursorRing, cursorSlice);
        activePlayer.SetPositionImmediate(cursorRing, cursorSlice, selectedTile.tileTransform);

        if (currentState == GameState.Setup_Player1)
        {
            ChangeState(GameState.Setup_Player2);
            // P1 yerleştikten sonra kamerayı P2'ye çevir
            if (CameraManager.Instance != null) CameraManager.Instance.SwitchTurnView(false);
        }
        else
        {
            if (GridFiller.Instance != null)
            {
                GridFiller.Instance.FillGrid();
            }
            else
            {
                Debug.LogError("Sahnede GridFiller objesi bulunamadı!");
            }

            ChangeState(GameState.Player1_MovePhase);
            // Oyun başladığında kamerayı tekrar P1'e çevir
            if (CameraManager.Instance != null) CameraManager.Instance.SwitchTurnView(true);
        }
    }

    void ConfirmMoveSelection()
    {
        // Oyuncu zaten olduğu yeri tekrar seçerse hiçbir şey yapma
        if (cursorRing == activePlayer.currentRing && cursorSlice == activePlayer.currentSlice) return;

        TileData targetTile = GridManager.Instance.GetTile(cursorRing, cursorSlice);

        // Sistemi kitle ve hareketi başlat
        isProcessingMovementOrTraps = true;
        HideCursor();

        activePlayer.MoveTo(cursorRing, cursorSlice, targetTile.tileTransform, EvaluateCurrentPlayerTile);
    }

    public void EvaluateCurrentPlayerTile()
    {
        TileData currentTile = GridManager.Instance.GetTile(activePlayer.currentRing, activePlayer.currentSlice);

        // 1. HAZİNE KONTROLÜ
        if (currentTile.hasTreasure)
        {
            Debug.Log($"<color=green>OYUN BİTTİ! {activePlayer.playerName} HAZİNEYİ BULDU!</color>");
            isProcessingMovementOrTraps = false;
            ChangeState(GameState.GameOver);
            return;
        }

        // 2. TUZAK KONTROLÜ
        if (currentTile.trapType != TrapType.None)
        {
            Debug.Log($"{activePlayer.playerName} tuzağa bastı! Tuzak Tipi: {currentTile.trapType}");

            // Tuzak eventini çalıştır
            currentTile.onTrapTriggered?.Invoke();
            return;
        }

        // 3. GÜVENLİ ALAN (None)
        isProcessingMovementOrTraps = false;

        if (currentState == GameState.Player1_MovePhase)
            ChangeState(GameState.Player1_ActionPhase);
        else if (currentState == GameState.Player2_MovePhase)
            ChangeState(GameState.Player2_ActionPhase);
    }

    void EndActionPhase()
    {
        if (currentState == GameState.Player1_ActionPhase)
        {
            ChangeState(GameState.Player2_MovePhase);
            if (CameraManager.Instance != null) CameraManager.Instance.SwitchTurnView(false);
        }
        else if (currentState == GameState.Player2_ActionPhase)
        {
            ChangeState(GameState.Player1_MovePhase);
            if (CameraManager.Instance != null) CameraManager.Instance.SwitchTurnView(true);
        }
    }

    // --- CURSOR (İMLEÇ) SİSTEMİ (İNVERT KONTROL EKLENDİ) ---

    void HandleFreeCursorInput()
    {
        bool moved = false;

        // YENİ: Kamera yönüne göre tuşları tersine çeviriyoruz
        KeyCode upKey = invertControls ? KeyCode.DownArrow : KeyCode.UpArrow;
        KeyCode downKey = invertControls ? KeyCode.UpArrow : KeyCode.DownArrow;
        KeyCode rightKey = invertControls ? KeyCode.LeftArrow : KeyCode.RightArrow;
        KeyCode leftKey = invertControls ? KeyCode.RightArrow : KeyCode.LeftArrow;

        if (Input.GetKeyDown(upKey) && cursorRing < GridManager.Instance.totalRings - 1) { cursorRing++; moved = true; }
        if (Input.GetKeyDown(downKey) && cursorRing > 0) { cursorRing--; moved = true; }

        if (Input.GetKeyDown(rightKey)) { cursorSlice = (cursorSlice + 1) % GridManager.Instance.totalSlices; moved = true; }
        if (Input.GetKeyDown(leftKey)) { cursorSlice = (cursorSlice - 1 + GridManager.Instance.totalSlices) % GridManager.Instance.totalSlices; moved = true; }

        if (moved) UpdateCursorVisual();
    }

    void HandleAdjacentCursorInput()
    {
        bool moved = false;
        int r = activePlayer.currentRing;
        int s = activePlayer.currentSlice;

        KeyCode upKey = invertControls ? KeyCode.DownArrow : KeyCode.UpArrow;
        KeyCode downKey = invertControls ? KeyCode.UpArrow : KeyCode.DownArrow;
        KeyCode rightKey = invertControls ? KeyCode.LeftArrow : KeyCode.RightArrow;
        KeyCode leftKey = invertControls ? KeyCode.RightArrow : KeyCode.LeftArrow;

        if (Input.GetKeyDown(upKey) && r < GridManager.Instance.totalRings - 1) { cursorRing = r + 1; cursorSlice = s; moved = true; }
        if (Input.GetKeyDown(downKey) && r > 0) { cursorRing = r - 1; cursorSlice = s; moved = true; }

        if (Input.GetKeyDown(rightKey)) { cursorRing = r; cursorSlice = (s + 1) % GridManager.Instance.totalSlices; moved = true; }
        if (Input.GetKeyDown(leftKey)) { cursorRing = r; cursorSlice = (s - 1 + GridManager.Instance.totalSlices) % GridManager.Instance.totalSlices; moved = true; }

        if (moved) UpdateCursorVisual();
    }

    void UpdateCursorVisual()
    {
        HideCursor();

        currentHighlightedTile = GridManager.Instance.GetTile(cursorRing, cursorSlice);

        if (currentHighlightedTile != null && currentHighlightedTile.tileTransform.childCount > 0)
        {
            currentHighlightedTile.tileTransform.GetChild(0).gameObject.SetActive(true);
        }
    }

    void HideCursor()
    {
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