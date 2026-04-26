using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    public string playerName;

    [Header("Anl�k Konum")]
    public int currentRing;
    public int currentSlice;

    [Header("Ge�mi� Konum (May�n ��in)")]
    [HideInInspector] public int previousRing;
    [HideInInspector] public int previousSlice;

    [Header("Hareket Ayarlar�")]
    public float moveSpeed = 5f;
    private bool isMoving = false;

    [Header("Kart Efektleri")]
    [HideInInspector] public bool skipNextMove = false;
    [HideInInspector] public bool cardBlocked = false;


    // Pozisyon geçmişi (Kart 8 için)
    private List<Vector2Int> positionHistory = new List<Vector2Int>();

    // Mevcut MoveTo metodunun içinde, hareket başlamadan ÖNCE ekle:
    // positionHistory.Add(new Vector2Int(currentRing, currentSlice));

    public Vector2Int GetPositionNMovesAgo(int n)
    {
        if (positionHistory.Count >= n)
            return positionHistory[positionHistory.Count - n];
        else if (positionHistory.Count > 0)
            return positionHistory[0];
        else
            return new Vector2Int(currentRing, currentSlice);
    }

    public void SetPositionImmediate(int ring, int slice, Transform targetPos)
    {
        currentRing = ring; currentSlice = slice;
        previousRing = ring; previousSlice = slice;
        transform.position = targetPos.position;
        transform.SetParent(targetPos); 
    }

    public void MoveTo(int targetRing, int targetSlice, Transform targetPos, Action onMovementComplete)
    {
        if (!isMoving)
        {
            previousRing = currentRing;
            previousSlice = currentSlice;
            positionHistory.Add(new Vector2Int(currentRing, currentSlice));  // BU SATIR

            StartCoroutine(MoveCoroutine(targetRing, targetSlice, targetPos, onMovementComplete));
        }
    }

    private IEnumerator MoveCoroutine(int targetRing, int targetSlice, Transform targetPos, Action onMovementComplete)
    {
        transform.SetParent(null); 
        isMoving = true;

        while (Vector3.Distance(transform.position, targetPos.position) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos.position, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPos.position;
        transform.SetParent(targetPos);

        currentRing = targetRing;
        currentSlice = targetSlice;
        isMoving = false;

        // Bastığı kareyi boya
        bool isP1 = (this == TurnManager.Instance.player1);
        GridManager.Instance.PaintTile(currentRing, currentSlice, isP1);

        onMovementComplete?.Invoke();
    }
}