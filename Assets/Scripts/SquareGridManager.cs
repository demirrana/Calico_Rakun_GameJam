using UnityEngine;
using System; // C# Action event'leri için (System namespace'inden geliyor)
using System.Collections.Generic; // List<> kullanacağız
using Random = UnityEngine.Random; // Random.ColorHSV için, kullanmıyorum artık
using System.Collections; // IEnumerator için

/// <summary>
/// Bir satır/sütun kaydırma yönünü belirten enum.
/// Left/Right → satır (yatay) kaydırma.
/// Up/Down    → sütun (dikey) kaydırma.
///
/// Class'ın DIŞINDA tanımladık: başka script'ler de "ShiftDirection.Up"
/// diye doğrudan kullanabilsin diye. (İçeride tanımlasak
/// "SquareGridManager.ShiftDirection.Up" yazmak gerekirdi — uzun ve gereksiz.)
/// </summary>
public enum ShiftDirection { Up, Down, Left, Right }
/// <summary>
/// Butonun grid kenarındaki konumu.
/// Shift yönüyle aynı şey değil: bir butonun side'ı, shift yönünün TERSİDİR.
/// (Üstteki buton AŞAĞI kaydırır → Side=Top, Direction=Down.)
/// Mouse-side görünürlük mantığı için ayrı bir kavram olarak tutuyoruz.
/// Şu anlık kullanımda değil !!!!!!
/// </summary>
public enum EdgeSide { Top, Bottom, Left, Right }

/// <summary>
/// Sahnedeki tile prefab'ını kullanarak istenen boyutlarda (width x height)
/// bir 2D grid oluşturur. Boyutlar Inspector'dan değiştirilebilir.
/// İlerde tile'lara event üzerinden mantık ekleyeceğiz (tıklama, seçim vs.).
/// </summary>
public class SquareGridManager : MonoBehaviour
{
    // ========== INSPECTOR'DAN AYARLANAN ALANLAR ==========
    // [SerializeField] private kullanıyoruz çünkü:
    //   - private: dışarıdan direkt yazılmasın (encapsulation)

    [Header("Animasyon Ayarları")]
    [Tooltip("Tile animasyon süresi (saniye). Animator'daki clip uzunluğuyla " +
         "aynı olmalı — yoksa shift mantığı animasyondan önce/sonra yanlış " +
         "zamanda tetiklenir.")]
    [SerializeField] private float animationDuration = 0.3f;

    [Header("Grid Boyutları")]
    [Tooltip("Grid'in yatay (X) yöndeki tile sayısı. Örn: 6x6 için 6, 8x3 için 8.")]
    [SerializeField] private int width = 6;

    [Tooltip("Grid'in dikey (Y) yöndeki tile sayısı. Örn: 6x6 için 6, 8x3 için 3.")]
    [SerializeField] private int height = 6;

    [Header("Referanslar")]
    [Tooltip("Sahneye instantiate edilecek tile prefab'ı. SpriteRenderer'a sahip olmalı.")]
    [SerializeField] private GameObject tilePrefab;

    [Tooltip("Kenarlara spawn edilecek üçgen buton prefab'ı. " +
         "ShiftButton script'i ve 2D Collider içermeli.")]
    [SerializeField] private GameObject shiftButtonPrefab;

    [Header("Üçgen Buton Ayarları")]
    [Tooltip("Üçgen butonların grid kenarındaki tile merkezinden " +
             "ne kadar uzakta duracağı (Unity birimi).")]
    [SerializeField] private float buttonDistance = 0.85f;

    [Header("Yerleşim Ayarları")]
    [Tooltip("Tile'lar arası mesafe (Unity birimi). " +
             "Tile sprite'ı 1x1 birimse, 1 yazınca bitişik olurlar.")]
    [SerializeField] private float tileSpacing = 1f;

    [Tooltip("İşaretliyse grid (0,0) dünya noktasına ortalanır. " +
             "Değilse sol-alt köşesi (0,0)'da olur.")]
    [SerializeField] private bool centerGrid = true;

