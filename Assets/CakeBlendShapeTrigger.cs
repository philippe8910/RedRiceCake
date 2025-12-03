using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using Sirenix.OdinInspector;

public class CakeBlendShapeTrigger : MonoBehaviour
{
    [Header("Trigger 設定")]
    public string targetTag = "Cake";

    [Header("BlendShape 設定")]
    public string blendShapeName = "whithballshapeflat"; 
    public float minValue = 0f;
    public float maxValue = 100f;
    public float duration = 0.2f;   // 來回動畫時間
    
    public UnityEvent onTrigger;

    private SkinnedMeshRenderer skinned;
    private int blendShapeIndex;

    private void Start()
    {
        skinned = GetComponent<SkinnedMeshRenderer>();

        // 找 BlendShape Index
        blendShapeIndex = skinned.sharedMesh.GetBlendShapeIndex(blendShapeName);

        if (blendShapeIndex == -1)
        {
            Debug.LogError($"找不到 BlendShape：{blendShapeName}");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(targetTag)) return;

        PlayBlendShapeTween();
    }

    
    [Button("Test")]
    private void PlayBlendShapeTween()
    {
        onTrigger?.Invoke();
        
        // 停掉舊動畫以免衝突
        DOTween.Kill(skinned);

        // 先拉到 max → 再回到 min
        DOTween.Sequence()
            .Append(DOTween.To(
                () => skinned.GetBlendShapeWeight(blendShapeIndex),
                v => skinned.SetBlendShapeWeight(blendShapeIndex, v),
                maxValue,
                duration
            ))
            .Append(DOTween.To(
                () => skinned.GetBlendShapeWeight(blendShapeIndex),
                v => skinned.SetBlendShapeWeight(blendShapeIndex, v),
                minValue,
                duration
            ));
    }
}