using UnityEngine;

/// <summary>
/// Klavye girişini dinler ve oyun state'ine göre uygun aksiyonu tetikler.
///
/// Setup phase'inde:
///   - Arrow keys → cursor'u hareket ettir.
///   - Space      → GameManager.ConfirmPlayerPlacement(cursor.GridPosition).
///
/// Playing phase'i (sonraki adımda): preview + onay mekaniği.
///
/// Event-driven: GameManager.OnGameStateChanged'e abone olup state geçişlerinde
/// cursor'u resetler. Update içinde sadece geçerli state için input okur.
/// </summary>
public class PlayerInputController : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SquareGridManager gridManager;
    [SerializeField] private GridCursor cursor;

    // Hangi oyuncunun OnMoved event'ine abone olduğumuzun referansı.
    // Turn değişince eski oyuncudan unsub, yeniye sub yapacağız.
    private Player subscribedPlayer;
    // Space basıldığında animasyon süresince ikinci hamleyi engellemek için lock.
    // OnTurnStarted geldiğinde (bir sonraki sıranın resmen başladığı an) açılır.
    private bool inputLocked = false;

    // ========== EVENT SUBSCRIPTION ==========

    private void OnEnable()
    {
        // OnEnable/OnDisable çiftinde subscribe/unsubscribe yapmak Unity'de
        // standart: object disable olunca event'i serbest bırakırız, yoksa
        // unsubscribe edilmemiş referans bellekte tutulur (memory leak).
        GameManager.OnGameStateChanged += HandleStateChanged;
        GameManager.OnTurnStarted += HandleTurnStarted;
        // SquareGridManager.OnLineShifted += HandleLineShifted;
    }

    private void OnDisable()
    {
        GameManager.OnGameStateChanged -= HandleStateChanged;
        GameManager.OnTurnStarted -= HandleTurnStarted;
        // SquareGridManager.OnLineShifted -= HandleLineShifted;

        if (subscribedPlayer != null)
        {
            subscribedPlayer.OnMoved -= HandleCurrentPlayerMoved;
            subscribedPlayer.OnMoveCompleted -= HandleCurrentPlayerMoveCompleted;
            subscribedPlayer = null;
        }

    }

    // ========== STATE GEÇİŞ TEPKİSİ ==========

    /// <summary>
    /// Yeni state'e göre cursor'u hazırla.
    /// Setup state'lerinde cursor görünür ve oyuncunun rengiyle boyalı,
    /// Playing'de şimdilik gizli (play phase implement edilince
    /// önizleme amaçlı tekrar görünür hale gelecek).
    /// </summary>
    private void HandleStateChanged(GameManager.GameState newState)
    {
        // Cursor başlangıç pozisyonu: grid'in ortası. Width/2, Height/2 →
        // tam ortalama olmasa da yeterince ortada bir nokta.
        Vector2Int center = new Vector2Int(gridManager.Width / 2, gridManager.Height / 2);

        switch (newState)
        {
            case GameManager.GameState.SettingUpP1:
                cursor.Initialize(gridManager, center);
                cursor.SetTint(WithAlpha(gameManager.Player1Color, 1f));
                cursor.SetVisible(true);
                break;

            case GameManager.GameState.SettingUpP2:
                cursor.SetGridPosition(center);
                cursor.SetTint(WithAlpha(gameManager.Player2Color, 1f));
                cursor.SetVisible(true);
                break;

            case GameManager.GameState.Playing:
                // Sonraki adımda burayı dolduracağız (preview cursor).
                //cursor.SetVisible(false);
                // Cursor'u burada açıp kapatmıyoruz: hemen ardından GameManager
                // OnTurnStarted event'ini tetikleyecek, HandleTurnStarted oradan
                // cursor'u doğru renge ve pozisyona kuracak. State geçişi ile
                // sıra başlangıcı iki ayrı sorumluluk; ikincisine bırakıyoruz.
                break;
        }
    }

    /// <summary>
    /// Sıra başladığında (Playing state'inde) cursor'u sıradaki oyuncunun
    /// rengine ve pozisyonuna ayarlar.
    ///
    /// Hem ilk Playing geçişinde (P2 yerleştikten sonra) hem de her ConfirmMove
    /// sonrası tetiklenir. Yani sıra her el değiştirdiğinde cursor güncellenir.
    /// </summary>
    private void HandleTurnStarted(int playerIndex)
    {
        // Yeni turn → input kilidini aç. (ConfirmMove'da kapatılmıştı.)
        inputLocked = false;
        // Setup phase sonu hariç bir durum gelirse (defensive) atla.
        if (gameManager.CurrentState != GameManager.GameState.Playing) return;

        Player current = gameManager.CurrentPlayer;
        if (current == null) return;

        // Sıradaki oyuncunun rengiyle cursor'u boya (alpha 0.8 ile preview hissi).
        Color color = (playerIndex == 0)
            ? gameManager.Player1Color
            : gameManager.Player2Color;
        cursor.SetTint(WithAlpha(color, 0.8f));

        // Cursor başlangıçta oyuncunun şu anki tile'ında (yani "stay" pozisyonu).
        // Arrow basılınca buradan bir yöne kayacak.
        cursor.SetGridPosition(current.GridPosition);
        cursor.SetVisible(true);
        // ----- Cursor sync: sıradaki oyuncunun OnMoved'una abone ol -----
        // Eski subscription'ı bırak ki cursor önceki oyuncunun hareketlerine tepki vermesin.
        if (subscribedPlayer != null)
        {
            subscribedPlayer.OnMoved -= HandleCurrentPlayerMoved;
            subscribedPlayer.OnMoveCompleted -= HandleCurrentPlayerMoveCompleted;
        }

        subscribedPlayer = current;
        subscribedPlayer.OnMoved += HandleCurrentPlayerMoved;
        subscribedPlayer.OnMoveCompleted += HandleCurrentPlayerMoveCompleted;
    }

    /// <summary>
    /// Sıradaki oyuncu hareket etmeye BAŞLADIĞINDA cursor'u gizler.
    /// Animasyon süresi boyunca cursor görünmeyecek; hedefe varınca
    /// HandleCurrentPlayerMoveCompleted onu tekrar gösterecek.
    ///
    /// Setup phase'inde cursor manuel kontrol ediliyor; bu handler sadece
    /// Playing state'inde devrede.
    /// </summary>
    private void HandleCurrentPlayerMoved(Player p, Vector2Int from, Vector2Int to)
    {
        if (gameManager.CurrentState != GameManager.GameState.Playing) return;
        cursor.SetVisible(false);
    }

    /// <summary>
    /// Sıradaki oyuncu hareketini TAMAMLADIĞINDA cursor'u yeni pozisyonda gösterir.
    /// İki kaynaktan gelir:
    ///   - SetGridPosition: instant, hemen aynı frame'de. Manuel hamlelerde böyle.
    ///   - AnimateMove: coroutine sonu. Shift-kaynaklı hareketlerde böyle.
    ///
    /// Yani manuel hamlelerde cursor gözle görülür şekilde "kaybolup yeniden
    /// belirmez" — aynı frame'de hide+show → flicker yok.
    /// Shift'lerde ise animasyon boyunca gizli kalır → tile'larla zıplayan
    /// cursor problemi çözüldü.
    /// </summary>
    private void HandleCurrentPlayerMoveCompleted(Player p)
    {
        if (gameManager.CurrentState != GameManager.GameState.Playing) return;

        // Önemli check: bu oyuncu HALA sıradaki mi?
        //   - Shift sonrası: evet (manuel hamle yapmadı, sıra hala onda) → cursor'u göster.
        //   - Manuel hamle sonrası: hayır (ConfirmMove zaten index'i flip etti) →
        //     cursor'u dokunmuyoruz; HandleTurnStarted (animasyon bitiminde
        //     GameManager tarafından tetiklenecek) cursor'u yeni oyuncuya kuracak.
        if (p == gameManager.CurrentPlayer)
        {
            cursor.SetGridPosition(p.GridPosition);
            cursor.SetVisible(true);
        }
    }

    /// <summary>
    /// Shift olunca cursor'u sıradaki oyuncunun yeni pozisyonuna senkronize et.
    /// Cursor sıradaki oyuncunun mevcut tile'ında durduğu için (Playing'de
    /// preview origin'i o), oyuncu shift ile taşındığında cursor'un da taşınması gerek.
    /// eskide kaldı !!!!!!!!!!!!!!!!!
    /// </summary>
    // private void HandleLineShifted(int lineIndex, ShiftDirection direction, int count)
    // {
    //     if (gameManager.CurrentState != GameManager.GameState.Playing) return;

    //     Player current = gameManager.CurrentPlayer;
    //     if (current == null) return;

    //     // Cursor'u sıradaki oyuncunun YENİ pozisyonuna oturt.
    //     // GameManager.HandleLineShifted bu event'ten ÖNCE çağrılıyor mu sonra mı?
    //     // C# event'leri abone olma sırasında çağrılır. GameManager OnEnable'da
    //     // subscribe oluyor, biz de OnEnable'da. Sahnede hangisi önce yüklenirse
    //     // o önce çağrılır. Bunu garantiye almak için GameManager'ın oyuncu
    //     // pozisyonlarını güncellemiş olduğunu varsayıyoruz — pratikte hata
    //     // yaşarsan execution order'a script execution settings'ten bakarız.
    //     cursor.SetGridPosition(current.GridPosition);
    // }

    /// <summary>Verilen rengin alpha'sını değiştirip yeni Color döner.</summary>
    private static Color WithAlpha(Color c, float alpha)
    {
        c.a = alpha;
        return c;
    }

    // ========== INPUT POLLING ==========

    private void Update()
    {
        // GameManager hazır değilse hiçbir şey yapma (Awake sırası garanti değil).
        if (gameManager == null) return;

        switch (gameManager.CurrentState)
        {
            case GameManager.GameState.SettingUpP1:
            case GameManager.GameState.SettingUpP2:
                HandleSetupInput();
                break;

            case GameManager.GameState.Playing:
                HandlePlayInput();
                break;
        }
    }

    /// <summary>
    /// Setup phase input mantığı.
    /// GetKeyDown kullanıyoruz (tuş tutulunca tek seferde tetiklensin diye).
    /// GetKey ise basılı tutuldukça her frame çalışır → cursor süzülürdü.
    /// </summary>
    private void HandleSetupInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow)) cursor.Move(0, 1);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) cursor.Move(0, -1);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) cursor.Move(-1, 0);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) cursor.Move(1, 0);
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            // Mevcut cursor pozisyonunu GameManager'a bildir.
            // GameManager hangi state'teyse uygun oyuncuyu spawn edip
            // bir sonraki state'e geçer.
            gameManager.ConfirmPlayerPlacement(cursor.GridPosition);
        }
    }

    /// <summary>
    /// Playing phase input mantığı.
    ///
    /// Arrow keys: cursor'u current_player_position + (dx, dy) noktasına götürür.
    ///   - dx, dy değerleri tek bir basışta sadece -1, 0 veya 1 olabilir
    ///     (çünkü her frame tek tuş okuyoruz, else-if zinciri).
    ///   - cursor.SetGridPosition içeride Clamp yaptığı için sınır dışı
    ///     hedefler en yakın kenara oturur (kenardaki oyuncu için
    ///     "o yöne gidemiyorum" görsel olarak hissedilir).
    ///
    /// Space: cursor'un mevcut pozisyonunu hamle olarak onaylar.
    ///   - Cursor oyuncunun mevcut tile'ında ise "stay" (sırasını geçer ama
    ///     yerinden oynamaz). İlerde stay'i yasaklamak istersen burada
    ///     ekstra check ekleriz.
    /// </summary>
    private void HandlePlayInput()
    {
        // Kilit açıkken hiçbir input alma (önceki hamlenin animasyonu bitsin).
        if (inputLocked) return;

        Player current = gameManager.CurrentPlayer;
        if (current == null) return;

        if (Input.GetKeyDown(KeyCode.UpArrow)) cursor.SetGridPosition(current.GridPosition + Vector2Int.up);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) cursor.SetGridPosition(current.GridPosition + Vector2Int.down);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) cursor.SetGridPosition(current.GridPosition + Vector2Int.left);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) cursor.SetGridPosition(current.GridPosition + Vector2Int.right);
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            // Animasyon başlayacak → input'u kilitle. Bir sonraki HandleTurnStarted
            // (animasyon bitiminde GameManager'dan gelecek) açacak.
            inputLocked = true;
            gameManager.ConfirmMove(cursor.GridPosition);
        }
    }

}