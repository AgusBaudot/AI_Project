using UnityEngine;
using UnityEngine.UI;

namespace Core
{
    public class ExitGameButton : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(HandleOnClick);
        }

        private void HandleOnClick() => Application.Quit();
    }
}