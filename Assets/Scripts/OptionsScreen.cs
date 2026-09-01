using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
 * Demo-only launch screen. Picks which demo flow to enter. General routes to
 * GeneralScene, where the CloudX integration sample lives; First Look routes to
 * FirstLookScene, the CloudX-first-with-AdMob-fallback template; Arbiter/TPA is WIP.
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
        arbiterTpaButton.onClick.AddListener(OpenArbiterTpa);
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

    private void OpenArbiterTpa() => throw new NotImplementedException("Arbiter/TPA demo is not implemented yet");
}
