using UnityEngine;

/// <summary>
/// 掛在餡料 Prefab 上，讓站點認得出這是哪一種餡，以及要套用哪個材質。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeFillingKind : MonoBehaviour
{
    [Tooltip("餡料識別字串，例如 紅豆 / 蓮子 / 巧克力 / 奶油")]
    public string fillingId = "紅豆";

    [Tooltip("要套到提示上的材質；留空則自動抓自己身上 Renderer 的材質")]
    public Material fillingMaterial;

    public Material ResolveMaterial()
    {
        if (fillingMaterial != null) return fillingMaterial;

        var r = GetComponentInChildren<Renderer>(true);
        return r != null ? r.sharedMaterial : null;
    }

    /// <summary>從任意一個碰進來的物件上找出餡料資訊。</summary>
    public static MooncakeFillingKind Find(GameObject source)
    {
        if (source == null) return null;

        var kind = source.GetComponentInParent<MooncakeFillingKind>();
        return kind != null ? kind : source.GetComponentInChildren<MooncakeFillingKind>(true);
    }
}
