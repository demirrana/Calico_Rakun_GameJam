using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class RingRotationManager : MonoBehaviour
{
    public static RingRotationManager Instance;

    public enum RotationState { Idle, SelectingRing, SelectingDirection, Animating }

    // Yeni alanlar
    private int lastRotatedRing = -1;
    private int lastRotationDirection = 0;

    [Header("Durum")]
    public RotationState currentState = RotationState.Idle;
    public float angle = 90f;

    // YENİ EKLENEN KONTROL KİLİDİ
    [HideInInspector] public bool invertControls = false;

    [Header("UI Elementleri")]
    public Button player1Button;
    public TMP_Text player1CooldownText;
    public Button player2Button;
    public TMP_Text player2CooldownText;

    [Header("Canvas Yön Okları (Sağ/Sol)")]
    [Tooltip("Canvas'a koyduğunuz Sağ Ok objesi")]
    public GameObject rightArrowUI;
    [Tooltip("Canvas'a koyduğunuz Sol Ok objesi")]
    public GameObject leftArrowUI;

    [Header("Ring Parent Objeleri")]
    [Tooltip("Sahnede 0'dan 4'e kadar her bir ringin 8 dilimini kapsayan Parent(Ana) objeler")]
    public Transform[] ringParents = new Transform[5];

    private int p1Cooldown = 0;
    private int p2Cooldown = 0;

    private int selectedRing = 0;
    private int selectedDirection = 1;
    private PlayerController activePlayer;
    private GameState lastGameState;

    public bool HasLastRotation()
    {
        return lastRotatedRing >= 0;
    }

    public IEnumerator UndoLastRotation()
    {
        selectedRing = lastRotatedRing;
        selectedDirection = -lastRotationDirection;
        currentState = RotationState.Animating;

        yield return StartCoroutine(RotateRingCoroutine());

        lastRotatedRing = -1; 
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        rightArrowUI.SetActive(false);
        leftArrowUI.SetActive(false);
    }

    void Update()
    {
        CheckTurnChangesForCooldown();
        UpdateUIButtons();

        if (currentState == RotationState.SelectingRing)
        {
            HandleRingSelection();
        }
        else if (currentState == RotationState.SelectingDirection)
        {
            HandleDirectionSelection();
        }
    }

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
        player1CooldownText.text = p1Cooldown > 0 ? p1Cooldown.ToString() : "";
        player2CooldownText.text = p2Cooldown > 0 ? p2Cooldown.ToString() : "";

        if (currentState != RotationState.Idle)
        {
            player1Button.interactable = false;
            player2Button.interactable = false;
            return;
        }

        bool p1CanPlay = TurnManager.Instance.currentState == GameState.Player1_ActionPhase
                         && TurnManager.Instance.cardOrRingTurnPlayable
                         && !TurnManager.Instance.cardOrRingTurnPlayed;

        bool p2CanPlay = TurnManager.Instance.currentState == GameState.Player2_ActionPhase
                         && TurnManager.Instance.cardOrRingTurnPlayable
                         && !TurnManager.Instance.cardOrRingTurnPlayed;

        player1Button.interactable = p1CanPlay && p1Cooldown == 0;
        player2Button.interactable = p2CanPlay && p2Cooldown == 0;
    }

    public void OnRingButtonClicked(int playerID)
    {
        if (currentState != RotationState.Idle) return;

        activePlayer = playerID == 1 ? TurnManager.Instance.player1 : TurnManager.Instance.player2;

        if (playerID == 1) p1Cooldown = 2;
        else p2Cooldown = 2;

        selectedRing = activePlayer.currentRing;

        TurnManager.Instance.cardOrRingTurnPlayable = false;
        ToggleRingCursors(selectedRing, true);

        currentState = RotationState.SelectingRing;
    }

    private void HandleRingSelection()
    {
        KeyCode upKey = invertControls ? KeyCode.DownArrow : KeyCode.UpArrow;
        KeyCode downKey = invertControls ? KeyCode.UpArrow : KeyCode.DownArrow;

        if (Input.GetKeyDown(upKey) && selectedRing < 4)
        {
            ToggleRingCursors(selectedRing, false);
            selectedRing++;
            ToggleRingCursors(selectedRing, true);
        }
        else if (Input.GetKeyDown(downKey) && selectedRing > 0)
        {
            ToggleRingCursors(selectedRing, false);
            selectedRing--;
            ToggleRingCursors(selectedRing, true);
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            currentState = RotationState.SelectingDirection;
            selectedDirection = 1;
            UpdateDirectionUI();
        }
    }

    private void HandleDirectionSelection()
    {
        KeyCode rightKey = invertControls ? KeyCode.LeftArrow : KeyCode.RightArrow;
        KeyCode leftKey = invertControls ? KeyCode.RightArrow : KeyCode.LeftArrow;

        if (Input.GetKeyDown(rightKey))
        {
            selectedDirection = 1;
            UpdateDirectionUI();
        }
        else if (Input.GetKeyDown(leftKey))
        {
            selectedDirection = -1;
            UpdateDirectionUI();
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleRingCursors(selectedRing, false);
            rightArrowUI.SetActive(false);
            leftArrowUI.SetActive(false);

            currentState = RotationState.Animating;
            StartCoroutine(RotateRingCoroutine());
        }
    }

    private void UpdateDirectionUI()
    {
        AudioManager.Instance.PlayOneShotSFX("StoneClick");
        rightArrowUI.SetActive(selectedDirection == 1);
        leftArrowUI.SetActive(selectedDirection == -1);
    }

    private void ToggleRingCursors(int ringIndex, bool state)
    {
        for (int i = 0; i < 8; i++)
        {
            TileData tile = GridManager.Instance.GetTile(ringIndex, i);
            if (tile != null && tile.tileTransform.childCount > 0)
            {
                tile.tileTransform.GetChild(0).gameObject.SetActive(state);
            }
        }
    }

    private IEnumerator RotateRingCoroutine()
    {
        AudioManager.Instance.PlayOneShotSFX("Stone");
        Transform ringTransform = ringParents[selectedRing];

        float targetAngle = selectedDirection == 1 ? -angle : angle;

        Quaternion startRotation = ringTransform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0, 0, targetAngle);

        float elapsed = 0f;
        float duration = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ringTransform.rotation = Quaternion.Lerp(startRotation, targetRotation, elapsed / duration);
            yield return null;
        }

        ringTransform.rotation = targetRotation;

        ShiftLogicalArray(selectedRing, selectedDirection);

        lastRotatedRing = selectedRing;
        lastRotationDirection = selectedDirection;

        currentState = RotationState.Idle;
        TurnManager.Instance.cardOrRingTurnPlayed = true;
    }

    private void ShiftLogicalArray(int ring, int direction)
    {
        int maxSlices = GridManager.Instance.totalSlices;
        TileData[] currentSlices = GridManager.Instance.mapGrid[ring].slices;
        TileData[] newSlices = new TileData[maxSlices];

        int shiftAmount = 2;

        for (int i = 0; i < maxSlices; i++)
        {
            int newIndex;
            if (direction == 1)
                newIndex = (i + shiftAmount) % maxSlices;
            else
                newIndex = (i - shiftAmount + maxSlices) % maxSlices;

            newSlices[newIndex] = currentSlices[i];
        }

        GridManager.Instance.mapGrid[ring].slices = newSlices;

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

            player.previousSlice = player.currentSlice;
        }
    }
}