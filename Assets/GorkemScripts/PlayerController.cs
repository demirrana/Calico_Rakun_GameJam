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

    // Sadece oyun baþýnda anýnda yerleþtirme için
    public void SetPositionImmediate(int ring, int slice, Transform targetPos)
    {
        currentRing = ring;
        currentSlice = slice;
        previousRing = ring;
        previousSlice = slice;
        transform.position = targetPos.position;
    }

    // Yürüme veya Fýrlatýlma komutu
    public void MoveTo(int targetRing, int targetSlice, Transform targetPos, Action onMovementComplete)
    {
        if (!isMoving)
        {
            // KRÝTÝK DÜZELTME: Karakter yer deðiþtirmeye baþladýðý an, þu anki konumu "önceki" olur.
            // Bu sayede pistonla fýrlatýlsa bile, fýrlatýldýðý yer "önceki" konumu olarak kalýr.
            previousRing = currentRing;
            previousSlice = currentSlice;

            StartCoroutine(MoveCoroutine(targetRing, targetSlice, targetPos, onMovementComplete));
        }
    }

    private IEnumerator MoveCoroutine(int targetRing, int targetSlice, Transform targetPos, Action onMovementComplete)
    {
        isMoving = true;

        while (Vector3.Distance(transform.position, targetPos.position) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos.position, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPos.position;
        currentRing = targetRing;
        currentSlice = targetSlice;
        isMoving = false;

        onMovementComplete?.Invoke(); // Hedefe varýldý, þefe (TurnManager) haber ver
    }
}