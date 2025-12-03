using System;
using System.Collections.Generic;
using UnityEngine;

public class WhiteCakePlaceInPotHolder : MonoBehaviour
{
    [Header("Target 設定")]
    public string targetTag = "Target";       // 用 Tag 判斷目標 (可改)
    public GameObject targetPrefab;           // 要生成的 Prefab
    public Transform spawnPoint;              // 生成位置（可選）

    private HashSet<GameObject> objectsInside = new HashSet<GameObject>();

    private void Start()
    {
        SpawnNewTarget();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            objectsInside.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            objectsInside.Remove(other.gameObject);

            // ⚠️ 當最後一個 Target 離開
            if (objectsInside.Count == 0)
            {
                SpawnNewTarget();
            }
        }
    }

    private void SpawnNewTarget()
    {
        Vector3 spawnPos = spawnPoint ? spawnPoint.position : transform.position;
        Instantiate(targetPrefab, spawnPos, Quaternion.identity);
    }
}