using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class SimpleCountdownTimer : MonoBehaviour
{
    [Header("UI 設定")]
    [SerializeField] private Image fillImage;
    
    [Header("倒數設定")]
    [SerializeField] private float countdownTime = 10f;
    [SerializeField] private bool autoStart = false;
    
    [Header("事件")]
    public UnityEvent onCountdownComplete;
    
    private float currentTime;
    private bool isRunning = false;
    
    private void Start()
    {
        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillAmount = 1f;
        }
        
        currentTime = countdownTime;
        
        if (autoStart)
        {
            StartCountdown();
        }
    }
    
    private void Update()
    {
        if (!isRunning)
            return;
        
        currentTime -= Time.deltaTime;
        
        // 更新填充圖片
        if (fillImage != null)
        {
            fillImage.fillAmount = currentTime / countdownTime;
        }
        
        // 檢查是否完成
        if (currentTime <= 0f)
        {
            currentTime = 0f;
            isRunning = false;
            
            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
            }
            
            onCountdownComplete?.Invoke();
            Debug.Log("倒數完成!");
        }
    }
    
    /// <summary>
    /// 開始倒數
    /// </summary>
    public void StartCountdown()
    {
        currentTime = countdownTime;
        isRunning = true;
        
        if (fillImage != null)
        {
            fillImage.fillAmount = 1f;
        }
    }
    
    /// <summary>
    /// 停止倒數
    /// </summary>
    public void StopCountdown()
    {
        isRunning = false;
    }
    
    /// <summary>
    /// 重置倒數
    /// </summary>
    public void ResetCountdown()
    {
        currentTime = countdownTime;
        isRunning = false;
        
        if (fillImage != null)
        {
            fillImage.fillAmount = 1f;
        }
    }
}