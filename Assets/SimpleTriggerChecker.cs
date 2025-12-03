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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            Destroy(other.gameObject);
            onEnter?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            onExit?.Invoke();
        }
    }
}