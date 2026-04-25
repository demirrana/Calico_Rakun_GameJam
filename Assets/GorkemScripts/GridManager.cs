using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    public int totalRings = 5;
    public int totalSlices = 8;

    [System.Serializable]
    public struct RingArray { public TileData[] slices; }

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