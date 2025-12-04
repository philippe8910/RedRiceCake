using UnityEngine;
using Sirenix.OdinInspector;

public class PrefabSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private Transform target;
    
    public void Spawn()
    {
        Instantiate(prefab, target.position, target.rotation);
    }
}