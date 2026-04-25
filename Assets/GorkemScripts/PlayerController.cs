using UnityEngine;
using System.Collections;
using System;

public class PlayerController : MonoBehaviour
{
    public string playerName;

    [Header("Anlýk Konum")]
    public int currentRing;
    public int currentSlice;

    [Header("Geçmiþ Konum (Mayýn Ýçin)")]
    [HideInInspector] public int previousRing;
    [HideInInspector] public int previousSlice;

    [Header("Hareket Ayarlarý")]
    public float moveSpeed = 5f;
    private bool isMoving = false;

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

        onMovementComplete?.Invoke();
    }
}