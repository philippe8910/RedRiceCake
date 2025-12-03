using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class SkinnedColorTriggerController : MonoBehaviour
{
    [Header("顏色設定")]
    public Color targetColor = Color.red;
    public int requiredSteps = 5;
    public float tweenDuration = 0.3f;

    [Header("完成事件")]
    public UnityEvent onCompleted;

    private SkinnedMeshRenderer skinned;
    private Material mat;
    private Color startColor;
    private int currentStep = 0;
    private bool done = false;

    void Start()
    {
        skinned = GetComponent<SkinnedMeshRenderer>();
        mat = skinned.material;          // 取得材質實例
        startColor = mat.color;
    }

    /// <summary>
    /// 外部呼叫一次 → 推進顏色一步
    /// </summary>
    public void TriggerStep()
    {
        if (done) return;

        currentStep++;
        currentStep = Mathf.Clamp(currentStep, 0, requiredSteps);

        float progress = (float)currentStep / requiredSteps;
        Color nextColor = Color.Lerp(startColor, targetColor, progress);

        mat.DOKill();                                   // 停止舊動畫
        mat.DOColor(nextColor, tweenDuration);          // 顏色推進

        if (currentStep >= requiredSteps)
        {
            done = true;
            onCompleted?.Invoke();
        }
    }

    /// <summary>
    /// 重置：顏色回到起點、步數清零、可再次使用
    /// </summary>
    public void ResetColor()
    {
        done = false;
        currentStep = 0;

        mat.DOKill();
        mat.DOColor(startColor, tweenDuration);
    }
}
