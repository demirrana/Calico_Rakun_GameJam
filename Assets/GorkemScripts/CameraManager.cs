using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance;

    [Header("Kamera Ayarlarý")]
    public Camera mainCamera;
    public float rotationDuration = 1.2f;
    [Tooltip("Zoom miktarýný belirler. (Örn: 0.7 = %30 yakýnlaþtýrýr)")]
    public float zoomMultiplier = 0.7f;

    private float originalOrthographicSize;
    private Vector3 originalPosition;

    private bool isPlayer1View = true;

    public Button button1;
    public Button button2;
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (mainCamera == null) mainCamera = Camera.main;

        if (mainCamera.orthographic)
            originalOrthographicSize = mainCamera.orthographicSize;
        else
            originalPosition = mainCamera.transform.position;
    }

    public void SwitchTurnView(bool toPlayer1)
    {
        if (isPlayer1View == toPlayer1) return; 

        isPlayer1View = toPlayer1;

        StartCoroutine(AnimateCamera(toPlayer1 ? 0f : 180f));
    }

    private IEnumerator AnimateCamera(float targetZRotation)
    {
        TurnManager.Instance.isCameraMoving = true;

        float startZRotation = mainCamera.transform.eulerAngles.z;
        float elapsed = 0f;

        TurnManager.Instance.invertControls = !isPlayer1View;
        if (RingRotationManager.Instance != null)
            RingRotationManager.Instance.invertControls = !isPlayer1View;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rotationDuration;
            float smoothT = Mathf.SmoothStep(0, 1, t);

            float zRot = Mathf.LerpAngle(startZRotation, targetZRotation, smoothT);
            mainCamera.transform.rotation = Quaternion.Euler(0, 0, zRot);

            float zoomFactor = Mathf.Sin(t * Mathf.PI);

            if (mainCamera.orthographic)
            {
                mainCamera.orthographicSize = Mathf.Lerp(originalOrthographicSize, originalOrthographicSize * zoomMultiplier, zoomFactor);
            }
            else
            {
                Vector3 zoomedPos = originalPosition + mainCamera.transform.forward * (zoomMultiplier * 5f);
                mainCamera.transform.position = Vector3.Lerp(originalPosition, zoomedPos, zoomFactor);
            }

            yield return null;
        }

        mainCamera.transform.rotation = Quaternion.Euler(0, 0, targetZRotation);
        if (mainCamera.orthographic) mainCamera.orthographicSize = originalOrthographicSize;
        else mainCamera.transform.position = originalPosition;

        TurnManager.Instance.isCameraMoving = false;
    }
}