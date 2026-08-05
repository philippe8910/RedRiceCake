using UnityEngine;
using UnityEngine.Events;

public class AnimatorClipEndChecker : MonoBehaviour
{
    [Header("Animator 設定")]
    public Animator animator;
    public string targetStateName;     // 要檢查的動畫狀態名稱
    public int layerIndex = 0;         // 動畫所在的 layer

    [Header("完成時事件")]
    public UnityEvent onAnimationFinished;

    public bool eventTriggered = false;

    void Update()
    {
        if (eventTriggered) return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);

        // 1. 是否是指定動畫
        if (info.IsName(targetStateName))
        {
            // 2. 是否播完（normalizedTime ≥ 1）
            if (info.normalizedTime >= 1f)
            {
                eventTriggered = true;
                onAnimationFinished?.Invoke();
            }
        }
    }
    
    public void SetBoolean(bool value)
    {
        eventTriggered = value;
    }
}