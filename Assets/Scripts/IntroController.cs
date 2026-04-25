using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class IntroController : MonoBehaviour
{
    [Header("UI References")]
    public Image background;
    public Image logo;
    public TextMeshProUGUI teamText;

    [Header("Timing")]
    public float fadeInDuration = 1.5f;
    public float displayDuration = 4.0f;
    public float fadeOutDuration = 1.0f;

    void Start()
    {
        // Başlangıçta logo ve text görünmez
        SetAlpha(logo, 0f);
        SetAlpha(teamText, 0f);
        background.color = Color.black;

        StartCoroutine(IntroSequence());
    }

    IEnumerator IntroSequence()
    {
        // Biraz karanlıkta bekle
        yield return new WaitForSeconds(0.5f);

        // Logo fade in
        yield return StartCoroutine(FadeIn(logo, fadeInDuration));

        // Text fade in
        yield return StartCoroutine(FadeIn(teamText, 0.8f));

        // Ekranda bekle
        yield return new WaitForSeconds(displayDuration);

        // Her şeyi fade out (karart)
        yield return StartCoroutine(FadeOutAll(fadeOutDuration));

        // MainMenu'ye geç
        SceneManager.LoadScene("MainMenu");
    }

    IEnumerator FadeIn(Graphic element, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            SetAlpha(element, alpha);
            yield return null;
        }
        SetAlpha(element, 1f);
    }

    IEnumerator FadeOutAll(float duration)
    {
        float elapsed = 0f;
        // Başlangıç alpha değerlerini kaydet
        float logoAlpha = logo.color.a;
        float textAlpha = teamText.color.a;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(logo, Mathf.Lerp(logoAlpha, 0f, t));
            SetAlpha(teamText, Mathf.Lerp(textAlpha, 0f, t));
            yield return null;
        }

        SetAlpha(logo, 0f);
        SetAlpha(teamText, 0f);
    }

    void SetAlpha(Graphic element, float alpha)
    {
        Color c = element.color;
        c.a = alpha;
        element.color = c;
    }
}