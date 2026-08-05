using UnityEngine;
using Sirenix.OdinInspector;

public class RigidbodyGravityTrigger : MonoBehaviour
{
    [Title("組件參考")]
    [Required("需要 Rigidbody 組件")]
    [SerializeField] private Rigidbody rb;
    
    [Title("偵測設定")]
    [InfoBox("當 Rigidbody 的位移量超過閾值時，會自動啟動重力")]
    [SerializeField, Range(0.001f, 1f)] 
    private float movementThreshold = 0.01f;
    
    [SerializeField] 
    private bool useGravityWhenTriggered = true;
    
    [Title("狀態顯示")]
    [ShowInInspector, ReadOnly]
    private bool gravityEnabled = false;
    
    [ShowInInspector, ReadOnly]
    private Vector3 lastPosition;
    
    [ShowInInspector, ReadOnly, PropertySpace(5)]
    private float currentMovement = 0f;
    
    private void Awake()
    {
        // 如果沒有指定 Rigidbody，自動抓取
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
        
        if (rb != null)
        {
            lastPosition = rb.position;
            gravityEnabled = rb.useGravity;
        }
    }

    private void FixedUpdate()
    {
        if (rb == null || gravityEnabled) return;
        
        CheckMovement();
    }

    private void CheckMovement()
    {
        // 計算位移量
        Vector3 currentPosition = rb.position;
        currentMovement = Vector3.Distance(currentPosition, lastPosition);
        
        // 如果位移量超過閾值，啟動重力
        if (currentMovement >= movementThreshold)
        {
            EnableGravity();
        }
        
        lastPosition = currentPosition;
    }
    
    private void EnableGravity()
    {
        if (rb != null && !gravityEnabled)
        {
            rb.useGravity = useGravityWhenTriggered;
            gravityEnabled = true;
            Debug.Log($"{gameObject.name} 的重力已啟動！位移量: {currentMovement:F4}");
        }
    }
    
    [Button("重置重力狀態", ButtonSizes.Medium)]
    [GUIColor(0.3f, 0.8f, 0.3f)]
    private void ResetGravity()
    {
        if (rb != null)
        {
            rb.useGravity = false;
            gravityEnabled = false;
            lastPosition = rb.position;
            currentMovement = 0f;
            Debug.Log($"{gameObject.name} 重力已重置");
        }
    }
    
    [Button("手動啟動重力", ButtonSizes.Medium)]
    [GUIColor(0.8f, 0.5f, 0.3f)]
    private void ManualEnableGravity()
    {
        EnableGravity();
    }
    
    [Button("模擬位移", ButtonSizes.Medium)]
    [GUIColor(0.3f, 0.5f, 0.8f)]
    private void SimulateMovement()
    {
        if (rb != null)
        {
            rb.AddForce(Vector3.right * 5f, ForceMode.Impulse);
            Debug.Log("已施加力來模擬位移");
        }
    }
}