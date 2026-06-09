using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core
{
    /// <summary>
    /// Main Menu Controller.
    ///
    /// Wires the Play button to load the game scene and the Quit button
    /// to exit the application. Optionally fades the canvas out before loading.
    /// </summary>
    [AddComponentMenu("UI/Main Menu Controller")]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Buttons")] [SerializeField] private Button _playButton;
        [SerializeField] private Button _quitButton;

        [Header("Transition")]
        [Tooltip("Assign to get a fade-out before loading. Leave null to load instantly.")]
        [SerializeField]
        private CanvasGroup _fadeGroup;

        [SerializeField] private float _fadeDuration = 0.35f;

        private void Start()
        {
            if (_playButton != null)
                _playButton.onClick.AddListener(OnPlayClicked);

            if (_quitButton != null)
                _quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void OnPlayClicked()
        {
            // Disable buttons immediately to prevent double-clicks
            if (_playButton != null) _playButton.interactable = false;
            if (_quitButton != null) _quitButton.interactable = false;

            if (_fadeGroup != null)
                StartCoroutine(FadeAndLoad());
            else
                LoadGame();
        }

        private void OnQuitClicked() => Application.Quit();

        private System.Collections.IEnumerator FadeAndLoad()
        {
            float elapsed = 0f;
            float startAlpha = _fadeGroup.alpha;

            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                _fadeGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / _fadeDuration);
                yield return null;
            }

            LoadGame();
        }

        // Build index 2 = Game scene
        private static void LoadGame() => SceneManager.LoadScene(2);
    }
}