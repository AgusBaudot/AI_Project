using UnityEngine;

namespace Core
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private GameObject _winPanel;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                Time.timeScale = 0;
                _winPanel.SetActive(true);
            }
        }
    }
}