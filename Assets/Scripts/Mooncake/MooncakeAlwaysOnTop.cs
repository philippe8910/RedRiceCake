using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;

/// <summary>
/// 讓一個世界空間 Canvas 永遠畫在所有物件之上。
///
/// 世界空間的 UI 跟一般 3D 物件一樣會做深度測試，所以桌子、烤箱這些擋在前面時
/// 面板就會被切掉。UI 與 TMP 的 shader 都把 ZTest 寫成 <c>ZTest [unity_GUIZTestMode]</c>，
/// 只要在材質上把這個值改成 Always 就不會被深度擋住；再把 renderQueue 拉到
/// Overlay 區段，確保排在不透明物件後面畫。
///
/// 材質都取「實體」而不是共用資產（Image 用 new Material、TMP 用 fontMaterial），
/// 不會污染到其他用同一個材質的 UI。
/// </summary>
[DisallowMultipleComponent]
public class MooncakeAlwaysOnTop : MonoBehaviour
{
    [Tooltip("連關著的子物件也一起處理；面板通常是關著的，這個要開")]
    public bool includeInactive = true;

    [Tooltip("Overlay 區段。要壓過所有不透明物件就得 >= 3000")]
    public int renderQueue = 4000;

    [Tooltip("同時把 Canvas 的 Sorting Order 提高；<0 表示不動")]
    public int sortingOrder = 500;

    private static readonly int ZTestId = Shader.PropertyToID("unity_GUIZTestMode");
    private readonly List<Material> _made = new List<Material>();

    private void OnEnable()
    {
        Apply();
    }

    private void OnDestroy()
    {
        foreach (var m in _made)
            if (m != null) Destroy(m);
        _made.Clear();
    }

    [Button("套用置頂", ButtonSizes.Large), GUIColor(0.6f, 0.9f, 0.6f)]
    public void Apply()
    {
        int n = 0;

        foreach (var g in GetComponentsInChildren<Graphic>(includeInactive))
        {
            if (g == null) continue;

            // TMP 的 fontMaterial 取用時就會自動產生實體，直接改它最安全
            if (g is TMP_Text tmp)
            {
                var fm = tmp.fontMaterial;
                if (fm == null) continue;
                fm.SetFloat(ZTestId, (float)CompareFunction.Always);
                fm.renderQueue = renderQueue;
                n++;
                continue;
            }

            var src = g.materialForRendering;
            if (src == null) continue;

            var mat = new Material(src) { name = src.name + " (置頂)" };
            mat.SetFloat(ZTestId, (float)CompareFunction.Always);
            mat.renderQueue = renderQueue;
            g.material = mat;
            _made.Add(mat);
            n++;
        }

        if (sortingOrder >= 0)
        {
            foreach (var c in GetComponentsInChildren<Canvas>(includeInactive))
            {
                c.overrideSorting = true;
                c.sortingOrder = sortingOrder;
            }
        }

        Debug.Log($"[置頂] {name}：{n} 個繪製元件已改成不做深度測試", this);
    }
}
