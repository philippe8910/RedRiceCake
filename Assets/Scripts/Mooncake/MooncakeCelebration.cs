using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

/// <summary>
/// 過關慶祝：在玩家周圍圍一圈彩帶爆開。
/// Hovl 的 Confetti prefab 預設是 looping，生成後會強制改成只播一次再自動清掉。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeCelebration : MonoBehaviour
{
    [Header("彩帶")]
    [Tooltip("會依序輪流使用，做出不同顏色的爆點")]
    public GameObject[] confettiPrefabs;
    [Tooltip("這些 Prefab 預設 looping=1，勾著才會只爆一次")]
    public bool forceOneShot = true;

    [Header("環繞玩家")]
    [Tooltip("留空會自動找 Main Camera")]
    public Transform player;
    [Tooltip("幾個爆點圍成一圈")]
    [Range(1, 12)] public int burstCount = 4;
    [Tooltip("離玩家多遠（公尺）")]
    public float radius = 1.8f;
    [Tooltip("相對玩家高度的位移")]
    public float heightOffset = -0.2f;
    [Tooltip("整圈的起始角度")]
    public float startAngle = 45f;
    [Tooltip("爆點朝向玩家（不勾則朝外）")]
    public bool faceInward = true;
    [Tooltip("每個爆點之間差幾秒引爆，0 = 同時")]
    public float delayBetweenBursts = 0.12f;

    [Header("清理")]
    [Tooltip("0 = 依粒子時長自動計算")]
    public float autoDestroySeconds = 0f;

    [Header("事件")]
    public UnityEvent onCelebrate;

    [ShowInInspector, ReadOnly] private bool _celebrating;

    public bool IsCelebrating => _celebrating;

    private Transform ResolvePlayer()
    {
        if (player != null) return player;

        var cam = Camera.main;
        if (cam != null) player = cam.transform;

        return player;
    }

    /// <summary>放彩帶。</summary>
    [Button("Debug：立刻慶祝", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 1f)]
    public void Celebrate()
    {
        if (confettiPrefabs == null || confettiPrefabs.Length == 0)
        {
            Debug.LogWarning("[月餅慶祝] 沒有指定任何 Confetti Prefab", this);
            return;
        }

        var p = ResolvePlayer();
        if (p == null)
        {
            Debug.LogWarning("[月餅慶祝] 找不到玩家（Main Camera），改用自己的位置", this);
            p = transform;
        }

        onCelebrate?.Invoke();

        if (Application.isPlaying && delayBetweenBursts > 0f)
        {
            StopAllCoroutines();
            StartCoroutine(BurstRoutine(p));
        }
        else
        {
            for (int i = 0; i < Mathf.Max(1, burstCount); i++) SpawnBurst(p, i);
        }
    }

    private IEnumerator BurstRoutine(Transform p)
    {
        _celebrating = true;

        int n = Mathf.Max(1, burstCount);
        for (int i = 0; i < n; i++)
        {
            SpawnBurst(p, i);
            yield return new WaitForSeconds(delayBetweenBursts);
        }

        _celebrating = false;
    }

    private GameObject SpawnBurst(Transform p, int index)
    {
        var prefab = confettiPrefabs[index % confettiPrefabs.Length];
        if (prefab == null) return null;

        int n = Mathf.Max(1, burstCount);
        float ang = (startAngle + 360f * index / n) * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
        Vector3 pos = p.position + offset + Vector3.up * heightOffset;

        // 不掛父物件：玩家會移動，掛上去彩帶會跟著飄
        var go = Instantiate(prefab, pos, Quaternion.identity);
        go.transform.localScale = prefab.transform.localScale;

        Vector3 look = faceInward ? (p.position - pos) : (pos - p.position);
        look.y = 0f;
        if (look.sqrMagnitude > 1e-6f) go.transform.rotation = Quaternion.LookRotation(look.normalized);

        float life = PrepareParticles(go);
        Destroy(go, autoDestroySeconds > 0f ? autoDestroySeconds : life);
        return go;
    }

    /// <summary>把 looping 關掉並算出整組粒子放完要多久。</summary>
    private float PrepareParticles(GameObject go)
    {
        float longest = 2f;

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            if (forceOneShot) main.loop = false;

            longest = Mathf.Max(longest, main.duration + main.startLifetime.constantMax);
        }

        return longest + 1f;
    }
}
