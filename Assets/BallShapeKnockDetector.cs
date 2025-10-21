using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using System.Collections.Generic;
using Sirenix.OdinInspector;

public class BallShapeKnockDetector : MonoBehaviour
{
    [Header("偵測設定")]
    [SerializeField] private Transform detectionCenter; // SphereCast 中心點
    [SerializeField] private float detectionRadius = 0.05f; // 偵測半徑
    [SerializeField] private LayerMask targetLayer; // 要偵測的 Layer
    [SerializeField] private bool showDebugGizmos = true;
    
    [Header("BlendShape 設定")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private string blendShapeName = "Ball shape"; // BlendShape 名稱
    [SerializeField, Range(0f, 100f)] private float maxBlendShapeValue = 100f; // 最大變形值
    [SerializeField, Range(0f, 100f)] private float minBlendShapeValue = 0f; // 最小變形值(回彈值)
    
    [Header("分段觸發設定")]
    [SerializeField, Range(1, 10)] private int triggerSegments = 3; // 分成幾段
    [SerializeField] private float[] segmentThresholds; // 每段的閾值(自動計算)
    
    [Header("動畫設定")]
    [SerializeField] private float knockDuration = 0.2f; // 敲擊下壓時間
    [SerializeField] private float returnDuration = 0.3f; // 回彈時間
    [SerializeField] private Ease knockEase = Ease.OutQuad;
    [SerializeField] private Ease returnEase = Ease.OutBounce;
    
    [Header("冷卻設定")]
    [SerializeField] private float cooldownTime = 0.1f; // 觸發冷卻時間
    
    [Title("事件設定")]
    [FoldoutGroup("事件")]
    [LabelText("段數觸發事件")]
    public UnityEvent<int> onSegmentTriggered; // 段數觸發事件 (傳入段數)
    
    [FoldoutGroup("事件")]
    [LabelText("完成所有段數事件")]
    public UnityEvent onKnockComplete; // 完成所有段數
    
    [FoldoutGroup("事件")]
    [LabelText("物體進入偵測")]
    public UnityEvent<GameObject> onObjectEnter;
    
    [FoldoutGroup("事件")]
    [LabelText("物體離開偵測")]
    public UnityEvent<GameObject> onObjectExit;
    
    // 私有變數
    private int blendShapeIndex = -1;
    private HashSet<Collider> collidersInRange = new HashSet<Collider>();
    private HashSet<Collider> previousFrameColliders = new HashSet<Collider>();
    private int currentSegment = 0;
    private float lastTriggerTime = 0f;
    private bool isAnimating = false;
    private Tween currentTween;
    
    private void Start()
    {
        InitializeBlendShape();
        CalculateSegmentThresholds();
        
        if (detectionCenter == null)
            detectionCenter = transform;
    }
    
    private void InitializeBlendShape()
    {
        if (skinnedMeshRenderer == null)
        {
            skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer == null)
            {
                Debug.LogError("找不到 SkinnedMeshRenderer!", this);
                return;
            }
        }
        
        // 找到 BlendShape 索引
        blendShapeIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(blendShapeName);
        
        if (blendShapeIndex == -1)
        {
            Debug.LogError($"找不到名為 '{blendShapeName}' 的 BlendShape!", this);
        }
        else
        {
            // 初始化為最小值
            skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, minBlendShapeValue);
        }
    }
    
    private void CalculateSegmentThresholds()
    {
        segmentThresholds = new float[triggerSegments];
        for (int i = 0; i < triggerSegments; i++)
        {
            segmentThresholds[i] = minBlendShapeValue + 
                (maxBlendShapeValue - minBlendShapeValue) * (i + 1) / triggerSegments;
        }
    }
    
    private void Update()
    {
        DetectColliders();
        CheckForKnock();
    }
    
    private void DetectColliders()
    {
        // 儲存上一幀的碰撞體
        previousFrameColliders.Clear();
        previousFrameColliders.UnionWith(collidersInRange);
        
        // 清空當前碰撞體列表
        collidersInRange.Clear();
        
        // 使用 OverlapSphere 偵測範圍內的碰撞體
        Collider[] hitColliders = Physics.OverlapSphere(
            detectionCenter.position, 
            detectionRadius, 
            targetLayer
        );
        
        foreach (var collider in hitColliders)
        {
            collidersInRange.Add(collider);
        }
    }
    
    private void CheckForKnock()
    {
        // 檢查是否有新的碰撞體進入
        foreach (var collider in collidersInRange)
        {
            if (!previousFrameColliders.Contains(collider))
            {
                // 新進入的碰撞體
                OnColliderEnter(collider);
            }
        }
        
        // 檢查是否有碰撞體離開
        foreach (var collider in previousFrameColliders)
        {
            if (!collidersInRange.Contains(collider))
            {
                // 離開的碰撞體
                OnColliderExit(collider);
            }
        }
    }
    
    private void OnColliderEnter(Collider collider)
    {
        Debug.Log($"物體進入偵測範圍: {collider.name}");
        onObjectEnter?.Invoke(collider.gameObject);
    }
    
    private void OnColliderExit(Collider collider)
    {
        Debug.Log($"物體離開偵測範圍: {collider.name}");
        onObjectExit?.Invoke(collider.gameObject);
        
        // 只有在離開時才觸發敲擊(進來又出去才算一次)
        TriggerKnock();
    }
    
    private void TriggerKnock()
    {
        // 檢查冷卻時間
        if (Time.time - lastTriggerTime < cooldownTime)
            return;
        
        // 檢查是否還有段數可以觸發
        if (currentSegment >= triggerSegments)
        {
            Debug.Log("已完成所有段數!");
            return;
        }
        
        lastTriggerTime = Time.time;
        currentSegment++;
        
        Debug.Log($"觸發第 {currentSegment} 段!");
        
        // 觸發事件
        onSegmentTriggered?.Invoke(currentSegment);
        
        // 播放動畫
        PlayKnockAnimation(currentSegment);
        
        // 檢查是否完成所有段數
        if (currentSegment >= triggerSegments)
        {
            Debug.Log("完成所有段數!");
            onKnockComplete?.Invoke();
        }
    }
    
    private void PlayKnockAnimation(int segment)
    {
        if (blendShapeIndex == -1 || isAnimating)
            return;
        
        isAnimating = true;
        
        // 停止之前的動畫
        currentTween?.Kill();
        
        // 計算目標值
        float targetValue = segmentThresholds[segment - 1];
        
        // 創建動畫序列
        Sequence sequence = DOTween.Sequence();
        
        // 下壓動畫
        sequence.Append(
            DOTween.To(
                () => skinnedMeshRenderer.GetBlendShapeWeight(blendShapeIndex),
                x => skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, x),
                targetValue,
                knockDuration
            ).SetEase(knockEase)
        );
        
        // 回彈動畫 (回到上一段的值,如果是第一段則回到 minBlendShapeValue)
        float returnValue = segment > 1 ? segmentThresholds[segment - 2] : minBlendShapeValue;
        sequence.Append(
            DOTween.To(
                () => skinnedMeshRenderer.GetBlendShapeWeight(blendShapeIndex),
                x => skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, x),
                returnValue,
                returnDuration
            ).SetEase(returnEase)
        );
        
        sequence.OnComplete(() => {
            isAnimating = false;
        });
        
        currentTween = sequence;
    }
    
    #region 公開方法
    
    /// <summary>
    /// 重置觸發計數
    /// </summary>
    [Button("重置敲擊計數", ButtonSizes.Medium)]
    [GUIColor(1f, 0.8f, 0.3f)]
    public void ResetKnock()
    {
        currentSegment = 0;
        currentTween?.Kill();
        
        // 重置 BlendShape
        if (blendShapeIndex != -1)
        {
            DOTween.To(
                () => skinnedMeshRenderer.GetBlendShapeWeight(blendShapeIndex),
                x => skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, x),
                minBlendShapeValue,
                0.3f
            ).SetEase(Ease.OutQuad);
        }
        
        isAnimating = false;
        Debug.Log("重置敲擊計數");
    }
    
    /// <summary>
    /// 手動觸發敲擊(用於測試)
    /// </summary>
    [Button("手動觸發敲擊", ButtonSizes.Medium)]
    [GUIColor(0.3f, 1f, 0.3f)]
    public void ManualTrigger()
    {
        TriggerKnock();
    }
    
    /// <summary>
    /// 取得當前段數
    /// </summary>
    public int GetCurrentSegment()
    {
        return currentSegment;
    }
    
    /// <summary>
    /// 取得總段數
    /// </summary>
    public int GetTotalSegments()
    {
        return triggerSegments;
    }
    
    /// <summary>
    /// 檢查是否已完成所有段數
    /// </summary>
    public bool IsComplete()
    {
        return currentSegment >= triggerSegments;
    }
    
    #endregion
    
    #region Gizmos 和除錯
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || detectionCenter == null)
            return;
        
        // 繪製偵測範圍
        Gizmos.color = collidersInRange.Count > 0 ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(detectionCenter.position, detectionRadius);
        
        // 繪製當前段數的視覺化
        if (Application.isPlaying && blendShapeIndex != -1)
        {
            Gizmos.color = Color.cyan;
            float progress = currentSegment / (float)triggerSegments;
            Gizmos.DrawWireSphere(detectionCenter.position + Vector3.up * 0.1f, detectionRadius * progress);
        }
    }
    
    private void OnValidate()
    {
        // 當 Inspector 值改變時重新計算
        if (Application.isPlaying)
        {
            CalculateSegmentThresholds();
        }
    }
    
    #endregion
}