    // ========== RUNTIME'DA TUTULAN VERİLER ==========

    // Dışarıdan grid sınırlarına erişim (read-only).
    // Width ve height alanları private, ama dışarıdan okunabilmeleri gerek
    // (cursor sınır kontrolü, vs.). Setter koymadık ki kimse runtime'da
    // değiştirip tutarsızlık yaratmasın.
    public int Width => width;
    public int Height => height;

    // GameManager bu süreyle eşleşen player animasyonu başlatabilsin diye dışa açıyoruz.
    // Tek noktadan kontrol: Inspector'da değiştirdiğinde her ikisi senkron kalır.
    public float AnimationDuration => animationDuration;

    // Oluşturulan tile'ları (x,y) indeksine göre saklıyoruz.
    // 2D dizi seçtim çünkü: koordinat üzerinden erişim O(1), kod okuması net.
    // İlerde tile'a "soldaki komşusu kim?" gibi sorular sorduğumuzda lazım olacak.
    private GameObject[,] tiles;

    // Spawn edilen üçgen butonları liste olarak tutuyoruz.
    // 2D dizi gerekmiyor çünkü "buton at (x,y)" diye sorgu yapmıyoruz,
    // sadece toplu olarak temizlememiz lazım. List bu iş için ideal.
    // ShiftButton script referansını tutalım, Side bilgisine doğrudan erişelim.
    private List<ShiftButton> shiftButtons = new List<ShiftButton>();

    // Aynı anda iki shift çakışmasın diye lock.
    // Animasyon sürerken yeni tıklamalar yok sayılır (yoksa coroutine'ler
    // üst üste binip race condition + bozuk grid'e sebep olur).
    private bool isAnimating = false;

    // ========== EVENT'LER ==========

    /// <summary>
    /// Grid başarıyla oluşturulduğunda tetiklenir.
    /// Parametreler: (width, height, tiles 2D dizisi).
    /// Olası dinleyiciler: kamera otomatik fit, oyun mantığı başlatma, UI güncelleme.
    /// 
    /// static seçme sebebim: GridManager'a referans tutmadan da subscribe olunabilsin.
    /// Sahne içinde tek bir GridManager olacağı varsayımıyla bu güvenli.
    /// (İlerde birden fazla grid olursa instance event'e geçeriz.)
    /// </summary>
    public static event Action<int, int, GameObject[,]> OnGridGenerated;

    /// <summary>
    /// Bir satır veya sütun başarıyla kaydırıldığında tetiklenir.
    /// Parametreler:
    ///   - int            : kaydırılan satır/sütunun indeksi
    ///   - ShiftDirection : kaydırma yönü
    ///   - int            : kaydırma miktarı (count)
    ///
    /// Tipik dinleyiciler: ses efektleri (SFX), animasyon tetikleyici,
    /// skor/turn manager, vs. Event üzerinden bağlanırlarsa
    /// GridManager'a referans tutmak zorunda kalmazlar — decoupling.
    /// </summary>
    public static event Action<int, ShiftDirection, int> OnLineShifted;

    /// <summary>
    /// Shift animasyonu BAŞLAMADAN HEMEN ÖNCE tetiklenir.
    /// OnLineShifted ise PerformShift tamamlandıktan SONRA tetiklenir.
    ///
    /// Aradaki fark kritik: animasyon süresince paralel iş yapmak isteyen sistemler
    /// (örn. oyuncuları tile'larla beraber kaydırmak) bu event'i dinler.
    /// OnLineShifted'a abone olsalardı tile animasyonu bittikten sonra başlatırlardı,
    /// "geç kalmış" görünürdü.
    /// </summary>
    public static event Action<int, ShiftDirection, int> OnLineShiftStarted;

    // ========== UNITY LIFECYCLE ==========

    private void Start()
    {
        // Sahne yüklenir yüklenmez grid'i oluştur.
        // İlerde "Play" butonuna bağlamak istersek bu çağrıyı oradan yaparız.
        GenerateGrid();
    }

    // ========== ANA METOTLAR ==========

