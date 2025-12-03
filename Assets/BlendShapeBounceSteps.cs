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
}
