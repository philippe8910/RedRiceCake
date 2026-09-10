using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 讓抓出來的麵團「放得住」。
///
/// 麵團 prefab 是球形碰撞體 + angularDrag 0.05，等於一顆幾乎無摩擦的球，
/// 放到桌上就會一路滾走，很難對準壓扁的位置。
///
/// 這支做兩件事（都只在沒被抓著的時候作用）：
///   1. 放開後把角速度／速度快速吃掉，讓它就地停下來
///   2. 離交接點夠近時，輕輕吸過去對正 —— 就是使用者要的「定位點」
///
/// 吸附只在水平距離內判斷，掉在地上的麵團不會憑空飛回檯面。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class MooncakeDoughAnchor : MonoBehaviour
{
    [Header("放開後停下來")]
    [Tooltip("放開後額外施加的角阻力，數字越大滾得越快停")]
    public float settleAngularDrag = 8f;
    [Tooltip("放開後額外施加的線性阻力")]
    public float settleDrag = 2.5f;

    [Header("定位點吸附")]
    [Tooltip("關掉就只剩「會停下來」，不會被吸過去")]
    public bool snapToHandoff = true;
    [Tooltip("水平距離小於這個才吸（公尺）")]
    public float snapRadius = 0.12f;
    [Tooltip("垂直高度差超過這個就不吸，避免掉到地上的麵團飛回檯面")]
    public float snapHeightTolerance = 0.15f;
    [Tooltip("吸附速度；越大越快貼上去")]
    public float snapLerp = 8f;

    private Rigidbody _rb;
    private XRGrabInteractable _grab;
    private Transform _target;
    private float _baseDrag, _baseAngularDrag;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _grab = GetComponent<XRGrabInteractable>();
        _baseDrag = _rb.drag;
        _baseAngularDrag = _rb.angularDrag;
    }

    private void OnEnable()
    {
        // 每次重新生出來都要重找：交接點是場景物件，麵團是 prefab 實例
        _target = FindHandoff();
    }

    private bool Held => _grab != null && _grab.isSelected;

    private void FixedUpdate()
    {
        if (_rb == null) return;

        if (Held)
        {
            // 拿在手上時不要干擾 XRI 的移動
            _rb.drag = _baseDrag;
            _rb.angularDrag = _baseAngularDrag;
            return;
        }

        _rb.drag = settleDrag;
        _rb.angularDrag = settleAngularDrag;

        if (!snapToHandoff || _target == null) return;

        Vector3 to = _target.position - _rb.position;
        if (Mathf.Abs(to.y) > snapHeightTolerance) return;

        var flat = new Vector3(to.x, 0f, to.z);
        if (flat.magnitude > snapRadius) return;

        // 靠近了就對正到交接點，順手把殘餘速度吃掉
        float k = 1f - Mathf.Exp(-snapLerp * Time.fixedDeltaTime);
        _rb.MovePosition(Vector3.Lerp(_rb.position, _target.position, k));
        _rb.velocity = Vector3.Lerp(_rb.velocity, Vector3.zero, k);
        _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, Vector3.zero, k);
    }

    /// <summary>場景裡等著收麵團的交接點；沒有就不吸附。</summary>
    private Transform FindHandoff()
    {
        var hint = FindObjectOfType<MooncakeHandoffHint>();
        if (hint == null) return null;

        // socket 才是真正的接收位置，沒設就退回提示物件本身
        if (hint.socket != null) return hint.socket.transform;
        if (hint.hintObject != null) return hint.hintObject.transform;
        return hint.transform;
    }
}
