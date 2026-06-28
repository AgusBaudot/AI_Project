using UnityEngine;
using Core;

namespace World
{
    [RequireComponent(typeof(Renderer))]
    public class EnemyStateVisualizer : MonoBehaviour
    {
        [SerializeField] private AIEventChannel _eventChannel;
        private Renderer _renderer;

        private void Awake()
        {
            // Guaranteed to find the MeshRenderer shown in your screenshot
            _renderer = GetComponent<Renderer>();
        }

        private void Start()
        {
            // Fixes the green start issue: Forces the initial color just in case 
            // the FSM fired its event before this script was ready.
            if (_renderer != null)
            {
                _renderer.material.color = Color.green;
            }
        }

        private void OnEnable()
        {
            if (_eventChannel != null)
                _eventChannel.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_eventChannel != null)
                _eventChannel.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(string stateName)
        {
            // Fixes the NullReference crash: Blocks ghost events from ScriptableObjects
            if (_renderer == null) return;

            switch (stateName)
            {
                case "GiveUp":
                case "Patrol":
                case "ResumePatrol":
                    _renderer.material.color = Color.green;
                    break;
                case "Investigate":
                    _renderer.material.color = Color.yellow;
                    break;
                case "Attack":
                    _renderer.material.color = Color.red;
                    break;
                case "RunAway":
                    _renderer.material.color = Color.cyan;
                    break;
                case "Idle":
                    _renderer.material.color = Color.gray;
                    break;
            }
        }
    }
}