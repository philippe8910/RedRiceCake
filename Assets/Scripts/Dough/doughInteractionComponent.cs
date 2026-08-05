using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using DG.Tweening;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class doughInteractionComponent : MonoBehaviour
{
    public doughTargetZone currentTargetZone;

    [Header("Visual")]
    public MeshRenderer meshRenderer;

    [Header("Input")]
    public InputActionReference actionRightControllerReference, actionLeftControllerReference;

    [Header("Goal")]
    public int targetPinchCount = 10;
    public int currentPinchCount = 0;

    [Header("Flags")]
    public bool isDyeing = false;
    public bool isControllerEnter = false;

    [Header("Events")]
    public UnityEvent OnComplete, OnPinchRight, OnPinchLeft;

    // ---------- 只用彈性回原 ----------
    [Header("Elastic Scale Params")]
    [Tooltip("放大幅度（0.2 = 放到 1.2x 再彈回）")]
    public float elasticAmplitude = 0.2f;
    [Tooltip("往上放大的時間")]
    public float elasticUpTime = 0.06f;
    [Tooltip("彈性回原時間")]
    public float elasticBackTime = 0.25f;

    // ---------- 內部 ----------
    private Vector3 _baseScale;
    private Tween _scaleTween;
    private bool _completedInvoked = false;
    private Material _matInstance;

    private void Awake()
    {
        _baseScale = transform.localScale;
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            // 避免共用材質被整場景改動
            _matInstance = meshRenderer.material;
        }
    }

    private void OnEnable()
    {
        if (actionRightControllerReference != null) actionRightControllerReference.action.Enable();
        if (actionLeftControllerReference  != null) actionLeftControllerReference.action.Enable();
    }

    private void OnDisable()
    {
        if (actionRightControllerReference != null) actionRightControllerReference.action.Disable();
        if (actionLeftControllerReference  != null) actionLeftControllerReference.action.Disable();
        _scaleTween?.Kill();
    }

    [ContextMenu("ExecuteTest")]
    public void Test()
    {
        currentPinchCount++;
        OnPinchRight?.Invoke();
        ScaleAnimation();
        Debug.Log("Pinch Dough (Right)");
    }

    public void ScaleAnimation()
    {
        // 防止疊加
        if (_scaleTween != null && _scaleTween.IsActive())
            _scaleTween.Kill(false);

        // 放大 → 彈性回原
        Vector3 targetUp = _baseScale * (1f + Mathf.Max(0f, elasticAmplitude));

        _scaleTween = DOTween.Sequence()
            .Append(transform.DOScale(targetUp, Mathf.Max(0.01f, elasticUpTime)).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(_baseScale, Mathf.Max(0.01f, elasticBackTime)).SetEase(Ease.OutElastic))
            .SetUpdate(false); // 若要 timeScale=0 也播放，改成 true
    }

    private void Update()
    {
        if (isControllerEnter)
        {
            if (actionRightControllerReference != null && actionRightControllerReference.action.WasPerformedThisFrame())
            {
                currentPinchCount++;
                OnPinchRight?.Invoke();
                ScaleAnimation();
                Debug.Log("Pinch Dough (Right)");
            }

            if (actionLeftControllerReference != null && actionLeftControllerReference.action.WasPerformedThisFrame())
            {
                currentPinchCount++;
                OnPinchLeft?.Invoke();
                ScaleAnimation();
                Debug.Log("Pinch Dough (Left)");
            }
        }

        if (isDyeing && _matInstance != null)
        {
            _matInstance.color = Color.red;
            // 若想更順，可用：
            // _matInstance.DOColor(Color.red, 0.15f);
        }

        if (!_completedInvoked && currentPinchCount >= targetPinchCount)
        {
            _completedInvoked = true;
            OnComplete?.Invoke();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == ("Controller"))
            isControllerEnter = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.tag == ("Controller"))
            isControllerEnter = false;
    }

    // 若外部改了縮放，可以重設基準
    public void RebindBaseScale()
    {
        _baseScale = transform.localScale;
    }

    // 需要重來時可呼叫
    public void ResetProgress()
    {
        currentPinchCount = 0;
        _completedInvoked = false;
    }
}
