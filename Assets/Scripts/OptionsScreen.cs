using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*
 * Demo-only launch screen. Picks which demo flow to enter. General routes to
 * HomeScene, where the CloudX integration sample lives; the other two are WIP.
 */
public class OptionsScreen : MonoBehaviour
{
    private const string TAG = "CloudXUnityDemo";
    private const string HomeSceneName = "HomeScene";

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
        SceneManager.LoadScene(HomeSceneName);
    }

    private void OpenFirstLook() => throw new NotImplementedException("First Look demo is not implemented yet");

    private void OpenArbiterTpa() => throw new NotImplementedException("Arbiter/TPA demo is not implemented yet");
}
