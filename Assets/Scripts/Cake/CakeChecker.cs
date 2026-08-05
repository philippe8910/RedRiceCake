using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

public class CakeChecker : MonoBehaviour
{
    [Title("要檢查的物件列表")]
    [InfoBox("將需要檢查的 GameObject 加入這個列表")]
    [SerializeField] private List<GameObject> objectsToCheck = new List<GameObject>();
    
    [Title("事件設定")]
    [InfoBox("當所有物件都關閉時，會執行此事件（僅執行一次）")]
    [SerializeField] private UnityEvent onAllObjectsClosed;
    
    [Title("狀態顯示")]
    [ShowInInspector, ReadOnly, PropertySpace(10)]
    private bool hasTriggered = false;
    
    [ShowInInspector, ReadOnly]
    private int ActiveCount => objectsToCheck.FindAll(obj => obj != null && obj.activeSelf).Count;
    
    [ShowInInspector, ReadOnly]
    private int TotalCount => objectsToCheck.Count;

    private void Update()
    {
        CheckAllObjectsClosed();
    }

    private void CheckAllObjectsClosed()
    {
        // 如果已經觸發過，就不再檢查
        if (hasTriggered) return;
        
        // 如果列表是空的，不做任何事
        if (objectsToCheck.Count == 0) return;
        
        // 檢查是否所有物件都關閉
        bool allClosed = true;
        foreach (var obj in objectsToCheck)
        {
            if (obj != null && obj.activeSelf)
            {
                allClosed = false;
                break;
            }
        }
        
        // 如果所有物件都關閉，執行事件
        if (allClosed)
        {
            hasTriggered = true;
            onAllObjectsClosed?.Invoke();
            Debug.Log("所有物件都已關閉！事件已觸發。");
        }
    }
    
    [Button("重置狀態", ButtonSizes.Medium)]
    [GUIColor(0.3f, 0.8f, 0.3f)]
    private void ResetTrigger()
    {
        hasTriggered = false;
        Debug.Log("CakeChecker 已重置");
    }
    
    [Button("測試：關閉所有物件", ButtonSizes.Medium)]
    [GUIColor(0.8f, 0.3f, 0.3f)]
    private void CloseAllObjects()
    {
        foreach (var obj in objectsToCheck)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
        Debug.Log("已關閉所有物件");
    }
}