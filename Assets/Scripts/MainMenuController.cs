using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    [Header("How To Play")]
    public GameObject howToPlayPanel;  // Kurallar paneli
    [SerializeField] Animator blackoutAnim;

    public void OnPlayClicked()
    {
        StartCoroutine(PlayGame());
    }

    public IEnumerator PlayGame()
    {
        blackoutOn();
        yield return new WaitForSeconds(1f);
        AudioManager.Instance.StopMusic();
        SceneManager.LoadScene("SampleScene");
        yield return new WaitForSeconds(0.3f);
        blackoutOff();
        yield break;
    }

    public void ShowHowToPlay()
    {
        howToPlayPanel.SetActive(true);
    }

    public void CloseHowToPlay()
    {
        howToPlayPanel.SetActive(false);
    }

    public void ExitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    public void blackoutOn(){
        blackoutAnim.SetTrigger("blackout");
    }
    public void blackoutOff(){
        blackoutAnim.SetTrigger("blackoutoff");
    }
    void Start()
    {
        AudioManager.Instance.PlayMusic("Main Menu Music");
    }
}