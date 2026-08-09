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

    [Header("放上去的煙霧")]
    [Tooltip("模具把月餅放到這一格時要放的煙霧特效")]
    public GameObject smokePrefab;
    [Tooltip("煙霧生成點；留空則用這一格自己的位置")]
    public Transform smokePoint;
    [Tooltip("相對生成點的位移")]
    public Vector3 smokeOffset = Vector3.zero;
    [Tooltip("播完之後自動刪掉（Smoke.prefab 的 Stop Action 是 None，不刪會一直留著）")]
    public bool destroySmokeWhenFinished = true;

    [Header("刷蛋液")]
    [Tooltip("掛在「月餅-放置」上的接收器，等蛋液刷碰進來")]
    public MooncakeDropSocket eggWashSocket;
    [Tooltip("刷到蛋液後「月餅-放置」要變成的顏色")]
    public Color eggWashColor = new Color(1f, 0.92f, 0.65f, 1f);

    [Header("事件")]
    public MooncakeGameObjectEvent onPlaced;
    public MooncakeGameObjectEvent onEggWashed;

    /// <summary>程式端訂閱用（不序列化）。</summary>
    public event System.Action<MooncakeTraySlot> Placed;
    public event System.Action<MooncakeTraySlot> EggWashed;

    public bool IsFilled { get; private set; }
    public bool IsBaked { get; private set; }
    public bool IsEggWashed { get; private set; }

    /// <summary>第一輪烘烤結束後才開放刷蛋液。</summary>
    private bool _eggWashEnabled;

    // 被染色的 Renderer 與它們原本的顏色
    private Renderer[] _placedRenderers;
    private Color[] _originalColors;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        if (triggerWorldRadius > 0f)
            MooncakeColliderUtil.FitTrigger(GetComponent<SphereCollider>(), triggerWorldRadius);

        if (placedObject != null) placedObject.SetActive(false);
        if (bakedObject != null) bakedObject.SetActive(false);

        CachePlacedColors();
        if (eggWashSocket != null) eggWashSocket.Received += HandleEggWashed;
    }

    private void OnDestroy()
    {
        if (eggWashSocket != null) eggWashSocket.Received -= HandleEggWashed;
    }

    private void CachePlacedColors()
    {
        if (placedObject == null) return;

        _placedRenderers = placedObject.GetComponentsInChildren<Renderer>(true);
        _originalColors = new Color[_placedRenderers.Length];

        for (int i = 0; i < _placedRenderers.Length; i++)
        {
            var m = _placedRenderers[i].sharedMaterial;
            _originalColors[i] = m == null ? Color.white
                : (m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId)
                    : (m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white));
        }
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

        PlaySmoke();

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

    // ---------------- 煙霧 ----------------

    /// <summary>放上去的瞬間冒一下煙。</summary>
    [Button("Debug：放一次煙霧", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public GameObject PlaySmoke()
    {
        if (smokePrefab == null) return null;

        Transform origin = smokePoint != null ? smokePoint : transform;

        // 生在場景最外層、不掛任何父物件：烤盤與這一格都有自己的縮放
        // （提示欄位是 0.0101），掛上去粒子大小會被父物件的 lossyScale 拉走
        var go = Instantiate(smokePrefab, origin.position + smokeOffset, origin.rotation);

        // 保險：尺寸一律跟 Prefab 一致
        go.transform.localScale = smokePrefab.transform.localScale;

        if (destroySmokeWhenFinished) Destroy(go, SmokeLifetime(go));
        return go;
    }

    private static float SmokeLifetime(GameObject go)
    {
        var ps = go.GetComponentInChildren<ParticleSystem>(true);
        if (ps == null) return 3f;

        var main = ps.main;
        return main.duration + main.startLifetime.constantMax + 0.5f;
    }

    // ---------------- 刷蛋液 ----------------

    /// <summary>第一輪烘烤結束後由流程呼叫，開放刷蛋液。</summary>
    public void EnableEggWash(bool value)
    {
        _eggWashEnabled = value;
        if (value && eggWashSocket != null) eggWashSocket.ResetSocket();
    }

    private void HandleEggWashed(GameObject brush)
    {
        if (!_eggWashEnabled || IsEggWashed) return;

        IsEggWashed = true;
        ApplyEggWashTint();

        onEggWashed?.Invoke(brush);
        EggWashed?.Invoke(this);
    }

    private void ApplyEggWashTint()
    {
        if (_placedRenderers == null) return;

        foreach (var r in _placedRenderers)
        {
            if (r == null) continue;

            // 用 material（實例）而不是 sharedMaterial，避免改到磁碟上的材質資產
            var m = r.material;
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, eggWashColor);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, eggWashColor);
        }
    }

    private void RestorePlacedColors()
    {
        if (_placedRenderers == null || _originalColors == null) return;

        for (int i = 0; i < _placedRenderers.Length && i < _originalColors.Length; i++)
        {
            var r = _placedRenderers[i];
            if (r == null) continue;

            var m = r.material;
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, _originalColors[i]);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, _originalColors[i]);
        }
    }

    [Button("Debug：刷上蛋液", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugEggWash()
    {
        _eggWashEnabled = true;
        HandleEggWashed(null);
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
        IsEggWashed = false;
        _eggWashEnabled = false;

        RestorePlacedColors();
        if (eggWashSocket != null) eggWashSocket.ResetSocket();

        if (placedObject != null) placedObject.SetActive(false);
        if (bakedObject != null) bakedObject.SetActive(false);
        gameObject.SetActive(true);
    }
}
