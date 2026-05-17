using UnityEngine;

/// <summary>
/// Grid üzerinde dolaşan seçim/önizleme imleci.
/// Hem setup phase'inde (oyuncu yerleşimini önizler) hem de play phase'inde
/// (hamle hedefini önizler) kullanılacak — aynı obje, farklı state.
///
/// Mantığı çok basit: bir gridPosition tutar, transform.position'unu
/// gridManager.GetWorldPosition ile günceller.
/// </summary>
public class GridCursor : MonoBehaviour
{
    private SquareGridManager gridManager;
    private Vector2Int gridPosition;
    private SpriteRenderer spriteRenderer;

    public Vector2Int GridPosition => gridPosition;

    private void Awake()
    {
        // SpriteRenderer'ı bir kez cache'le.
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Cursor'u bir grid manager'a bağlar ve başlangıç pozisyonuna yerleştirir.
    /// </summary>
    public void Initialize(SquareGridManager manager, Vector2Int startPos)
    {
        gridManager = manager;
        SetGridPosition(startPos.x, startPos.y);
    }

    /// <summary>
    /// Cursor'u verilen koordinata yerleştirir. Sınır dışındaysa
    /// (Clamp ile) en yakın kenara oturur — Out-of-bounds problemi olmaz.
    /// </summary>
    public void SetGridPosition(int x, int y)
    {
        if (gridManager == null) return;

        // Clamp: girilen koordinat grid içinde kalsın.
        // Width=6 ise geçerli aralık 0..5, o yüzden Width-1.
        x = Mathf.Clamp(x, 0, gridManager.Width - 1);
        y = Mathf.Clamp(y, 0, gridManager.Height - 1);

        gridPosition = new Vector2Int(x, y);

        // World pozisyonu: tile'ın hemen üstünde, oyuncudan biraz arkada.
        // Z sıralaması: tile (z=0) < cursor (z=-0.5) < player (z=-1).
        // 2D ortho'da küçük Z = kameraya yakın = önde.
        Vector3 worldPos = gridManager.GetWorldPosition(x, y);
        worldPos.z = -0.5f;
        transform.position = worldPos;
    }

    public void SetGridPosition(Vector2Int pos) => SetGridPosition(pos.x, pos.y);

    /// <summary>
    /// Cursor'u delta kadar hareket ettir. Sınır dışına çıkarsa Clamp devreye girer.
    /// </summary>
    public void Move(int dx, int dy)
    {
        SetGridPosition(gridPosition.x + dx, gridPosition.y + dy);
    }

    public void SetVisible(bool visible)
    {
        // Tüm GameObject'i kapatmak yerine sadece renderer'ı kapatıyoruz;
        // script aktif kalsın, lazımsa görünmez durumda bile çalışabilsin.
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
    }

    public void SetTint(Color color)
    {
        if (spriteRenderer != null) spriteRenderer.color = color;
    }
}