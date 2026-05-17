using System;
using UnityEngine;

/// <summary>
/// Oyunun üst-düzey state'ini ve sıra mantığını yönetir.
///
/// State machine:
///   SettingUpP1 → P1 başlangıç tile'ını seçiyor.
///   SettingUpP2 → P2 başlangıç tile'ını seçiyor.
///   Playing     → Sırayla hamle yapılıyor.
///
/// Diğer sistemler (input, UI) state'i dinler, GameManager'a doğrudan
/// emir vermez (input → GameManager.ConfirmX metotları).
/// State değişimleri event olarak duyurulur.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ========== STATE ENUM ==========

    public enum GameState
    {
        SettingUpP1,
        SettingUpP2,
        Playing
    }

    private GameState currentState;
    public GameState CurrentState => currentState;

    // ========== INSPECTOR REFERANSLARI ==========

    [Header("Referanslar")]
    [Tooltip("Sahnedeki SquareGridManager.")]
    [SerializeField] private SquareGridManager gridManager;

    [Tooltip("Player prefab. Her iki oyuncu da bundan üretilir, " +
             "Initialize ile rengi/ID'si verilir.")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Oyuncu Renkleri")]
    [SerializeField] private Color player1Color = Color.red;
    [SerializeField] private Color player2Color = Color.blue;
    public Color Player1Color => player1Color;
    public Color Player2Color => player2Color;

    public Sprite GetPlayerSprite(int index) => (index == 0) ? player1Sprite : player2Sprite;
    public Color GetPlayerColor(int index) => (index == 0) ? player1Color : player2Color;

    [Header("Oyuncu Sprite'ları")]
    [Tooltip("Player 1 sprite'ı. Boş bırakılırsa prefab'taki default kullanılır.")]
    [SerializeField] private Sprite player1Sprite;

    [Tooltip("Player 2 sprite'ı. Boş bırakılırsa prefab'taki default kullanılır.")]
    [SerializeField] private Sprite player2Sprite;

    // ========== OYUNCU STATE'İ ==========

    // index 0 = P1, index 1 = P2. Spawn edilene kadar null.
    private Player[] players = new Player[2];

    private int currentPlayerIndex = 0;
    public int CurrentPlayerIndex => currentPlayerIndex;
    public Player CurrentPlayer => players[currentPlayerIndex];

    // Tüm oyuncuları gezmek isteyen sistemler için (UI vs.).
    public Player GetPlayer(int index) => players[index];

    // ========== EVENT'LER ==========

    /// <summary>State değiştiğinde UI ve input controller dinler.</summary>
    public static event Action<GameState> OnGameStateChanged;

    /// <summary>Playing state'inde sıra başladığında (parametre: oyuncu indeksi).</summary>
    public static event Action<int> OnTurnStarted;

    // ========== SINGLETON (HAFİF) ==========
    // Tam DontDestroyOnLoad'lı singleton değil — sadece "tek instance var,
    // kolay erişim" amaçlı statik referans.

    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameManager] Birden fazla instance var, ekstrayı siliyorum.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        // SquareGridManager'ın shift event'ine abone ol.
        // OnEnable/OnDisable çiftinde yapmak Unity standart pattern'i:
        // obje kapatılınca event leak olmasın diye.
        SquareGridManager.OnLineShiftStarted += HandleLineShiftStarted;
    }

    private void OnDisable()
    {
        SquareGridManager.OnLineShiftStarted -= HandleLineShiftStarted;
    }

    private void Start()
    {
        // Oyun başlar başlamaz P1 setup phase'i.
        ChangeState(GameState.SettingUpP1);
    }

    // ========== STATE GEÇIŞ ==========

    /// <summary>
    /// Tüm state geçişlerinin tek girişi. Event'i tetikler.
    /// Doğrudan currentState atamak yerine bunu çağırmak event'lerin
    /// kaçırılmamasını garanti eder.
    /// </summary>
    private void ChangeState(GameState newState)
    {
        currentState = newState;
        Debug.Log($"[GameManager] State → {newState}");
        OnGameStateChanged?.Invoke(newState);
    }

    // ========== SETUP CONFIRM ==========

    /// <summary>
    /// Input controller, kullanıcı space ile başlangıç tile'ını onayladığında
    /// bu metodu çağırır. Mevcut state'e göre uygun oyuncuyu spawn eder
    /// ve bir sonraki state'e geçer.
    /// </summary>
    public void ConfirmPlayerPlacement(Vector2Int gridPos)
    {
        if (currentState == GameState.SettingUpP1)
        {
            SpawnPlayer(0, gridPos, player1Color);
            ChangeState(GameState.SettingUpP2);
        }
        else if (currentState == GameState.SettingUpP2)
        {
            SpawnPlayer(1, gridPos, player2Color);
            currentPlayerIndex = 0; // P1 başlar
            ChangeState(GameState.Playing);
            OnTurnStarted?.Invoke(currentPlayerIndex);
        }
        else
        {
            Debug.LogWarning($"[GameManager] ConfirmPlayerPlacement Setup state'inde değil ({currentState}).");
        }
    }

    private void SpawnPlayer(int index, Vector2Int gridPos, Color color)
    {
        if (playerPrefab == null || gridManager == null)
        {
            Debug.LogError("[GameManager] playerPrefab veya gridManager atanmamış!");
            return;
        }

        GameObject obj = Instantiate(playerPrefab, transform);
        obj.name = $"Player_{index + 1}";

        Player p = obj.GetComponent<Player>();
        if (p == null)
        {
            Debug.LogError("[GameManager] playerPrefab'da Player script'i yok!");
            return;
        }

        // Renk Initialize'a parametre olarak girmiyor; basitlik adına
        // önce Initialize, sonra renk için ayrı setter yapabilirdik ama
        // şu an Player.Initialize içinde playerColor'a göre boyuyor →
        // bunu Initialize öncesi set edelim:

        Sprite sprite = (index == 0) ? player1Sprite : player2Sprite;
        p.Initialize(gridManager, index + 1, color, sprite);
        p.SetGridPosition(gridPos);

        players[index] = p;
    }

    // ========== HAMLE CONFIRM ==========

    /// <summary>
    /// Input controller, Playing state'inde hamle onaylanınca çağırır.
    ///
    /// Akış:
    ///   1. Sıradaki oyuncuyu yakala (henüz flip etmeden).
    ///   2. currentPlayerIndex'i HEMEN flip et — böylece animasyon sırasında
    ///      "current player kim?" sorusuna doğru cevap verilir (örn. shift
    ///      gelirse hangi oyuncunun event'ine bakacağız).
    ///   3. Eski oyuncuya AnimateMove ile hareket emri ver.
    ///   4. AnimateMove'un OnMoveCompleted'ına bir kerelik abone ol;
    ///      animasyon bittiğinde OnTurnStarted'ı tetikle.
    ///
    /// Bu sayede cursor, sıradaki oyuncuya animasyon BİTTİKTEN sonra atlar
    /// (önceki davranışta animasyon başlar başlamaz atlıyordu, görsel olarak kötüydü).
    /// </summary>
    public void ConfirmMove(Vector2Int newGridPos)
    {
        if (currentState != GameState.Playing)
        {
            Debug.LogWarning("[GameManager] ConfirmMove Playing state'inde değil.");
            return;
        }

        Player movingPlayer = CurrentPlayer;

        // Index'i şimdi flip et: animasyon süresince CurrentPlayer = sıradaki oyuncu.
        // OnTurnStarted'ı ise animasyon bitince ateşleyeceğiz.
        currentPlayerIndex = 1 - currentPlayerIndex;

        // Bir-kerelik subscription: animasyon bitince OnTurnStarted yayımla.
        movingPlayer.OnMoveCompleted += OnConfirmedMoveCompleted;

        // recordPrevious: true → bu, kullanıcının kendi hamlesi. Revert hedefi
        // olarak saklanmalı (shift sırasında grid dışına düşerse buraya döner).
        movingPlayer.AnimateMove(newGridPos, gridManager.AnimationDuration, recordPrevious: true);
    }

    /// <summary>
    /// ConfirmMove'da subscribe edilen one-shot handler. Animasyon bitince
    /// turn'ün resmen başladığını duyurur ve subscription'ı temizler.
    /// </summary>
    private void OnConfirmedMoveCompleted(Player p)
    {
        // Kendini hemen unsubscribe et — bir sonraki hamle için tekrar abone olunacak.
        p.OnMoveCompleted -= OnConfirmedMoveCompleted;

        OnTurnStarted?.Invoke(currentPlayerIndex);
    }

    /// <summary>
    /// SquareGridManager bir satır/sütun kaydırınca tetiklenir.
    /// Etkilenen oyuncuların grid koordinatını günceller; yeni koordinat
    /// grid dışına düşerse (oyuncu yok olan tile'ın üstündeydi) oyuncuyu
    /// önceki pozisyonuna döndürür.
    ///
    /// Çağrılma sırası:
    ///   1. Kullanıcı üçgene basar → SquareGridManager.ShiftLine.
    ///   2. ShiftLine animasyon coroutine'i çalışır.
    ///   3. Coroutine sonunda PerformShift tile'ları mantıksal olarak kaydırır,
    ///      en sonda OnLineShifted event'ini tetikler.
    ///   4. Biz burada (event handler) oyuncuları senkronize ederiz.
    ///
    /// Önemli: tile'ların görsel animasyonu coroutine içinde olur,
    /// PerformShift tile'ı instant olarak yeni yere koyar. Oyuncuların
    /// da instant taşınması bu yüzden tile mantığıyla tutarlı.
    /// </summary>
    // ESKİ HandleLineShifted metodunu SİL, yerine bunu koy:

    /// <summary>
    /// SquareGridManager bir satır/sütun kaydırma animasyonuna BAŞLAMADAN ÖNCE tetiklenir.
    /// Etkilenen oyuncuların grid koordinatını günceller ve tile'larla paralel
    /// olarak world pozisyonunu animate eder.
    ///
    /// Önceki versiyon OnLineShifted (animasyon sonu) dinliyordu → oyuncu tile'dan
    /// sonra ışınlanıyordu. Şimdi Start event'i ile tile ile aynı anda kayıyor.
    ///
    /// Grid dışına itilen oyuncu (yok olan tile'daydı) için "revert":
    ///   Şimdilik animasyonsuz olarak önceki kendi pozisyonuna döndürüyoruz.
    ///   İlerde ölme/respawn/puan kaybı gibi mekanikleri buraya bağlayabiliriz.
    /// </summary>
    private void HandleLineShiftStarted(int lineIndex, ShiftDirection direction, int count)
    {
        bool isHorizontal = (direction == ShiftDirection.Left ||
                            direction == ShiftDirection.Right);

        Vector2Int delta = Vector2Int.zero;
        switch (direction)
        {
            case ShiftDirection.Up: delta = new Vector2Int(0, count); break;
            case ShiftDirection.Down: delta = new Vector2Int(0, -count); break;
            case ShiftDirection.Right: delta = new Vector2Int(count, 0); break;
            case ShiftDirection.Left: delta = new Vector2Int(-count, 0); break;
        }

        // Tile animasyonuyla aynı süreyi kullan → senkron hareket.
        // gridManager.AnimationDuration property'sini de bu yüzden açtık.
        float duration = gridManager.AnimationDuration;

        foreach (Player player in players)
        {
            if (player == null) continue;

            Vector2Int pos = player.GridPosition;

            bool affected = isHorizontal
                ? (pos.y == lineIndex)
                : (pos.x == lineIndex);

            if (!affected) continue;

            Vector2Int newPos = pos + delta;

            bool inBounds =
                newPos.x >= 0 && newPos.x < gridManager.Width &&
                newPos.y >= 0 && newPos.y < gridManager.Height;

            if (inBounds)
            {
                // Tile ile EŞZAMANLI animasyon. SetGridPosition (instant) yerine AnimateMove.
                // recordPrevious: false → shift pasif olay, "previous" kullanıcı hamlesi olarak kalsın.
                player.AnimateMove(newPos, duration, recordPrevious: false);
            }
            else
            {
                // Grid dışına itildi → revert. Şimdilik instant.
                Debug.Log($"[GameManager] Player {player.PlayerId} grid dışına itildi, geri dönüyor.");
                player.RevertToPreviousPosition();
            }
        }
    }

    // ========== GEÇİCİ TEST ==========
    // Input controller'ı yazana kadar setup'ı simüle etmek için.
    // Inspector → ⋮ → "TEST: Skip Setup". Test bitince silinecek.

    [ContextMenu("TEST: Skip Setup (place players at corners)")]
    private void TestSkipSetup()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[GameManager] TEST sadece Play mode'da çalışır.");
            return;
        }
        ConfirmPlayerPlacement(new Vector2Int(0, 0));
        ConfirmPlayerPlacement(new Vector2Int(5, 5));
    }
}