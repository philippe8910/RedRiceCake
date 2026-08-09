using UnityEngine;

/// <summary>
/// 月餅場景裡的模型縮放差距很大（0.01 ~ 35），碰撞體半徑直接填 local 值很難抓。
/// 這裡統一改成用「世界公尺」設定，執行時再依 lossyScale 換算回 local。
/// </summary>
public static class MooncakeColliderUtil
{
    /// <summary>把 SphereCollider 設成 trigger，半徑用世界公尺指定。</summary>
    public static void FitTrigger(SphereCollider col, float worldRadius)
    {
        if (col == null || worldRadius <= 0f) return;

        col.isTrigger = true;
        col.radius = worldRadius / MaxScale(col.transform);
    }

    /// <summary>把 BoxCollider 套到所有 Renderer 的世界 bounds 上。</summary>
    public static void FitToRenderers(BoxCollider col, GameObject root, float padding = 1f)
    {
        if (col == null || root == null) return;

        // 只算目前開著的 Renderer：模具底下的提示／完成體是關著的，不該撐大抓取範圍
        var renderers = root.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length == 0) return;

        Bounds world = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);

        var t = col.transform;
        col.center = t.InverseTransformPoint(world.center);

        Vector3 s = t.lossyScale;
        col.size = new Vector3(
            SafeDiv(world.size.x, s.x),
            SafeDiv(world.size.y, s.y),
            SafeDiv(world.size.z, s.z)) * Mathf.Max(0.01f, padding);
    }

    private static float MaxScale(Transform t)
    {
        Vector3 s = t.lossyScale;
        float m = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        return m > 1e-6f ? m : 1f;
    }

    private static float SafeDiv(float a, float b)
    {
        return Mathf.Abs(b) > 1e-6f ? a / Mathf.Abs(b) : a;
    }
}
