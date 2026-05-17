using System;
using UnityEngine;
using System.Collections;

/// <summary>
/// Bir oyuncuyu temsil eder. Sadece "ben şu grid pozisyonundayım" bilgisini
/// taşır ve transform'unu ona göre günceller. Oyun mantığı (hamle kuralları,
/// sıra) GameManager'da; Player kendi içinde mümkün olduğunca aptal kalsın
/// (single responsibility).
/// </summary>
public class Player : MonoBehaviour
{
    // ========== TANIM ==========

    [Header("Oyuncu Bilgisi")]
    [Tooltip("Oyuncu ID'si (1 veya 2). UI ve sıra mantığında kullanılır.")]
    [SerializeField] private int playerId = 1;

    [Tooltip("Bu oyuncunun rengi. SpriteRenderer.color'a uygulanır, " +
             "iki oyuncuyu görsel olarak ayırmak için.")]
    [SerializeField] private Color playerColor = Color.red;

    // ========== POZISYON ==========

    // Vector2Int → int x ve y'yi tek struct'ta toplar; cast/round hatası yok.
    private Vector2Int gridPosition;
    private Vector2Int previousGridPosition;
    // Aktif move coroutine referansı. Yeni hareket gelirse eskisini iptal etmek için.
    // (Üst üste binme olursa transform'un pozisyonu sıçrar.)
    private Coroutine activeMoveCoroutine;
    // Grid manager referansı: world pozisyonu hesaplamak için.
    // Dependency injection: dışarıdan veriyoruz, içeride FindObjectOfType yok.
    private SquareGridManager gridManager;

    // ========== PUBLIC ERIŞIM ==========

    public int PlayerId => playerId;
    public Vector2Int GridPosition => gridPosition;
    public Vector2Int PreviousGridPosition => previousGridPosition;

    // ========== EVENT ==========

    /// <summary>
    /// Bu oyuncu hareket ettiğinde tetiklenir. (player, from, to)
    /// Instance event çünkü her oyuncunun hareketi ayrı dinlenebilsin.
    /// </summary>
    public event Action<Player, Vector2Int, Vector2Int> OnMoved;

    /// <summary>
    /// Hareket TAMAMLANDIĞINDA tetiklenir (animasyon dahil).
    ///   - SetGridPosition (instant) için: OnMoved'dan hemen sonra, aynı frame.
    ///   - AnimateMove (animasyonlu) için: coroutine bitince, hedefe ulaşıldığında.
    ///
    /// OnMoved + OnMoveCompleted çifti birlikte "movement lifecycle" oluşturuyor:
    /// dinleyiciler "başladı / bitti" tepkilerini ayrı verebilsin diye.
    /// (Örn: cursor → başladı=gizle, bitti=göster.)
    /// </summary>
    public event Action<Player> OnMoveCompleted;

    // ========== INITIALIZATION ==========

