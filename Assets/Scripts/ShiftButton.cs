using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Grid'in kenarındaki üçgen butonu temsil eder.
///
/// Implement edilen EventSystem interface'leri:
///   - IPointerClickHandler : tıklama tamamlanınca → ShiftLine çağır.
///   - IPointerEnterHandler : mouse üstüne gelince → hover state.
///   - IPointerExitHandler  : mouse ayrılınca → idle state.
///   - IPointerDownHandler  : tıklamanın "basıldı" anı → pressed state.
///   - IPointerUpHandler    : tıklamanın "bırakıldı" anı → hover/idle'a dön.
///
/// State akışı:
///   Idle ↔ Hover ↔ Pressed
///   Mouse butonu çekme/bırakma kombinasyonlarına göre arası geçişler oluyor.
/// </summary>
public class ShiftButton : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    // ========== KAYDIRMA BİLGİLERİ ==========
    private SquareGridManager gridManager;
    private int lineIndex;
    private ShiftDirection direction;
    private int shiftAmount = 1;

    // Butonun grid kenarındaki konumu. Initialize'da set ediliyor.
    // SquareGridManager mouse pozisyonuna göre side bazında görünürlüğü ayarlar.
    private EdgeSide side;
    public EdgeSide Side => side;

    // ========== HOVER AYARLARI ==========

    [Header("Hover Efekti")]
    [Tooltip("Hover sırasında butonun ölçek çarpanı. 1.15 → %15 büyür.")]
    [SerializeField] private float hoverScaleMultiplier = 1.15f;

    [Tooltip("Hover sırasında butonun rengi (tint).")]
    [SerializeField] private Color hoverColor = new Color(1f, 0.9f, 0.5f, 1f);

    // ========== PRESS AYARLARI ==========

    [Header("Press (Tıklama) Efekti")]
    [Tooltip("Basılı tutulurken butonun ölçek çarpanı. " +
             "1'in altında olmalı ki 'içe çökme' hissi versin (örn 0.9).")]
    [SerializeField] private float pressScaleMultiplier = 0.9f;

    [Tooltip("Basılı tutulurken butonun rengi. Parlak (saturasyonu yüksek, " +
             "alpha'sı 1'e yakın) bir ton seç ki 'parlama' hissini versin.")]
    [SerializeField] private Color pressColor = new Color(1f, 1f, 0.7f, 1f);

    // ========== TWEEN AYARLARI ==========

    [Header("Genel Tween Ayarları")]
    [Tooltip("Hover'a geçiş süresi. Snappy hissi için kısa (0.08-0.12).")]
    [SerializeField] private float hoverTweenDuration = 0.1f;

    [Tooltip("Press'e geçiş süresi. Hover'dan daha kısa olmalı ki tıklama anı " +
             "anında hissedilsin (0.04-0.07).")]
    [SerializeField] private float pressTweenDuration = 0.05f;

    // ========== CACHE'LENEN ORIJINAL DEĞERLER ==========
    private Vector3 originalScale;
    private Color originalColor;
    private SpriteRenderer spriteRenderer;

    // Collider'ı da cache'liyoruz: görünmez olduğunda tıklamayı da
    // kapatacağız ki gizli buton yanlışlıkla raycast yakalamasın.
    private Collider2D myCollider;

    // ========== STATE TRACKING ==========
    // Mouse'un mevcut durumunu takip ediyoruz ki PointerUp tetiklendiğinde
    // "şimdi nereye dönmeliyim — hover'a mı idle'a mı?" diye doğru karar verelim.
    //
    // isHovering: pointer üzerimizde mi (Enter geldi ama Exit gelmedi).
    // isPressed:  şu an basılı tutuluyor mu (Down geldi ama Up gelmedi).
    private bool isHovering = false;
    private bool isPressed = false;

    // Aktif tween coroutine'i: yeni geldiğinde eskisini iptal etmek için.
    private Coroutine activeTween;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        myCollider = GetComponent<Collider2D>();
        originalScale = transform.localScale;
        // ----- originalColor'ı prefab rengin ALPHA=0 versiyonu olarak set et -----
        // Mantık: default state görünmez olsun. Mouse üstüne gelince OnPointerEnter
        // tween'i originalColor'dan hoverColor'a (alpha 1) götürecek → buton
        // yumuşakça fade-in olur. Mouse ayrılınca tersi → fade-out.
        //
        // Collider DOKUNULMAZ → her zaman aktif kalır ki OnPointerEnter
        // tetiklenebilsin (raycast collider'a bakar, sprite görünürlüğüne değil).
        if (spriteRenderer != null)
        {
            Color baseColor = spriteRenderer.color;
            baseColor.a = 0f;
            originalColor = baseColor;
            spriteRenderer.color = originalColor; // anında invisible
        }
    }

    public void Initialize(SquareGridManager manager, int line, ShiftDirection dir, EdgeSide edgeSide)
    {
        gridManager = manager;
        lineIndex = line;
        direction = dir;
        side = edgeSide;
    }

    /// <summary>
    /// Butonu görünür/görünmez yapar. SpriteRenderer ve Collider'ı birlikte
    /// kontrol ediyoruz: invisible iken click de almasın.
    /// GameObject.SetActive(false) yapmıyoruz çünkü o tüm event subscription'ı
    /// da öldürür; biz sadece görünürlük + interaction'ı kapatıyoruz.
    /// </summary>
    public void SetSideVisible(bool visible)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
        if (myCollider != null) myCollider.enabled = visible;
    }

    // ========== EVENTSYSTEM CALLBACKS ==========

    public void OnPointerClick(PointerEventData eventData)
    {
        if (gridManager == null)
        {
            Debug.LogWarning("[ShiftButton] gridManager atanmamış.", this);
            return;
        }
        gridManager.ShiftLine(lineIndex, direction, shiftAmount);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        // Eğer şu an basılı tutuluyorsa pressed state korunmalı (state önceliği:
        // pressed > hover > idle). Yani sadece basılı değilsek hover'a geç.
        if (!isPressed)
            TweenTo(originalScale * hoverScaleMultiplier, hoverColor, hoverTweenDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        // Basılı değilsek idle'a dön.
        // Basılıyken çıkış olursa (drag out) pressed görünüm korunur — bu
        // genelde istenir, kullanıcı parmağını/mouse'unu butondan dışarı
        // çekene kadar "basılı" hissetmesi doğru.
        if (!isPressed)
            TweenTo(originalScale, originalColor, hoverTweenDuration);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        // Press state'ine geç: küçül + parla.
        TweenTo(originalScale * pressScaleMultiplier, pressColor, pressTweenDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        // Şu an pointer hala üzerimizdeyse → hover state'e dön.
        // Değilse (basılı tutarken dışarı sürüklenmiş) → idle'a dön.
        if (isHovering)
            TweenTo(originalScale * hoverScaleMultiplier, hoverColor, hoverTweenDuration);
        else
            TweenTo(originalScale, originalColor, hoverTweenDuration);
    }

    // ========== TWEEN MANTIĞI ==========

    /// <summary>
    /// Hedef scale ve color'a verilen süre boyunca tween yapar.
    /// Aktif tween varsa iptal eder; yeni tween mevcut canlı değerden başlar.
    /// </summary>
    private void TweenTo(Vector3 targetScale, Color targetColor, float duration)
    {
        if (activeTween != null)
            StopCoroutine(activeTween);
        activeTween = StartCoroutine(TweenCoroutine(targetScale, targetColor, duration));
    }

    private IEnumerator TweenCoroutine(Vector3 targetScale, Color targetColor, float duration)
    {
        Vector3 startScale = transform.localScale;
        Color startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            if (spriteRenderer != null)
                spriteRenderer.color = Color.Lerp(startColor, targetColor, smoothT);

            yield return null;
        }

        activeTween = null;
    }
}