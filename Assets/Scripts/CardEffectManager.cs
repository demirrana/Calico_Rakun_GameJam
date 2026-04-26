using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CardEffectManager : MonoBehaviour
{
    public static CardEffectManager Instance;

    [Header("Highlight Colors")]
    public Color truthColor = Color.blue;    // Kart 1: gerçek ipucu
    public Color decoyColor = Color.red;     // Kart 2: sahte ipucu
    public float highlightDuration = 3f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        CardManager.Instance.OnCardChosen += HandleCardChosen;
    }

    void OnDestroy()
    {
        if (CardManager.Instance != null)
            CardManager.Instance.OnCardChosen -= HandleCardChosen;
    }

    private void HandleCardChosen(object sender, CardEventArgs.ChooseCardEventArgs args)
    {
        // Action Phase kontrolü
        GameState state = TurnManager.Instance.currentState;
        bool isP1Action = state == GameState.Player1_ActionPhase && args.IsPlayer1sTurn;
        bool isP2Action = state == GameState.Player2_ActionPhase && !args.IsPlayer1sTurn;

        if (!isP1Action && !isP2Action) return;
        if (!TurnManager.Instance.cardOrRingTurnPlayable) return;
        if (TurnManager.Instance.cardOrRingTurnPlayed) return;

        PlayerController owner = args.IsPlayer1sTurn ? TurnManager.Instance.player1 : TurnManager.Instance.player2;
        PlayerController opponent = args.IsPlayer1sTurn ? TurnManager.Instance.player2 : TurnManager.Instance.player1;

        if (owner.cardBlocked)
        {
            Debug.Log($"<color=red>{owner.playerName} bu tur kart oynayamıyor!</color>");
            owner.cardBlocked = false;
            TurnManager.Instance.cardOrRingTurnPlayed = true;
            return;
        }

        Card card = args.ChosenCard;
        Debug.Log($"<color=yellow>[KART OYNANDI] {owner.playerName} → {card.GetCardData().cardType}</color>");

        // Efekti çalıştır
        PlayCard(card.GetCardData().cardType, owner, opponent);

        // Kartı sil
        CardManager.Instance.RemoveCard(card, args.IsPlayer1sTurn);
        Destroy(card.gameObject);
    }
    public void PlayCard(CardManager.CardType cardType, PlayerController owner, PlayerController opponent)
    {
        Debug.Log($"<color=yellow>[KART] {owner.playerName} → {cardType} oynuyor!</color>");

        switch (cardType)
        {
            case CardManager.CardType.Card1:
                StartCoroutine(Effect_RevealTreasureHint(true));
                break;
            case CardManager.CardType.Card2:
                StartCoroutine(Effect_RevealTreasureHint(false));
                break;
            case CardManager.CardType.Card3:
                StartCoroutine(Effect_UndoRotation());
                break;
            case CardManager.CardType.Card4:
                Effect_MoveTreasure();
                break;
            case CardManager.CardType.Card5:
                Effect_SkipOpponentMove(opponent);
                break;
            case CardManager.CardType.Card6:
                Effect_AddExtraTreasure();
                break;
            case CardManager.CardType.Card7:
                StartCoroutine(Effect_BurnOpponentCard(owner, opponent));
                break;
            case CardManager.CardType.Card8:
                StartCoroutine(Effect_TeleportToPast(opponent));
                break;
            case CardManager.CardType.Card9:
                Effect_DoubleStep(owner);
                break;
            case CardManager.CardType.Card10:
                StartCoroutine(Effect_RandomTeleport(opponent));
                break;
            case CardManager.CardType.Card11:
                StartCoroutine(Effect_SwapPlayers(owner, opponent));
                break;
            case CardManager.CardType.Card12:
                Effect_BlockOpponentCards(opponent);
                break;
            case CardManager.CardType.Card13:
                StartCoroutine(Effect_BlockTile());
                break;
        }
    }

    // ============ KART 1 & 2: Hazine İpucu ============
    private IEnumerator Effect_RevealTreasureHint(bool revealTruth)
    {
        Vector2Int treasurePos = FindTreasurePosition();
        List<Vector2Int> highlightPositions = new List<Vector2Int>();
        Color color;

        if (revealTruth)
        {
            // 3 kare, biri gerçek hazine konumu — MAVİ
            color = truthColor;
            highlightPositions.Add(treasurePos);
            highlightPositions.AddRange(GetRandomPositions(2, highlightPositions));
            Debug.Log($"<color=blue>[KART 1] Hazine ipucu gösteriliyor. Gerçek konum: Ring {treasurePos.x}, Slice {treasurePos.y}</color>");
        }
        else
        {
            // 3 kare, her biri hazinenin OLMADIĞI yer — KIRMIZI
            color = decoyColor;
            highlightPositions = GetRandomPositions(3, new List<Vector2Int> { treasurePos });
            Debug.Log($"<color=red>[KART 2] Sahte ipucu gösteriliyor. Gösterilen kareler hazine DEĞİL.</color>");
        }

        ShuffleList(highlightPositions);

        // Kareleri boya
        List<SpriteRenderer> highlightedRenderers = new List<SpriteRenderer>();
        List<Color> originalColors = new List<Color>();

        foreach (var pos in highlightPositions)
        {
            TileData tile = GridManager.Instance.GetTile(pos.x, pos.y);
            SpriteRenderer sr = tile.tileTransform.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                originalColors.Add(sr.color);
                sr.color = color;
                highlightedRenderers.Add(sr);
                Debug.Log($"  → Kare boyandı: Ring {pos.x}, Slice {pos.y}");
            }
        }

        yield return new WaitForSeconds(highlightDuration);

        // Orijinal renklere dön
        for (int i = 0; i < highlightedRenderers.Count; i++)
        {
            highlightedRenderers[i].color = originalColors[i];
        }

        Debug.Log("[KART 1/2] İpucu süresi bitti, renkler sıfırlandı.");
        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    // ============ KART 3: Döndürmeyi Geri Al ============
    private IEnumerator Effect_UndoRotation()
    {
        if (RingRotationManager.Instance.HasLastRotation())
        {
            Debug.Log("<color=orange>[KART 3] Son ring döndürmesi geri alınıyor!</color>");
            yield return StartCoroutine(RingRotationManager.Instance.UndoLastRotation());
        }
        else
        {
            Debug.Log("<color=orange>[KART 3] Geri alınacak döndürme yok!</color>");
        }

        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    // ============ KART 4: Hazine Yerini Değiştir ============
    private void Effect_MoveTreasure()
    {
        Vector2Int oldPos = FindTreasurePosition();
        GridManager.Instance.GetTile(oldPos.x, oldPos.y).hasTreasure = false;

        List<Vector2Int> validSpots = GetEmptyPositions();
        // Hazineyi eski yerine koymasın
        validSpots.RemoveAll(p => p.x == oldPos.x && p.y == oldPos.y);

        if (validSpots.Count > 0)
        {
            Vector2Int newPos = validSpots[Random.Range(0, validSpots.Count)];
            GridManager.Instance.GetTile(newPos.x, newPos.y).hasTreasure = true;
            Debug.Log($"<color=magenta>[KART 4] Hazine taşındı: ({oldPos.x},{oldPos.y}) → ({newPos.x},{newPos.y})</color>");
        }

        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    // ============ KART 5: Karşı Oyuncunun Hareketini İptal Et ============
    private void Effect_SkipOpponentMove(PlayerController opponent)
    {
        opponent.skipNextMove = true;
        Debug.Log($"<color=red>[KART 5] {opponent.playerName} bir sonraki hareket hakkını kaybetti!</color>");
        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    // ============ KART 6: Ekstra Hazine Ekle ============
    private void Effect_AddExtraTreasure()
    {
        PlayerController p1 = TurnManager.Instance.player1;
        PlayerController p2 = TurnManager.Instance.player2;
        int minDist = GridFiller.Instance.minTreasureDistance;

        List<Vector2Int> validSpots = new List<Vector2Int>();
        foreach (var pos in GetEmptyPositions())
        {
            int distP1 = GetDistance(pos.x, pos.y, p1.currentRing, p1.currentSlice);
            int distP2 = GetDistance(pos.x, pos.y, p2.currentRing, p2.currentSlice);

            if (distP1 >= minDist && distP2 >= minDist)
                validSpots.Add(pos);
        }

        if (validSpots.Count > 0)
        {
            Vector2Int pos = validSpots[Random.Range(0, validSpots.Count)];
            GridManager.Instance.GetTile(pos.x, pos.y).hasTreasure = true;
            Debug.Log($"<color=green>[KART 6] Ekstra hazine eklendi: Ring {pos.x}, Slice {pos.y} (Min mesafe: {minDist})</color>");
        }
        else
        {
            Debug.Log("<color=green>[KART 6] Uygun konum bulunamadı, hazine eklenemedi!</color>");
        }

        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    // ============ KART 7: Karşı Oyuncunun Kartını Yak ============
    private IEnumerator Effect_BurnOpponentCard(PlayerController owner, PlayerController opponent)
    {
        Debug.Log($"<color=red>[KART 7] {opponent.playerName} rastgele bir kartını kaybediyor!</color>");
        yield break; // TODO: Kart seçtirme UI'ı eklenecek
    }

    // ============ KART 8: 2 Hamle Önceki Konuma Işınla ============
    private IEnumerator Effect_TeleportToPast(PlayerController opponent)
    {
        Vector2Int pastPos = opponent.GetPositionNMovesAgo(2);
        TileData targetTile = GridManager.Instance.GetTile(pastPos.x, pastPos.y);

        Debug.Log($"<color=cyan>[KART 8] {opponent.playerName} 2 hamle öncesine ışınlanıyor: Ring {pastPos.x}, Slice {pastPos.y}</color>");

        bool done = false;
        opponent.MoveTo(pastPos.x, pastPos.y, targetTile.tileTransform, () => done = true);
        yield return new WaitUntil(() => done);

        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    // ============ KART 9: Bu El 2 Adım At ============
    private void Effect_DoubleStep(PlayerController owner)
    {
        owner.extraSteps = 1;
        Debug.Log($"<color=green>[KART 9] {owner.playerName} bu tur 2 adım atacak!</color>");
        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    // ============ KART 10: Karşıyı Rastgele Yere Işınla ============
    private IEnumerator Effect_RandomTeleport(PlayerController opponent)
    {
        List<Vector2Int> validSpots = GetEmptyPositions();
        if (validSpots.Count > 0)
        {
            Vector2Int pos = validSpots[Random.Range(0, validSpots.Count)];
            TileData targetTile = GridManager.Instance.GetTile(pos.x, pos.y);

            Debug.Log($"<color=cyan>[KART 10] {opponent.playerName} rastgele ışınlanıyor: Ring {pos.x}, Slice {pos.y}</color>");

            bool done = false;
            opponent.MoveTo(pos.x, pos.y, targetTile.tileTransform, () => done = true);
            yield return new WaitUntil(() => done);
        }

        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    // ============ KART 11: Oyuncular Konum Değiştirir ============
    private IEnumerator Effect_SwapPlayers(PlayerController owner, PlayerController opponent)
    {
        int ownerRing = owner.currentRing, ownerSlice = owner.currentSlice;
        int oppRing = opponent.currentRing, oppSlice = opponent.currentSlice;

        TileData ownerTargetTile = GridManager.Instance.GetTile(oppRing, oppSlice);
        TileData oppTargetTile = GridManager.Instance.GetTile(ownerRing, ownerSlice);

        Debug.Log($"<color=yellow>[KART 11] Konum takası: {owner.playerName}({ownerRing},{ownerSlice}) ↔ {opponent.playerName}({oppRing},{oppSlice})</color>");

        bool ownerDone = false, oppDone = false;
        owner.MoveTo(oppRing, oppSlice, ownerTargetTile.tileTransform, () => ownerDone = true);
        opponent.MoveTo(ownerRing, ownerSlice, oppTargetTile.tileTransform, () => oppDone = true);

        yield return new WaitUntil(() => ownerDone && oppDone);

        Debug.Log($"[KART 11] Takas tamamlandı!");
        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    // ============ KART 12: Karşıya Kart Attırma ============
    private void Effect_BlockOpponentCards(PlayerController opponent)
    {
        opponent.cardBlocked = true;
        Debug.Log($"<color=red>[KART 12] {opponent.playerName} bir sonraki tur kart oynayamayacak!</color>");
        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    // ============ KART 13: Kareyi Basılamaz Yap ============
    private IEnumerator Effect_BlockTile()
    {
        // TODO: Oyuncuya kare seçtirme UI'ı eklenecek
        Debug.Log("<color=red>[KART 13] Kare engelleme - henüz kare seçim UI'ı yok.</color>");
        yield return null;
        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    // ============ YARDIMCI METODLAR ============

    private Vector2Int FindTreasurePosition()
    {
        for (int r = 0; r < GridManager.Instance.totalRings; r++)
        {
            for (int s = 0; s < GridManager.Instance.totalSlices; s++)
            {
                TileData tile = GridManager.Instance.GetTile(r, s);
                if (tile != null && tile.hasTreasure)
                    return new Vector2Int(r, s);
            }
        }
        Debug.LogWarning("[CardEffectManager] Hazine bulunamadı!");
        return Vector2Int.zero;
    }

    private List<Vector2Int> GetEmptyPositions()
    {
        List<Vector2Int> spots = new List<Vector2Int>();
        PlayerController p1 = TurnManager.Instance.player1;
        PlayerController p2 = TurnManager.Instance.player2;

        for (int r = 0; r < GridManager.Instance.totalRings; r++)
        {
            for (int s = 0; s < GridManager.Instance.totalSlices; s++)
            {
                if (r == p1.currentRing && s == p1.currentSlice) continue;
                if (r == p2.currentRing && s == p2.currentSlice) continue;

                TileData tile = GridManager.Instance.GetTile(r, s);
                if (tile != null && !tile.hasTreasure)
                    spots.Add(new Vector2Int(r, s));
            }
        }
        return spots;
    }

    private List<Vector2Int> GetRandomPositions(int count, List<Vector2Int> exclude)
    {
        List<Vector2Int> all = new List<Vector2Int>();
        for (int r = 0; r < GridManager.Instance.totalRings; r++)
        {
            for (int s = 0; s < GridManager.Instance.totalSlices; s++)
            {
                Vector2Int pos = new Vector2Int(r, s);
                if (!exclude.Contains(pos))
                    all.Add(pos);
            }
        }

        List<Vector2Int> result = new List<Vector2Int>();
        for (int i = 0; i < count && all.Count > 0; i++)
        {
            int idx = Random.Range(0, all.Count);
            result.Add(all[idx]);
            all.RemoveAt(idx);
        }
        return result;
    }

    private int GetDistance(int r1, int s1, int r2, int s2)
    {
        int dr = Mathf.Abs(r1 - r2);
        int ds = Mathf.Abs(s1 - s2);
        int totalSlices = GridManager.Instance.totalSlices;
        if (ds > totalSlices / 2) ds = totalSlices - ds;
        return dr + ds;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}