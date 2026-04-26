using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GridFiller : MonoBehaviour
{
    public static GridFiller Instance;

    [Header("Yerleştirme Ayarları")]
    public int minTreasureDistance = 2; // Oyunculara olan minimum Manhattan uzaklığı
    public int trapCountPerType = 5;

    [Header("Tuzak Prefabları (Animasyonlu/Spriteli)")]
    public GameObject bombPrefab;
    public GameObject Explosion;
    [Tooltip("Piston Yönleri Sırasıyla -> 0: Dışa (Up), 1: İçe (Down), 2: Sağa, 3: Sola")]
    public GameObject[] pistonPrefabs = new GameObject[4];
    public GameObject teleporterPrefab;

    private List<Vector2Int> availableTiles = new List<Vector2Int>();
    private Vector2Int treasureLocation;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void FillGrid()
    {
        int maxRings = GridManager.Instance.totalRings;
        int maxSlices = GridManager.Instance.totalSlices;
        PlayerController p1 = TurnManager.Instance.player1;
        PlayerController p2 = TurnManager.Instance.player2;

        for (int r = 0; r < maxRings; r++)
        {
            for (int s = 0; s < maxSlices; s++)
            {
                if ((r == p1.currentRing && s == p1.currentSlice) ||
                    (r == p2.currentRing && s == p2.currentSlice)) continue;

                availableTiles.Add(new Vector2Int(r, s));
            }
        }

        PlaceTreasure(p1, p2);

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
            Debug.Log($"Hazine yerleştirildi: Ring {treasureLocation.x}, Slice {treasureLocation.y}");
        }
    }

    // ====================================================================
    // KESİN ÇÖZÜM: PREFAB BOYUTUNU ZORLA KORUYAN YARDIMCI FONKSİYON
    // ====================================================================
    private GameObject SpawnTrap(GameObject prefab, Transform parentTile)
    {
        // 1. Objeyi parent olmadan bağımsız yarat (Böylece prefabın kendi boyutuyla doğar)
        GameObject obj = Instantiate(prefab, parentTile.position, parentTile.rotation);

        // 2. Prefabın orijinal scale değerini garantiye al
        obj.transform.localScale = prefab.transform.localScale;

        // 3. Objeyi Tile'ın içine at ve 'true' parametresiyle Unity'e bu "Dünya Boyutunu" 
        // ne pahasına olursa olsun (Tile'ın boyutu tuhaf olsa bile) korumasını emret.
        obj.transform.SetParent(parentTile, true);

        obj.transform.SetAsLastSibling();
        obj.SetActive(false); // Başlangıçta gizli

        return obj;
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

            // DÜZELTİLDİ: Yeni yardımcı fonksiyonumuzu kullanıyoruz
            tile.trapElement = SpawnTrap(prefab, tile.tileTransform);
            tile.onTrapTriggered.RemoveAllListeners();

            TileData localTile = tile;

            if (type == TrapType.Mine)
            {
                // DÜZELTİLDİ: Patlama efekti için de boyut korumalı yaratma
                GameObject explosionObj = SpawnTrap(Explosion, tile.tileTransform);

                localTile.onTrapTriggered.AddListener(() => StartCoroutine(BombCoroutine(localTile, explosionObj)));
            }
            else if (type == TrapType.Teleport)
            {
                localTile.onTrapTriggered.AddListener(() => StartCoroutine(TeleportCoroutine(localTile)));
            }
        }
    }

    private void PlacePistons(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (availableTiles.Count == 0) break;

            int randomIndex = Random.Range(0, availableTiles.Count);
            Vector2Int pos = availableTiles[randomIndex];

            List<int> validDirs = new List<int>();
            if (pos.x + 2 < GridManager.Instance.totalRings) validDirs.Add(0);
            if (pos.x - 2 >= 0) validDirs.Add(1);
            validDirs.Add(2);
            validDirs.Add(3);

            if (validDirs.Count == 0) continue;

            int chosenDir = validDirs[Random.Range(0, validDirs.Count)];
            availableTiles.RemoveAt(randomIndex);

            TileData tile = GridManager.Instance.GetTile(pos.x, pos.y);
            tile.trapType = TrapType.Piston;

            // DÜZELTİLDİ: Pistonlar için de yardımcı fonksiyonumuzu kullanıyoruz
            tile.trapElement = SpawnTrap(pistonPrefabs[chosenDir], tile.tileTransform);

            TileData localTile = tile;
            Vector2Int localPos = pos;

            tile.onTrapTriggered.RemoveAllListeners();
            tile.onTrapTriggered.AddListener(() => StartCoroutine(PistonCoroutine(localTile, localPos, chosenDir)));
        }
    }

    private IEnumerator BombCoroutine(TileData tile, GameObject explosionObj)
    {
        tile.trapElement.SetActive(true);
        PlayerController player = TurnManager.Instance.activePlayer;
        yield return new WaitForSeconds(1f);

        tile.trapElement.GetComponent<Animator>().SetTrigger("triggerAnim");

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayOneShotSFX("Bomb");

        yield return new WaitForSeconds(0.3f);

        if (explosionObj != null)
        {
            explosionObj.SetActive(true);
            Animator expAnim = explosionObj.GetComponent<Animator>();
            if (expAnim != null)
            {
                expAnim.SetTrigger("explosion");
            }
            Destroy(explosionObj, 1.5f);
        }

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

        yield return new WaitForSeconds(1f);
        tile.trapElement.GetComponent<Animator>().SetTrigger("piston");
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayOneShotSFX("jumppad");

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

        yield return new WaitForSeconds(1f);
        tile.trapElement.GetComponent<Animator>().SetTrigger("teleport");
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayOneShotSFX("teleport");

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