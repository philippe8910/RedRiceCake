using UnityEngine;

/// <summary>
/// 一滴液體。掉下去碰到東西就在落點留一片痕跡，然後自己消失。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeLiquidDrop : MonoBehaviour
{
    [Header("落點痕跡")]
    public GameObject splatPrefab;
    [Tooltip("痕跡縮放的隨機範圍")]
    public Vector2 splatScaleRange = new Vector2(0.8f, 1.3f);
    [Tooltip("痕跡離表面的距離，避免與表面 Z-fighting")]
    public float splatSurfaceOffset = 0.0015f;
    [Tooltip("把痕跡掛到被滴到的物件底下，這樣對方移動時痕跡會跟著")]
    public bool parentSplatToTarget = true;

    [Header("判定")]
    [Tooltip("只有這些 Layer 會留下痕跡")]
    public LayerMask splatMask = ~0;
    [Tooltip("碰到 trigger 也算（月餅的偵測區都是 trigger，不開就會穿過去）")]
    public bool splatOnTrigger = true;
    [Tooltip("超過這個秒數還沒碰到東西就自己消失")]
    public float maxLifetime = 6f;
    [Tooltip("生成後這段時間內不算數，讓它先離開刷頭")]
    public float armDelay = 0.06f;
    [Tooltip("不要被手把擋下來")]
    public string[] ignoreTags = { "Controller" };

    private bool _spent;
    private float _age;

    private void Start()
    {
        if (maxLifetime > 0f) Destroy(gameObject, maxLifetime);
    }

    private void Update()
    {
        _age += Time.deltaTime;
    }

    /// <summary>由滴落器呼叫：忽略跟來源（刷子）本身的碰撞，不然一生出來就撞在一起。</summary>
    public void IgnoreSource(Collider[] sourceColliders)
    {
        if (sourceColliders == null) return;

        var mine = GetComponentsInChildren<Collider>(true);
        foreach (var a in mine)
        {
            if (a == null) continue;
            foreach (var b in sourceColliders)
            {
                if (b != null) Physics.IgnoreCollision(a, b, true);
            }
        }
    }

    /// <summary>這個對象該不該算一次落點。</summary>
    private bool IsValidTarget(GameObject other)
    {
        if (_spent) return false;
        if (_age < armDelay) return false;                       // 剛生出來，還在刷頭附近
        if (!InMask(other.layer)) return false;
        if (other.GetComponentInParent<MooncakeLiquidDrop>() != null) return false;   // 別的液滴
        if (other.GetComponentInParent<MooncakeLiquidSplat>() != null) return false;  // 已有的痕跡

        foreach (var t in ignoreTags)
        {
            if (!string.IsNullOrEmpty(t) && other.CompareTag(t)) return false;
        }
        return true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsValidTarget(collision.collider.gameObject)) return;

        Vector3 point = transform.position;
        Vector3 normal = Vector3.up;

        if (collision.contactCount > 0)
        {
            var c = collision.GetContact(0);
            point = c.point;
            normal = c.normal;
        }

        Splat(point, normal, collision.collider.transform);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!splatOnTrigger) return;
        if (!IsValidTarget(other.gameObject)) return;

        // trigger 沒有接觸點資訊，就用目前位置與朝上的法線
        Splat(transform.position, Vector3.up, other.transform);
    }

    private bool InMask(int layer)
    {
        return (splatMask.value & (1 << layer)) != 0;
    }

    private void Splat(Vector3 point, Vector3 normal, Transform target)
    {
        _spent = true;

        if (splatPrefab != null)
        {
            var rot = Quaternion.FromToRotation(Vector3.up, normal);
            var go = Instantiate(splatPrefab, point + normal * splatSurfaceOffset, rot);

            float k = Random.Range(splatScaleRange.x, splatScaleRange.y);
            Vector3 worldScale = splatPrefab.transform.localScale * k;

            if (parentSplatToTarget && target != null) go.transform.SetParent(target, true);

            // 一定要「掛好父物件之後」才設大小：痕跡可能掛在縮放 0.01 或 35 的物件底下，
            // 而 Splat 的 Update 會把 _targetScale 當 local scale 套回去
            var splat = go.GetComponent<MooncakeLiquidSplat>();
            if (splat != null) splat.SetWorldScale(worldScale);
            else go.transform.localScale = worldScale;
        }

        Destroy(gameObject);
    }
}
