using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core
{
    /// <summary>
    /// Splash Screen Controller.
    ///
    /// Displays developer names and contact info, then advances to the Main Menu
    /// either automatically after _displayDuration seconds or immediately on
    /// any click/tap.
    /// </summary>
    [AddComponentMenu("UI/Splash Screen")]
    public class SplashScreen : MonoBehaviour
    {
        [Header("Timing")] [Tooltip("Seconds before automatically advancing to Main Menu.")] [SerializeField]
        private float _displayDuration = 4f;

        [Header("Optional")]
        [Tooltip("If assigned, clicking this button skips immediately. " +
                 "If left empty, any mouse click skips.")]
        [SerializeField]
        private Button _skipButton;

        [Tooltip("Fade CanvasGroup out before loading. Leave null to skip fade.")] [SerializeField]
        private CanvasGroup _fadeGroup;

        [SerializeField] private float _fadeDuration = 0.4f;

        private float _timer;
        private bool _advancing;

        private void Start()
        {
            _timer = _displayDuration;
            if (_skipButton != null)
                _skipButton.onClick.AddListener(Advance);
        }

        private void Update()
        {
            if (_advancing) return;

            if (_skipButton == null && Input.GetMouseButtonDown(0))
            {
                Advance();
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                Advance();
        }

        private void Advance()
        {
            if (_advancing) return;
            _advancing = true;

            if (_fadeGroup != null)
                StartCoroutine(FadeAndLoad());
            else
                LoadMainMenu();
        }

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

            LoadMainMenu();
        }

        private static void LoadMainMenu() => SceneManager.LoadScene(1);
    }
}