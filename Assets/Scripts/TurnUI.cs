using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sol üstte, sıradaki oyuncunun sprite'ını ve rengini gösteren UI panel.
///
/// İki event'i dinler:
///   - OnGameStateChanged: setup phase'lerinde de hangi oyuncunun yerleştiğini
///     gösterebilelim diye.
///   - OnTurnStarted: Playing'de her sıra geçişinde günceller.
///
/// GameManager'a sprite ve renk için getter çağırır; ayrı bir kaynaktan bilgi
/// almak yerine her şey tek noktada (GameManager) saklı kalsın diye.
/// </summary>
public class TurnUI : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private GameManager gameManager;

    [Tooltip("Oyuncu sprite'ını gösterecek Image (öndeki portrait).")]
    [SerializeField] private Image portraitImage;

    [Tooltip("Portrait'in arkasındaki çerçeve/arkaplan. Oyuncu rengiyle boyanır. " +
             "Atanmazsa boyama atlanır (görsel olarak gerek yoksa silebilirsin).")]
    [SerializeField] private Image backgroundImage;

    // ========== EVENT SUBSCRIPTION ==========

    private void OnEnable()
    {
        GameManager.OnGameStateChanged += HandleStateChanged;
        GameManager.OnTurnStarted += HandleTurnStarted;
    }

    private void OnDisable()
    {
        GameManager.OnGameStateChanged -= HandleStateChanged;
        GameManager.OnTurnStarted -= HandleTurnStarted;
    }

    // ========== HANDLERS ==========

    /// <summary>
    /// Setup phase'lerinde UI'ı uygun oyuncuya kur. Playing'de bir şey yapma —
    /// HandleTurnStarted az sonra zaten gelecek ve doğru oyuncuyu set edecek.
    /// (İki handler aynı state'te iki kere update yapsa zararsız ama tek noktadan
    /// güncelleme tutarlı olsun diye.)
    /// </summary>
    private void HandleStateChanged(GameManager.GameState state)
    {
        switch (state)
        {
            case GameManager.GameState.SettingUpP1:
                ShowFor(0);
                break;
            case GameManager.GameState.SettingUpP2:
                ShowFor(1);
                break;
            // GameState.Playing: OnTurnStarted halledecek.
        }
    }

    /// <summary>Her sıra başlangıcında çağrılır.</summary>
    private void HandleTurnStarted(int playerIndex)
    {
        ShowFor(playerIndex);
    }

    /// <summary>
    /// Verilen oyuncu indeksine göre sprite ve arkaplan rengini günceller.
    /// </summary>
    private void ShowFor(int playerIndex)
    {
        if (gameManager == null) return;

        Sprite sprite = gameManager.GetPlayerSprite(playerIndex);
        Color color = gameManager.GetPlayerColor(playerIndex);

        if (portraitImage != null)
        {
            portraitImage.sprite = sprite;
            // Sprite null gelirse Image otomatik boş kalır; ekstra null kontrolü gereksiz.

            // enabled: sprite olmasa bile arkadaki Image kaybolmasın diye true bırakıyoruz.
            // İstersen sprite null ise portrait'i kapat:
            // portraitImage.enabled = (sprite != null);
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = color;
        }
    }
}