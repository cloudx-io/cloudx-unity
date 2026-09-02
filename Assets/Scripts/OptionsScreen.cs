using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
 * Demo-only launch screen. Picks which demo flow to enter. General routes to
 * GeneralScene, where the CloudX integration sample lives; First Look routes to
 * FirstLookScene, the CloudX-first-with-AdMob-fallback template. Arbiter/TPA is
 * not implemented yet, so its button stays hidden.
 */
public class OptionsScreen : MonoBehaviour
{
    private const string TAG = "CloudXUnityDemo";
    private const string GeneralSceneName = "GeneralScene";
    private const string FirstLookSceneName = "FirstLookScene";

    public Button generalButton;
    public Button firstLookButton;
    public Button arbiterTpaButton;

    private void Start()
    {
        generalButton.onClick.AddListener(OpenGeneral);
        firstLookButton.onClick.AddListener(OpenFirstLook);

        /* Nothing to route to yet; hide the button rather than leave it dead. */
        arbiterTpaButton.gameObject.SetActive(false);
    }

    private void OpenGeneral()
    {
        Debug.Log($"[{TAG}] Opening the General demo");
        SceneManager.LoadScene(GeneralSceneName);
    }

    private void OpenFirstLook()
    {
        Debug.Log($"[{TAG}] Opening the First Look demo");
        SceneManager.LoadScene(FirstLookSceneName);
    }
}
