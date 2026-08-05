using UnityEngine;
using UnityEngine.Events;

public class SimpleTriggerChecker : MonoBehaviour
{
    [Header("要檢查的 Tag")]
    public string targetTag = "Player";

    [Header("事件：當 Tag 物件進入時")]
    public UnityEvent onEnter;

    [Header("事件：當 Tag 物件離開時")]
    public UnityEvent onExit;
    
    public bool isDestroyOnEnter = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == targetTag)
        {
            if(isDestroyOnEnter)
                Destroy(other.gameObject);
            
            onEnter?.Invoke();
        }

        Debug.Log("Trigger Enter: " + other.name);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            onExit?.Invoke();
        }
        
        Debug.Log("Trigger Exit: " + other.name);
    }
}