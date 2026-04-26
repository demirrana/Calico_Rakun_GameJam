using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    public int totalRings = 5;
    public int totalSlices = 8;

    [System.Serializable]
    public struct RingArray { public TileData[] slices; }

    [Header("Oyuncu İz Renkleri")]
    public Color player1TrailColor = new Color(0.2f, 1f, 1f, 1f);    // Fosforlu cyan
    public Color player2TrailColor = new Color(1f, 0.3f, 0.4f, 1f);  // Fosforlu kırmızı

    public void PaintTile(int ring, int slice, bool isPlayer1)
    {
        TileData tile = GetTile(ring, slice);
        if (tile == null) return;

        SpriteRenderer sr = tile.tileTransform.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = isPlayer1 ? player1TrailColor : player2TrailColor;
        }
    }

    public RingArray[] mapGrid;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public TileData GetTile(int ring, int slice)
    {
        if (ring < 0 || ring >= totalRings || slice < 0 || slice >= totalSlices)
            return null;

        return mapGrid[ring].slices[slice];
    }
}