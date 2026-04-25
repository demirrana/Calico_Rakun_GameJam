using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class RingRotationManager : MonoBehaviour
{
    public static RingRotationManager Instance;

    public enum RotationState { Idle, SelectingRing, SelectingDirection, Animating }

    [Header("Durum")]
    public RotationState currentState = RotationState.Idle;

    [Header("UI Elementleri")]
    public Button player1Button;
    public TMP_Text player1CooldownText;
    public Button player2Button;
    public TMP_Text player2CooldownText;

    [Header("Canvas Yön Oklarý (Sað/Sol)")]
    [Tooltip("Canvas'a koyduðunuz Sað Ok objesi")]
    public GameObject rightArrowUI;
    [Tooltip("Canvas'a koyduðunuz Sol Ok objesi")]
    public GameObject leftArrowUI;

    [Header("Ring Parent Objeleri")]
    [Tooltip("Sahnede 0'dan 4'e kadar her bir ringin 8 dilimini kapsayan Parent(Ana) objeler")]
    public Transform[] ringParents = new Transform[5];

    // Cooldown Takibi
    private int p1Cooldown = 0;
    private int p2Cooldown = 0;

    // Seçim Deðiþkenleri
    private int selectedRing = 0;
    private int selectedDirection = 1; // 1: Sað (Saat Yönü), -1: Sol (Tersi)
    private PlayerController activePlayer;
    private GameState lastGameState;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // UI oklarýný baþlangýçta kapat
        rightArrowUI.SetActive(false);
        leftArrowUI.SetActive(false);
    }

    void Update()
    {
        CheckTurnChangesForCooldown();
        UpdateUIButtons();

        // State Machine
        if (currentState == RotationState.SelectingRing)
        {
            HandleRingSelection();
        }
        else if (currentState == RotationState.SelectingDirection)
        {
            HandleDirectionSelection();
        }
    }

    // Her yeni el baþladýðýnda cooldownlarý 1 düþürür
    private void CheckTurnChangesForCooldown()
    {
        GameState currentTurnState = TurnManager.Instance.currentState;

        if (currentTurnState != lastGameState)
        {
            if (currentTurnState == GameState.Player1_MovePhase)
                p1Cooldown = Mathf.Max(0, p1Cooldown - 1);
            else if (currentTurnState == GameState.Player2_MovePhase)
                p2Cooldown = Mathf.Max(0, p2Cooldown - 1);

            lastGameState = currentTurnState;
        }
    }

    private void UpdateUIButtons()
    {
        // Yazýlarý güncelle
        player1CooldownText.text = p1Cooldown > 0 ? p1Cooldown.ToString() : "";
        player2CooldownText.text = p2Cooldown > 0 ? p2Cooldown.ToString() : "";

        // Eðer bir iþlem yapýlýyorsa butonlarý kitle
        if (currentState != RotationState.Idle)
        {
            player1Button.interactable = false;
            player2Button.interactable = false;
            return;
        }

        // Sýra kimdeyse, action fazýndaysa ve oynanmadýysa aktif et
        bool p1CanPlay = TurnManager.Instance.currentState == GameState.Player1_ActionPhase
                         && TurnManager.Instance.cardOrRingTurnPlayable
                         && !TurnManager.Instance.cardOrRingTurnPlayed;

        bool p2CanPlay = TurnManager.Instance.currentState == GameState.Player2_ActionPhase
                         && TurnManager.Instance.cardOrRingTurnPlayable
                         && !TurnManager.Instance.cardOrRingTurnPlayed;

        player1Button.interactable = p1CanPlay && p1Cooldown == 0;
        player2Button.interactable = p2CanPlay && p2Cooldown == 0;
    }

    // --- BUTON ONCLICK EVENTLERÝ ---

    // Player 1 butonu için OnClick'e bu fonksiyonu ata ve parametreye 1 yaz
    // Player 2 butonu için OnClick'e bu fonksiyonu ata ve parametreye 2 yaz
    public void OnRingButtonClicked(int playerID)
    {
        if (currentState != RotationState.Idle) return;

        activePlayer = playerID == 1 ? TurnManager.Instance.player1 : TurnManager.Instance.player2;

        // Cooldown'u baþlat (Kullandýðý el hariç 3 tur bekleyecek)
        if (playerID == 1) p1Cooldown = 4; // Bu elin bitiþiyle 3'e düþecek
        else p2Cooldown = 4;

        // Baþlangýç olarak oyuncunun üstünde bulunduðu ringi seç
        selectedRing = activePlayer.currentRing;

        TurnManager.Instance.cardOrRingTurnPlayable = false; // Card sistemini kitle
        ToggleRingCursors(selectedRing, true);

        currentState = RotationState.SelectingRing;
    }

    // --- SEÇÝM GÝRDÝLERÝ ---

    private void HandleRingSelection()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) && selectedRing < 4)
        {
            ToggleRingCursors(selectedRing, false);
            selectedRing++;
            ToggleRingCursors(selectedRing, true);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) && selectedRing > 0)
        {
            ToggleRingCursors(selectedRing, false);
            selectedRing--;
            ToggleRingCursors(selectedRing, true);
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            // Ring seçildi, yön seçimine geç
            currentState = RotationState.SelectingDirection;
            selectedDirection = 1; // Varsayýlan sað
            UpdateDirectionUI();
        }
    }

    private void HandleDirectionSelection()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            selectedDirection = 1;
            UpdateDirectionUI();
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            selectedDirection = -1;
            UpdateDirectionUI();
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            // Yön seçildi, animasyonu baþlat
            ToggleRingCursors(selectedRing, false);
            rightArrowUI.SetActive(false);
            leftArrowUI.SetActive(false);

            currentState = RotationState.Animating;
            StartCoroutine(RotateRingCoroutine());
        }
    }

    private void UpdateDirectionUI()
    {
        rightArrowUI.SetActive(selectedDirection == 1);
        leftArrowUI.SetActive(selectedDirection == -1);
    }

    private void ToggleRingCursors(int ringIndex, bool state)
    {
        // Seçilen ringdeki tüm 8 dilimin cursorlarýný (1. child) aç/kapat
        for (int i = 0; i < 8; i++)
        {
            TileData tile = GridManager.Instance.GetTile(ringIndex, i);
            if (tile != null && tile.tileTransform.childCount > 0)
            {
                tile.tileTransform.GetChild(0).gameObject.SetActive(state);
            }
        }
    }

    // --- ANÝMASYON VE MANTIKSAL KAYDIRMA ---

    private IEnumerator RotateRingCoroutine()
    {
        Transform ringTransform = ringParents[selectedRing];

        // 90 derece (2 dilim) hesapla
        float targetAngle = selectedDirection == 1 ? -90f : 90f; // Sað ok ise saat yönü (-90), sol ise tersi (+90)

        Quaternion startRotation = ringTransform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0, 0, targetAngle); // 2D ise Z ekseni. 3D ortamda Y ekseni dönüyorsa: Quaternion.Euler(0, targetAngle, 0) yapmalýsýn.

        float elapsed = 0f;
        float duration = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ringTransform.rotation = Quaternion.Lerp(startRotation, targetRotation, elapsed / duration);
            yield return null;
        }

        ringTransform.rotation = targetRotation;

        // Mantýksal Array Kaydýrmasý!
        ShiftLogicalArray(selectedRing, selectedDirection);

        // Turu tamamla ve TurnManager'a haber ver
        currentState = RotationState.Idle;
        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    private void ShiftLogicalArray(int ring, int direction)
    {
        int maxSlices = GridManager.Instance.totalSlices;
        TileData[] currentSlices = GridManager.Instance.mapGrid[ring].slices;
        TileData[] newSlices = new TileData[maxSlices];

        // 90 derece = 2 dilim (Slice)
        int shiftAmount = 2;

        for (int i = 0; i < maxSlices; i++)
        {
            // Sað (1) ise index artar, Sol (-1) ise index azalýr
            int newIndex;
            if (direction == 1)
                newIndex = (i + shiftAmount) % maxSlices;
            else
                newIndex = (i - shiftAmount + maxSlices) % maxSlices;

            newSlices[newIndex] = currentSlices[i];
        }

        // GridManager'daki Array'i yenisiyle deðiþtir
        GridManager.Instance.mapGrid[ring].slices = newSlices;

        // EÐER BU RÝNG'DE OYUNCU VARSA, ONLARIN MANTIKSAL KONUMUNU DA GÜNCELLE
        UpdatePlayerLogicalPosition(TurnManager.Instance.player1, ring, direction, shiftAmount);
        UpdatePlayerLogicalPosition(TurnManager.Instance.player2, ring, direction, shiftAmount);
    }

    private void UpdatePlayerLogicalPosition(PlayerController player, int ring, int direction, int shiftAmount)
    {
        if (player.currentRing == ring)
        {
            int maxSlices = GridManager.Instance.totalSlices;
            if (direction == 1)
                player.currentSlice = (player.currentSlice + shiftAmount) % maxSlices;
            else
                player.currentSlice = (player.currentSlice - shiftAmount + maxSlices) % maxSlices;

            player.previousSlice = player.currentSlice; // Hata olmamasý için previous'ý da güncelliyoruz
        }
    }
}