    /// <summary>
    /// Belirlenen width ve height değerlerine göre grid'i oluşturur.
    /// public olmasının sebebi: dışarıdan (UI butonu, başka manager) tekrar
    /// çağırıp farklı boyutlarda yeniden kurabilelim.
    /// </summary>
    [ContextMenu("Generate Grid")]
    public void GenerateGrid()
    {
        // ----- 1) Güvenlik kontrolleri -----
        // Prefab atanmamışsa null reference hatası almak yerine
        // anlaşılır bir log basıp çıkıyoruz.
        if (tilePrefab == null)
        {
            Debug.LogError("[GridManager] tilePrefab atanmamış! Inspector'dan ata.", this);
            return;
        }

        // Negatif veya 0 boyut girilirse anlamsız; uyar ve çık.
        if (width <= 0 || height <= 0)
        {
            Debug.LogError($"[GridManager] Geçersiz boyut: {width}x{height}. " +
                           "İkisi de pozitif olmalı.", this);
            return;
        }

        // ----- 2) Önceki grid varsa temizle -----
        // GenerateGrid tekrar çağrılırsa eski tile'lar sahnede kalmasın.
        ClearGrid();

        // ----- 3) Tile dizisini yeni boyutlara göre oluştur -----
        tiles = new GameObject[width, height];

        // ----- 4) Ortalama offset'i hesapla -----
        // centerGrid true ise grid (0,0) dünya noktasına ortalanır.
        // (width - 1) kullanma sebebi: tile pozisyonları merkezinden alınır,
        // yani 6 tile varsa 0..5 arası index, toplam genişlik 5 birim
        // (6 değil), o yüzden yarısı 2.5 birim sola kaydırma yapıyoruz.
        Vector2 offset = Vector2.zero;
        if (centerGrid)
        {
            offset.x = -(width - 1) * tileSpacing * 0.5f;
            offset.y = -(height - 1) * tileSpacing * 0.5f;
        }

        // ----- 5) Çift döngü ile her hücreye bir tile yerleştir -----
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Tile'ın dünya pozisyonunu hesapla.
                // (x * spacing) = bu tile'ın x yönündeki konumu
                // + offset.x       = ortalama düzeltmesi
                Vector3 spawnPosition = new Vector3(
                    x * tileSpacing + offset.x,
                    y * tileSpacing + offset.y,
                    0f // 2D olduğu için Z hep 0
                );

                // Prefab'tan kopya oluştur.
                // 4. parametre 'transform' → GridManager objesini parent yapıyoruz.
                // Bu sayede Hierarchy temiz, ayrıca grid'i topluca taşıyabiliriz.
                GameObject newTile = Instantiate(
                    tilePrefab,
                    spawnPosition,
                    Quaternion.identity, // Rotasyon yok (sıfır)
                    transform            // Parent = GridManager
                );

                // Hierarchy'de ayırt etmek için anlamlı isim ver. Debug için altın değerinde.
                newTile.name = $"Tile_{x}_{y}";

                // --- GEÇİCİ DEBUG: her tile'a rastgele renk ver ki kaydırma görsel olarak görünsün ---
                // Test bitince bu bloğu silebiliriz; gerçek oyunda kartların kendi sprite'ları olacak zaten.
                SpriteRenderer sr = newTile.GetComponent<SpriteRenderer>();
                // if (sr != null)
                // {
                //     // Random.ColorHSV: hue/saturation/value aralıklarından rastgele renk üretir.
                //     // Saturation 0.5-1 → cansız renkler olmasın; Value 0.7-1 → çok karanlık olmasın.
                //     sr.color = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.7f, 1f);
                // }

                // Diziye kaydet ki sonradan (x,y) ile erişebilelim.
                tiles[x, y] = newTile;
            }
        }


        // ----- 6) Event'i tetikle -----
        // ?.Invoke null-conditional operatör: dinleyici yoksa NullReferenceException
        // fırlatmaz, sessizce geçer. Modern C# eventleri için standart kullanım.
        // ----- 6.5) Üçgen butonları spawn et -----
        SpawnShiftButtons();
        OnGridGenerated?.Invoke(width, height, tiles);

        Debug.Log($"[GridManager] {width}x{height} grid oluşturuldu " +
                  $"(toplam {width * height} tile).");
    }

    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        // ============================================================
        // GridManager'ın altındaki TÜM child'ları (tile + buton) sil.
        // ============================================================
        // Neden tracking dizilerine (tiles, shiftButtons) güvenmiyoruz:
        //   - Bunlar runtime field; sahne yeniden açılınca veya script
        //     recompile olunca null'a/boşa dönüyorlar.
        //   - Ama sahnedeki gerçek GameObject'ler kalıyor.
        //   - Bu durumda eski foreach'lı yapı "silecek bir şey yok" sanıp
        //     erken çıkıyordu → temizleme çalışmıyor gibi görünüyordu.
        //
        // transform.childCount runtime'da hep güncel, en güvenilir kaynak.

        // Sondan başa iterasyon: GetChild indeksleri silme sırasında
        // kaydığı için tersten gitmek standart Unity pattern'i.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;

            // Hem edit hem play mode'da çalışsın: DestroyImmediate her ikisinde de OK.
            // (Normalde runtime'da Destroy önerilir, ama bu metot ContextMenu için
            // çağrıldığında genelde edit mode'da oluyor; tutarlılık için tek API.)
            DestroyImmediate(child);
        }

        // Tracking koleksiyonlarını da temizle ki ileride yanlış referans
        // kalmasın (özellikle Play mode'da ShiftLine çağrılırsa).
        tiles = null;
        foreach (ShiftButton btn in shiftButtons)
        {
            if (btn != null) DestroyImmediate(btn.gameObject);
        }
        shiftButtons.Clear();
    }

    // ========== DIŞARIDAN ERİŞİM ==========

    /// <summary>
    /// Verilen (x,y) koordinatındaki tile'ı döner.
    /// Sınır dışındaysa veya grid yoksa null döner.
    /// Başka script'lerin tile'a koordinatla erişebilmesi için public.
    /// </summary>
    public GameObject GetTile(int x, int y)
    {
        if (tiles == null) return null;
        if (x < 0 || x >= width || y < 0 || y >= height) return null;
        return tiles[x, y];
    }

    /// <summary>
    /// Grid'in 4 kenarına üçgen kaydırma butonlarını spawn eder.
    ///   Top    edge: her sütun için, tepe AŞAĞI bakar → Down shift.
    ///   Bottom edge: her sütun için, tepe YUKARI bakar → Up shift.
    ///   Right  edge: her satır için, tepe SOLA bakar → Left shift.
    ///   Left   edge: her satır için, tepe SAĞA bakar → Right shift.
    /// </summary>
    private void SpawnShiftButtons()
    {
        if (shiftButtonPrefab == null)
        {
            Debug.LogWarning("[SquareGridManager] shiftButtonPrefab atanmamış, " +
                             "butonlar spawn edilmedi.", this);
            return;
        }

        // ----- TOP kenar (her sütun için) → Down shift -----
        for (int x = 0; x < width; x++)
        {
            SpawnButton(
                gridX: x, gridY: height - 1,
                offset: new Vector3(0f, buttonDistance, 0f),
                zRotation: 180f, // varsayılan tip yukarı → tip aşağı
                lineIndex: x,
                direction: ShiftDirection.Down,
                edgeSide: EdgeSide.Top,
                name: $"ShiftBtn_Top_Col{x}"
            );
        }

        // ----- BOTTOM kenar (her sütun için) → Up shift -----
        for (int x = 0; x < width; x++)
        {
            SpawnButton(
                gridX: x, gridY: 0,
                offset: new Vector3(0f, -buttonDistance, 0f),
                zRotation: 0f, // tip yukarı (varsayılan)
                lineIndex: x,
                direction: ShiftDirection.Up,
                edgeSide: EdgeSide.Bottom,
                name: $"ShiftBtn_Bottom_Col{x}"
            );
        }

        // ----- RIGHT kenar (her satır için) → Left shift -----
        for (int y = 0; y < height; y++)
        {
            SpawnButton(
                gridX: width - 1, gridY: y,
                offset: new Vector3(buttonDistance, 0f, 0f),
                zRotation: 90f, // tip sola
                lineIndex: y,
                direction: ShiftDirection.Left,
                edgeSide: EdgeSide.Right,
                name: $"ShiftBtn_Right_Row{y}"
            );
        }

        // ----- LEFT kenar (her satır için) → Right shift -----
        for (int y = 0; y < height; y++)
        {
            SpawnButton(
                gridX: 0, gridY: y,
                offset: new Vector3(-buttonDistance, 0f, 0f),
                zRotation: -90f, // tip sağa
                lineIndex: y,
                direction: ShiftDirection.Right,
                edgeSide: EdgeSide.Left,
                name: $"ShiftBtn_Left_Row{y}"
            );
        }
    }

    /// <summary>
    /// Tek bir buton spawn etmenin ortak işlerini toparlayan yardımcı.
    /// 4 kenar için aynı kod 4 kez yerine tek yerden.
    /// </summary>
    private void SpawnButton(int gridX, int gridY, Vector3 offset, float zRotation,
                             int lineIndex, ShiftDirection direction,
                             EdgeSide edgeSide, string name)
    {
        Vector3 anchorTilePos = GetWorldPosition(gridX, gridY);
        Vector3 buttonPos = anchorTilePos + offset;
        Quaternion rotation = Quaternion.Euler(0f, 0f, zRotation);

        GameObject btnObj = Instantiate(shiftButtonPrefab, buttonPos, rotation, transform);
        btnObj.name = name;

        ShiftButton script = btnObj.GetComponent<ShiftButton>();
        if (script == null)
        {
            Debug.LogError("[SquareGridManager] shiftButtonPrefab'da ShiftButton script'i yok!", this);
            return;
        }

        script.Initialize(this, lineIndex, direction, edgeSide);
        shiftButtons.Add(script);
    }

    /// <summary>
    /// Verilen (x,y) grid koordinatını dünya (world) pozisyonuna çevirir.
    /// GenerateGrid'deki pozisyon hesaplama mantığının aynısı; tekrar yazmamak
    /// için ayrı bir helper'a ayıklandı. ShiftLine bunu kullanacak.
    ///
    /// (İstersen GenerateGrid'i de bu helper'ı kullanacak şekilde refactor
    /// edebiliriz — şimdilik mevcut kodu bozmadım, sen söyleyince yaparız.)
    /// </summary>
    public Vector3 GetWorldPosition(int x, int y)
    {
        // Ortalama offset'i, GenerateGrid ile birebir aynı formülle hesapla.
        Vector2 offset = Vector2.zero;
        if (centerGrid)
        {
            offset.x = -(width - 1) * tileSpacing * 0.5f;
            offset.y = -(height - 1) * tileSpacing * 0.5f;
        }

        // 2D olduğu için Z hep 0.
        return new Vector3(x * tileSpacing + offset.x,
                        y * tileSpacing + offset.y,
                        0f);
    }

    /// <summary>
    /// Dışarıdan çağrılan ana shift fonksiyonu. ShiftButton bunu çağırıyor.
    ///
    /// Davranış:
    ///   - Play mode'da animasyonlu çalışır (coroutine üzerinden).
    ///   - Edit mode'da anında çalışır (coroutine'ler edit mode'da çalışmaz,
    ///     ContextMenu'den manuel test ederken animasyon olmadan ilerler).
    ///   - Bir animasyon sürerken yeni çağrılar yok sayılır (lock).
    /// </summary>
    public void ShiftLine(int lineIndex, ShiftDirection direction, int count)
    {
        // Edit mode kontrolü: StartCoroutine sadece Application.isPlaying iken
        // güvenle çalışır. Edit mode'da çağrılırsa hata fırlatır.
        if (!Application.isPlaying)
        {
            PerformShift(lineIndex, direction, count);
            return;
        }

        // Lock kontrolü: animasyon sürerken gelen tıklamaları yut.
        // İstersen ileride "queue" mantığı (sıraya al) ekleriz; şimdilik en basiti.
        if (isAnimating)
            return;

        StartCoroutine(ShiftLineCoroutine(lineIndex, direction, count));
    }

    /// <summary>
    /// Animasyonlu shift coroutine'i. Animator yerine Lerp ile her tile'ı
    /// kendi mevcut pozisyonundan hedef pozisyonuna yumuşatarak taşır.
    ///
    /// Mantık:
    ///   1. Çizgideki tile'ları topla, her birinin start ve end pozisyonunu hesapla.
    ///   2. animationDuration boyunca her frame Lerp ile pozisyonu güncelle.
    ///   3. Animasyon bittiğinde PerformShift'i çağır → mantıksal kaydırma
    ///      + dışarı çıkanı destroy + boş slota yeni tile spawn.
    ///
    /// Neden Animator değil:
    ///   - Animator clip'i mutlak pozisyon kaydeder; başlangıç pozisyonu
    ///     farklı olan tile'larda tile başlangıç noktasına ışınlanır.
    ///   - Code-based animasyon her tile'ın gerçek konumundan başlar.
    /// </summary>
    private IEnumerator ShiftLineCoroutine(int lineIndex, ShiftDirection direction, int count)
    {
        isAnimating = true;

        // ---- 1) Yön ve çizgi mantığı (PerformShift ile aynı) ----
        bool isHorizontal = (direction == ShiftDirection.Left ||
                            direction == ShiftDirection.Right);
        int lineLength = isHorizontal ? width : height;

        // Hareket vektörü: count * tileSpacing kadar yön ekseninde.
        // Down → -Y, Up → +Y, Left → -X, Right → +X
        Vector3 moveVector = Vector3.zero;
        switch (direction)
        {
            case ShiftDirection.Down: moveVector = new Vector3(0f, -count * tileSpacing, 0f); break;
            case ShiftDirection.Up: moveVector = new Vector3(0f, count * tileSpacing, 0f); break;
            case ShiftDirection.Left: moveVector = new Vector3(-count * tileSpacing, 0f, 0f); break;
            case ShiftDirection.Right: moveVector = new Vector3(count * tileSpacing, 0f, 0f); break;
        }

        // ---- 2) Çizgideki tile'ları, start ve end pozisyonlarıyla topla ----
        // Diziyi şimdi snapshot'lıyoruz çünkü PerformShift sonra tiles[]'ı
        // değiştirecek; o sırada eski referanslara erişebilelim diye.
        Transform[] lineTiles = new Transform[lineLength];
        Vector3[] startPositions = new Vector3[lineLength];
        Vector3[] endPositions = new Vector3[lineLength];

        for (int i = 0; i < lineLength; i++)
        {
            GameObject tile = isHorizontal ? tiles[i, lineIndex] : tiles[lineIndex, i];
            if (tile == null) continue;

            lineTiles[i] = tile.transform;
            startPositions[i] = tile.transform.position;
            endPositions[i] = startPositions[i] + moveVector;
        }

        // ---- 2.5) Listeners'a haber ver: animasyon başlıyor ----
        // GameManager bunu yakalayıp etkilenen oyuncuları paralel animate edecek.
        OnLineShiftStarted?.Invoke(lineIndex, direction, count);
        // ---- 3) animationDuration boyunca Lerp ile taşı ----
        // elapsed = geçen süre; t = 0..1 arası normalize edilmiş ilerleme.
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            // t'yi 1'i geçmesin diye Clamp01: son frame'de tam endPosition'a otursun.
            float t = Mathf.Clamp01(elapsed / animationDuration);

            // SmoothStep daha "easy-in-out" hissi verir (lineer yerine).
            // Daha keskin hareket istersen direkt t kullan.
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < lineTiles.Length; i++)
            {
                if (lineTiles[i] == null) continue;
                lineTiles[i].position = Vector3.LerpUnclamped(
                    startPositions[i],
                    endPositions[i],
                    smoothT
                );
            }

            // Bir sonraki frame'i bekle. yield return null = "next frame".
            yield return null;
        }

        // ---- 4) Mantıksal shift ----
        // PerformShift transform.position'ları logical pozisyonlara sabitleyecek,
        // dışarı çıkan tile'ı destroy edecek, boş slot için yeni tile spawn edecek.
        PerformShift(lineIndex, direction, count);

        isAnimating = false;
    }
    /// <summary>
    /// Bir satır veya sütunu belirtilen yönde, belirtilen sayıda kaydırır.
    /// Sınır dışına çıkan tile'lar yok edilir, boşalan hücrelere yeni tile spawn edilir.
    ///
    /// PARAMETRELER:
    ///   lineIndex : 
    ///       direction Left/Right ise → SATIR indeksi (y koordinatı, 0..height-1).
    ///       direction Up/Down    ise → SÜTUN indeksi (x koordinatı, 0..width-1).
    ///   direction : kaydırma yönü (enum).
    ///   count     : kaç hücre kaydırılacak (pozitif tam sayı).
    ///
    /// ÖRNEK:
    ///   ShiftLine(0, ShiftDirection.Right, 2)
    ///   → y=0 satırını sağa 2 birim kaydırır. Sağdaki 2 tile yok olur,
    ///     soldan 2 yeni tile gelir.
    /// </summary>
    private void PerformShift(int lineIndex, ShiftDirection direction, int count)
    {
        // ============================================================
        // 1) GÜVENLİK KONTROLLERİ
        // ============================================================

        // Grid henüz oluşturulmamışsa işlem yapma.
        if (tiles == null)
        {
            Debug.LogWarning("[SquareGridManager] Grid yok, ShiftLine atlandı.");
            return;
        }

        // 0 veya negatif count → mantıksız, çık.
        if (count <= 0)
        {
            Debug.LogWarning($"[SquareGridManager] Geçersiz count: {count}. Pozitif olmalı.");
            return;
        }

        // Yön yatay mı dikey mi? Tüm işlemler bu flag'e göre dallanacak.
        bool isHorizontal = (direction == ShiftDirection.Left ||
                            direction == ShiftDirection.Right);

        // Çalıştığımız çizginin uzunluğu (içerdiği tile sayısı):
        //   Yatay satır → width
        //   Dikey sütun → height
        int lineLength = isHorizontal ? width : height;

        // lineIndex'in geçerli üst sınırı:
        //   Yatay satır kaydırıyorsak lineIndex bir y değeri → max = height
        //   Dikey sütun kaydırıyorsak lineIndex bir x değeri → max = width
        int lineIndexMax = isHorizontal ? height : width;

        if (lineIndex < 0 || lineIndex >= lineIndexMax)
        {
            Debug.LogError($"[SquareGridManager] Geçersiz lineIndex: {lineIndex}. " +
                        $"0..{lineIndexMax - 1} aralığında olmalı.");
            return;
        }

        // ============================================================
        // 2) KAYDIRMA MİKTARINI İMZALI (+/-) DEĞERE ÇEVİR
        // ============================================================
        //
        // "Line içindeki pozisyon indeksi" (i) konseptini kullanıyoruz:
        //   - Yatay satırda  i = x koordinatı (y sabit)
        //   - Dikey sütunda  i = y koordinatı (x sabit)
        //
        // Yön → i'nin nasıl değiştiği:
        //   Right : x artar → i +count
        //   Left  : x azalır → i -count
        //   Up    : y artar → i +count
        //   Down  : y azalır → i -count

        int signedShift = (direction == ShiftDirection.Right ||
                        direction == ShiftDirection.Up)
            ? count
            : -count;

        // count > lineLength ise zaten tüm tile'lar dışarı çıkar; mantıklı bir tavan koy.
        // (Hata fırlatmak yerine pratik yaklaşım: tüm satırı yenile.)
        if (Mathf.Abs(signedShift) > lineLength)
            signedShift = (signedShift > 0) ? lineLength : -lineLength;

        // ============================================================
        // 3) MEVCUT ÇİZGİDEKİ TILE'LARI GEÇİCİ DİZİYE AL
        // ============================================================
        // 2D diziden ilgili satırı/sütunu 1D'ye çekiyoruz; sonraki işlemleri
        // tek boyutlu indeks (i) ile yapmak çok daha temiz oluyor.

        GameObject[] currentLine = new GameObject[lineLength];
        for (int i = 0; i < lineLength; i++)
        {
            currentLine[i] = isHorizontal
                ? tiles[i, lineIndex]   // satır: x=i değişiyor, y=lineIndex sabit
                : tiles[lineIndex, i];  // sütun: x=lineIndex sabit, y=i değişiyor
        }

        // ============================================================
        // 4) YENİ ÇİZGİYİ HESAPLA
        // ============================================================
        // newLine[i] = kaydırma sonrası i pozisyonunda olacak tile.
        // Önce null'larla başlıyor: mevcut tile'ları yeni indekslerine
        // yerleştireceğiz, boş kalan slotlara aşağıda yeni tile spawn edeceğiz.

        GameObject[] newLine = new GameObject[lineLength];

        for (int i = 0; i < lineLength; i++)
        {
            int newIndex = i + signedShift;

            if (newIndex < 0 || newIndex >= lineLength)
            {
                // Bu tile sınır dışına çıkıyor → yok et.
                // Destroy çağrısı frame sonunda gerçekleşir, bu yüzden
                // referansı kullanmaya devam edersek (hemen sonra) crash olmaz,
                // ama biz zaten kullanmıyoruz.
                Destroy(currentLine[i]);
            }
            else
            {
                // Tile geçerli bir slota düşüyor → yeni dizide o slota koy.
                newLine[newIndex] = currentLine[i];
            }
        }

        // ============================================================
        // 5) TILE'LARI YERLEŞTİR + BOŞ SLOTLARI YENİ TILE İLE DOLDUR
        // ============================================================

        for (int i = 0; i < lineLength; i++)
        {
            // Bu line-slotu'nun grid'deki gerçek (x,y) koordinatı:
            int gridX = isHorizontal ? i : lineIndex;
            int gridY = isHorizontal ? lineIndex : i;

            if (newLine[i] == null)
            {
                // Boş slot → buraya yeni bir tile spawn et.
                GameObject newTile = Instantiate(
                    tilePrefab,
                    GetWorldPosition(gridX, gridY),
                    Quaternion.identity,
                    transform // parent = GridManager, Hierarchy temiz kalsın
                );
                newTile.name = $"Tile_{gridX}_{gridY}";
                newLine[i] = newTile;
            }
            else
            {
                // Mevcut tile → yeni dünya pozisyonuna taşı, ismini güncelle.
                // Şu an instant teleport; animasyon (lerp/tween) istersen
                // sonradan ekleriz, OnLineShifted event'ine bağlı bir
                // sistem üzerinden de yapılabilir.
                newLine[i].transform.position = GetWorldPosition(gridX, gridY);
                newLine[i].name = $"Tile_{gridX}_{gridY}";
            }
        }

        // ============================================================
        // 6) 2D TILES DİZİSİNİ GÜNCELLENMİŞ ÇİZGİYLE GERİ YAZ
        // ============================================================
        for (int i = 0; i < lineLength; i++)
        {
            if (isHorizontal)
                tiles[i, lineIndex] = newLine[i];
            else
                tiles[lineIndex, i] = newLine[i];
        }

        // ============================================================
        // 7) EVENT TETİKLE
        // ============================================================
        // Decoupling: SFX/animasyon/oyun mantığı bu event'e abone olarak
        // GridManager'a referans tutmadan tepki verebilir.
        OnLineShifted?.Invoke(lineIndex, direction, count);

        Debug.Log($"[SquareGridManager] Line {lineIndex} → {direction}, count={count}");
    }


}