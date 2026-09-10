using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 烤好的月餅可以拿起來看。
///
/// 烤盤格子上的「月餅-完成體」原本沒有碰撞體、沒有剛體、也不能抓
/// （它只是個擺著看的模型）。這支在它被打開的時候補齊那些元件，
/// 讓玩家出爐後可以拿起來端詳。
///
/// 用 kinematic 而不是給重力，跟場上其他工具（模具、印章、烤盤、蛋液刷）
/// 一致 —— 放開就停在原地，不會掉到地上滾走撿不回來。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeSouvenir : MonoBehaviour
{
    [Tooltip("碰撞體要比模型大多少，太貼合在 VR 裡很難抓到")]
    public float colliderPadding = 1.15f;

    private bool _ready;

    private void OnEnable()
    {
        if (_ready) return;
        _ready = true;

        var col = GetComponent<Collider>();
        if (col == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            MooncakeColliderUtil.FitToRenderers(box, gameObject, colliderPadding);
            col = box;
        }
        col.isTrigger = false;

        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var grab = GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            grab = gameObject.AddComponent<XRGrabInteractable>();
            // 扁圓餅沒有固定握法，抓哪維持哪個姿態
            grab.useDynamicAttach = true;
            grab.throwOnDetach = false;   // 別讓它被甩飛出去
        }
    }
}
