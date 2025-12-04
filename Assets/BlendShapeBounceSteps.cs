using System;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using Sirenix.OdinInspector;

public class BlendShapeBounceSteps : MonoBehaviour
{
    [Header("BlendShape 設定")]
    public string blendShapeName = "whithballshapeflat";
    public float startValue = 100f;     // 初始值
    public float minValue = 0f;         // 終點（到這裡觸發事件）
    public float stepValue = 10f;       // 每次扣多少
    public float bouncePeak = 100f;     // 每次彈到哪（可用 maxValue）
    public float duration = 0.15f;      // 彈上與回落的動畫時間

    [Header("完成時事件")]
    public UnityEvent onReachMin;

    private SkinnedMeshRenderer skinned;
    private int index;
    private float currentValue;
    private bool finished = false;

    private void Start()
    {
        skinned = GetComponent<SkinnedMeshRenderer>();
        index = skinned.sharedMesh.GetBlendShapeIndex(blendShapeName);

        if (index < 0)
        {
            Debug.LogError($"BlendShape {blendShapeName} 找不到!");
            return;
        }

        currentValue = startValue;
        skinned.SetBlendShapeWeight(index, currentValue);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Controller")
        {
            TriggerStep();
        }
    }

    /// <summary>
    /// 外部呼叫此函式，讓 BlendShape 彈一下並下降
    /// </summary>
    [Button("Trigger Step")]
    public void TriggerStep()
    {
        if (finished) return;

        float nextValue = Mathf.Max(minValue, currentValue - stepValue);

        // 停掉重複動畫
        DOTween.Kill(skinned);

        // 彈到 bouncePeak → 再回到 nextValue
        DOTween.Sequence()
            .Append(DOTween.To(
                () => skinned.GetBlendShapeWeight(index),
                v => skinned.SetBlendShapeWeight(index, v),
                bouncePeak,
                duration
            ))
            .Append(DOTween.To(
                () => skinned.GetBlendShapeWeight(index),
                v => skinned.SetBlendShapeWeight(index, v),
                nextValue,
                duration
            ));

        currentValue = nextValue;

        if (currentValue <= minValue)
        {
            finished = true;
            onReachMin?.Invoke();
        }
    }

    /// <summary>
    /// 重置到初始狀態
    /// </summary>
    [Button("Reset", ButtonSizes.Large), GUIColor(1f, 0.5f, 0.3f)]
    public void Reset()
    {
        // 停掉所有動畫
        DOTween.Kill(skinned);

        // 重置數值
        currentValue = startValue;
        finished = false;

        // 立即設定 BlendShape
        if (skinned != null && index >= 0)
        {
            skinned.SetBlendShapeWeight(index, currentValue);
        }

        Debug.Log($"[BlendShapeBounceSteps] 已重置到初始值: {startValue}");
    }

    /// <summary>
    /// 重置並播放動畫（平滑回到初始值）
    /// </summary>
    [Button("Reset with Animation", ButtonSizes.Medium), GUIColor(0.3f, 0.8f, 1f)]
    public void ResetWithAnimation()
    {
        // 停掉所有動畫
        DOTween.Kill(skinned);

        // 重置狀態
        finished = false;

        // 平滑動畫回到初始值
        if (skinned != null && index >= 0)
        {
            DOTween.To(
                () => skinned.GetBlendShapeWeight(index),
                v => skinned.SetBlendShapeWeight(index, v),
                startValue,
                duration * 2
            ).OnComplete(() =>
            {
                currentValue = startValue;
                Debug.Log($"[BlendShapeBounceSteps] 已平滑重置到初始值: {startValue}");
            });
        }
    }

    private void OnDestroy()
    {
        // 清理 DOTween
        DOTween.Kill(skinned);
    }
}