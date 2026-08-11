using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 教學箭頭：浮在目標正上方、上下飄、尖端指著目標，而且永遠側向玩家看得到的角度。
/// 沒有給 Mesh 的話會自己長一支低面數箭頭出來，不需要任何美術資源。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class MooncakeTutorialArrow : MonoBehaviour
{
    [Header("目標")]
    [Tooltip("要指的東西；設成 null 就自動收起來")]
    public Transform target;
    [Tooltip("用目標的 Renderer 範圍決定高度，抓不到才用 Fallback Height")]
    public bool autoFitToBounds = true;
    [Tooltip("疊在目標最高點再往上多少公尺")]
    public float heightOffset = 0.05f;
    [Tooltip("目標沒有 Renderer 時，直接離原點多高")]
    public float fallbackHeight = 0.12f;

    [Header("動態")]
    [Tooltip("跟隨的阻尼，越大越跟得緊")]
    public float followDamping = 12f;
    public float bobAmplitude = 0.022f;
    public float bobSpeed = 2.2f;
    [Tooltip("繞自身軸慢慢轉，讓低面數的形狀看得出立體")]
    public float spinSpeed = 55f;
    [Tooltip("往玩家方向傾斜幾度，正面看比較有指向感")]
    public float tiltDegrees = 16f;
    public bool faceCamera = true;

    [Header("外觀")]
    public Color color = new Color(1f, 0.72f, 0.2f, 1f);
    [Tooltip("被桌子、鍋子擋住時也看得到")]
    public bool renderThroughWalls = true;
    [Tooltip("整支箭頭的長度（公尺）")]
    public float length = 0.15f;
    [Tooltip("粗細倍率")]
    public float thickness = 1f;
    [Tooltip("出現／收起的縮放時間")]
    public float popDuration = 0.22f;

    [Header("玩家")]
    [Tooltip("留空會自動抓 Camera.main")]
    public Transform playerCamera;

    // ---- 內部狀態 ----
    private MeshFilter _filter;
    private MeshRenderer _renderer;
    private Material _material;
    private Transform _boundsSource;
    private Renderer[] _targetRenderers;
    private float _phase;          // 每支箭頭的飄動相位錯開
    private float _spin;           // 目前轉到的角度
    private float _visible;        // 0 = 收起、1 = 完全出現
    private bool _wantVisible;
    private bool _snapped;

    private static Mesh s_sharedMesh;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

    public bool IsShowing => _wantVisible;

    private void Awake()
    {
        _filter = GetComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();

        if (_filter.sharedMesh == null) _filter.sharedMesh = GetSharedMesh();

        EnsureMaterial();

        _phase = Random.value * Mathf.PI * 2f;
        _visible = 0f;
        ApplyScale();
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    private void EnsureMaterial()
    {
        // 每支箭頭自己一份材質（顏色可以各自不同），數量很少不心疼
        var baseMat = _renderer.sharedMaterial;

        if (baseMat != null && baseMat.shader != null)
        {
            _material = new Material(baseMat);
        }
        else
        {
            var shader = Shader.Find("Mooncake/Tutorial Overlay");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            _material = new Material(shader);
        }

        _material.name = "教學箭頭 (Instance)";
        _renderer.sharedMaterial = _material;

        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        ApplyMaterialSettings();
    }

    /// <summary>顏色、穿牆設定改完之後套用。</summary>
    [Button("套用外觀", ButtonSizes.Medium), GUIColor(0.3f, 0.8f, 1f)]
    public void ApplyMaterialSettings()
    {
        if (_material == null) return;

        if (_material.HasProperty(BaseColorId)) _material.SetColor(BaseColorId, color);
        if (_material.HasProperty(ColorId)) _material.SetColor(ColorId, color);

        if (_material.HasProperty(ZTestId))
        {
            _material.SetFloat(ZTestId, (float)(renderThroughWalls
                ? UnityEngine.Rendering.CompareFunction.Always
                : UnityEngine.Rendering.CompareFunction.LessEqual));
        }
    }

    // ---------------- 顯示 / 收起 ----------------

    /// <summary>指向某個目標並出現。</summary>
    public void ShowAt(Transform newTarget)
    {
        SetTarget(newTarget);
        Show();
    }

    public void SetTarget(Transform newTarget)
    {
        if (target == newTarget) return;

        target = newTarget;
        _boundsSource = null;
        _targetRenderers = null;
        _snapped = false;
    }

    [Button("顯示", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 0.6f)]
    public void Show()
    {
        _wantVisible = true;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    [Button("收起", ButtonSizes.Medium), GUIColor(1f, 0.7f, 0.3f)]
    public void Hide()
    {
        _wantVisible = false;
    }

    // ---------------- 每幀 ----------------

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;

        bool alive = target != null && target.gameObject.activeInHierarchy;
        float goal = (_wantVisible && alive) ? 1f : 0f;

        _visible = popDuration <= 0f
            ? goal
            : Mathf.MoveTowards(_visible, goal, dt / popDuration);

        if (_visible <= 0.001f)
        {
            if (_renderer.enabled) _renderer.enabled = false;
            return;
        }
        if (!_renderer.enabled) _renderer.enabled = true;

        if (alive) UpdateTransform(dt);
        ApplyScale();
    }

    private void UpdateTransform(float dt)
    {
        Vector3 anchor = ResolveAnchor();

        _phase += dt * bobSpeed;
        _spin += dt * spinSpeed;
        anchor += Vector3.up * (Mathf.Sin(_phase) * 0.5f + 0.5f) * bobAmplitude;

        if (!_snapped)
        {
            transform.position = anchor;
            _snapped = true;
        }
        else
        {
            float k = followDamping <= 0f ? 1f : 1f - Mathf.Exp(-followDamping * dt);
            transform.position = Vector3.Lerp(transform.position, anchor, k);
        }

        transform.rotation = ResolveRotation();
    }

    /// <summary>箭尖要停的世界座標：目標範圍的頂端再往上一點。</summary>
    private Vector3 ResolveAnchor()
    {
        if (!autoFitToBounds) return target.position + Vector3.up * fallbackHeight;

        if (_boundsSource != target)
        {
            _boundsSource = target;
            _targetRenderers = target.GetComponentsInChildren<Renderer>(true);
        }

        bool any = false;
        Bounds b = new Bounds(target.position, Vector3.zero);

        if (_targetRenderers != null)
        {
            foreach (var r in _targetRenderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (any) b.Encapsulate(r.bounds);
                else { b = r.bounds; any = true; }
            }
        }

        if (!any) return target.position + Vector3.up * fallbackHeight;

        var top = b.center;
        top.y = b.max.y;
        return top + Vector3.up * heightOffset;
    }

    /// <summary>箭頭本體朝 -Y（尖端在原點），這裡疊上朝玩家的偏擺與傾斜。</summary>
    private Quaternion ResolveRotation()
    {
        var spin = Quaternion.AngleAxis(_spin, Vector3.up);

        if (!faceCamera) return spin;

        var cam = ResolveCamera();
        if (cam == null) return spin;

        Vector3 toCam = cam.position - transform.position;
        toCam.y = 0f;
        if (toCam.sqrMagnitude < 1e-6f) return spin;

        var yaw = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        return yaw * Quaternion.AngleAxis(-tiltDegrees, Vector3.right) * spin;
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

    private void ApplyScale()
    {
        // 出現時彈一下：0 →（過頭）→ 1
        float s = _visible;
        float pop = s < 1f ? Mathf.Sin(s * Mathf.PI * 0.5f) * (1f + 0.18f * (1f - s)) : 1f;

        float len = Mathf.Max(0.001f, length) * pop;
        float thick = len * Mathf.Max(0.01f, thickness);
        transform.localScale = new Vector3(thick, len, thick);
    }

    // ---------------- 程序化網格 ----------------

    private static Mesh GetSharedMesh()
    {
        if (s_sharedMesh == null)
        {
            s_sharedMesh = BuildMesh(14, 0.46f, 0.23f, 0.075f);
            s_sharedMesh.name = "MooncakeTutorialArrow";
        }
        return s_sharedMesh;
    }

    /// <summary>
    /// 長出一支尖端在原點、身體往 +Y 長、總長 1 的箭頭。
    /// 平面著色（每個三角形自己的法線），面數很少，直接硬刻不必進資產。
    /// </summary>
    public static Mesh BuildMesh(int segments, float headHeight, float headRadius, float shaftRadius)
    {
        segments = Mathf.Max(3, segments);
        headHeight = Mathf.Clamp(headHeight, 0.05f, 0.95f);

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        void AddTri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            // Unity 的正面：cross(b-a, c-a) 與外側同向；反了就把 b、c 交換
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            {
                var tmp = b; b = c; c = tmp;
            }

            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            norms.Add(outward); norms.Add(outward); norms.Add(outward);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        Vector3 Ring(int i, float y, float r)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        var apex = Vector3.zero;
        float shaftTop = 1f;

        for (int i = 0; i < segments; i++)
        {
            int j = (i + 1) % segments;

            Vector3 h0 = Ring(i, headHeight, headRadius);
            Vector3 h1 = Ring(j, headHeight, headRadius);
            Vector3 s0 = Ring(i, headHeight, shaftRadius);
            Vector3 s1 = Ring(j, headHeight, shaftRadius);
            Vector3 t0 = Ring(i, shaftTop, shaftRadius);
            Vector3 t1 = Ring(j, shaftTop, shaftRadius);

            // 錐面：外側法線朝外且略微朝下
            Vector3 side = ((h0 + h1) * 0.5f - apex);
            Vector3 outward = Vector3.Cross(h1 - h0, side).normalized;
            if (Vector3.Dot(outward, new Vector3(h0.x, 0f, h0.z)) < 0f) outward = -outward;
            AddTri(apex, h0, h1, outward);

            // 錐底（朝上的環形，補住頭跟桿之間的縫）
            AddTri(h0, h1, s1, Vector3.up);
            AddTri(h0, s1, s0, Vector3.up);

            // 桿身
            Vector3 radial = new Vector3((h0.x + h1.x) * 0.5f, 0f, (h0.z + h1.z) * 0.5f).normalized;
            AddTri(s0, s1, t1, radial);
            AddTri(s0, t1, t0, radial);

            // 桿頂封口
            AddTri(t0, t1, new Vector3(0f, shaftTop, 0f), Vector3.up);
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) ApplyMaterialSettings();
    }
#endif
}
