using UnityEngine;
using UnityEngine.UI;

namespace Core
{
    public class PlayAgainButton : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(HandleOnClick);
        }

        private void HandleOnClick()
        {
            Time.timeScale = 1;
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }
}