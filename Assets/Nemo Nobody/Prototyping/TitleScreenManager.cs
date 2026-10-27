using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TitleScreenManager : MonoBehaviour
{
    [Header("UI Buttons")]
    [Tooltip("The button used to begin the miniature (e.g. 'Step Onto Platform Nº 1').")]
    [SerializeField] private Button startButton;

    [Tooltip("The button used to quit (e.g. 'Leave The Station').")]
    [SerializeField] private Button quitButton;

    [Header("Scene Transition")]
    [Tooltip("The exact name of the playable scene.")]
    [SerializeField] private string targetSceneName = "OutdoorsScene"; //[cite: 6]

    [Tooltip("Optional delay in seconds before loading (useful for audio stings or fadeouts).")]
    [SerializeField] private float loadDelay = 0f;

    private void OnEnable()
    {
        startButton.onClick.AddListener(HandleStartClicked);

        quitButton.onClick.AddListener(HandleQuitClicked);
    }

    private void OnDisable()
    {
        startButton.onClick.RemoveListener(HandleStartClicked);

        quitButton.onClick.RemoveListener(HandleQuitClicked);
    }

    private void HandleStartClicked()
    {
        // Disable button to prevent multi-click during transition delay
        startButton.interactable = false;

        if (loadDelay > 0f)
        {
            Invoke(nameof(LoadTargetScene), loadDelay);
        }
        else
        {
            LoadTargetScene();
        }
    }

    private void LoadTargetScene()
    {
        SceneManager.LoadScene(targetSceneName); //[cite: 6]
    }

    private void HandleQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}