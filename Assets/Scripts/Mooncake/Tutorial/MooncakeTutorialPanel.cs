using System.Collections;
using UnityEngine;
using TMPro;
using Sirenix.OdinInspector;

/// <summary>
/// 教學文字板：world-space canvas，一個字一個字打出來，並且用「死區跟隨」黏在玩家視線裡
/// （看向別處超過一定角度才慢慢追過來，不會一直黏在臉上晃）。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeTutorialPanel : MonoBehaviour
{
    [Header("元件")]
    public CanvasGroup group;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    [Tooltip("整塊面板的容器，出現時會彈一下")]
    public Transform scaleRoot;

    [Header("跟隨玩家")]
    public bool followPlayer = true;
    [Tooltip("留空會自動抓 Camera.main")]
    public Transform playerCamera;
    [Tooltip("離眼睛多遠（公尺）")]
    public float distance = 1.05f;
    [Tooltip("相對視線中心往上／往下多少，負值 = 往下，不擋住工作檯")]
    public float heightOffset = -0.16f;
    [Tooltip("相對視線往右／往左多少，正值 = 往右，避開檯面上的鍋子")]
    public float sideOffset = 0.3f;
    [Tooltip("視線偏離超過這個角度才把面板追過來")]
    public float followDeadAngle = 24f;
    public float moveDamping = 3.5f;
    public float rotateDamping = 6f;

    [Header("打字效果")]
    [Tooltip("每秒打幾個字")]
    public float charsPerSecond = 26f;
    [Tooltip("遇到標點多停多久")]
    public float punctuationPause = 0.16f;
    public string punctuationChars = "，。、！？；：…,.!?;:";
    [Tooltip("標題也要一個字一個字打")]
    public bool typeTitle = false;

    [Header("聲音")]
    public AudioSource audioSource;
    [Tooltip("打字聲；每 N 個字響一次")]
    public AudioClip typeClip;
    public AudioClip showClip;
    [Range(0f, 1f)] public float typeVolume = 0.35f;
    public int typeClipEveryNChars = 2;
    public Vector2 typePitchRange = new Vector2(0.94f, 1.1f);

    [Header("淡入淡出")]
    public float fadeInDuration = 0.22f;
    public float fadeOutDuration = 0.18f;
    public float popScale = 1.06f;

    // ---- 內部狀態 ----
    private Coroutine _typeRoutine;
    private Coroutine _fadeRoutine;
    private Vector3 _anchorDir = Vector3.forward;
    private bool _anchored;
    private bool _visible;
    private Vector3 _baseScale = Vector3.one;

    public bool IsVisible => _visible;
    public bool IsTyping => _typeRoutine != null;

    private void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (scaleRoot == null) scaleRoot = transform;

        // world-space canvas 本身縮得很小，彈跳要乘在原本的縮放上，不能直接寫 one
        _baseScale = scaleRoot.localScale;

        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        SetCanvasEnabled(false);
    }

    // ---------------- 對外 ----------------

    /// <summary>換一段教學文字並且出現（會重新打字）。</summary>
    public void Show(string title, string body)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        if (titleText != null) titleText.text = title ?? "";
        if (bodyText != null) bodyText.text = body ?? "";

        bool wasVisible = _visible;
        _visible = true;

        SetCanvasEnabled(true);

        if (!wasVisible) SnapToPlayer();
        StartFade(1f, wasVisible ? 0f : fadeInDuration, wasVisible ? 1f : popScale);

        if (!wasVisible && audioSource != null && showClip != null)
            audioSource.PlayOneShot(showClip);

        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = StartCoroutine(TypeRoutine());
    }

    [Button("收起", ButtonSizes.Medium), GUIColor(1f, 0.7f, 0.3f)]
    public void Hide()
    {
        if (!_visible) return;

        _visible = false;

        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }

        StartFade(0f, fadeOutDuration, 1f);
    }

    /// <summary>把還沒打完的字一次補完。</summary>
    [Button("直接打完", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void SkipTyping()
    {
        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }
        RevealAll();
    }

    // ---------------- 打字 ----------------

    private IEnumerator TypeRoutine()
    {
        // 先把兩段文字都藏起來，再依序放出來
        int titleCount = PrepareForTyping(titleText, typeTitle);
        int bodyCount = PrepareForTyping(bodyText, true);

        if (typeTitle && titleText != null)
            yield return TypeInto(titleText, titleCount);

        if (bodyText != null)
            yield return TypeInto(bodyText, bodyCount);

        _typeRoutine = null;
    }

    /// <summary>回傳這段文字實際有幾個可見字元，並把它設成全部隱藏（或全部顯示）。</summary>
    private static int PrepareForTyping(TMP_Text text, bool willType)
    {
        if (text == null) return 0;

        text.ForceMeshUpdate();
        int count = text.textInfo.characterCount;
        text.maxVisibleCharacters = willType ? 0 : count;
        return count;
    }

    private IEnumerator TypeInto(TMP_Text text, int count)
    {
        if (count <= 0) yield break;

        float perChar = charsPerSecond <= 0f ? 0f : 1f / charsPerSecond;
        int sinceSound = 0;

        for (int i = 1; i <= count; i++)
        {
            text.maxVisibleCharacters = i;

            char c = text.textInfo.characterInfo[i - 1].character;

            if (++sinceSound >= Mathf.Max(1, typeClipEveryNChars) && !char.IsWhiteSpace(c))
            {
                sinceSound = 0;
                PlayTypeClip();
            }

            float wait = perChar;
            if (punctuationPause > 0f && !string.IsNullOrEmpty(punctuationChars) &&
                punctuationChars.IndexOf(c) >= 0)
                wait += punctuationPause;

            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
        }
    }

    private void PlayTypeClip()
    {
        if (audioSource == null || typeClip == null || typeVolume <= 0f) return;

        audioSource.pitch = Random.Range(typePitchRange.x, typePitchRange.y);
        audioSource.PlayOneShot(typeClip, typeVolume);
    }

    private void RevealAll()
    {
        if (titleText != null)
        {
            titleText.ForceMeshUpdate();
            titleText.maxVisibleCharacters = titleText.textInfo.characterCount;
        }
        if (bodyText != null)
        {
            bodyText.ForceMeshUpdate();
            bodyText.maxVisibleCharacters = bodyText.textInfo.characterCount;
        }
    }

    // ---------------- 淡入淡出 ----------------

    private void StartFade(float targetAlpha, float duration, float overshoot)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration, overshoot));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration, float overshoot)
    {
        float from = group != null ? group.alpha : targetAlpha;

        if (duration > 0f)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);

                if (group != null) group.alpha = Mathf.Lerp(from, targetAlpha, k);

                if (scaleRoot != null && overshoot > 1f)
                {
                    // 0 → 過頭 → 1 的小彈跳
                    float s = targetAlpha > 0f
                        ? Mathf.LerpUnclamped(overshoot, 1f, k * k)
                        : 1f;
                    scaleRoot.localScale = _baseScale * s;
                }
                yield return null;
            }
        }

        if (group != null) group.alpha = targetAlpha;
        if (scaleRoot != null) scaleRoot.localScale = _baseScale;

        if (targetAlpha <= 0f) SetCanvasEnabled(false);

        _fadeRoutine = null;
    }

    private void SetCanvasEnabled(bool value)
    {
        // 完全收起時關掉 Canvas，省下每幀的 UI 重建
        var canvas = GetComponent<Canvas>();
        if (canvas != null) canvas.enabled = value;
    }

    // ---------------- 跟隨 ----------------

    private void LateUpdate()
    {
        if (!followPlayer || !_visible) return;

        var cam = ResolveCamera();
        if (cam == null) return;

        Vector3 eye = cam.position;
        Vector3 fwd = cam.forward;

        // 比的是「頭轉離上次定錨方向多少」，這樣固定的右偏移不會一直吃掉死區
        if (!_anchored || Vector3.Angle(fwd, _anchorDir) > Mathf.Max(1f, followDeadAngle))
        {
            _anchorDir = fwd.normalized;
            _anchored = true;
        }

        Vector3 desired = ResolveAnchorPosition(eye);

        float dt = Time.unscaledDeltaTime;
        float km = moveDamping <= 0f ? 1f : 1f - Mathf.Exp(-moveDamping * dt);
        transform.position = Vector3.Lerp(transform.position, desired, km);

        Vector3 away = transform.position - eye;
        if (away.sqrMagnitude > 1e-6f)
        {
            // canvas 的正面是 +Z，所以 +Z 要朝著「離開玩家」的方向，字才是正的
            var rot = Quaternion.LookRotation(away.normalized, Vector3.up);
            float kr = rotateDamping <= 0f ? 1f : 1f - Mathf.Exp(-rotateDamping * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, kr);
        }
    }

    /// <summary>直接跳到玩家面前，不要從上一個位置飛過來。</summary>
    public void SnapToPlayer()
    {
        var cam = ResolveCamera();
        if (cam == null) return;

        _anchorDir = cam.forward.normalized;
        _anchored = true;

        transform.position = ResolveAnchorPosition(cam.position);

        Vector3 away = transform.position - cam.position;
        if (away.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
    }

    /// <summary>面板該待的世界座標：視線前方，再往右／往下偏一點避開檯面上的東西。</summary>
    private Vector3 ResolveAnchorPosition(Vector3 eye)
    {
        // 用水平面上的右方向，玩家轉頭時偏移量才會跟著轉
        Vector3 right = Vector3.Cross(Vector3.up, _anchorDir);
        right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;

        return eye
             + _anchorDir * distance
             + right * sideOffset
             + Vector3.up * heightOffset;
    }

    private Transform ResolveCamera()
    {
        if (playerCamera != null) return playerCamera;

        var main = Camera.main;
        if (main == null)
        {
            // XR Origin 的相機沒設成 MainCamera 也要能運作
            foreach (var cam in Camera.allCameras)
            {
                if (cam != null && cam.enabled) { main = cam; break; }
            }
        }

        if (main != null) playerCamera = main.transform;
        return playerCamera;
    }

    // ---------------- Debug ----------------

    [FoldoutGroup("Debug"), Tooltip("測試用的標題")]
    public string debugTitle = "① 拿一團麵團";
    [FoldoutGroup("Debug"), TextArea(2, 4), Tooltip("測試用的內文")]
    public string debugBody = "把手伸到麵團桶上，按下扳機鍵抓一團麵團出來。";

    [FoldoutGroup("Debug"), Button("Debug：顯示這段文字", ButtonSizes.Large), GUIColor(0.6f, 0.9f, 0.6f)]
    public void DebugShow()
    {
        Show(debugTitle, debugBody);
    }
}
