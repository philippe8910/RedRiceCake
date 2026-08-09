using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 烤盤欄位（月餅-提示欄位-N）。
/// 帶著月餅的模具碰到這裡 → 這個提示欄位關閉、對應的「月餅-放置」打開。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeTraySlot : MonoBehaviour
{
    [Tooltip("放好之後要打開的「月餅-放置」（生的）")]
    public GameObject placedObject;

    [Tooltip("烤完之後要換上的「月餅-完成體」（烤過的）")]
    public GameObject bakedObject;

    [Tooltip("接受哪個 Tag 碰進來（模具）")]
    public string acceptTag = "Mold";

    [Tooltip("觸發範圍（世界公尺）。大於 0 時會在 Awake 依實際縮放重算 SphereCollider 半徑")]
    public float triggerWorldRadius = 0.06f;

    [Tooltip("模具身上必須真的有月餅才算數")]
    public bool requireMooncakeOnMold = true;

    [Header("事件")]
    public MooncakeGameObjectEvent onPlaced;

    /// <summary>程式端訂閱用（不序列化）。</summary>
    public event System.Action<MooncakeTraySlot> Placed;

    public bool IsFilled { get; private set; }
    public bool IsBaked { get; private set; }

    private void Awake()
    {
        if (triggerWorldRadius > 0f)
            MooncakeColliderUtil.FitTrigger(GetComponent<SphereCollider>(), triggerWorldRadius);

        if (placedObject != null) placedObject.SetActive(false);
        if (bakedObject != null) bakedObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsFilled) return;
        if (!other.CompareTag(acceptTag)) return;

        var mold = other.GetComponentInParent<MooncakeMoldStation>();
        if (requireMooncakeOnMold && (mold == null || !mold.HasMooncake)) return;

        Place(mold);
    }

    /// <summary>把月餅放上這一格。</summary>
    public void Place(MooncakeMoldStation mold)
    {
        if (IsFilled) return;

        IsFilled = true;

        if (mold != null) mold.ReleaseMooncake();
        if (placedObject != null) placedObject.SetActive(true);

        onPlaced?.Invoke(gameObject);
        Placed?.Invoke(this);

        // 提示欄位本身收起來（這會一併關掉這顆 trigger）
        gameObject.SetActive(false);
    }

    [Button("Debug：模擬放上這一格", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugPlace()
    {
        Place(FindObjectOfType<MooncakeMoldStation>());
    }

    /// <summary>烤完了：把生的換成烤過的完成體。</summary>
    public void SetBaked(bool value)
    {
        if (!IsFilled) return;   // 這一格根本沒東西，不用換

        IsBaked = value;

        if (placedObject != null) placedObject.SetActive(!value);
        if (bakedObject != null) bakedObject.SetActive(value);
    }

    [Button("Debug：換成烤過的", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugBake()
    {
        if (!IsFilled) IsFilled = true;   // Debug 時直接視為已放置
        SetBaked(true);
    }

    /// <summary>清空這一格。</summary>
    public void ResetSlot()
    {
        IsFilled = false;
        IsBaked = false;
        if (placedObject != null) placedObject.SetActive(false);
        if (bakedObject != null) bakedObject.SetActive(false);
        gameObject.SetActive(true);
    }
}
