using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform _player;
    
    void Update()
    {
        transform.position = new Vector3(_player.transform.position.x, 10f, _player.transform.position.z);
    }
}
