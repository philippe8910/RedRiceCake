using UnityEngine;

public class FollowTarget : MonoBehaviour
{
    [Header("目標設定")]
    [SerializeField] private Transform target;
    
    [Header("跟隨設定")]
    [SerializeField] private bool followPosition = true;
    [SerializeField] private bool followRotation = true;
    [SerializeField] private bool followScale = false;
    
    [Header("位置跟隨")]
    [SerializeField] private bool smoothPosition = true;
    [SerializeField, Range(0f, 20f)] private float positionSpeed = 10f;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    
    [Header("旋轉跟隨")]
    [SerializeField] private bool smoothRotation = true;
    [SerializeField, Range(0f, 20f)] private float rotationSpeed = 10f;
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;
    
    [Header("縮放跟隨")]
    [SerializeField] private bool smoothScale = true;
    [SerializeField, Range(0f, 20f)] private float scaleSpeed = 10f;
    [SerializeField] private Vector3 scaleMultiplier = Vector3.one;
    
    [Header("進階設定")]
    [SerializeField] private UpdateMode updateMode = UpdateMode.Update;
    [SerializeField] private Space offsetSpace = Space.World;
    
    public enum UpdateMode
    {
        Update,
        LateUpdate,
        FixedUpdate
    }
    
    // Public 屬性
    public Transform Target
    {
        get => target;
        set => target = value;
    }
    
    private void Update()
    {
        if (updateMode == UpdateMode.Update)
            FollowUpdate(Time.deltaTime);
    }
    
    private void LateUpdate()
    {
        if (updateMode == UpdateMode.LateUpdate)
            FollowUpdate(Time.deltaTime);
    }
    
    private void FixedUpdate()
    {
        if (updateMode == UpdateMode.FixedUpdate)
            FollowUpdate(Time.fixedDeltaTime);
    }
    
    private void FollowUpdate(float deltaTime)
    {
        if (target == null)
            return;
        
        // 位置跟隨
        if (followPosition)
        {
            Vector3 targetPosition = target.position;
            
            // 套用偏移
            if (offsetSpace == Space.World)
                targetPosition += positionOffset;
            else
                targetPosition += target.TransformDirection(positionOffset);
            
            if (smoothPosition)
                transform.position = Vector3.Lerp(transform.position, targetPosition, positionSpeed * deltaTime);
            else
                transform.position = targetPosition;
        }
        
        // 旋轉跟隨
        if (followRotation)
        {
            Quaternion targetRotation = target.rotation * Quaternion.Euler(rotationOffset);
            
            if (smoothRotation)
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * deltaTime);
            else
                transform.rotation = targetRotation;
        }
        
        // 縮放跟隨
        if (followScale)
        {
            Vector3 targetScale = Vector3.Scale(target.localScale, scaleMultiplier);
            
            if (smoothScale)
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, scaleSpeed * deltaTime);
            else
                transform.localScale = targetScale;
        }
    }
    
    /// <summary>
    /// 設定目標物件
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    /// <summary>
    /// 立即對齊到目標位置(無平滑)
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null)
            return;
        
        if (followPosition)
        {
            Vector3 targetPosition = target.position;
            if (offsetSpace == Space.World)
                targetPosition += positionOffset;
            else
                targetPosition += target.TransformDirection(positionOffset);
            transform.position = targetPosition;
        }
        
        if (followRotation)
        {
            transform.rotation = target.rotation * Quaternion.Euler(rotationOffset);
        }
        
        if (followScale)
        {
            transform.localScale = Vector3.Scale(target.localScale, scaleMultiplier);
        }
    }
}