    /// <summary>
    /// GameManager spawn ettikten sonra çağırır.
    /// Constructor yerine bunu kullanma sebebi: Unity'de MonoBehaviour'lara
    /// Instantiate sırasında parametre geçemiyoruz, bu standart pattern.
    /// </summary>
    public void Initialize(SquareGridManager manager, int id, Color color, Sprite sprite)
    {
        gridManager = manager;
        playerId = id;
        playerColor = color;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // Sprite atanmışsa kullan; null geçilirse prefab'taki default kalır.
            // Bu sayede henüz sprite hazırlanmamış oyuncular için sistem
            // kırılmadan default daire ile çalışmaya devam eder.
            if (sprite != null)
                sr.sprite = sprite;
        }
    }

    // ========== POZISYON GÜNCELLEME ==========

    /// <summary>
    /// Grid pozisyonunu set eder ve transform.position'ı otomatik günceller.
    ///
    /// recordPrevious=false ise previousGridPosition güncellenmez.
    /// Bu, shift'ten kaynaklı pasif hareketler için lazım: shift kullanıcı
    /// kararı olmadığı için "geri dönüş hedefi" olamaz, eski "kendi hamlemiz"
    /// koordinatını koruması gerek.
    /// </summary>
    public void SetGridPosition(int x, int y, bool recordPrevious = true)
    {
        if (gridManager == null)
        {
            Debug.LogError("[Player] gridManager null. Initialize çağrıldı mı?", this);
            return;
        }

        if (recordPrevious)
            previousGridPosition = gridPosition;

        Vector2Int oldPos = gridPosition;
        gridPosition = new Vector2Int(x, y);

        // World pozisyonu. Z'yi negatif yap ki oyuncu tile'ın ÜSTÜNDE gözüksün
        // (2D ortographic'te küçük Z = kameraya yakın).
        Vector3 worldPos = gridManager.GetWorldPosition(x, y);
        worldPos.z = -1f;
        transform.position = worldPos;

        // Pozisyon gerçekten değiştiyse event'i tetikle.
        if (oldPos != gridPosition)
        {
            OnMoved?.Invoke(this, oldPos, gridPosition);
            // Instant hareket → "tamamlandı" da aynı frame'de tetiklenir.
            // Cursor gibi dinleyiciler hide → show'u back-to-back yapar,
            // aynı frame içinde olduğu için ekranda flicker olmaz.
            OnMoveCompleted?.Invoke(this);
        }
    }

    /// <summary>
    /// Grid pozisyonunu HEMEN günceller ama world pozisyonunu verilen süre
    /// boyunca yumuşatarak kaydırır. Tile'ların Lerp animasyonuyla aynı pattern.
    ///
    /// recordPrevious: false → shift-kaynaklı pasif hareketlerde previousGridPosition'ı koruruz
    /// (revert mekaniği için "kullanıcının kendi son hamlesi" tek nokta olarak saklı kalsın).
    /// </summary>
    public void AnimateMove(Vector2Int newPos, float duration, bool recordPrevious = false)
    {
        if (gridManager == null)
        {
            Debug.LogError("[Player] gridManager null. Initialize çağrıldı mı?", this);
            return;
        }

        if (recordPrevious)
            previousGridPosition = gridPosition;

        // Grid koordinatını mantıksal olarak HEMEN güncelle.
        // Bu sayede shift sırasında başka sistemler (UI, oyun mantığı) doğru
        // koordinatı okur, "henüz bitmedi mi" diye beklemez.
        Vector2Int oldPos = gridPosition;
        gridPosition = newPos;

        // Animasyon target'ı: yeni grid koordinatının dünya pozisyonu.
        // Z'yi koru ki render sırası bozulmasın (oyuncu hep tile'ın üstünde).
        Vector3 startWorld = transform.position;
        Vector3 endWorld = gridManager.GetWorldPosition(newPos.x, newPos.y);
        endWorld.z = startWorld.z;

        // Önceki animasyon hala çalışıyorsa iptal et.
        if (activeMoveCoroutine != null)
            StopCoroutine(activeMoveCoroutine);

        activeMoveCoroutine = StartCoroutine(MoveCoroutine(startWorld, endWorld, duration));

        if (oldPos != newPos)
            OnMoved?.Invoke(this, oldPos, newPos);
    }

    /// <summary>
    /// World pozisyonunu duration süresince Lerp ile başlangıçtan hedefe taşır.
    /// Tile shift'teki coroutine'in birebir aynı mantığı.
    /// </summary>
    private IEnumerator MoveCoroutine(Vector3 start, Vector3 end, float duration)
    {
        // Edge case: duration 0 veya negatifse direkt hedefe ışınla.
        // (Edit mode'da veya animasyon kapalıysa kullanılabilir.)
        if (duration <= 0f)
        {
            transform.position = end;
            activeMoveCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t); // ease in-out

            transform.position = Vector3.LerpUnclamped(start, end, smoothT);
            yield return null;
        }

        // Floating-point hatasını sıfırlamak için son frame'de tam hedefe oturt.
        transform.position = end;
        activeMoveCoroutine = null;

        // Animasyon gerçekten bitti → dinleyicilere haber ver.
        // SetGridPosition'da bunu OnMoved'la birlikte tetikliyoruz; burada
        // ise coroutine bittikten SONRA, yani transform.position hedefe oturduktan sonra.
        OnMoveCompleted?.Invoke(this);
    }

    public void SetGridPosition(Vector2Int pos, bool recordPrevious = true)
    {
        SetGridPosition(pos.x, pos.y, recordPrevious);
    }

    /// <summary>
    /// Önceki kendi pozisyonuna dön. Yok olan tile edge case'i için.
    /// </summary>
    public void RevertToPreviousPosition()
    {
        SetGridPosition(previousGridPosition, recordPrevious: false);
    }
}