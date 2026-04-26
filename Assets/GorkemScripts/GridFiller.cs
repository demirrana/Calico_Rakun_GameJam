using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GridFiller : MonoBehaviour
{
    public static GridFiller Instance;

    [Header("Yerle�tirme Ayarlar�")]
    public int minTreasureDistance = 2; // Oyunculara olan minimum Manhattan uzakl���
    public int trapCountPerType = 5;

    [Header("Tuzak Prefablar� (Animasyonlu/Spriteli)")]
    public GameObject bombPrefab;
    [Tooltip("Piston Y�nleri S�ras�yla -> 0: D��a (Up), 1: ��e (Down), 2: Sa�a, 3: Sola")]
    public GameObject[] pistonPrefabs = new GameObject[4];
    public GameObject teleporterPrefab;

    private List<Vector2Int> availableTiles = new List<Vector2Int>();
    private Vector2Int treasureLocation;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // TurnManager taraf�ndan oyuncular yerle�tikten hemen sonra �a�r�l�r
    public void FillGrid()
    {
        int maxRings = GridManager.Instance.totalRings;
        int maxSlices = GridManager.Instance.totalSlices;
        PlayerController p1 = TurnManager.Instance.player1;
        PlayerController p2 = TurnManager.Instance.player2;

        // 1. Oyuncular�n oldu�u yerler hari� t�m bo� haritay� listeye ekle
        for (int r = 0; r < maxRings; r++)
        {
            for (int s = 0; s < maxSlices; s++)
            {
                if ((r == p1.currentRing && s == p1.currentSlice) ||
                    (r == p2.currentRing && s == p2.currentSlice)) continue;

                availableTiles.Add(new Vector2Int(r, s));
            }
        }

        // 2. Hazineyi yerle�tir
        PlaceTreasure(p1, p2);

        // 3. Tuzaklar� s�rayla yerle�tir
        PlaceTraps(TrapType.Mine, bombPrefab, trapCountPerType);
        PlacePistons(trapCountPerType);
        PlaceTraps(TrapType.Teleport, teleporterPrefab, trapCountPerType);
    }

    private void PlaceTreasure(PlayerController p1, PlayerController p2)
    {
        List<Vector2Int> validSpots = new List<Vector2Int>();

        foreach (var pos in availableTiles)
        {
            if (GetDistance(pos.x, pos.y, p1.currentRing, p1.currentSlice) >= minTreasureDistance &&
                GetDistance(pos.x, pos.y, p2.currentRing, p2.currentSlice) >= minTreasureDistance)
            {
                validSpots.Add(pos);
            }
        }

        if (validSpots.Count > 0)
        {
            treasureLocation = validSpots[Random.Range(0, validSpots.Count)];
            GridManager.Instance.GetTile(treasureLocation.x, treasureLocation.y).hasTreasure = true;
            availableTiles.Remove(treasureLocation);
            Debug.Log($"Hazine yerle�tirildi: Ring {treasureLocation.x}, Slice {treasureLocation.y}");
        }
    }

    private void PlaceTraps(TrapType type, GameObject prefab, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (availableTiles.Count == 0) break;

            int randomIndex = Random.Range(0, availableTiles.Count);
            Vector2Int pos = availableTiles[randomIndex];
            availableTiles.RemoveAt(randomIndex);

            TileData tile = GridManager.Instance.GetTile(pos.x, pos.y);
            tile.trapType = type;

            // Prefab� tile'�n son child'� olarak yarat ve SetActive(false) yap
            GameObject trapObj = Instantiate(prefab, tile.tileTransform);
            trapObj.transform.SetAsLastSibling();
            trapObj.SetActive(false);
            tile.trapElement = trapObj;

            tile.onTrapTriggered.RemoveAllListeners();
            TileData localTile = tile;

            if (type == TrapType.Mine)
                localTile.onTrapTriggered.AddListener(() => StartCoroutine(BombCoroutine(localTile)));
            else if (type == TrapType.Teleport)
                localTile.onTrapTriggered.AddListener(() => StartCoroutine(TeleportCoroutine(localTile)));
        }
    }

    private void PlacePistons(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (availableTiles.Count == 0) break;

            int randomIndex = Random.Range(0, availableTiles.Count);
            Vector2Int pos = availableTiles[randomIndex];

            // Pistonun 2 blok itebilece�i ge�erli y�nleri bul
            List<int> validDirs = new List<int>();
            if (pos.x + 2 < GridManager.Instance.totalRings) validDirs.Add(0); // D��a iter
            if (pos.x - 2 >= 0) validDirs.Add(1); // ��e iter
            validDirs.Add(2); // Sa�a iter 
            validDirs.Add(3); // Sola iter 

            if (validDirs.Count == 0) continue;

            int chosenDir = validDirs[Random.Range(0, validDirs.Count)];
            availableTiles.RemoveAt(randomIndex);

            TileData tile = GridManager.Instance.GetTile(pos.x, pos.y);
            tile.trapType = TrapType.Piston;

            GameObject trapObj = Instantiate(pistonPrefabs[chosenDir], tile.tileTransform);
            trapObj.transform.SetAsLastSibling();
            trapObj.SetActive(false);
            tile.trapElement = trapObj;

            TileData localTile = tile;
            Vector2Int localPos = pos;

            tile.onTrapTriggered.RemoveAllListeners();
            tile.onTrapTriggered.AddListener(() => StartCoroutine(PistonCoroutine(localTile, localPos, chosenDir)));
        }
    }


    private IEnumerator BombCoroutine(TileData tile)
    {
        tile.trapElement.SetActive(true); 
        PlayerController player = TurnManager.Instance.activePlayer;
        yield return new WaitForSeconds(0.5f);
        tile.trapElement.GetComponent<Animator>().SetTrigger("triggerAnim");
        AudioManager.Instance.PlayOneShotSFX("Bomb");
        yield return new WaitForSeconds(0.6f); 

        TileData targetTile = GridManager.Instance.GetTile(player.previousRing, player.previousSlice);

        bool isMoveDone = false;
        player.MoveTo(player.previousRing, player.previousSlice, targetTile.tileTransform, () => isMoveDone = true);

        yield return new WaitUntil(() => isMoveDone);

        CleanUpAndContinue(tile);
    }

    private IEnumerator PistonCoroutine(TileData tile, Vector2Int trapPos, int direction)
    {
        tile.trapElement.SetActive(true);
        PlayerController player = TurnManager.Instance.activePlayer;

        yield return new WaitForSeconds(0.5f);

        int targetR = trapPos.x;
        int targetS = trapPos.y;
        int maxSlices = GridManager.Instance.totalSlices;

        if (direction == 0) targetR += 2;
        else if (direction == 1) targetR -= 2;
        else if (direction == 2) targetS = (targetS + 2) % maxSlices;
        else if (direction == 3) targetS = (targetS - 2 + maxSlices) % maxSlices;

        TileData targetTile = GridManager.Instance.GetTile(targetR, targetS);

        bool isMoveDone = false;
        player.MoveTo(targetR, targetS, targetTile.tileTransform, () => isMoveDone = true);

        yield return new WaitUntil(() => isMoveDone);

        CleanUpAndContinue(tile);
    }

    private IEnumerator TeleportCoroutine(TileData tile)
    {
        tile.trapElement.SetActive(true);
        PlayerController player = TurnManager.Instance.activePlayer;
        PlayerController opponent = (player == TurnManager.Instance.player1) ? TurnManager.Instance.player2 : TurnManager.Instance.player1;

        yield return new WaitForSeconds(0.5f);

        List<Vector2Int> validTps = new List<Vector2Int>();
        for (int r = 0; r < GridManager.Instance.totalRings; r++)
        {
            for (int s = 0; s < GridManager.Instance.totalSlices; s++)
            {
                if ((r == player.currentRing && s == player.currentSlice) ||
                    (r == opponent.currentRing && s == opponent.currentSlice) ||
                    (r == treasureLocation.x && s == treasureLocation.y)) continue;

                validTps.Add(new Vector2Int(r, s));
            }
        }

        Vector2Int tpTarget = validTps[Random.Range(0, validTps.Count)];
        TileData targetTile = GridManager.Instance.GetTile(tpTarget.x, tpTarget.y);

        bool isMoveDone = false;
        player.MoveTo(tpTarget.x, tpTarget.y, targetTile.tileTransform, () => isMoveDone = true);

        yield return new WaitUntil(() => isMoveDone);

        CleanUpAndContinue(tile);
    }

    private void CleanUpAndContinue(TileData tile)
    {
        tile.trapType = TrapType.None;
        tile.onTrapTriggered.RemoveAllListeners();
        Destroy(tile.trapElement); 

        TurnManager.Instance.EvaluateCurrentPlayerTile();
    }

    private int GetDistance(int r1, int s1, int r2, int s2)
    {
        int dr = Mathf.Abs(r1 - r2);
        int ds = Mathf.Abs(s1 - s2);
        int totalSlices = GridManager.Instance.totalSlices;

        if (ds > totalSlices / 2) ds = totalSlices - ds;

        return dr + ds;
    }
}