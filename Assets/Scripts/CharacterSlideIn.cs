using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CharacterSlideIn : MonoBehaviour
{
    public enum SlideDirection { Left, Right }

    [Header("Settings")]
    public SlideDirection direction = SlideDirection.Left;
    public float slideDuration = 1.0f;
    public float startDelay = 0.3f;

    private RectTransform rectTransform;
    private Vector3 targetPos;
    private Animator animator;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        animator = GetComponent<Animator>();
        targetPos = rectTransform.localPosition;

        // Slide bitene kadar animatörü kapat
        if (animator != null)
            animator.enabled = false;

        Canvas canvas = GetComponentInParent<Canvas>();
        float screenWidth = canvas.GetComponent<RectTransform>().sizeDelta.x;

        float offscreen = screenWidth;
        if (direction == SlideDirection.Left)
            rectTransform.localPosition = new Vector3(-offscreen, targetPos.y, targetPos.z);
        else
            rectTransform.localPosition = new Vector3(offscreen, targetPos.y, targetPos.z);

        StartCoroutine(SlideIn());
    }

    IEnumerator SlideIn()
    {
        yield return new WaitForSeconds(startDelay);

        float elapsed = 0f;
        Vector3 startPos = rectTransform.localPosition;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            rectTransform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        rectTransform.localPosition = targetPos;

        // Slide bitti, idle animasyonu başlasın
        if (animator != null)
            animator.enabled = true;
    }
}